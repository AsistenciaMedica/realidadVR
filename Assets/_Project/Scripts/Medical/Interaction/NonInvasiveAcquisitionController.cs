using System;
using EmergencyVR.Scenarios;
using UnityEngine;

namespace EmergencyVR.Medical.Interaction
{
    public enum AcquisitionPhase { Idle, PickedUp, Positioning, ValidPlacement, Measuring, Invalid, Completed }

    // Optional equipment contract. CASE 01 I0 never enables this component's permissions.
    // Placement is evaluated from transforms and support geometry, never inferred from a button.
    public abstract class NonInvasiveAcquisitionController : MonoBehaviour
    {
        public Transform contact, anatomy, movingPart;
        public Collider armSupport;
        [Min(.001f)] public float placementTolerance=.055f;
        [Range(1,90)] public float orientationTolerance=30;
        [Min(.001f)] public float maximumMotion=.025f;
        [Min(.5f)] public float acquisitionSeconds=12;
        protected abstract string MeasurementType { get; }
        protected abstract string EquipmentId { get; }
        protected abstract string DeviceSource { get; }
        protected virtual bool RequiresArmSupport => false;
        ScenarioManager manager;
        MedicalScenarioRuntime attempt;
        string attemptId;
        double started;
        Vector3 anatomyAtStart,contactAtStart;
        Quaternion anatomyRotationAtStart;
        Quaternion closedRotation;
        bool opened,closed;
        MeasurementObservation result;
        public AcquisitionPhase Phase { get; private set; }
        public string Quality { get; private set; }="NotAcquired";
        public bool IsOpen => opened;
        public MeasurementObservation Result => result==null?null:(MeasurementObservation)result.Copy();
        public float Progress => Phase==AcquisitionPhase.Measuring&&attempt!=null?
            Mathf.Clamp01((float)((attempt.Elapsed-started)/acquisitionSeconds)):Phase==AcquisitionPhase.Completed?1:0;
        public bool Permitted
        {
            get
            {
                var runtime=manager?.MedicalSession;
                var profile=runtime?.TrainingProfile;
                return runtime!=null&&runtime.Capabilities.usesAdvancedMeasurements&&runtime.Capabilities.usesObservedPatientData&&
                    profile!=null&&profile.allowedMeasurements.Contains(MeasurementType)&&profile.allowedEquipment.Contains(EquipmentId);
            }
        }
        public void Initialize(ScenarioManager owner,Transform anatomicalTarget,Transform deviceContact,Collider support=null)
        {
            manager=owner;anatomy=anatomicalTarget;contact=deviceContact;armSupport=support;
            if(movingPart!=null)closedRotation=movingPart.localRotation;
            ResetAcquisition();
        }
        public void ResetAcquisition()
        {
            attempt=manager?.MedicalSession;attemptId=attempt?.AttemptId;
            Phase=AcquisitionPhase.Idle;opened=false;closed=false;result=null;Quality="NotAcquired";started=0;
            if(movingPart!=null)movingPart.localRotation=closedRotation;
        }
        void Synchronize()
        {
            if(attempt!=manager?.MedicalSession||attemptId!=manager?.MedicalSession?.AttemptId)ResetAcquisition();
        }
        bool Ready()
        {
            Synchronize();return manager!=null&&manager.AcceptsInput&&Permitted;
        }
        public bool PickUp()
        {
            if(!Ready())return false;
            if(Phase==AcquisitionPhase.Measuring)Fail("ContactLost");
            Phase=AcquisitionPhase.PickedUp;opened=false;closed=false;result=null;return true;
        }
        public bool Open()
        {
            if(!Ready()||Phase==AcquisitionPhase.Idle||Phase==AcquisitionPhase.Measuring)return false;
            opened=true;closed=false;Phase=AcquisitionPhase.Positioning;
            if(movingPart!=null)movingPart.localRotation=closedRotation*Quaternion.Euler(0,0,24);
            return true;
        }
        public bool Close()
        {
            if(!Ready()||!opened||Phase!=AcquisitionPhase.Positioning)return false;
            string invalid=PlacementError();
            if(invalid!=null){Quality=invalid;Phase=AcquisitionPhase.Invalid;return false;}
            opened=false;closed=true;Phase=AcquisitionPhase.ValidPlacement;Quality="PlacementVerified";
            if(movingPart!=null)movingPart.localRotation=closedRotation;
            return true;
        }
        string PlacementError()
        {
            if(contact==null||anatomy==null||!contact.gameObject.activeInHierarchy||!anatomy.gameObject.activeInHierarchy)return "ContactLost";
            if(Vector3.Distance(contact.position,anatomy.position)>placementTolerance||Quaternion.Angle(contact.rotation,anatomy.rotation)>orientationTolerance)return "InvalidPlacement";
            if(RequiresArmSupport&&(armSupport==null||!armSupport.enabled||!armSupport.gameObject.activeInHierarchy||
                Vector3.Distance(armSupport.ClosestPoint(anatomy.position),anatomy.position)>.12f))return "UnsupportedArm";
            return null;
        }
        public bool BeginAcquisition()
        {
            if(!Ready()||!closed||(Phase!=AcquisitionPhase.ValidPlacement&&Phase!=AcquisitionPhase.Completed))return false;
            string invalid=PlacementError();if(invalid!=null){Fail(invalid);return false;}
            bool accepted=false;
            manager.PerformClinical((runtime,time)=>accepted=runtime.RecordMeasurementStarted(MeasurementType,time,DeviceSource,attemptId));
            if(!accepted)return false;
            result=null;started=attempt.Elapsed;anatomyAtStart=anatomy.position;contactAtStart=contact.position;anatomyRotationAtStart=anatomy.rotation;
            Phase=AcquisitionPhase.Measuring;Quality="Acquiring";return true;
        }
        void Update(){SampleAcquisition();}
        public void SampleAcquisition()
        {
            Synchronize();
            if(Phase!=AcquisitionPhase.Measuring||manager==null||!manager.AcceptsInput)return;
            if(!Permitted){Phase=AcquisitionPhase.Invalid;Quality="NotPermitted";result=null;return;}
            string invalid=PlacementError();
            if(invalid==null&&(Vector3.Distance(anatomy.position,anatomyAtStart)>maximumMotion||Vector3.Distance(contact.position,contactAtStart)>maximumMotion||Quaternion.Angle(anatomy.rotation,anatomyRotationAtStart)>8))invalid="Motion";
            if(invalid!=null){Fail(invalid);return;}
            if(attempt.Elapsed-started<acquisitionSeconds)return;
            bool accepted=false;
            manager.PerformClinical((runtime,time)=>accepted=runtime.RecordMeasurement(MeasurementType,true,"Valid",time,DeviceSource,attemptId));
            if(!accepted){Phase=AcquisitionPhase.Invalid;Quality="NotPermitted";return;}
            result=attempt.Observations.LatestMeasurement(MeasurementType);Phase=AcquisitionPhase.Completed;Quality="Valid";
        }
        public void Remove()
        {
            if(!Ready())return;
            if(Phase==AcquisitionPhase.Measuring)Fail("ContactLost");
            closed=false;opened=true;Phase=AcquisitionPhase.Positioning;
            if(movingPart!=null)movingPart.localRotation=closedRotation*Quaternion.Euler(0,0,24);
            // The latest historical result intentionally survives device removal.
        }
        void Fail(string quality)
        {
            if(attempt==null)return;
            manager.PerformClinical((runtime,time)=>runtime.RecordMeasurement(MeasurementType,false,quality,time,DeviceSource,attemptId));
            result=attempt.Observations.LatestMeasurement(MeasurementType);Phase=AcquisitionPhase.Invalid;Quality=quality;
        }
    }
}
