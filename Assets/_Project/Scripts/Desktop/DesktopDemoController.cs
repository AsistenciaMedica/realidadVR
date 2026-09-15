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
        bool menu=true;
        string interaction="Acércate al paciente y haz clic para seleccionarlo.";
        Rect panel;
        GUIStyle label,heading,button,primaryButton,muted,field,panelStyle,brandHeading;
        Texture2D brandLockup,brandMark;
        readonly System.Collections.Generic.List<Texture2D> brandTextures=new System.Collections.Generic.List<Texture2D>();
        Vector2 scroll;
        string search="", seedText="2026";
        bool showLibrary;
        public bool HasHeldTool => held!=null;
        public bool PointerOverInterface => menu&&Mouse.current!=null&&panel.Contains(new Vector2(Mouse.current.position.x.ReadValue(),Screen.height-Mouse.current.position.y.ReadValue()));

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
            if(useDesktop) SceneManager.sceneLoaded+=Loaded;
        }
        static void Loaded(Scene scene,LoadSceneMode mode)
        {
            if(scene.name=="TrainingRoom") Create();
        }
        public static DesktopDemoController Create()
        {
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
            ResetPosition();
        }
        IEnumerator Start()
        {
            yield return null;
            Review=FindFirstObjectByType<ReviewCaseSession>();
#if EMERGENCYVR_DESKTOP
            if(System.Environment.GetCommandLineArgs().Contains("-demo-smoke-test")) yield return Smoke();
#endif
        }
        public void ResetPosition()
        {
            Release(); Body.enabled=false; transform.SetPositionAndRotation(new Vector3(0,.05f,-1.8f),Quaternion.Euler(0,22,0)); Body.enabled=true;
            pitch=0; View.transform.localRotation=Quaternion.identity; verticalSpeed=0;
        }
        void Update()
        {
            var keys=Keyboard.current; var mouse=Mouse.current; if(keys==null || mouse==null) return;
            if(keys.escapeKey.wasPressedThisFrame) menu=!menu;
            if(keys.rKey.wasPressedThisFrame) ResetPosition();
            if(mouse.rightButton.isPressed)
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
            if(keys.eKey.wasPressedThisFrame) { if(held!=null) Release(); else TryGrab(View.ScreenPointToRay(mouse.position.ReadValue())); }
            if(keys.qKey.wasPressedThisFrame)
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
            if(held==null) return;
            Vector3 target=View.transform.position+View.transform.forward*heldDistance;
            if(Physics.Linecast(View.transform.position,target,out var hit,~0,QueryTriggerInteraction.Ignore) && hit.rigidbody!=held) target=hit.point-View.transform.forward*.035f;
            held.MovePosition(target);
        }
        public bool TryInspect(Ray ray)
        {
            if(!Physics.Raycast(ray,out var hit,3f,~0,QueryTriggerInteraction.Ignore)) return false;
            var patient=hit.collider.GetComponentInParent<PatientController>(); if(patient==null) return false;
            patient.GetComponent<XRSimpleInteractable>().selectEntered.Invoke(new SelectEnterEventArgs());
            interaction=manager.Feedback; return true;
        }
        public bool TryGrab(Ray ray)
        {
            if(!Physics.Raycast(ray,out var hit,2f,~0,QueryTriggerInteraction.Ignore)) return false;
            var grab=hit.collider.GetComponentInParent<XRGrabInteractable>(); if(grab==null || !grab.isActiveAndEnabled || grab.isSelected) return false;
            held=grab.GetComponent<Rigidbody>(); if(held==null) return false;
            heldKinematic=held.isKinematic; heldGravity=held.useGravity; held.isKinematic=true; held.useGravity=false;
            held.GetComponent<MedicalPhysicalTool>()?.OnGrabbed();
            interaction="Objeto sujeto. Pulsa E para soltar."; return true;
        }
        public void Release()
        {
            if(held==null) return; var released=held; held=null;
            released.isKinematic=heldKinematic; released.useGravity=heldGravity;
            released.GetComponent<MedicalPhysicalTool>()?.OnReleased();
        }
        void OnDisable() { Release(); }

        Texture2D BrandTexture(string name,Color fill,Color border)
        {
            var texture=new Texture2D(3,3,TextureFormat.RGBA32,false) { name="Vital VR UI "+name,hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp };
            var pixels=new Color[9];for(int i=0;i<pixels.Length;i++)pixels[i]=i==4?fill:border;
            texture.SetPixels(pixels);texture.Apply(false,true);brandTextures.Add(texture);return texture;
        }
        void PrepareBrandStyles()
        {
            label=new GUIStyle(GUI.skin.label) {fontSize=15,wordWrap=true};label.normal.textColor=VitalBrand.White;
            heading=new GUIStyle(label) {fontSize=20,fontStyle=FontStyle.Bold};
            muted=new GUIStyle(label) {fontSize=13};muted.normal.textColor=VitalBrand.Muted;
            brandHeading=new GUIStyle(heading) {fontSize=27,fontStyle=FontStyle.BoldAndItalic,richText=true};
            var surface=BrandTexture("surface",VitalBrand.Surface,VitalBrand.Line);
            var hover=BrandTexture("hover",VitalBrand.Line,VitalBrand.Cyan);
            var pressed=BrandTexture("pressed",VitalBrand.Navy,VitalBrand.Cyan);
            var action=BrandTexture("action",VitalBrand.Action,VitalBrand.Action);
            var actionHover=BrandTexture("action hover",Color.Lerp(VitalBrand.Action,VitalBrand.Red,.2f),VitalBrand.Cyan);
            panelStyle=new GUIStyle(GUI.skin.box) {border=new RectOffset(1,1,1,1),padding=new RectOffset(0,0,0,0)};
            panelStyle.normal.background=BrandTexture("panel",VitalBrand.Navy,VitalBrand.Line);
            button=new GUIStyle(GUI.skin.button) {fontSize=14,wordWrap=true,padding=new RectOffset(10,10,9,9),border=new RectOffset(1,1,1,1)};
            button.normal.background=surface;button.normal.textColor=VitalBrand.White;
            button.hover.background=hover;button.hover.textColor=VitalBrand.White;
            button.active.background=pressed;button.active.textColor=VitalBrand.White;
            button.focused.background=hover;button.focused.textColor=VitalBrand.White;
            button.onNormal.background=hover;button.onNormal.textColor=VitalBrand.White;
            button.onHover.background=hover;button.onHover.textColor=VitalBrand.White;
            button.onActive.background=pressed;button.onActive.textColor=VitalBrand.White;
            button.onFocused.background=hover;button.onFocused.textColor=VitalBrand.White;
            primaryButton=new GUIStyle(button);primaryButton.normal.background=action;primaryButton.hover.background=actionHover;
            primaryButton.active.background=action;primaryButton.focused.background=actionHover;
            field=new GUIStyle(GUI.skin.textField) {fontSize=14,padding=new RectOffset(9,9,7,7),border=new RectOffset(1,1,1,1)};
            field.normal.background=surface;field.normal.textColor=VitalBrand.White;
            field.focused.background=hover;field.focused.textColor=VitalBrand.White;
            field.hover.background=surface;field.hover.textColor=VitalBrand.White;
            brandLockup=VitalBrand.LoadLockup();brandMark=VitalBrand.LoadMark();
        }
        void DrawBrandHeader()
        {
            if(brandLockup!=null)
            {
                var rect=GUILayoutUtility.GetRect(1,54,GUILayout.ExpandWidth(true));
                float ratio=Mathf.Min(Mathf.Min(300,rect.width)/brandLockup.width,rect.height/brandLockup.height);
                var size=new Vector2(brandLockup.width*ratio,brandLockup.height*ratio);
                GUI.DrawTexture(new Rect(rect.x,rect.y+(rect.height-size.y)*.5f,size.x,size.y),brandLockup,ScaleMode.ScaleToFit,true);
            }
            else GUILayout.Label("Vital <color=#ED1939>VR</color>",brandHeading);
            GUILayout.Label(VitalBrand.Tagline,muted);
            GUILayout.Space(3);
        }
        void OnDestroy()
        {
            // Only generated GUI backgrounds are owned here; Resources brand images are shared assets.
            foreach(var texture in brandTextures)if(texture!=null)Destroy(texture);
            brandTextures.Clear();
        }

        void OnGUI()
        {
            if(manager==null) return;
            if(label==null)
            {
                PrepareBrandStyles();
            }
            if(!menu)
            {
                bool hints=Review?.Procedures?.TrainingMode==true;
                GUI.Box(new Rect(14,14,Mathf.Min(710,Screen.width-28),hints?140:50),GUIContent.none,panelStyle);
                if(brandMark!=null)GUI.DrawTexture(new Rect(24,24,27,27),brandMark,ScaleMode.ScaleToFit,true);
                GUI.Label(new Rect(60,22,Mathf.Min(650,Screen.width-88),40),"WASD · mirar: botón derecho · E: coger · Q: usar · C: RCP · Esc: panel",muted);
                if(hints) GUI.Label(new Rect(24,69,Mathf.Min(670,Screen.width-48),77),Review.Procedures.Hint,label);
                return;
            }
            panel=new Rect(16,16,Mathf.Min(410,Screen.width-32),Screen.height-32);
            GUI.Box(panel,GUIContent.none,panelStyle);
            GUILayout.BeginArea(new Rect(panel.x+14,panel.y+12,panel.width-28,panel.height-24));
            scroll=GUILayout.BeginScrollView(scroll);
            DrawBrandHeader();
            GUILayout.Label("Revisión de desarrollo · Windows",muted);
            GUILayout.Label("WASD: caminar · botón derecho: mirar\nE: coger/soltar · Q: usar · rueda: alcance\nZ/X: orientar · Shift/Alt: otros ejes\nC: RCP manual · R: volver · Esc: panel",muted);
            if(Review!=null)
            {
                GUI.enabled=!manager.IsRunning;
                if(GUILayout.Button(showLibrary?"Cerrar biblioteca":"Biblioteca · "+(Review.Catalog.entries.Length-1)+" escenarios",button)) showLibrary=!showLibrary;
                if(showLibrary)
                {
                    GUILayout.Label("Buscar nombre, categoría o ambiente",label); search=GUILayout.TextField(search,field);
                    for(int i=0;i<Review.Catalog.entries.Length;i++)
                    {
                        var entry=Review.Catalog.entries[i]; string description=entry.definition.displayName+" · "+entry.medical?.category+" · "+entry.medical?.environment;
                        if(search.Length>0&&description.IndexOf(search,StringComparison.OrdinalIgnoreCase)<0)continue;
                        if(GUILayout.Button(description,button)) {Review.Select(i);showLibrary=false;scroll=Vector2.zero;break;}
                    }
                }
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("‹ Caso",button)) Review.Select((Review.SelectedIndex+Review.Catalog.entries.Length-1)%Review.Catalog.entries.Length);
                if(GUILayout.Button("Caso ›",button)) Review.Select((Review.SelectedIndex+1)%Review.Catalog.entries.Length);
                GUILayout.EndHorizontal(); GUI.enabled=true;
                GUILayout.Label(Review.Selected.definition.displayName,heading);
                GUILayout.Label(Review.Selected.briefing,label);
                if(Review.Selected.medical!=null)
                {
                    GUILayout.Label(Review.Selected.medical.category+" · "+Review.Selected.medical.environment+" · "+Review.Selected.medical.difficulty,label);
                    GUILayout.Label("PENDING MEDICAL VALIDATION · CLIENT_REVIEW",label);
                    GUI.enabled=!manager.IsRunning; GUILayout.Label("Seed reproducible",label);seedText=GUILayout.TextField(seedText,field);
                    if(int.TryParse(seedText,out var seed)) manager.MedicalSeed=seed; GUI.enabled=true;
                    if(Review.Procedures.TrainingMode) GUILayout.Label(Review.PatientReadout(),label);
                    GUI.enabled=!manager.IsRunning;
                    if(GUILayout.Button(Review.Procedures.TrainingMode?"Modo: entrenamiento":"Modo: evaluación",button)) Review.Procedures.TrainingMode=!Review.Procedures.TrainingMode;
                    GUI.enabled=true;
                    if(Review.Procedures.TrainingMode) GUILayout.Label(Review.Procedures.Hint,label);
                    if(manager.MedicalSession!=null) GUILayout.Label($"Tiempo simulado: {manager.MedicalSession.Elapsed:0} s",label);
                }
            }
            GUI.enabled=!manager.IsRunning; if(GUILayout.Button("Iniciar intento",primaryButton)) manager.StartCase(); GUI.enabled=true;
            GUILayout.Label(manager.Feedback,label);
            if(manager.IsRunning && Review!=null)
            {
                GUILayout.Label("Registrar una acción",heading);
                foreach(var id in Review.ActionIds)
                {
                    bool physical=Review.Procedures.IsPhysicalAction(id);
                    GUI.enabled=!physical;
                    if(GUILayout.Button(Review.ActionLabel(id)+(physical?" · usar equipo":"")+(manager.MedicalSession?.Completed.Contains(id)==true?" ✓":""),button)) Review.Submit(id);
                    GUI.enabled=true;
                }
                if(Review.Selected.medical!=null && GUILayout.Button("Avanzar 30 s de simulación",button)) manager.AdvanceTrainingTime(30);
            }
            GUI.enabled=manager.IsRunning; if(GUILayout.Button("Finalizar y evaluar",button)) manager.FinishCase(); GUI.enabled=true;
            var result=FindFirstObjectByType<EvaluationManager>().LatestResult;
            if(manager.MedicalResult!=null)
            {
                var medical=manager.MedicalResult;
                if(medical.procedures!=null)
                {
                    var p=medical.procedures; var c=p.cpr;
                    GUILayout.Label($"RCP virtual: {c.compressions} compresiones · {c.meanDepthMeters*100:0.0} cm · {c.meanRatePerMinute:0}/min\nParche D/I: {p.rightPadPlaced}/{p.leftPadPlaced} · Descargas: {p.shocks} · Lecturas: {p.measurements}",label);
                }
                GUILayout.Label($"{medical.scorePercent:0}/100 · {medical.outcome}\n{medical.durationSeconds:0} s · seed {medical.seed}",heading);
                GUILayout.Label("ERRORES CRÍTICOS\n"+(medical.criticalErrors.Length==0?"Ninguno":string.Join("\n",medical.criticalErrors)),label);
                GUILayout.Label("Correctas: "+string.Join(", ",medical.correctActions)+"\nIncorrectas: "+string.Join(", ",medical.incorrectActions)+"\nOmitidas: "+string.Join(", ",medical.omittedActions),label);
                foreach(var section in medical.sections) GUILayout.Label(section.name+": "+(section.measured?section.scorePercent.ToString("0")+"/100":"No evaluado"),label);
                foreach(var entry in medical.timeline) GUILayout.Label($"{entry.elapsedSeconds:0}s · {entry.id} · {entry.disposition}",label);
                foreach(var recommendation in medical.recommendations) GUILayout.Label(recommendation,label);
                foreach(var reference in medical.medicalReferences) GUILayout.Label(reference.organization+" · "+reference.title+" · "+reference.url,label);
                if(GUILayout.Button("Guardar resultado JSON",button)) Review.ExportResult();
            }
            if(result!=null)
            {
                GUILayout.Label($"{result.ScorePercent:0}/100 · {result.DurationSeconds:0.0} s\nErrores: {result.Errors} · Omisiones: {result.OmittedActions.Count}",heading);
                if(Review!=null && GUILayout.Button("Guardar resultado JSON",button)) Review.ExportResult();
            }
            if(Review!=null) GUILayout.Label(Review.ExportMessage,label);
            GUILayout.Label(interaction,label);
            if(GUILayout.Button("Volver al punto de inicio",button)) ResetPosition();
            if(GUILayout.Button("Salir",button)) Application.Quit();
            GUILayout.EndScrollView(); GUILayout.EndArea();
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
                    Directory.CreateDirectory(args[flag+1]); menu=false;
                    foreach(var environment in new[]{"gym","mall","dental","football"})
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
