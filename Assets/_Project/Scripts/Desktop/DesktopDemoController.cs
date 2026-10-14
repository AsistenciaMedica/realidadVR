using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EmergencyVR.Evaluation;
using EmergencyVR.Patient;
using EmergencyVR.Scenarios;
using EmergencyVR.UI;
using EmergencyVR.Medical.Interaction;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace EmergencyVR.Desktop
{
    // Compiled into every target, automatically installed ONLY in explicit desktop builds/previews.
    public sealed class DesktopDemoController : MonoBehaviour
    {
        public const string PreviewKey="EmergencyVR.DesktopPreview";
        public Camera View { get; private set; }
        public CharacterController Body { get; private set; }
        public ReviewCaseSession Review { get; private set; }
        ScenarioManager manager;
        Rigidbody held;
        bool heldKinematic,heldGravity;
        float heldDistance=.75f;
        float pitch,verticalSpeed;
        TrainingExperience experience;
        EmergencyVR.Medical.Interaction.HandsOnInteraction handsOn;
        bool drawerWasOpen;
        float frameYawRemaining;
        // Never stuck: the patient and the instruments are not walls, spawn points are checked, and a blocked
        // doctor is moved to the nearest free spot.
        readonly HashSet<Collider> passThrough = new HashSet<Collider>();
        readonly Collider[] overlaps = new Collider[32];
        float passThroughAt, stuckFor, notice;
        string noticeText = "";
        // Action lean (look, listen and feel): an offset applied after everything else and removed next frame.
        Vector3 leanEye, leanLook, leanBasePosition; Quaternion leanBaseRotation; float leanTarget, leanWeight; bool leanApplied;
        // Doctor locomotion: free look, walk/run with inertia and kneeling to reach a patient on the floor.
        public const float StandingEyeHeight=1.62f, KneelingEyeHeight=.98f;
        public const float WalkSpeed=1.8f, RunSpeed=3.6f, KneelSpeed=.8f;
        const float StandingBodyHeight=1.72f, KneelingBodyHeight=1.1f, Acceleration=10f, DragLookSensitivity=.09f, FreeLookSensitivity=.075f;
        Vector3 planarVelocity;
        float eyeHeight=StandingEyeHeight;
        /// <summary>Tab: cursor locked to the screen centre and the mouse turns the head (FPS style).</summary>
        public bool FreeLook { get; private set; }
        /// <summary>Ctrl: the doctor kneels beside the patient (lower eye height, slower movement).</summary>
        public bool Kneeling { get; private set; }
        /// <summary>I: hides the training panels for an unobstructed, presentation-ready view of the scene.</summary>
        public bool InterfaceHidden { get; private set; }
        public bool HasHeldTool => held!=null;
        public MedicalPhysicalTool HeldTool => held==null?null:held.GetComponent<MedicalPhysicalTool>();
        public bool PointerOverInterface => experience != null && experience.PointerOverInterface;
        public bool WorldInputBlocked => experience != null && experience.BlocksWorldInput;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded-=Loaded;
            bool useDesktop=false;
#if EMERGENCYVR_DESKTOP
            useDesktop=true;
#elif UNITY_EDITOR
            useDesktop=UnityEditor.SessionState.GetBool(PreviewKey,false);
#endif
            if(useDesktop && !QuestLookSimulation.Enabled) SceneManager.sceneLoaded+=Loaded;
        }
        static void Loaded(Scene scene,LoadSceneMode mode)
        {
            if(scene.name=="TrainingRoom") Create();
        }
        public static DesktopDemoController Create()
        {
            if(QuestLookSimulation.Enabled) throw new InvalidOperationException("Quest look uses the authored XR rig; a desktop controller cannot be created.");
            var existing=FindFirstObjectByType<DesktopDemoController>(); if(existing!=null) return existing;
            return new GameObject("Desktop Demo Controller").AddComponent<DesktopDemoController>();
        }
        void Awake()
        {
            manager=FindFirstObjectByType<ScenarioManager>();
            foreach(var origin in FindObjectsByType<XROrigin>(FindObjectsSortMode.None)) origin.gameObject.SetActive(false);
            foreach(var ui in FindObjectsByType<TrainingPanel>(FindObjectsSortMode.None)) ui.gameObject.SetActive(false);
            EmergencyVR.XR.WalkableGround.Ensure();
            Body=gameObject.AddComponent<CharacterController>(); Body.height=StandingBodyHeight; Body.radius=.22f; Body.center=Vector3.up*(StandingBodyHeight*.5f+.03f); Body.stepOffset=.16f; Body.skinWidth=.025f;
            var cam=new GameObject("Desktop Camera",typeof(Camera),typeof(AudioListener)); cam.transform.SetParent(transform,false); cam.transform.localPosition=Vector3.up*StandingEyeHeight;
            View=cam.GetComponent<Camera>(); View.tag="MainCamera"; View.nearClipPlane=.045f; View.farClipPlane=100;
            View.fieldOfView=72; View.clearFlags=CameraClearFlags.SolidColor; View.backgroundColor=new Color(.12f,.17f,.19f);
            DesktopAtmosphere.Apply(View);
            ResetPosition();
        }
        IEnumerator Start()
        {
            yield return null;
            Review=FindFirstObjectByType<ReviewCaseSession>();
            experience=FindFirstObjectByType<TrainingExperience>();
#if EMERGENCYVR_DESKTOP
            if(System.Environment.GetCommandLineArgs().Contains("-demo-smoke-test")) yield return Smoke();
#endif
        }
        public void ResetPosition()
        {
            Release(); Body.enabled=false; transform.SetPositionAndRotation(new Vector3(0,.05f,-1.8f),Quaternion.Euler(0,22,0)); Body.enabled=true;
            pitch=0; View.transform.localRotation=Quaternion.identity; verticalSpeed=0;
            planarVelocity=Vector3.zero; SetKneeling(false,true);
        }
        /// <summary>R: back beside the current patient (or the room entry for the technical exercise).</summary>
        public void ReturnToPatient()
        {
            var anchor=Review?.Selected?.medical!=null?Review.Procedures?.Visuals?.ChestAnchor:null;
            if(anchor!=null) { planarVelocity=Vector3.zero; FocusPatient(anchor.position); }
            else ResetPosition();
        }
        public void SetKneeling(bool kneel,bool immediate=false)
        {
            if(!kneel && Kneeling && !immediate)
            {
                // Do not stand up into a table or shelf (the patient and instruments never block standing up).
                if(!Free(transform.position,StandingBodyHeight)) { Notice("No hay espacio para levantarte aquí: muévete un poco."); return; }
            }
            Kneeling=kneel;
            float height=kneel?KneelingBodyHeight:StandingBodyHeight;
            Body.height=height; Body.center=Vector3.up*(height*.5f+.03f);
            if(immediate) { eyeHeight=kneel?KneelingEyeHeight:StandingEyeHeight; View.transform.localPosition=Vector3.up*eyeHeight; }
        }
        public void SetFreeLook(bool enabled)
        {
            FreeLook=enabled;
            Cursor.lockState=enabled?CursorLockMode.Locked:CursorLockMode.None;
            Cursor.visible=!enabled;
        }
        /// <summary>Turns so the current patient appears in the right part of the screen, clear of the drawers.</summary>
        public void FramePatientBesideDrawer()
        {
            var anchor=Review?.Selected?.medical!=null?Review.Procedures?.Visuals?.ChestAnchor:null;
            if(anchor==null || View==null) return;
            var viewport=View.WorldToViewportPoint(anchor.position);
            if(viewport.z<=0 || viewport.z>7 || viewport.x>=.45f) return;
            float horizontalFov=Camera.VerticalToHorizontalFieldOfView(View.fieldOfView,View.aspect);
            frameYawRemaining=(viewport.x-.66f)*horizontalFov;
        }
        public void SetInterfaceHidden(bool hidden)
        {
            if(experience==null || experience.InterfaceCanvas==null) return;
            InterfaceHidden=hidden;
            experience.InterfaceCanvas.enabled=!hidden;
        }
        /// <summary>The patient's body and the medical instruments are not obstacles for the doctor.</summary>
        void UpdatePassThrough(bool force)
        {
            if(!force && Time.unscaledTime<passThroughAt) return;
            passThroughAt=Time.unscaledTime+1;
            foreach(var patient in FindObjectsByType<EmergencyVR.Patient.PatientController>(FindObjectsSortMode.None))
                foreach(var c in patient.GetComponentsInChildren<Collider>(true)) Ignore(c);
            foreach(var tool in FindObjectsByType<MedicalPhysicalTool>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                foreach(var c in tool.GetComponentsInChildren<Collider>(true)) Ignore(c);
            void Ignore(Collider c){ if(c!=null && c!=Body && passThrough.Add(c)) Physics.IgnoreCollision(Body,c,true); }
        }
        /// <summary>True when a standing (or kneeling) doctor fits at this foot position.</summary>
        public bool Free(Vector3 feet,float height=0)
        {
            if(height<=0) height=Body.height;
            float r=Body.radius;
            var a=feet+Vector3.up*(r+.08f); var b=feet+Vector3.up*Mathf.Max(r+.09f,height-r);
            int n=Physics.OverlapCapsuleNonAlloc(a,b,r*.95f,overlaps,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<n;i++)
            {
                var c=overlaps[i];
                if(c==null || c==Body || passThrough.Contains(c) || c.attachedRigidbody!=null && !c.attachedRigidbody.isKinematic) continue;
                return false;
            }
            return true;
        }
        Vector3 FindFree(Vector3 preferred,Vector3 around,float radius)
        {
            if(Free(preferred)) return preferred;
            var flat=new Vector3(around.x,preferred.y,around.z);
            foreach(float ring in new[]{radius,radius+.45f,radius+.9f,radius+1.5f})
                for(int i=0;i<16;i++)
                {
                    float angle=i*Mathf.PI*2/16;
                    var candidate=flat+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*ring;
                    if(Free(candidate)) return candidate;
                }
            return preferred;
        }
        void Unstick()
        {
            if(Free(transform.position)) return;
            var anchor=Review?.Selected?.medical!=null?Review.Procedures?.Visuals?.ChestAnchor:null;
            var target=FindFree(transform.position,transform.position,.4f);
            if(!Free(target) && anchor!=null) target=FindFree(transform.position,anchor.position,1.2f);
            if(!Free(target)) return;
            Body.enabled=false; transform.position=target; Body.enabled=true; planarVelocity=Vector3.zero;
            Notice("Estabas atascado: te hemos movido a un sitio libre.");
        }
        public void Notice(string text){ noticeText=text; notice=Time.unscaledTime+3f; }
        /// <summary>Requested every frame while an action holds the view near the patient; weight 0..1.</summary>
        public void SetActionLean(Vector3 eye,Vector3 lookAt,float weight){leanEye=eye;leanLook=lookAt;leanTarget=Mathf.Clamp01(weight);}
        void LateUpdate()
        {
            leanWeight=Mathf.MoveTowards(leanWeight,leanTarget,Time.deltaTime*2.5f);
            leanTarget=0;
            if(leanWeight<=.001f || View==null) return;
            leanBasePosition=View.transform.localPosition; leanBaseRotation=View.transform.localRotation;
            var start=View.transform.position;
            var eye=Vector3.Lerp(start,leanEye,leanWeight*.85f);
            var look=leanLook-eye;
            var rotation=look.sqrMagnitude<1e-4f?View.transform.rotation:Quaternion.Slerp(View.transform.rotation,Quaternion.LookRotation(look.normalized,Vector3.up),leanWeight);
            View.transform.SetPositionAndRotation(eye,rotation); leanApplied=true;
        }

        /// <summary>World interaction ray: screen centre in free look, otherwise the mouse pointer.</summary>
        public Ray AimRay(Vector2 pointer)=>FreeLook?View.ViewportPointToRay(new Vector3(.5f,.5f,0)):View.ScreenPointToRay(pointer);
        public void FocusPatient(Vector3 chest)
        {
            Release(); Body.enabled=false; planarVelocity=Vector3.zero;
            UpdatePassThrough(true);
            var preferred=new Vector3(chest.x-.9f,.05f,chest.z-1.15f);
            transform.position=FindFree(preferred,new Vector3(chest.x,.05f,chest.z),1.2f);
            Body.enabled=true;
            var facing=Quaternion.LookRotation(chest-View.transform.position,Vector3.up).eulerAngles;
            transform.rotation=Quaternion.Euler(0,facing.y,0);
            pitch=Mathf.Clamp(facing.x>180?facing.x-360:facing.x,-70,70);
            View.transform.localRotation=Quaternion.Euler(pitch,0,0); verticalSpeed=0;
        }
        void Update()
        {
            if(leanApplied){View.transform.localPosition=leanBasePosition;View.transform.localRotation=leanBaseRotation;leanApplied=false;}
            var keys=Keyboard.current; var mouse=Mouse.current; if(keys==null || mouse==null) return;
            if(experience==null) experience=FindFirstObjectByType<TrainingExperience>();
            if(keys.escapeKey.wasPressedThisFrame) { if(FreeLook) SetFreeLook(false); experience?.TogglePause(); }
            float dt=Mathf.Min(Time.deltaTime,.05f);
            if(WorldInputBlocked) { if(FreeLook) SetFreeLook(false); if(InterfaceHidden) SetInterfaceHidden(false); planarVelocity=Vector3.zero; return; }
            if(keys.iKey.wasPressedThisFrame && experience!=null && experience.Page==ExperiencePage.Training) SetInterfaceHidden(!InterfaceHidden);
            bool handsOnPatient=Review?.Procedures?.CPR.WindowsEngaged==true || Review?.Procedures?.Choking.WindowsEngaged==true;
            // CPR and choking read the vertical pointer drag, which a locked cursor cannot provide.
            if(FreeLook && handsOnPatient) SetFreeLook(false);
            if(keys.tabKey.wasPressedThisFrame && !handsOnPatient) SetFreeLook(!FreeLook);
            if(keys.rKey.wasPressedThisFrame) ReturnToPatient();
            if(keys.leftCtrlKey.wasPressedThisFrame || keys.rightCtrlKey.wasPressedThisFrame) SetKneeling(!Kneeling);

            // Look: free look follows the mouse; otherwise hold the right button and drag.
            bool dragLook=!FreeLook && mouse.rightButton.isPressed && !PointerOverInterface;
            if((FreeLook || dragLook) && Review?.Procedures?.Choking.HasContact!=true)
            {
                var delta=mouse.delta.ReadValue(); float sensitivity=FreeLook?FreeLookSensitivity:DragLookSensitivity;
                transform.Rotate(Vector3.up,delta.x*sensitivity);
                pitch=Mathf.Clamp(pitch-delta.y*sensitivity,-85,85); View.transform.localRotation=Quaternion.Euler(pitch,0,0);
            }

            // Drawers (actions, conversation, phone) cover the left third of the screen: when one opens, turn
            // smoothly so the patient sits beside it instead of behind it. Any manual look cancels the turn.
            bool drawerOpen=experience!=null && experience.AnyDrawerOpen;
            if(drawerOpen && !drawerWasOpen) FramePatientBesideDrawer();
            drawerWasOpen=drawerOpen;
            if((FreeLook && mouse.delta.ReadValue().sqrMagnitude>4) || dragLook) frameYawRemaining=0;
            if(Mathf.Abs(frameYawRemaining)>.01f)
            {
                float step=Mathf.Sign(frameYawRemaining)*Mathf.Min(Mathf.Abs(frameYawRemaining),150f*dt);
                transform.Rotate(Vector3.up,step); frameYawRemaining-=step;
            }

            // Walk (WASD), run (Shift) and kneel (Ctrl) with a little inertia so movement does not feel robotic.
            var move=new Vector2((keys.dKey.isPressed||keys.rightArrowKey.isPressed?1:0)-(keys.aKey.isPressed||keys.leftArrowKey.isPressed?1:0),
                (keys.wKey.isPressed||keys.upArrowKey.isPressed?1:0)-(keys.sKey.isPressed||keys.downArrowKey.isPressed?1:0));
            if(move.sqrMagnitude>1) move.Normalize();
            bool rotatingHeld=held!=null && (keys.xKey.isPressed || keys.zKey.isPressed);
            bool running=!Kneeling && !rotatingHeld && keys.leftShiftKey.isPressed && move.y>0;
            float speed=Kneeling?KneelSpeed:running?RunSpeed:WalkSpeed;
            var target=(transform.right*move.x+transform.forward*move.y)*speed;
            planarVelocity=Vector3.MoveTowards(planarVelocity,target,Acceleration*speed*dt);
            verticalSpeed=Body.isGrounded?-1:Mathf.Max(-12,verticalSpeed-9.81f*dt);
            UpdatePassThrough(false);
            Body.Move((planarVelocity+Vector3.up*verticalSpeed)*dt);
            // Trying to walk but not moving while overlapping something (spawned inside furniture, pushed into a
            // prop): step out to the nearest free spot. Pushing against a real wall from free space never triggers.
            bool tryingToMove=move.sqrMagnitude>.04f;
            var horizontal=Body.velocity; horizontal.y=0;
            stuckFor=tryingToMove && horizontal.magnitude<.08f ? stuckFor+dt : 0;
            if(stuckFor>1.2f) { stuckFor=0; Unstick(); }
            // Only animate the eye while kneeling/standing up, so captures and tests that place the camera keep it.
            float eyeGoal=Kneeling?KneelingEyeHeight:StandingEyeHeight;
            if(!Mathf.Approximately(eyeHeight,eyeGoal))
            {
                eyeHeight=Mathf.MoveTowards(eyeHeight,eyeGoal,2.2f*dt);
                View.transform.localPosition=Vector3.up*eyeHeight;
            }
            if(transform.position.y< -1.5f) ReturnToPatient();

            bool overMenu=PointerOverInterface && !FreeLook;
            var pointer=mouse.position.ReadValue();
            // A click on a hands-on zone (shoulders, chest, AED button...) belongs to that zone only; inspecting the
            // patient here would also register "check responsiveness" on every click.
            if(handsOn==null) handsOn=FindFirstObjectByType<EmergencyVR.Medical.Interaction.HandsOnInteraction>();
            bool zoneFocused=handsOn!=null && handsOn.HasFocus;
            if(mouse.leftButton.wasPressedThisFrame && !overMenu && !handsOnPatient && !zoneFocused) TryInspect(AimRay(pointer));
            if(keys.eKey.wasPressedThisFrame && !overMenu) { if(held!=null) Release(); else TryGrab(AimRay(pointer)); }
            if(keys.qKey.wasPressedThisFrame && !overMenu)
            {
                var tool=held==null?null:held.GetComponent<MedicalPhysicalTool>();
                if(tool!=null) tool.Use();
                else if(Physics.Raycast(AimRay(pointer),out var useHit,2f))
                {
                    useHit.collider.GetComponentInParent<MedicalPhysicalTool>()?.Use();
                    useHit.collider.GetComponentInParent<MedicalWorldButton>()?.Use();
                }
            }
            if(held!=null)
            {
                heldDistance=Mathf.Clamp(heldDistance+mouse.scroll.ReadValue().y*.0015f,.35f,2f);
                float direction=(keys.xKey.isPressed?1:0)-(keys.zKey.isPressed?1:0);
                var axis=keys.leftShiftKey.isPressed?View.transform.right:keys.leftAltKey.isPressed?View.transform.forward:Vector3.up;
                held.MoveRotation(Quaternion.AngleAxis(direction*90*Time.deltaTime,axis)*held.rotation);
            }
        }
        void FixedUpdate()
        {
            if(held==null || WorldInputBlocked) return;
            Vector3 target=View.transform.position+View.transform.forward*heldDistance;
            if(Physics.Linecast(View.transform.position,target,out var hit,~0,QueryTriggerInteraction.Ignore) && hit.rigidbody!=held) target=hit.point-View.transform.forward*.035f;
            held.MovePosition(target);
        }
        public bool TryInspect(Ray ray)
        {
            if(!Physics.Raycast(ray,out var hit,3f,~0,QueryTriggerInteraction.Ignore)) return false;
            var patient=hit.collider.GetComponentInParent<PatientController>(); if(patient==null) return false;
            patient.GetComponent<XRSimpleInteractable>().selectEntered.Invoke(new SelectEnterEventArgs());
            return true;
        }
        public bool TryGrab(Ray ray)
        {
            if(!Physics.Raycast(ray,out var hit,2f,~0,QueryTriggerInteraction.Ignore)) return false;
            var grab=hit.collider.GetComponentInParent<XRGrabInteractable>(); if(grab==null || !grab.isActiveAndEnabled || grab.isSelected) return false;
            held=grab.GetComponent<Rigidbody>(); if(held==null) return false;
            heldKinematic=held.isKinematic; heldGravity=held.useGravity; held.isKinematic=true; held.useGravity=false;
            held.GetComponent<MedicalPhysicalTool>()?.OnGrabbed();
            return true;
        }
        public void Release()
        {
            if(held==null) return; var released=held; held=null;
            released.isKinematic=heldKinematic; released.useGravity=heldGravity;
            released.GetComponent<MedicalPhysicalTool>()?.OnReleased();
        }
        void OnDisable() { Release(); if(FreeLook) SetFreeLook(false); if(InterfaceHidden) SetInterfaceHidden(false); if(leanApplied&&View!=null){View.transform.localPosition=leanBasePosition;View.transform.localRotation=leanBaseRotation;leanApplied=false;} }
        GUIStyle noticeStyle;
        void OnGUI()
        {
            if(Time.unscaledTime<notice && !string.IsNullOrEmpty(noticeText))
            {
                if(noticeStyle==null) noticeStyle=new GUIStyle(GUI.skin.box){fontSize=Mathf.RoundToInt(Mathf.Clamp(Screen.height/48f,13,24)),wordWrap=true,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}};
                float w=Mathf.Min(Screen.width*.5f,620), h=noticeStyle.CalcHeight(new GUIContent(noticeText),w)+14;
                GUI.Box(new Rect((Screen.width-w)*.5f,Screen.height*.58f,w,h),noticeText,noticeStyle);
            }
            if(!FreeLook || WorldInputBlocked) return;
            // Small crosshair: in free look every click, grab and use goes through the screen centre.
            float x=Screen.width*.5f, y=Screen.height*.5f;
            var previous=GUI.color;
            GUI.color=new Color(0,0,0,.55f); GUI.DrawTexture(new Rect(x-4,y-4,8,8),Texture2D.whiteTexture);
            GUI.color=new Color(1,1,1,.9f); GUI.DrawTexture(new Rect(x-2.5f,y-2.5f,5,5),Texture2D.whiteTexture);
            GUI.color=previous;
        }

        IEnumerator Smoke()
        {
            yield return new WaitForSeconds(1);
            bool passed=false; string error="";
            try
            {
                manager.StartCase();
                var patient=FindFirstObjectByType<PatientController>();
                Body.enabled=false; transform.position=new Vector3(.1f,.05f,1.95f); Body.enabled=true; Physics.SyncTransforms();
                var target=patient.GetComponentsInChildren<Collider>().First().bounds.center;
                if(!TryInspect(new Ray(View.transform.position,target-View.transform.position))) throw new InvalidOperationException("Desktop patient ray missed.");
                manager.CompleteDemoTransition(); manager.FinishCase();
                var result=FindFirstObjectByType<EvaluationManager>().LatestResult;
                if(result==null || result.ScorePercent!=100) throw new InvalidOperationException("Technical sequence did not score 100.");
                foreach(var index in Enumerable.Range(1,Review.Catalog.entries.Length-1))
                {
                    if(!Review.Select(index)) throw new InvalidOperationException("Case selection failed.");
                    manager.StartCase();
                    if(manager.MedicalSession.Capabilities.usesObjectiveBasedEvaluation)
                    {
                        var authored=Review.Selected.medical.clinicalV2;
                        if(manager.MedicalSession.ClinicalState.ClinicalStateId!=authored.metadata.initialClinicalStateId || manager.MedicalSession.Observations.Count!=0)
                            throw new InvalidOperationException("Clinical attempt did not start with an isolated observation history.");
                        manager.SubmitAction("ReassessPatient"); manager.SubmitAction("ReassessPatient");
                        if(manager.MedicalSession.ClinicalEvents.Count(e=>e.eventType=="ActionRejected")>0)
                            throw new InvalidOperationException("Clinical repeatable action was rejected.");
                        manager.FinishCase();
                        if(manager.MedicalSession.ObjectiveProgress.Count!=authored.learningObjectives.Length)
                            throw new InvalidOperationException("Clinical objectives were lost at finish.");
                        var clinicalExport=Review.ExportResult();if(!File.Exists(clinicalExport))throw new InvalidOperationException("Missing clinical export.");
                        continue;
                    }
                    foreach(var step in Review.Selected.medical.recommendedSequence)
                    {
                        var earliest=manager.MedicalSession.EarliestTime(step);
                        if(earliest>manager.MedicalSession.Elapsed) manager.AdvanceTrainingTime(earliest-manager.MedicalSession.Elapsed+.01);
                        Review.Submit(step);
                    }
                    manager.FinishCase();
                    if(Review.Score!=100) throw new InvalidOperationException("Medical case failed: "+Review.Selected.medical.id+" score "+Review.Score+" "+string.Join(",",manager.MedicalResult.criticalErrors));
                    var export=Review.ExportResult(); if(!File.Exists(export)) throw new InvalidOperationException("Missing medical export.");
                }
                Review.ExportResult(); passed=true;
            }
            catch(Exception exception) { error=exception.ToString(); Debug.LogException(exception); }
            if(passed)
            {
                var args=System.Environment.GetCommandLineArgs();var flag=Array.IndexOf(args,"-demo-capture-directory");
                if(flag>=0 && flag+1<args.Length)
                {
                    Directory.CreateDirectory(args[flag+1]);
                    if(experience!=null) experience.InterfaceCanvas.enabled=false;
                    View.cullingMask=~0;
                    foreach(var environment in Review.Scope.environments.Select(e=>e.id))
                    {
                        var index=Array.FindIndex(Review.Catalog.entries,e=>e.medical?.environment==environment);
                        Review.Select(index); ResetPosition();
                        EmergencyVR.Environment.ScenarioEnvironmentPresenter.GetCaptureView(environment,out var position,out var lookAt);
                        View.transform.position=position;View.transform.LookAt(lookAt);
                        yield return new WaitForEndOfFrame();
                        RuntimeCapture.Save(View,Path.Combine(args[flag+1],"vital-"+environment+".png"));
                        yield return new WaitForSeconds(.3f);
                    }
                    var visuals=Review.Procedures.Visuals;
                    View.transform.position=visuals.ChestAnchor.position+new Vector3(.65f,.65f,.9f);
                    View.transform.LookAt(visuals.ChestAnchor.position+Vector3.forward*.45f);
                    RuntimeCapture.Save(View,Path.Combine(args[flag+1],"vital-patient-closeup.png"));
                }
                var captureDirectory=flag>=0&&flag+1<args.Length?args[flag+1]:null;
                var physical=ProcedureValidation.Run(Review,View,captureDirectory);
                while(true)
                {
                    bool more=false; object current=null;
                    try {more=physical.MoveNext();if(more)current=physical.Current;}
                    catch(Exception exception){passed=false;error=exception.ToString();Debug.LogException(exception);}
                    if(!more)break;yield return current;
                }
            }
            File.WriteAllText(Path.Combine(Application.persistentDataPath,"desktop-smoke.json"),JsonUtility.ToJson(new SmokeResult {passed=passed,error=error}));
            Debug.Log("DESKTOP_SMOKE "+(passed?"PASS":"FAIL")); Application.Quit(passed?0:1);
        }
        [Serializable] sealed class SmokeResult {public bool passed; public string error;}
    }
}
