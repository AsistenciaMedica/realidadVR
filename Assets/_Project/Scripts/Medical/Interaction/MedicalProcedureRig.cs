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
        public ProcedureHandAnimator Hands {get;private set;}
        public AEDInteractionController AED {get;private set;}
        public MedicalProcedureAudio Audio {get;private set;}
        public Material GloveMaterial {get;private set;}
        public bool TrainingMode {get;set;}=true;
        public string Hint {get;set;}="Acércate al paciente. C: RCP · E: coger/soltar · Q: usar objeto.";
        public int Measurements {get;set;}
        public int UnsafeAttempts {get;set;}
        public event Action<MedicalToolKind, PatientSnapshot, double> MeasurementRecorded;
        public void RecordMeasurement(MedicalToolKind kind, PatientSnapshot snapshot)
        {
            var session=Manager?.MedicalSession;
            if(session==null || !Manager.AcceptsInput) return;
            if(session.Capabilities.usesObservedPatientData)
            {
                // Existing proximity tools cannot impersonate advanced validated acquisitions.
                // CASE 01 I0 has no measurement grants; future adapters submit their own evidence.
                return;
            }
            MeasurementRecorded?.Invoke(kind, snapshot.Copy(), Manager.MedicalSession.Elapsed);
        }
        public bool AllowsEquipment(MedicalToolKind kind)
        {
            var config=Manager?.MedicalDefinition?.clinicalV2;
            if(config==null || !config.capabilities.usesObservedPatientData) return true;
            var profile=Manager.MedicalSession?.TrainingProfile ?? config.trainingProfiles.First(p=>p.ExportId==config.metadata.trainingProfile);
            var name=kind==MedicalToolKind.Oximeter?"SpO2":kind==MedicalToolKind.RightPad||kind==MedicalToolKind.LeftPad?"AED":kind.ToString();
            return profile.allowedEquipment.Contains(name);
        }
        public bool IsWindows => FindFirstObjectByType<EmergencyVR.Desktop.DesktopDemoController>()!=null;
        readonly List<Material> materials=new List<Material>();
        readonly List<MedicalPhysicalTool> tools=new List<MedicalPhysicalTool>();
        GameObject communicationStand;
        Material white,navy,red,cyan,metal;
        GameObject equipment,airway,hiddenSceneryAED;bool hiddenSceneryWasActive;MedicalScenarioRuntime lastSession;bool wasRunning;
        public static MedicalProcedureRig Attach(ReviewCaseSession review)
        {
            if(review==null)throw new ArgumentNullException(nameof(review));
            var existing=FindFirstObjectByType<MedicalProcedureRig>();if(existing!=null&&existing.Review==review)return existing;
            var go=new GameObject("VITAL VR medical interaction kit");var rig=go.AddComponent<MedicalProcedureRig>();rig.Initialize(review);return rig;
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
            Hands=gameObject.AddComponent<ProcedureHandAnimator>();Hands.Initialize(this);
            BuildKit();Review.SelectionChanged+=Selected;Manager.Changed+=StateChanged;Selected();
        }
        public bool IsPhysicalAction(string id)
        {
            var action=Review.Selected.medical?.actions.FirstOrDefault(a=>a.id==id)?.action;
            return new[]{"StartCPR","ChestCompression","ContinueCPR","BringAED","AttachAEDPads","AnalyzeRhythm","DeliverAEDShock","FollowNoShock","CheckBloodPressure","CheckSpO2","CheckGlucose","CallEmergencyServices","OpenAirway","ApplyBandage","UseEpinephrineAutoInjector"}.Contains(action);
        }
        public bool SubmitNext(string action)
        {
            if(Manager==null||!Manager.AcceptsInput||Manager.MedicalSession==null||Manager.MedicalDefinition==null)return false;
            var done=Manager.MedicalSession.Completed;
            var rule=Manager.MedicalDefinition.actions.FirstOrDefault(r=>r.action==action&&(!done.Contains(r.id)||r.repeatPolicy!=ActionRepeatPolicy.LegacySingleUse)&&r.kind!="dangerous"&&r.kind!="incorrect"&&r.prerequisites.All(done.Contains)&&Manager.MedicalSession.Elapsed>=Manager.MedicalSession.EarliestTime(r.id));
            if(rule==null)return false;
            return Manager.TrySubmitAction(rule.id);
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
            bool initialResponder=medical&&Review.Selected.medical.clinicalV2?.capabilities.usesObservedPatientData==true;
            foreach(Transform child in equipment.transform)child.gameObject.SetActive(!initialResponder);
            equipment.SetActive(medical);if(airway!=null)airway.SetActive(medical);
            foreach(var tool in tools) tool.gameObject.SetActive(medical && AllowsEquipment(tool.Kind));
            if(medical && Review.Selected.medical.clinicalV2?.capabilities.usesObservedPatientData==true && airway!=null) airway.SetActive(false);
            if(communicationStand!=null)communicationStand.SetActive(initialResponder);
            Measurements=0;UnsafeAttempts=0;
            if(!medical)return;
            var anchor=GameObject.Find("PortableEquipmentAnchor");equipment.transform.position=anchor!=null?anchor.transform.position:new Vector3(-1.8f,.98f,3.75f);
            ConfigureStorage(Review.Selected.medical.environment);
            if(initialResponder)
            {
                if(communicationStand==null)
                {
                    communicationStand=new GameObject("Gym emergency communication point");communicationStand.transform.SetParent(transform,false);
                    Shape(communicationStand,"Reception side table",new Vector3(0,.85f,0),new Vector3(.48f,.07f,.38f),navy);
                    Shape(communicationStand,"Reception table support",new Vector3(0,.42f,0),new Vector3(.10f,.84f,.10f),metal);
                    var support=communicationStand.AddComponent<BoxCollider>();support.center=new Vector3(0,.85f,0);support.size=new Vector3(.48f,.07f,.38f);
                    Shape(communicationStand,"Telephone sign support",new Vector3(0,1.0f,.13f),new Vector3(.025f,.30f,.025f),metal);
                    Shape(communicationStand,"Telephone sign board",new Vector3(0,1.15f,.13f),new Vector3(.48f,.24f,.025f),navy);
                    var label=Display(communicationStand,"Emergency telephone sign",new Vector3(0,1.15f,.112f),.065f);
                    label.text="TELÉFONO\n112";label.alignment=TextAlignment.Center;
                    MedicalDeviceTextFit.Fit(label,.44f,.20f);
                }
                communicationStand.SetActive(true);communicationStand.transform.position=new Vector3(-1.8f,0,2.8f);
                foreach(var tool in tools)if(tool.Kind==MedicalToolKind.Phone)tool.transform.position=communicationStand.transform.position+new Vector3(0,.90f,0);
            }
            var scenery=anchor==null?null:anchor.transform.parent.Find("Reused medical equipment/DefibrillatorPlaceholder");
            if(scenery!=null){hiddenSceneryAED=scenery.gameObject;hiddenSceneryWasActive=hiddenSceneryAED.activeSelf;hiddenSceneryAED.SetActive(false);}
            Hint=Review.Selected.medical.clinicalV2?.capabilities.usesObservedPatientData==true?"Habla con el paciente y registra tus observaciones.":"E: coger/soltar · Q: usar equipo · C: RCP manual";
        }
        void StateChanged()
        {
            if(Manager.MedicalSession!=lastSession){lastSession=Manager.MedicalSession;ResetTools();AED.ResetAttempt();CPR.ResetAttempt();Measurements=0;UnsafeAttempts=0;}
            Audio.SetPatient(Manager.MedicalSession?.Patient??Review.Selected.medical?.initialState,
                Manager.MedicalSession?.Capabilities.usesClinicalStateMachine==true);
            if(wasRunning&&!Manager.IsRunning&&Manager.MedicalResult!=null)Manager.MedicalResult.procedures=Report();
            wasRunning=Manager.IsRunning;
        }
        public ProcedureMetrics Report()=>new ProcedureMetrics {mode=TrainingMode?"TRAINING":"EVALUATION",cpr=CPR.Metrics,rightPadPlaced=AED.State.RightPad,leftPadPlaced=AED.State.LeftPad,shocks=AED.State.Shocks,measurements=Measurements,unsafeDeviceAttempts=UnsafeAttempts};
        void ResetTools()
        {
            foreach(var tool in tools)if(tool!=null)tool.ResetTool();
            if(communicationStand!=null&&Manager?.MedicalDefinition?.clinicalV2?.capabilities.usesObservedPatientData==true)
                foreach(var tool in tools)if(tool.Kind==MedicalToolKind.Phone)tool.transform.position=communicationStand.transform.position+new Vector3(0,.90f,0);
        }
        void ConfigureStorage(string environment)
        {
            foreach (var tool in tools)
            {
                Vector3 position;
                switch (tool.Kind)
                {
                    case MedicalToolKind.AED:
                        position = environment == "gym" || environment == "mall"
                            ? new Vector3(-.68f, .185f, .93f) : new Vector3(-.65f, .03f, 0);
                        break;
                    case MedicalToolKind.RightPad: position = new Vector3(-.23f, .03f, -.05f); break;
                    case MedicalToolKind.LeftPad: position = new Vector3(-.09f, .03f, -.05f); break;
                    case MedicalToolKind.BloodPressure: position = new Vector3(.12f, .035f, .10f); break;
                    case MedicalToolKind.Oximeter: position = new Vector3(.29f, .035f, .10f); break;
                    case MedicalToolKind.Glucose: position = new Vector3(.43f, .035f, .10f); break;
                    case MedicalToolKind.Phone: position = new Vector3(.44f, .025f, -.10f); break;
                    case MedicalToolKind.AutoInjector: position = new Vector3(.07f, .03f, -.10f); break;
                    default: position = new Vector3(.25f, .04f, -.10f); break;
                }
                tool.SetStoragePose(position, Quaternion.identity);
            }
        }

        void BuildKit()
        {
            equipment=new GameObject("Portable medical tools");equipment.transform.SetParent(transform,false);
            Shape(equipment,"First aid case base",new Vector3(.12f,-.012f,0),new Vector3(.92f,.04f,.46f),red);
            foreach(float x in new[]{-.35f,.59f})
                Shape(equipment,"First aid case side",new Vector3(x,.035f,0),new Vector3(.025f,.08f,.46f),red);
            foreach(float z in new[]{-.225f,.225f})
                Shape(equipment,"First aid case rim",new Vector3(.12f,.035f,z),new Vector3(.92f,.08f,.025f),red);
            Shape(equipment,"Open first aid case lid",new Vector3(.12f,.25f,.22f),new Vector3(.94f,.43f,.025f),red);
            Shape(equipment,"First aid white cross vertical",new Vector3(.12f,.25f,.204f),new Vector3(.04f,.18f,.01f),white);
            Shape(equipment,"First aid white cross horizontal",new Vector3(.12f,.25f,.203f),new Vector3(.18f,.04f,.01f),white);
            var trayCollider=new GameObject("First aid case physics",typeof(BoxCollider));trayCollider.transform.SetParent(equipment.transform,false);trayCollider.transform.localPosition=new Vector3(.12f,-.012f,0);trayCollider.GetComponent<BoxCollider>().size=new Vector3(.92f,.04f,.46f);
            var caseRoot=new GameObject("Interactive AED");caseRoot.transform.SetParent(equipment.transform,false);caseRoot.transform.localPosition=new Vector3(0,.09f,0);
            Shape(caseRoot,"AED shell",Vector3.zero,new Vector3(.32f,.12f,.25f),white);
            Shape(caseRoot,"Rubber trim",new Vector3(0,-.025f,0),new Vector3(.34f,.05f,.27f),navy);
            var aedLid=Shape(caseRoot,"AED lid",new Vector3(0,.075f,.06f),new Vector3(.32f,.03f,.13f),cyan);AED.Lid=aedLid.transform;
            Shape(caseRoot,"AED carry handle",new Vector3(0,.08f,.15f),new Vector3(.14f,.025f,.035f),navy);
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
            // Measurements remain on the actual instruments; public first-aid kits have no hospital monitor.
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
