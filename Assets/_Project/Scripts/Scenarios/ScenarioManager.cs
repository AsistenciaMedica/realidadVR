using System;
using EmergencyVR.Core;
using EmergencyVR.Evaluation;
using EmergencyVR.Patient;
using EmergencyVR.Medical;
using UnityEngine;

namespace EmergencyVR.Scenarios
{
    public sealed class ScenarioManager : MonoBehaviour
    {
        [SerializeField] ScenarioDefinition scenario;
        [SerializeField] PatientController patient;
        [SerializeField] EvaluationManager evaluation;
        CaseSession session;
        MedicalScenarioDefinition medicalDefinition;
        MedicalLibrary medicalLibrary;
        MedicalScenarioRuntime medicalSession;
        public MedicalScenarioRuntime MedicalSession => medicalSession;
        public MedicalScenarioDefinition MedicalDefinition => medicalDefinition;
        public MedicalDebrief MedicalResult { get; private set; }
        public PatientPositionTransitionController PositionTransitions { get; private set; }
        public EmergencyVR.Dialogue.ClinicalDialogueController Dialogue { get; private set; }
        public int MedicalSeed { get; set; } = 2026;
        public event Action<string> ActionAccepted;
        double medicalStart, simulationOffset;
        double pausedAt, pausedDuration;
        float previousTimeScale = 1;
        bool previousAudioPause;
        public bool IsPaused { get; private set; }
        public bool AcceptsInput => IsRunning && !IsPaused;
        public double SimulationClock => (IsPaused ? pausedAt : Time.realtimeSinceStartupAsDouble) - pausedDuration;
        public double ElapsedSeconds => medicalDefinition != null ? MedicalTime() : session == null ? 0 : SimulationClock - medicalStart;
        int lastSecond=-1;
        public event Action Changed;
        public bool IsRunning { get { return medicalDefinition!=null ? medicalSession!=null && !medicalSession.IsFinished : session != null && !session.IsFinished; } }
        public CaseSession Session { get { return session; } }
        public string Feedback { get; private set; } = "Pulsa Iniciar caso demo.";
        public ScenarioDefinition Scenario { get { return scenario; } }

        public void Configure(ScenarioDefinition definition, PatientController patientController,
            EvaluationManager evaluationManager)
        {
            SetPaused(false);
            scenario = definition;
            patient = patientController;
            evaluation = evaluationManager;
            medicalDefinition=null; medicalSession=null; MedicalResult=null; session=null;
            patient.ClearMedicalState();
            EnsureClinicalAdapters();
            PositionTransitions.ResetForAttempt();
            Dialogue.ResetForAttempt();
        }

        public void ConfigureMedical(MedicalScenarioDefinition definition,MedicalLibrary library)
        {
            if(IsRunning) throw new InvalidOperationException("Finish current attempt before selecting a case.");
            definition.Validate(library); medicalDefinition=definition.Copy(); medicalLibrary=library;
            medicalSession=null; MedicalResult=null; session=null; evaluation.Clear();
            Show("VITAL VR · "+definition.name+" · PENDING MEDICAL VALIDATION");
        }

        public void StartCase()
        {
            if (IsRunning) { Show("Ya hay un caso en curso."); return; }
            if (scenario == null || scenario.defaultCase == null || patient == null || evaluation == null)
            {
                Show("Faltan referencias del escenario. Revisa la configuración.");
                return;
            }
            try
            {
                SetPaused(false);
                medicalStart=SimulationClock;
                if(medicalDefinition!=null)
                {
                    medicalSession=new MedicalScenarioRuntime(medicalDefinition,medicalLibrary,MedicalSeed,Application.version);
                    simulationOffset=0; lastSecond=-1;
                    EnsureClinicalAdapters();
                    PositionTransitions.ResetForAttempt(); Dialogue.ResetForAttempt();
                    GetComponent<EmergencyVR.Dialogue.ClinicalHelpController>()?.ResetForAttempt();
                    MedicalResult=null; evaluation.Clear(); PresentMedical();
                    Show("Entrenamiento iniciado. Observa al paciente y valora la situación."); return;
                }
                // Snapshot: editing the ScriptableObject never changes a running attempt.
                var next = new CaseSession(scenario.defaultCase.ToDomain(), SimulationClock);
                session = next;
                evaluation.Clear();
                patient.Present(session.State);
                Show("Caso demo iniciado. Interactúa con el paciente usando Grip.");
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is OverflowException)
            {
                Debug.LogError("Invalid case definition: " + exception.Message, this);
                Show("Caso inválido: " + exception.Message);
            }
        }

