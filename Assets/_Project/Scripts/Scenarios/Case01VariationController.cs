using System;
using System.Linq;
using EmergencyVR.Medical;
using EmergencyVR.Patient.Presentation;
using UnityEngine;

namespace EmergencyVR.Scenarios
{
    public enum Case01Variation { Normal, Persistent, Recurrence }

    /// <summary>Pre-session narrative configuration. Every clinical change remains a runtime input.</summary>
    public sealed class Case01VariationController : MonoBehaviour
    {
        [SerializeField] Case01Variation practiceVariation = Case01Variation.Normal;
        [SerializeField] Case01Variation evaluationVariation = Case01Variation.Normal;
        [Min(0)] public float recurrencePresentationDelaySeconds = 8;
        ReviewCaseSession review;
        MedicalScenarioRuntime attempt;
        string attemptId, lastState;
        double improvedAt;
        bool triggered, logged, handling, recurrencePresented;
        public Case01Variation SelectedVariant => practiceVariation;
        public Case01Variation EvaluationVariant => evaluationVariation;
        public Case01Variation ActiveVariant { get; private set; }
        public bool HasTriggered => triggered;
        public bool CanConfigure => review != null && !review.Manager.IsRunning;

        public static bool Supports(MedicalScenarioDefinition definition) => definition?.clinicalV2?.capabilities.usesClinicalStateMachine == true &&
            definition.clinicalV2.clinicalStates.Any(x => x.id == "HYP_00_INITIAL_PRESYNCOPE");

        public void Initialize(ReviewCaseSession owner)
        {
            if (review != null) review.Manager.Changed -= UpdatePresentation;
            review = owner;
            if (review != null) review.Manager.Changed += UpdatePresentation;
            UpdatePresentation();
        }
        public bool Configure(Case01Variation variation)
        {
            if (!CanConfigure || !Enum.IsDefined(typeof(Case01Variation), variation)) return false;
            practiceVariation = variation; return true;
        }
        // Instructor/test configuration only. The learner's assessment briefing never displays this selection.
        public bool ConfigureEvaluation(Case01Variation variation)
        {
            if (!CanConfigure || !Enum.IsDefined(typeof(Case01Variation), variation)) return false;
            evaluationVariation = variation; return true;
        }
        void Update() { UpdatePresentation(); }
        public void UpdatePresentation()
        {
            if (handling || review == null) return;
            handling = true;
            try
            {
                var runtime = review.Manager.MedicalSession;
                var currentAttemptId = runtime?.ClinicalState?.AttemptId;
                if (attempt != runtime || attemptId != currentAttemptId)
                {
                    attempt = runtime; attemptId = currentAttemptId;
                    lastState = null; improvedAt = 0; triggered = logged = recurrencePresented = false;
                    ActiveVariant = review.Procedures.TrainingMode ? practiceVariation : evaluationVariation;
                }
                if (attempt == null || !review.Manager.AcceptsInput || !Supports(review.Manager.MedicalDefinition)) return;
                if (!logged)
                {
                    bool accepted = false;
                    review.Manager.PerformClinical((source, time) => accepted = source.RecordCommunication("TrainingConfiguration",
                        "Variante del ejercicio: " + Label(ActiveVariant) + ".", time, "ScenarioConfiguration"));
                    logged = accepted;
                }
                string state = attempt.ClinicalState.ClinicalStateId;
                if (state != lastState)
                {
                    lastState = state;
                    if (state == "HYP_02_IMPROVING") improvedAt = attempt.ClinicalEvents.LastOrDefault(x =>
                        x.eventType == "ClinicalStateChanged" && x.result == state)?.simulationTime ?? attempt.Elapsed;
                }
                if (triggered) return;
                if (ActiveVariant == Case01Variation.Persistent && state == "HYP_01_SUPPORTED_OBSERVATION")
                {
                    bool accepted = false;
                    review.Manager.PerformClinical((source, time) => accepted = source.ReportClinicalSignal("NoMeaningfulImprovement", time, "ConfiguredScenarioVariation", attemptId));
                    triggered = accepted;
                }
                else if (!recurrencePresented && ActiveVariant == Case01Variation.Recurrence && state == "HYP_02_IMPROVING" &&
                    attempt.Elapsed - improvedAt >= Math.Max(0, recurrencePresentationDelaySeconds))
                {
                    var body = review.GetComponent<Case01PatientPresentation>();
                    // The body validates its route, warns the learner, allows prevention and confirms actual standing itself.
                    if (body != null && body.IsActive && !body.IsAssisting && body.FinalPositionValidated)
                    {
                        recurrencePresented = true;
                        triggered = body.BeginRiseAttempt();
                        if (!triggered) review.Manager.PerformClinical((source, time) => source.RecordCommunication("TrainingPresentation",
                            "La incorporación prevista no se inició porque no se pudo validar el espacio. El paciente conserva el apoyo.", time, "ScenarioConfiguration"));
                    }
                }
            }
            finally { handling = false; }
        }
        public static string Label(Case01Variation variation)
        {
            switch (variation)
            {
                case Case01Variation.Persistent: return "Síntomas persistentes";
                case Case01Variation.Recurrence: return "Intento de incorporación";
                default: return "Práctica habitual";
            }
        }
        void OnDestroy() { if (review != null && review.Manager != null) review.Manager.Changed -= UpdatePresentation; }
    }
}
