using EmergencyVR.Patient.Presentation;
using UnityEngine;

namespace EmergencyVR.Medical.Interaction
{
    public sealed class CPRInteractionController : MonoBehaviour
    {
        MedicalProcedureRig rig;
        PatientVisualController visuals;
        CPRSampleEvaluator evaluator;
        Transform leftHand,rightHand;
        float shownDepth;
        float lastError,lastAngle;
        bool manualContact,proximityContact,lastBothHands,hasSample;
        string lastSource="NONE";
        public bool Touching => manualContact||proximityContact;
        public bool HasManualContact => manualContact;
        public bool WindowsEngaged {get;private set;}
        public CPRMetrics Metrics => evaluator.Metrics;
        public Transform ChestAnchor => visuals.ChestAnchor;
        public Vector3 ChestRestPosition => visuals.ChestRestPosition;
        public bool IsPatientContact(Vector3 worldPosition) => visuals!=null && visuals.IsPatientContact(worldPosition);
        public void Initialize(MedicalProcedureRig rig,PatientVisualController visuals)
        {
            this.rig=rig;this.visuals=visuals;evaluator=new CPRSampleEvaluator(rig.Settings);
            leftHand=Hand("CPR left hand");rightHand=Hand("CPR right hand");
            gameObject.AddComponent<WindowsCPRInput>().Initialize(this);
            gameObject.AddComponent<VRCPRInput>().Initialize(this);
        }
        Transform Hand(string name)
        {
            var root=new GameObject(name).transform;root.SetParent(transform,false);
            var palm=GameObject.CreatePrimitive(PrimitiveType.Sphere);palm.transform.SetParent(root,false);palm.transform.localScale=new Vector3(.08f,.023f,.11f);Destroy(palm.GetComponent<Collider>());palm.GetComponent<Renderer>().sharedMaterial=rig.GloveMaterial;
            for(int i=0;i<4;i++){var finger=GameObject.CreatePrimitive(PrimitiveType.Capsule);finger.transform.SetParent(root,false);finger.transform.localPosition=new Vector3((i-1.5f)*.016f,0,.06f);finger.transform.localRotation=Quaternion.Euler(90,0,0);finger.transform.localScale=new Vector3(.016f,.035f,.016f);Destroy(finger.GetComponent<Collider>());finger.GetComponent<Renderer>().sharedMaterial=rig.GloveMaterial;}
            root.gameObject.SetActive(false);return root;
        }
        public void ResetAttempt() {hasSample=false;manualContact=false;proximityContact=false;WindowsEngaged=false;evaluator=new CPRSampleEvaluator(rig.Settings);ReleaseContact();}
        public bool CanCompress
        {
            get {
                if(rig==null||rig.Review==null||rig.Manager==null||!rig.Manager.AcceptsInput||rig.Manager.MedicalSession==null)return false;
                var patient=rig.Manager.MedicalSession.Patient;
                return patient.consciousness=="Unresponsive"&&(patient.respiration=="absent"||patient.respiration=="agonal");
            }
        }
        public bool DeviceRequiresNoContact => rig!=null&&rig.AED!=null&&(rig.AED.State.Phase==AEDPhase.Analysing||rig.AED.State.Phase==AEDPhase.ShockAdvised);
        public void SetProximityContact(bool touching) {proximityContact=touching;}
        public void SetWindowsEngaged(bool enabled){WindowsEngaged=enabled;if(!enabled)ReleaseContact();}
        public void Feed(float depth,float error,float angle,bool bothHands,string source)
        {
            if(!CanCompress){ReleaseContact();return;}
            manualContact=true;
            if(DeviceRequiresNoContact)
            {
                FinishSample();shownDepth=0;visuals.SetCompressionDepth(0);ShowHands(true);
                rig.Hint="Retira las manos durante el análisis y antes de una descarga.";return;
            }
            shownDepth=Mathf.Clamp(depth,0,.075f);visuals.SetCompressionDepth(shownDepth);
            lastError=error;lastAngle=angle;lastBothHands=bothHands;lastSource=source;hasSample=true;
            if(evaluator.Sample(rig.Manager.SimulationClock,shownDepth,error,angle,bothHands,source))CompletedCycle();
            if(rig.TrainingMode)rig.Hint=error>rig.Settings.maximumHandError?"Centra ambas manos sobre el tórax.":$"Compresiones: {Metrics.compressions} · recorrido virtual {shownDepth*100:0.0} cm";
            ShowHands(true);
        }
        public void ReleaseContact()
        {
            FinishSample();manualContact=false;shownDepth=0;if(visuals!=null)visuals.SetCompressionDepth(0);ShowHands(false);
        }
        void FinishSample()
        {
            if(!hasSample||evaluator==null)return;
            hasSample=false;
            if(rig.Manager.IsPaused){evaluator.CancelPendingCycle();return;}
            if(evaluator.Sample(rig.Manager.SimulationClock,0,lastError,lastAngle,lastBothHands,lastSource))CompletedCycle();
        }
        void CompletedCycle()
        {
            // Both pointer recoil and physical release pass through the same completion path.
            // Evaluator emits once per cycle, so a Feed(0) followed by ReleaseContact cannot double-count.
            if(!CanCompress||DeviceRequiresNoContact)return;
            rig.SubmitNext("StartCPR");rig.SubmitNext("ChestCompression");rig.SubmitNext("ContinueCPR");
            rig.Audio.Pulse(.045f,180);MedicalHaptics.Pulse(.2f,.035f);
        }
        void ShowHands(bool visible)
        {
            if(leftHand==null)return;leftHand.gameObject.SetActive(visible);rightHand.gameObject.SetActive(visible);
            if(!visible)return;
            var anchor=ChestAnchor;var contact=ChestRestPosition-anchor.up*shownDepth;
            leftHand.SetPositionAndRotation(contact+anchor.up*.012f,anchor.rotation);rightHand.SetPositionAndRotation(contact+anchor.up*.036f,anchor.rotation*Quaternion.Euler(0,25,0));
        }
        void OnDisable(){ReleaseContact();proximityContact=false;}
    }
}
