using System;
using System.Linq;
using EmergencyVR.Medical;

namespace EmergencyVR.Scenarios
{
    // Learner report schema: evidence and objectives, without a legacy numeric grade or unseen vitals.
    [Serializable] public sealed class ClinicalV2Report
    {
        public int schemaVersion=4;
        public string attemptId, scenarioId, caseId, caseName, scenarioVersion, clinicalSpecVersion, trainingProfile, buildVersion, createdUtc;
        public double durationSeconds;
        public bool clinicalReviewRequired=true;
        public ClinicalEvent[] clinicalEvents;
        public LearningObjectiveProgress[] objectives;
        public MeasurementObservation[] measurements;
        public InterviewObservation[] interviews;
        public PhysicalObservation[] physicalObservations;

        public static ClinicalV2Report From(MedicalDebrief result)
        {
            if(result==null) throw new ArgumentNullException(nameof(result));
            return new ClinicalV2Report {
                attemptId=result.attemptId,scenarioId=result.caseId,caseId=result.caseId,caseName=result.caseName,
                scenarioVersion=result.scenarioVersion,clinicalSpecVersion=result.clinicalSpecVersion,
                trainingProfile=result.trainingProfile,buildVersion=result.buildVersion,createdUtc=result.createdUtc,durationSeconds=result.durationSeconds,
                clinicalEvents=result.clinicalEvents.Select(e=>e.Copy()).ToArray(),
                objectives=result.objectives.Select(o=>o.Copy()).ToArray(),
                measurements=result.measurements.Select(o=>(MeasurementObservation)o.Copy()).ToArray(),
                interviews=result.interviews.Select(o=>(InterviewObservation)o.Copy()).ToArray(),
                physicalObservations=result.physicalObservations.Select(o=>(PhysicalObservation)o.Copy()).ToArray()
            };
        }
    }
}
