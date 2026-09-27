using System;

namespace EmergencyVR.Medical
{
    [Serializable] public sealed class ScenarioCapabilities
    {
        public bool usesClinicalStateMachine, usesObservedPatientData, usesPhysicalPositionConfirmation,
            usesIntentDialogue, usesAdvancedMeasurements, usesObjectiveBasedEvaluation;
        public ScenarioCapabilities Copy() { return (ScenarioCapabilities)MemberwiseClone(); }
    }
    public enum ActionRepeatPolicy { LegacySingleUse, RepeatableNoAdditionalCredit, RepeatableWithNewObservation }
    public enum TrainingProfileId { I0_FirstResponder, I1_NonInvasiveEquipment, I2_HealthcareProfessional }
    public enum ConsciousnessState { Alert, Confused, Drowsy, Unresponsive }
    public enum OrientationState { Oriented, Disoriented, NotAssessable }
    public enum ClinicalPosition { Unknown, SeatedSupported, Supine, Standing }
    public enum PhysicalPosition { Unknown, SeatedSupported, Supine, Standing }
    public enum HelpRequestState { None, Requested, Delegated, Confirmed, HandoverAvailable, Completed }
    public enum ObjectiveResult { NotEvaluated, NotDemonstrated, AchievedWithGuidance, AchievedIndependently }
    public enum CurveType { Linear, SmoothStep }
}
