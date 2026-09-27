using System;
using System.Linq;

namespace EmergencyVR.Medical
{
    [Serializable] public sealed class ClinicalScenarioMetadata
    {
        public string scenarioId, scenarioVersion, clinicalSpecVersion, trainingProfile, initialClinicalStateId;
        public string displayName, category, difficulty, environment, description, history, incident;
        public string[] referenceIds=Array.Empty<string>();
        public bool clinicalReviewRequired=true;
        public ClinicalScenarioMetadata Copy() { var copy=(ClinicalScenarioMetadata)MemberwiseClone(); copy.referenceIds=(string[])referenceIds.Clone(); return copy; }
    }
    [Serializable] public sealed class ClinicalPatientProfile
    {
        public string id, name, sex;
        public int age;
        public ClinicalPatientProfile Copy() { return (ClinicalPatientProfile)MemberwiseClone(); }
    }
    [Serializable] public sealed class LearningObjectiveDefinition
    {
        public string id, name;
        public string[] requiredOutcomes=Array.Empty<string>(), anyOutcomes=Array.Empty<string>();
        public LearningObjectiveDefinition Copy() { return new LearningObjectiveDefinition {id=id,name=name,requiredOutcomes=(string[])requiredOutcomes.Clone(),anyOutcomes=(string[])anyOutcomes.Clone()}; }
    }
    [Serializable] public sealed class AcceptableAlternativeDefinition
    {
        public string id, objectiveId;
        public string[] equivalentOutcomes=Array.Empty<string>();
        public AcceptableAlternativeDefinition Copy() { return new AcceptableAlternativeDefinition {id=id,objectiveId=objectiveId,equivalentOutcomes=(string[])equivalentOutcomes.Clone()}; }
    }
    [Serializable] public sealed class ClinicalRuleReference
    {
        public string ruleId, sourceId, notes;
        public bool clinicalReviewRequired;
        public ClinicalRuleReference Copy() { return (ClinicalRuleReference)MemberwiseClone(); }
    }
    [Serializable] public sealed class ClinicalScenarioV2Definition
    {
        public ClinicalScenarioMetadata metadata=new ClinicalScenarioMetadata();
        public ClinicalPatientProfile patientProfile=new ClinicalPatientProfile();
        public ScenarioCapabilities capabilities=new ScenarioCapabilities();
        public TrainingProfileDefinition[] trainingProfiles=Array.Empty<TrainingProfileDefinition>();
        public ClinicalStateDefinition[] clinicalStates=Array.Empty<ClinicalStateDefinition>();
        public ClinicalTransitionDefinition[] transitions=Array.Empty<ClinicalTransitionDefinition>();
        public LearningObjectiveDefinition[] learningObjectives=Array.Empty<LearningObjectiveDefinition>();
        public DialogueIntentDefinition[] dialogue=Array.Empty<DialogueIntentDefinition>();
        public AcceptableAlternativeDefinition[] acceptableAlternatives=Array.Empty<AcceptableAlternativeDefinition>();
        public ClinicalRuleReference[] clinicalRules=Array.Empty<ClinicalRuleReference>();
        public ClinicalScenarioV2Definition Copy()
        {
            return new ClinicalScenarioV2Definition {metadata=metadata.Copy(),patientProfile=patientProfile.Copy(),capabilities=capabilities.Copy(),
                trainingProfiles=trainingProfiles.Select(x=>x.Copy()).ToArray(),clinicalStates=clinicalStates.Select(x=>x.Copy()).ToArray(),
                transitions=transitions.Select(x=>x.Copy()).ToArray(),learningObjectives=learningObjectives.Select(x=>x.Copy()).ToArray(),
                dialogue=dialogue.Select(x=>x.Copy()).ToArray(),acceptableAlternatives=acceptableAlternatives.Select(x=>x.Copy()).ToArray(),clinicalRules=clinicalRules.Select(x=>x.Copy()).ToArray()};
        }
        public void Validate(string scenarioId)
        {
            if(metadata==null||capabilities==null||patientProfile==null||trainingProfiles==null||clinicalStates==null||transitions==null||learningObjectives==null||dialogue==null||acceptableAlternatives==null||clinicalRules==null)
                throw new ArgumentException("Incomplete clinical v2 definition.");
            if(metadata.scenarioId!=scenarioId||string.IsNullOrWhiteSpace(metadata.scenarioVersion)||string.IsNullOrWhiteSpace(metadata.clinicalSpecVersion)||string.IsNullOrWhiteSpace(metadata.trainingProfile))
                throw new ArgumentException("Clinical metadata must identify the scenario, specification and training profile.");
            if(trainingProfiles.Length==0||trainingProfiles.Select(x=>x.id).Distinct().Count()!=trainingProfiles.Length||!trainingProfiles.Any(x=>x.ExportId==metadata.trainingProfile))
                throw new ArgumentException("Missing or duplicate configured training profile.");
            foreach(var profile in trainingProfiles)
            {
                if(!Enum.IsDefined(typeof(TrainingProfileId),profile.id)||profile.allowedActions==null||profile.allowedEquipment==null||profile.allowedMeasurements==null||profile.allowedProcedures==null)
                    throw new ArgumentException("Invalid training profile grants.");
            }
            if(capabilities.usesClinicalStateMachine&&(clinicalStates.Length==0||clinicalStates.Select(x=>x.id).Distinct().Count()!=clinicalStates.Length||!clinicalStates.Any(x=>x.id==metadata.initialClinicalStateId)))
                throw new ArgumentException("Missing initial state or duplicate clinical state.");
            // These branches are explicitly reserved and excluded from this implementation.
            if(clinicalStates.Any(x=>x.id=="HYP_05_BRIEF_TLOC"||x.id=="HYP_06_EARLY_RECOVERY_AFTER_TLOC")) throw new ArgumentException("Reserved clinical states are not implemented.");
            foreach(var state in clinicalStates) state.Validate();
            foreach(var transition in transitions)
            {
                if(!clinicalStates.Any(x=>x.id==transition.from)||!clinicalStates.Any(x=>x.id==transition.to)||transition.from==transition.to||transition.conditions==null||transition.conditions.Length==0||transition.optionalTrajectory==null)
                    throw new ArgumentException("Invalid clinical transition.");
                foreach(var condition in transition.conditions)
                {
                    if(!new[]{"Signal","StateElapsed","TrajectoryCompleted","PhysicalPosition","ClinicalState"}.Contains(condition.type)) throw new ArgumentException("Unknown transition condition.");
                    PatientSnapshot.Bounds(condition.seconds,0,86400);
                }
                foreach(var trajectory in transition.optionalTrajectory) trajectory.Validate();
            }
            if(learningObjectives.Select(x=>x.id).Distinct().Count()!=learningObjectives.Length||learningObjectives.Any(x=>string.IsNullOrWhiteSpace(x.id)||x.requiredOutcomes==null||x.anyOutcomes==null))
                throw new ArgumentException("Invalid learning objectives.");
            foreach(var alternative in acceptableAlternatives) if(!learningObjectives.Any(x=>x.id==alternative.objectiveId)||alternative.equivalentOutcomes==null||alternative.equivalentOutcomes.Length==0) throw new ArgumentException("Unknown alternative objective.");
            foreach(var intent in dialogue)
            {
                if(!Enum.IsDefined(typeof(DialogueIntent),intent.intent)||intent.responses==null) throw new ArgumentException("Invalid dialogue intent.");
                foreach(var response in intent.responses)
                {
                    if(response.intent!=intent.intent||string.IsNullOrWhiteSpace(response.id)||string.IsNullOrWhiteSpace(response.text)||response.allowedClinicalStates==null||response.allowedClinicalStates.Any(id=>!clinicalStates.Any(s=>s.id==id))||(!string.IsNullOrEmpty(response.audioReference)&&(response.audioReference.Contains("/")||response.audioReference.Contains("\\"))))
                        throw new ArgumentException("Invalid dialogue response or Unity asset path in clinical JSON.");
                    PatientSnapshot.Bounds(response.minStateSeconds,0,86400);
                }
            }
            if(clinicalRules.Any(x=>string.IsNullOrWhiteSpace(x.ruleId)||string.IsNullOrWhiteSpace(x.sourceId))) throw new ArgumentException("Invalid clinical rule reference.");
            if(patientProfile.age<18||patientProfile.age>100||string.IsNullOrWhiteSpace(patientProfile.sex)||string.IsNullOrWhiteSpace(patientProfile.name)) throw new ArgumentException("Invalid authored adult patient identity.");
            if(transitions.Select(x=>x.id).Distinct().Count()!=transitions.Length||transitions.Any(x=>string.IsNullOrWhiteSpace(x.id))) throw new ArgumentException("Duplicate or empty transition IDs.");
            if(clinicalRules.Select(x=>x.ruleId).Distinct().Count()!=clinicalRules.Length) throw new ArgumentException("Duplicate clinical rule IDs.");
            foreach(var trajectory in clinicalStates.SelectMany(x=>x.vitalTrajectories).Concat(transitions.SelectMany(x=>x.optionalTrajectory)))
                if(!string.IsNullOrEmpty(trajectory.ruleId)&&!clinicalRules.Any(x=>x.ruleId==trajectory.ruleId)) throw new ArgumentException("Unknown trajectory clinical rule.");
        }
    }
}