        // XR, UI and future hand-tracking adapters share this semantic entry point.
        public void SubmitAction(string actionId)
        {
            TrySubmitAction(actionId);
        }
        public bool TrySubmitAction(string actionId)
        {
            if (IsPaused) return false;
            if (!IsRunning) { Show("Inicia un caso antes de interactuar."); return false; }
            if(medicalDefinition!=null)
            {
                var submittedAction=actionId=="demo.inspect"?(medicalSession.Capabilities.usesObservedPatientData?"AssessResponsiveness":"CheckResponsiveness"):actionId;
                if(submittedAction=="AssistPatient" && medicalSession.Capabilities.usesPhysicalPositionConfirmation)
                {
                    var body=GetComponent<EmergencyVR.Patient.Presentation.Case01PatientPresentation>();
                    return body!=null&&body.IsActive?body.BeginAssistance():PositionTransitions.Request(PhysicalPosition.Supine);
                }
                if(medicalSession.Capabilities.usesIntentDialogue)
                {
                    var help=GetComponent<EmergencyVR.Dialogue.ClinicalHelpController>();
                    if(help!=null&&help.IsActive)
                    {
                        if(submittedAction=="RequestHelp"||submittedAction=="DelegateHelp") return help.RequestCall(submittedAction=="DelegateHelp");
                        if(submittedAction=="PerformHandover") return help.PerformHandover();
                    }
                }
                var disposition=medicalSession.Submit(submittedAction,MedicalTime());
                if(disposition=="Accepted"||disposition=="Late") ActionAccepted?.Invoke(submittedAction);
                PresentMedical(); Show(medicalSession.LastFeedback); return disposition=="Accepted"||disposition=="Late";
            }
            var record = session.Record(actionId, SimulationClock);
            patient.Present(session.State);
            if(record.Disposition==ActionDisposition.Accepted) ActionAccepted?.Invoke(actionId);
            var status = record.Disposition == ActionDisposition.Accepted ? "registrada" :
                record.Disposition == ActionDisposition.Late ? "registrada fuera de tiempo" :
                record.Disposition == ActionDisposition.Duplicate ? "repetida" :
                record.Disposition == ActionDisposition.OutOfOrder ? "fuera de orden" : "desconocida";
            Show("Acción " + status + ".");
            return record.Disposition==ActionDisposition.Accepted || record.Disposition==ActionDisposition.Late;
        }

        public void CompleteDemoTransition() { SubmitAction("demo.confirm"); }

        public void FinishCase()
        {
            if (!IsRunning) { Show("No hay un caso en curso."); return; }
            SetPaused(false);
            if(medicalDefinition!=null)
            {
                MedicalResult=medicalSession.Finish(MedicalTime()); PresentMedical();
                Show(medicalSession.LastFeedback); return;
            }
            evaluation.Publish(session.Finish(SimulationClock));
            Show("Caso finalizado. Puedes iniciar otro intento.");
        }

        double MedicalTime() => SimulationClock-medicalStart+simulationOffset;
        void EnsureClinicalAdapters()
        {
            if(PositionTransitions==null) PositionTransitions=GetComponent<PatientPositionTransitionController>()??gameObject.AddComponent<PatientPositionTransitionController>();
            PositionTransitions.Initialize(this);
            if(Dialogue==null) Dialogue=GetComponent<EmergencyVR.Dialogue.ClinicalDialogueController>()??gameObject.AddComponent<EmergencyVR.Dialogue.ClinicalDialogueController>();
            Dialogue.Initialize(this);
        }
        // Adapters submit facts/intent using this attempt's existing clock; they never own physiology.
        public bool PerformClinical(Action<MedicalScenarioRuntime,double> command)
        {
            if(!AcceptsInput || medicalSession==null || command==null) return false;
            command(medicalSession,MedicalTime());
            PresentMedical(); Show(medicalSession.LastFeedback);
            return true;
        }
        void PresentMedical()
        {
            if(medicalSession!=null) patient.PresentMedical(medicalSession.Patient,medicalSession.ClinicalState);
        }
        public void SetPaused(bool paused)
        {
            if (paused == IsPaused || (paused && !IsRunning)) return;
            if (paused)
            {
                if(medicalSession!=null) { medicalSession.Tick(MedicalTime()); PresentMedical(); }
                pausedAt = Time.realtimeSinceStartupAsDouble;
                previousTimeScale = Time.timeScale;
                previousAudioPause = AudioListener.pause;
                IsPaused = true;
                Time.timeScale = 0;
                AudioListener.pause = true;
            }
            else
            {
                pausedDuration += Time.realtimeSinceStartupAsDouble - pausedAt;
                IsPaused = false;
                Time.timeScale = previousTimeScale;
                AudioListener.pause = previousAudioPause;
            }
            medicalSession?.SetPaused(paused);
            Changed?.Invoke();
        }
        void OnApplicationPause(bool paused) { if (paused) SetPaused(true); }
        void OnDestroy() { if (IsPaused) { Time.timeScale = previousTimeScale; AudioListener.pause = previousAudioPause; } }
        public void AdvanceTrainingTime(double seconds)
        {
            if(!AcceptsInput||medicalSession==null) return;
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0||seconds>3600) throw new ArgumentOutOfRangeException(nameof(seconds));
            simulationOffset+=seconds; medicalSession.Tick(MedicalTime()); PresentMedical(); Show(medicalSession.LastFeedback);
        }
        void Update()
        {
            if(medicalDefinition==null||!AcceptsInput) return;
            var time=MedicalTime();
            bool continuous=medicalSession.Capabilities.usesClinicalStateMachine;
            if(!continuous && (int)time==lastSecond)return;
            medicalSession.Tick(time); PresentMedical();
            // Smooth runtime projections do not require rebuilding clinical UI every frame.
            if((int)time!=lastSecond) { lastSecond=(int)time; Show(medicalSession.LastFeedback); }
        }

        void Show(string message)
        {
            Feedback = message;
            Changed?.Invoke();
        }
    }
}
