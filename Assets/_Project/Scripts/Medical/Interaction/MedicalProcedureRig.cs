using System;
using System.Collections.Generic;
using System.Linq;
using EmergencyVR.Scenarios;
using EmergencyVR.Patient.Presentation;
using EmergencyVR.Patient;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace EmergencyVR.Medical.Interaction
{
    public sealed class MedicalProcedureRig : MonoBehaviour
    {
        public ReviewCaseSession Review {get;private set;}
        public ScenarioManager Manager => Review == null ? null : Review.Manager;
        public MedicalInteractionSettings Settings {get;private set;}
        public PatientVisualController Visuals {get;private set;}
        public CPRInteractionController CPR {get;private set;}
        public AEDInteractionController AED {get;private set;}
        public MedicalProcedureAudio Audio {get;private set;}
        public Material GloveMaterial {get;private set;}
        public bool TrainingMode {get;set;}=true;
        public string Hint {get;set;}="Acércate al paciente. C: RCP · E: coger/soltar · Q: usar objeto.";
        public int Measurements {get;set;}
        public int UnsafeAttempts {get;set;}
        public bool IsWindows => FindFirstObjectByType<EmergencyVR.Desktop.DesktopDemoController>()!=null;
        readonly List<Material> materials=new List<Material>();
        readonly List<MedicalPhysicalTool> tools=new List<MedicalPhysicalTool>();
        Material white,navy,red,cyan,metal;
        GameObject equipment,airway,hiddenSceneryAED;bool hiddenSceneryWasActive;MedicalScenarioRuntime lastSession;bool wasRunning;TextMesh bedside;
        public static MedicalProcedureRig Attach(ReviewCaseSession review)
        {
            if(review==null)throw new ArgumentNullException(nameof(review));
            var existing=FindFirstObjectByType<MedicalProcedureRig>();if(existing!=null&&existing.Review==review)return existing;
            var go=new GameObject("Vital VR medical interaction kit");var rig=go.AddComponent<MedicalProcedureRig>();rig.Initialize(review);return rig;
        }
        void Initialize(ReviewCaseSession review)
        {
            Review=review;Settings=JsonUtility.FromJson<MedicalInteractionSettings>(Resources.Load<TextAsset>("MedicalInteractionSettings").text);Settings.Validate();
            Visuals=PatientVisualController.Install(FindFirstObjectByType<PatientController>());
            Material Make(Color color){var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.25f);materials.Add(m);return m;}
            white=Make(new Color(.85f,.87f,.86f));navy=Make(new Color(.025f,.045f,.07f));red=Make(new Color(.65f,.02f,.045f));cyan=Make(new Color(.1f,.45f,.53f));metal=Make(new Color(.32f,.4f,.43f));GloveMaterial=Make(new Color(.48f,.65f,.74f));
            Audio=gameObject.AddComponent<MedicalProcedureAudio>();Audio.Initialize(Visuals.ChestAnchor);
            AED=gameObject.AddComponent<AEDInteractionController>();AED.Initialize(this);
            CPR=gameObject.AddComponent<CPRInteractionController>();CPR.Initialize(this,Visuals);
            BuildKit();Review.SelectionChanged+=Selected;Manager.Changed+=StateChanged;Selected();
        }
        public bool IsPhysicalAction(string id)
        {
            var action=Review.Selected.medical?.actions.FirstOrDefault(a=>a.id==id)?.action;
            return new[]{"StartCPR","ChestCompression","ContinueCPR","BringAED","AttachAEDPads","AnalyzeRhythm","DeliverAEDShock","FollowNoShock","CheckBloodPressure","CheckSpO2","CheckGlucose","CallEmergencyServices","OpenAirway","ApplyBandage","UseEpinephrineAutoInjector"}.Contains(action);
        }
        public bool SubmitNext(string action)
        {
            if(Manager==null||!Manager.IsRunning||Manager.MedicalSession==null||Manager.MedicalDefinition==null)return false;
            var done=Manager.MedicalSession.Completed;
            var rule=Manager.MedicalDefinition.actions.FirstOrDefault(r=>r.action==action&&!done.Contains(r.id)&&r.kind!="dangerous"&&r.kind!="incorrect"&&r.prerequisites.All(done.Contains)&&Manager.MedicalSession.Elapsed>=Manager.MedicalSession.EarliestTime(r.id));
            if(rule==null)return false;
            Manager.SubmitAction(rule.id);return Manager.MedicalSession.Completed.Contains(rule.id);
        }
        public string NextId(string action)
        {
            var session=Manager?.MedicalSession;var definition=Manager?.MedicalDefinition;
            if(session==null||definition==null||!Manager.IsRunning)return null;
            var done=session.Completed;
            return definition.actions.FirstOrDefault(a=>a.action==action&&!done.Contains(a.id)&&a.kind=="required")?.id;
        }
        void Selected()
        {
            // Return attached instruments to their owner before hiding the kit or switching patients/environments.
            ResetTools();AED.ResetAttempt();CPR.ResetAttempt();CPR.SetWindowsEngaged(false);RestoreSceneryAED();
            bool medical=Review.Selected.medical!=null;
            equipment.SetActive(medical);if(airway!=null)airway.SetActive(medical);
            Measurements=0;UnsafeAttempts=0;
            if(!medical)return;
            var anchor=GameObject.Find("PortableEquipmentAnchor");equipment.transform.position=anchor!=null?anchor.transform.position:new Vector3(-1.8f,.98f,3.75f);
            var scenery=anchor==null?null:anchor.transform.parent.Find("Reused medical equipment/DefibrillatorPlaceholder");
            if(scenery!=null){hiddenSceneryAED=scenery.gameObject;hiddenSceneryWasActive=hiddenSceneryAED.activeSelf;hiddenSceneryAED.SetActive(false);}
            Hint="E: coger/soltar · Q: usar equipo · C: RCP manual";
        }
        void StateChanged()
        {
            if(Manager.MedicalSession!=lastSession){lastSession=Manager.MedicalSession;ResetTools();AED.ResetAttempt();CPR.ResetAttempt();Measurements=0;UnsafeAttempts=0;}
            Audio.SetPatient(Manager.MedicalSession?.Patient??Review.Selected.medical?.initialState);
            if(wasRunning&&!Manager.IsRunning&&Manager.MedicalResult!=null)Manager.MedicalResult.procedures=Report();
            wasRunning=Manager.IsRunning;
            if(bedside!=null)bedside.text="VITAL VR\n"+(TrainingMode?Review.PatientReadout():"MODO EVALUACIÓN\nConsultar instrumentos")+"\n"+(TrainingMode?Hint:"");
        }
        public ProcedureMetrics Report()=>new ProcedureMetrics {mode=TrainingMode?"TRAINING":"EVALUATION",cpr=CPR.Metrics,rightPadPlaced=AED.State.RightPad,leftPadPlaced=AED.State.LeftPad,shocks=AED.State.Shocks,measurements=Measurements,unsafeDeviceAttempts=UnsafeAttempts};
        void ResetTools(){foreach(var tool in tools)if(tool!=null)tool.ResetTool();}
        void BuildKit()
        {
            equipment=new GameObject("Portable medical tools");equipment.transform.SetParent(transform,false);
            // Fold-out worktop supports the complete kit; the original cart alone is narrower than these tools.
            Shape(equipment,"Fold-out worktop",new Vector3(0,-.025f,0),new Vector3(2.08f,.04f,.48f),white);
            var trayCollider=new GameObject("Worktop physics",typeof(BoxCollider));trayCollider.transform.SetParent(equipment.transform,false);trayCollider.transform.localPosition=new Vector3(0,-.025f,0);trayCollider.GetComponent<BoxCollider>().size=new Vector3(2.08f,.04f,.48f);
            foreach(float x in new[]{-.93f,.93f})foreach(float z in new[]{-.17f,.17f})
                Shape(equipment,"Folding support",new Vector3(x,-.51f,z),new Vector3(.032f,.94f,.032f),metal);
            Shape(equipment,"Tablet support",new Vector3(0,.20f,.20f),new Vector3(.03f,.42f,.03f),metal);
            Shape(equipment,"Bedside tablet case",new Vector3(0,.47f,.20f),new Vector3(.75f,.40f,.032f),navy);
            var caseRoot=new GameObject("Interactive AED");caseRoot.transform.SetParent(equipment.transform,false);caseRoot.transform.localPosition=new Vector3(0,.09f,0);
            Shape(caseRoot,"AED shell",Vector3.zero,new Vector3(.32f,.12f,.25f),white);
            Shape(caseRoot,"Rubber trim",new Vector3(0,-.025f,0),new Vector3(.34f,.05f,.27f),navy);
            var lid=Shape(caseRoot,"AED lid",new Vector3(0,.075f,.06f),new Vector3(.32f,.03f,.13f),cyan);AED.Lid=lid.transform;
            Shape(caseRoot,"AED display glass",new Vector3(-.035f,.067f,-.06f),new Vector3(.215f,.005f,.09f),navy);
            AED.Display=Display(caseRoot,"AED screen",new Vector3(-.035f,.071f,-.06f),.022f);AED.Display.transform.localRotation=Quaternion.Euler(90,0,0);
            MedicalDeviceTextFit.Fit(AED.Display,.185f,.065f);
            Shape(caseRoot,"Shock button",new Vector3(.09f,.075f,-.06f),new Vector3(.04f,.025f,.04f),red);
            AddTool(caseRoot,MedicalToolKind.AED,new Vector3(.36f,.16f,.3f));AED.Device=caseRoot.transform;
            Pad(true,new Vector3(-.35f,.04f,0));Pad(false,new Vector3(-.48f,.04f,0));
            Instrument("Tensiómetro",MedicalToolKind.BloodPressure,new Vector3(.38f,.035f,0),new Vector3(.14f,.045f,.1f),white);
            Instrument("Pulsioxímetro",MedicalToolKind.Oximeter,new Vector3(.58f,.035f,0),new Vector3(.055f,.045f,.075f),cyan);
            Instrument("Glucómetro",MedicalToolKind.Glucose,new Vector3(.72f,.035f,0),new Vector3(.07f,.03f,.11f),navy);
            Instrument("Teléfono de emergencias",MedicalToolKind.Phone,new Vector3(.86f,.025f,0),new Vector3(.07f,.018f,.14f),navy);
            Instrument("Autoinyector de entrenamiento",MedicalToolKind.AutoInjector,new Vector3(-.7f,.03f,0),new Vector3(.035f,.035f,.16f),cyan);
            Instrument("Vendaje",MedicalToolKind.Bandage,new Vector3(-.86f,.03f,0),new Vector3(.09f,.06f,.07f),white);
            airway=new GameObject("Airway interaction");airway.transform.SetParent(Visuals.ChinAnchor,false);airway.transform.localPosition=Vector3.zero;
            var collider=airway.AddComponent<BoxCollider>();collider.size=new Vector3(.1f,.08f,.12f);
            var target=airway.AddComponent<MedicalWorldButton>();target.Initialize(this,()=>{if(SubmitNext("OpenAirway")){Visuals.SetAirwayTilt(1);Hint="Vía aérea representada; revisar respiración.";}});
            var simple=airway.AddComponent<XRSimpleInteractable>();simple.selectEntered.AddListener(_=>target.Use());
            bedside=Display(equipment,"Bedside tablet",new Vector3(0,.47f,.178f),.007f);
        }
        void Pad(bool right,Vector3 position)
        {
            var go=new GameObject(right?"AED right upper pad":"AED left lateral pad");go.transform.SetParent(equipment.transform,false);go.transform.localPosition=position;
            Shape(go,"Electrode",Vector3.zero,new Vector3(.09f,.008f,.12f),white);Shape(go,"Peel backing",new Vector3(0,.006f,0),new Vector3(.087f,.005f,.117f),cyan);
            var label=Display(go,right?"R":"L",new Vector3(0,.009f,0),.025f);label.text=right?"R":"L";label.transform.localRotation=Quaternion.Euler(90,0,0);
            MedicalDeviceTextFit.Fit(label,.032f,.04f);
            var tool=AddTool(go,right?MedicalToolKind.RightPad:MedicalToolKind.LeftPad,new Vector3(.1f,.025f,.13f));AED.RegisterPad(tool,right);
        }
        void Instrument(string name,MedicalToolKind kind,Vector3 position,Vector3 size,Material material)
        {
            var root=new GameObject(name);root.transform.SetParent(equipment.transform,false);root.transform.localPosition=position;Shape(root,name+" body",Vector3.zero,size,material);
            Shape(root,"Screen",new Vector3(0,size.y*.55f,0),new Vector3(size.x*.65f,.005f,size.z*.55f),navy);
            if(kind==MedicalToolKind.Glucose)Shape(root,"Test strip",new Vector3(0,0,size.z*.65f),new Vector3(.013f,.004f,.055f),white);
            if(kind==MedicalToolKind.BloodPressure){Shape(root,"Cuff",new Vector3(.11f,0,0),new Vector3(.1f,.018f,.16f),metal);}
            var tool=AddTool(root,kind,size+Vector3.one*.02f);tool.Display=Display(root,"Reading",new Vector3(0,size.y*.65f,0),.008f);tool.Display.transform.localRotation=Quaternion.Euler(90,0,0);tool.Display.text=kind==MedicalToolKind.Phone?"SOS":"--";
            MedicalDeviceTextFit.Fit(tool.Display,size.x*.65f,size.z*.48f);
        }
        MedicalPhysicalTool AddTool(GameObject go,MedicalToolKind kind,Vector3 size)
        {
            var collider=go.AddComponent<BoxCollider>();collider.size=size;var body=go.AddComponent<Rigidbody>();body.mass=kind==MedicalToolKind.AED?1.8f:.08f;body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            var grab=go.AddComponent<XRGrabInteractable>();grab.movementType=XRBaseInteractable.MovementType.Kinematic;grab.throwOnDetach=false;grab.useDynamicAttach=true;
            var tool=go.AddComponent<MedicalPhysicalTool>();tool.Initialize(this,kind);tools.Add(tool);return tool;
        }
        public GameObject Shape(GameObject parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent.transform,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Collider>().enabled=false;Destroy(go.GetComponent<Collider>());var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return go;
        }
        public TextMesh Display(GameObject parent,string label,Vector3 position,float size)
        {
            var go=new GameObject(label,typeof(TextMesh));go.transform.SetParent(parent.transform,false);go.transform.localPosition=position;var text=go.GetComponent<TextMesh>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.characterSize=size;text.fontSize=32;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.45f,.9f,.83f);go.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;return text;
        }
        void RestoreSceneryAED(){if(hiddenSceneryAED!=null)hiddenSceneryAED.SetActive(hiddenSceneryWasActive);hiddenSceneryAED=null;}
        void OnDestroy(){RestoreSceneryAED();if(airway!=null)Destroy(airway);if(Review!=null){Review.SelectionChanged-=Selected;if(Review.Manager!=null)Review.Manager.Changed-=StateChanged;}foreach(var material in materials)Destroy(material);}
    }
    public sealed class MedicalWorldButton : MonoBehaviour
    {
        Action action;MedicalProcedureRig rig;
        public void Initialize(MedicalProcedureRig rig,Action action){this.rig=rig;this.action=action;}
        public void Use(){if(rig.Manager.IsRunning)action();}
    }
}
