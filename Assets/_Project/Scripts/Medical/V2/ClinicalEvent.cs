using System;
using System.Linq;

namespace EmergencyVR.Medical
{
    [Serializable] public sealed class ClinicalEventMetadata
    {
        public string key, value;
        public ClinicalEventMetadata Copy() { return (ClinicalEventMetadata)MemberwiseClone(); }
    }
    [Serializable] public sealed class ClinicalEvent
    {
        public string eventId, attemptId, scenarioId, scenarioVersion, clinicalSpecVersion, trainingProfile, buildVersion,
            clinicalStateId, eventType, source, result, quality;
        public double simulationTime;
        public ClinicalEventMetadata[] metadata=Array.Empty<ClinicalEventMetadata>();
        public ClinicalEvent Copy() { var x=(ClinicalEvent)MemberwiseClone();x.metadata=metadata.Select(m=>m.Copy()).ToArray();return x; }
    }
    [Serializable] public sealed class LearningObjectiveProgress
    {
        public string objectiveId, name;
        public ObjectiveResult result;
        public string[] evidenceEventIds=Array.Empty<string>();
        public LearningObjectiveProgress Copy() { var x=(LearningObjectiveProgress)MemberwiseClone();x.evidenceEventIds=(string[])evidenceEventIds.Clone();return x; }
    }
}
