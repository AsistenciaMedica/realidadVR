using System;
using System.Collections.Generic;

namespace EmergencyVR.Medical
{
    public sealed partial class MedicalScenarioRuntime
    {
        readonly Dictionary<string,string> objectiveGuidance=new Dictionary<string,string>();

        // Educational evidence belongs to the same attempt and log as clinical interactions.
        // Entering guided practice alone never marks an objective as assisted.
        public bool RecordGuidance(string objectiveId,double time,string source="Learner")
        {
            if(!Ready(time,null)||!Capabilities.usesObjectiveBasedEvaluation||!objectives.TryGetValue(objectiveId,out var progress)) return false;
            var entry=Emit("GuidanceUsed",source,objectiveId,"Observed",
                new ClinicalEventMetadata {key="objectiveId",value=objectiveId});
            if(progress.result!=ObjectiveResult.AchievedIndependently&&progress.result!=ObjectiveResult.AchievedWithGuidance)
                objectiveGuidance[objectiveId]=entry.eventId;
            return true;
        }

        public bool RecordCommunication(string audience,string message,double time,string source="Learner")
        {
            if(!Ready(time,null)||string.IsNullOrWhiteSpace(audience)||string.IsNullOrWhiteSpace(message)||message.Length>2000) return false;
            if(!Allows("RequestHelp")&&!Allows("DelegateHelp")&&!Allows("PerformHandover")) return false;
            Emit("CommunicationRecorded",source,"Communicated","Observed",
                new ClinicalEventMetadata {key="audience",value=audience.Trim()},
                new ClinicalEventMetadata {key="message",value=message.Trim()});
            return true;
        }

        public bool RecordMeasurementStarted(string type,double time,string source,string suppliedAttemptId=null)
        {
            if(!Ready(time,suppliedAttemptId)||!Capabilities.usesAdvancedMeasurements||!Capabilities.usesObservedPatientData||
                !activeProfile.allowedMeasurements.Contains(type)) return false;
            Emit("MeasurementStarted",source,type,"Pending");
            return true;
        }
    }
}
