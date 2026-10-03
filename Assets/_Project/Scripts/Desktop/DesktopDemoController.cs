using System;
using System.Collections;
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
            Body=gameObject.AddComponent<CharacterController>(); Body.height=1.72f; Body.radius=.22f; Body.center=Vector3.up*.89f; Body.stepOffset=.16f; Body.skinWidth=.025f;
            var cam=new GameObject("Desktop Camera",typeof(Camera),typeof(AudioListener)); cam.transform.SetParent(transform,false); cam.transform.localPosition=Vector3.up*1.62f;
            View=cam.GetComponent<Camera>(); View.tag="MainCamera"; View.nearClipPlane=.045f; View.farClipPlane=100;
            View.fieldOfView=65; View.clearFlags=CameraClearFlags.SolidColor; View.backgroundColor=new Color(.12f,.17f,.19f);
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
        }
        public void FocusPatient(Vector3 chest)
        {
            Release(); Body.enabled=false;
            transform.position=new Vector3(chest.x+.9f,.05f,chest.z-1.15f);
            Body.enabled=true;
            var facing=Quaternion.LookRotation(chest-View.transform.position,Vector3.up).eulerAngles;
            transform.rotation=Quaternion.Euler(0,facing.y,0);
            pitch=Mathf.Clamp(facing.x>180?facing.x-360:facing.x,-70,70);
            View.transform.localRotation=Quaternion.Euler(pitch,0,0); verticalSpeed=0;
        }
        void Update()
        {
            var keys=Keyboard.current; var mouse=Mouse.current; if(keys==null || mouse==null) return;
            if(experience==null) experience=FindFirstObjectByType<TrainingExperience>();
            if(keys.escapeKey.wasPressedThisFrame) experience?.TogglePause();
            if(WorldInputBlocked) return;
            if(keys.rKey.wasPressedThisFrame) ResetPosition();
            if(mouse.rightButton.isPressed && !PointerOverInterface)
            {
                var delta=mouse.delta.ReadValue(); transform.Rotate(Vector3.up,delta.x*.09f);
                pitch=Mathf.Clamp(pitch-delta.y*.09f,-70,70); View.transform.localRotation=Quaternion.Euler(pitch,0,0);
            }
            var move=new Vector2((keys.dKey.isPressed?1:0)-(keys.aKey.isPressed?1:0),(keys.wKey.isPressed?1:0)-(keys.sKey.isPressed?1:0));
            if(move.sqrMagnitude>1) move.Normalize();
            verticalSpeed=Body.isGrounded?-1:Mathf.Max(-12,verticalSpeed-9.81f*Time.deltaTime);
            Body.Move((transform.right*move.x*1.6f+transform.forward*move.y*1.6f+Vector3.up*verticalSpeed)*Mathf.Min(Time.deltaTime,.05f));
            if(transform.position.y< -1.5f) ResetPosition();
            bool overMenu=PointerOverInterface;
            if(mouse.leftButton.wasPressedThisFrame && !overMenu && Review?.Procedures?.CPR.WindowsEngaged!=true) TryInspect(View.ScreenPointToRay(mouse.position.ReadValue()));
            if(keys.eKey.wasPressedThisFrame && !overMenu) { if(held!=null) Release(); else TryGrab(View.ScreenPointToRay(mouse.position.ReadValue())); }
            if(keys.qKey.wasPressedThisFrame && !overMenu)
            {
                var tool=held==null?null:held.GetComponent<MedicalPhysicalTool>();
                if(tool!=null) tool.Use();
                else if(Physics.Raycast(View.ScreenPointToRay(mouse.position.ReadValue()),out var useHit,2f))
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
        void OnDisable() { Release(); }

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
