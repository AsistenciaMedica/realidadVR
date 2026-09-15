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
        public int MedicalSeed { get; set; } = 2026;
        double medicalStart, simulationOffset;
        int lastSecond=-1;
        public event Action Changed;
        public bool IsRunning { get { return medicalDefinition!=null ? medicalSession!=null && !medicalSession.IsFinished : session != null && !session.IsFinished; } }
        public CaseSession Session { get { return session; } }
        public string Feedback { get; private set; } = "Pulsa Iniciar caso demo.";
        public ScenarioDefinition Scenario { get { return scenario; } }

        public void Configure(ScenarioDefinition definition, PatientController patientController,
            EvaluationManager evaluationManager)
        {
            scenario = definition;
            patient = patientController;
            evaluation = evaluationManager;
            medicalDefinition=null; medicalSession=null; MedicalResult=null; session=null;
            patient.ClearMedicalState();
        }

        public void ConfigureMedical(MedicalScenarioDefinition definition,MedicalLibrary library)
        {
            if(IsRunning) throw new InvalidOperationException("Finish current attempt before selecting a case.");
            definition.Validate(library); medicalDefinition=definition.Copy(); medicalLibrary=library;
            medicalSession=null; MedicalResult=null; session=null; evaluation.Clear();
            Show("Vital VR · "+definition.name+" · PENDING MEDICAL VALIDATION");
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
                if(medicalDefinition!=null)
                {
                    medicalSession=new MedicalScenarioRuntime(medicalDefinition,medicalLibrary,MedicalSeed);
                    medicalStart=Time.realtimeSinceStartupAsDouble; simulationOffset=0; lastSecond=-1;
                    MedicalResult=null; evaluation.Clear(); patient.PresentMedical(medicalSession.Patient);
                    Show("Vital VR · Caso iniciado. Seed "+MedicalSeed+". Valores simulados."); return;
                }
                // Snapshot: editing the ScriptableObject never changes a running attempt.
                var next = new CaseSession(scenario.defaultCase.ToDomain(), Time.realtimeSinceStartupAsDouble);
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
            if (!IsRunning) { Show("Inicia un caso antes de interactuar."); return; }
            if(medicalDefinition!=null)
            {
                medicalSession.Submit(actionId=="demo.inspect"?"CheckResponsiveness":actionId,MedicalTime());
                patient.PresentMedical(medicalSession.Patient); Show(medicalSession.LastFeedback); return;
            }
            var record = session.Record(actionId, Time.realtimeSinceStartupAsDouble);
            patient.Present(session.State);
            var status = record.Disposition == ActionDisposition.Accepted ? "registrada" :
                record.Disposition == ActionDisposition.Late ? "registrada fuera de tiempo" :
                record.Disposition == ActionDisposition.Duplicate ? "repetida" :
                record.Disposition == ActionDisposition.OutOfOrder ? "fuera de orden" : "desconocida";
            Show("Acción " + status + ".");
        }

        public void CompleteDemoTransition() { SubmitAction("demo.confirm"); }

        public void FinishCase()
        {
            if (!IsRunning) { Show("No hay un caso en curso."); return; }
            if(medicalDefinition!=null)
            {
                MedicalResult=medicalSession.Finish(MedicalTime()); patient.PresentMedical(MedicalResult.finalPatient);
                Show(medicalSession.LastFeedback); return;
            }
            evaluation.Publish(session.Finish(Time.realtimeSinceStartupAsDouble));
            Show("Caso finalizado. Puedes iniciar otro intento.");
        }

        double MedicalTime() => Time.realtimeSinceStartupAsDouble-medicalStart+simulationOffset;
        public void AdvanceTrainingTime(double seconds)
        {
            if(!IsRunning||medicalSession==null) return;
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0||seconds>3600) throw new ArgumentOutOfRangeException(nameof(seconds));
            simulationOffset+=seconds; medicalSession.Tick(MedicalTime()); patient.PresentMedical(medicalSession.Patient); Show(medicalSession.LastFeedback);
        }
        void Update()
        {
            if(medicalDefinition==null||!IsRunning) return;
            var time=MedicalTime();if((int)time==lastSecond)return;
            lastSecond=(int)time;medicalSession.Tick(time);patient.PresentMedical(medicalSession.Patient);Show(medicalSession.LastFeedback);
        }

        void Show(string message)
        {
            Feedback = message;
            Changed?.Invoke();
        }
    }
}
