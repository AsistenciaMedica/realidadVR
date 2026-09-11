using System;
using EmergencyVR.Core;
using EmergencyVR.Evaluation;
using EmergencyVR.Patient;
using UnityEngine;

namespace EmergencyVR.Scenarios
{
    public sealed class ScenarioManager : MonoBehaviour
    {
        [SerializeField] ScenarioDefinition scenario;
        [SerializeField] PatientController patient;
        [SerializeField] EvaluationManager evaluation;
        CaseSession session;
        public event Action Changed;
        public bool IsRunning { get { return session != null && !session.IsFinished; } }
        public CaseSession Session { get { return session; } }
        public string Feedback { get; private set; } = "Pulsa Iniciar caso demo.";
        public ScenarioDefinition Scenario { get { return scenario; } }

        public void Configure(ScenarioDefinition definition, PatientController patientController,
            EvaluationManager evaluationManager)
        {
            scenario = definition;
            patient = patientController;
            evaluation = evaluationManager;
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
            evaluation.Publish(session.Finish(Time.realtimeSinceStartupAsDouble));
            Show("Caso finalizado. Puedes iniciar otro intento.");
        }

        void Show(string message)
        {
            Feedback = message;
            Changed?.Invoke();
        }
    }
}
