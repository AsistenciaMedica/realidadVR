using System;
using System.Linq;
using EmergencyVR.Medical;
using EmergencyVR.Scenarios;
using UnityEngine;

namespace EmergencyVR.Dialogue
{
    public enum ClinicalHelpStage { Idle, Connecting, AwaitingLocation, AwaitingSituation, AwaitingConfirmation, Confirmed, HandoverCompleted }

    /// <summary>A simulated conversation adapter. It reports communication, never invents clinical observations.</summary>
    public sealed class ClinicalHelpController : MonoBehaviour
    {
        [Min(0)] public float ContactPresentationSeconds = 1.5f;
        ScenarioManager manager;
        MedicalScenarioRuntime attempt;
        string attemptId;
        double contactAt;
        bool delegated;
        public ClinicalHelpStage Status { get; private set; }
        public string CurrentLine { get; private set; } = "La llamada y la petición al compañero son simuladas.";
        public bool IsActive => SameAttempt && attempt.Capabilities.usesObservedPatientData;
        public bool CanCommunicateLocation => Ready && Status == ClinicalHelpStage.AwaitingLocation;
        public bool CanCommunicateSituation => Ready && Status == ClinicalHelpStage.AwaitingSituation;
        public bool CanConfirmOperator => Ready && Status == ClinicalHelpStage.AwaitingConfirmation;
        public bool CanPerformHandover => Ready && Status == ClinicalHelpStage.Confirmed &&
            (attempt.HelpState == HelpRequestState.Confirmed || attempt.HelpState == HelpRequestState.HandoverAvailable);
        public bool IsDelegated => delegated;
        public string SituationSummary => SameAttempt ? SummarizeObserved(attempt.Observations) : "No hay un intento activo.";
        public event Action Changed;
        bool SameAttempt => attempt != null && attempt == manager?.MedicalSession && attemptId == attempt.ClinicalState?.AttemptId;
        bool Ready => SameAttempt && manager.AcceptsInput && !attempt.IsFinished;

        public void Initialize(ScenarioManager owner) { manager = owner; ResetForAttempt(); }
        public void ResetForAttempt()
        {
            attempt = manager?.MedicalSession; attemptId = attempt?.ClinicalState?.AttemptId;
            contactAt = 0; delegated = false; Status = ClinicalHelpStage.Idle;
            CurrentLine = "Puedes solicitar ayuda desde el comienzo. La llamada es simulada.";
            Changed?.Invoke();
        }
        void Update() { RefreshContext(); }
        void RefreshContext()
        {
            if (attempt != manager?.MedicalSession || attemptId != manager?.MedicalSession?.ClinicalState?.AttemptId) ResetForAttempt();
            if (!Ready) return;
            if (attempt.HelpState == HelpRequestState.Completed && Status != ClinicalHelpStage.HandoverCompleted)
            {
                Status = ClinicalHelpStage.HandoverCompleted; CurrentLine = "El relevo simulado ha quedado registrado. Puedes revisar tu práctica.";
                Changed?.Invoke(); return;
            }
            if ((attempt.HelpState == HelpRequestState.Confirmed || attempt.HelpState == HelpRequestState.HandoverAvailable) &&
                Status != ClinicalHelpStage.Confirmed && Status != ClinicalHelpStage.HandoverCompleted)
            {
                Status = ClinicalHelpStage.Confirmed; CurrentLine = "La ayuda está confirmada. Puedes continuar observando y comunicar el relevo.";
                Changed?.Invoke(); return;
            }
            if (Status == ClinicalHelpStage.Idle && (attempt.HelpState == HelpRequestState.Requested || attempt.HelpState == HelpRequestState.Delegated))
            {
                delegated = attempt.HelpState == HelpRequestState.Delegated;
                Status = ClinicalHelpStage.Connecting; contactAt = attempt.Elapsed + Math.Max(0, ContactPresentationSeconds);
                CurrentLine = "Petición recibida. Conectando la comunicación simulada…"; Changed?.Invoke();
            }
            if (!Ready || Status != ClinicalHelpStage.Connecting || attempt.Elapsed < contactAt) return;
            Status = ClinicalHelpStage.AwaitingLocation;
            CurrentLine = delegated ? "Compañero (fuera de escena): «Voy a avisar. Confírmame dónde estamos»." : "Operador simulado: «Emergencias. ¿Dónde estáis?»";
            Changed?.Invoke();
        }
        public bool RequestCall(bool delegateToCompanion)
        {
            RefreshContext();
            if (!Ready || !IsActive || Status != ClinicalHelpStage.Idle) return false;
            bool accepted = false;
            manager.PerformClinical((runtime, time) => accepted = runtime.Submit(delegateToCompanion ? "DelegateHelp" : "RequestHelp", time) == "Accepted");
            if (!accepted) return false;
            delegated = delegateToCompanion; Status = ClinicalHelpStage.Connecting;
            contactAt = attempt.Elapsed + Math.Max(0, ContactPresentationSeconds);
            CurrentLine = delegated ? "Has pedido al compañero que avise. Espera su respuesta." : "Llamada simulada iniciada. Conectando con el operador…";
            Changed?.Invoke(); return true;
        }
        public bool CommunicateLocation()
        {
            RefreshContext();
            if (!CanCommunicateLocation) return false;
            var environment = manager.MedicalDefinition.environment;
            string location = environment == "gym" ? "Estamos en el gimnasio, en la zona de cardio." :
                environment == "football" ? "Estamos en el campo de fútbol, junto a la zona de entrenamiento." :
                environment == "mall" ? "Estamos en la zona común del centro comercial." : "Estamos en la clínica, junto al paciente.";
            manager.PerformClinical((runtime, time) => runtime.RecordCommunication(delegated ? "Companion" : "Operator", location, time, "Player"));
            Status = ClinicalHelpStage.AwaitingSituation;
            CurrentLine = "Ubicación comunicada. " + (delegated ? "Compañero" : "Operador simulado") + ": «¿Qué has observado?»";
            Changed?.Invoke(); return true;
        }
        public bool CommunicateSituation()
        {
            RefreshContext();
            if (!CanCommunicateSituation) return false;
            string summary = SituationSummary;
            manager.PerformClinical((runtime, time) => runtime.RecordCommunication(delegated ? "Companion" : "Operator", summary, time, "Player"));
            Status = ClinicalHelpStage.AwaitingConfirmation;
            CurrentLine = "Información comunicada. " + (delegated ? "El compañero" : "El operador simulado") + " confirma la recepción de tu mensaje.";
            Changed?.Invoke(); return true;
        }
        public bool ConfirmOperator()
        {
            RefreshContext();
            if (!CanConfirmOperator) return false;
            bool confirmed = false;
            string reply = delegated ? "Compañero: «Ya están avisados. He transmitido la ubicación y lo que has observado»." : "Operador simulado: «He recibido la información. La ayuda queda avisada».";
            manager.PerformClinical((runtime, time) =>
            {
                confirmed = runtime.ConfirmHelp(time, delegated ? "SimulatedCompanion" : "SimulatedOperator", attemptId);
                if (confirmed) runtime.RecordCommunication("Player", reply, time, delegated ? "SimulatedCompanion" : "SimulatedOperator");
            });
            if (!confirmed) return false;
            Status = ClinicalHelpStage.Confirmed;
            CurrentLine = reply + " Puedes seguir observando al paciente y comunicar el relevo cuando corresponda.";
            Changed?.Invoke(); return true;
        }
        public bool PerformHandover()
        {
            RefreshContext();
            if (!CanPerformHandover) return false;
            bool completed = false;
            string summary = SituationSummary;
            manager.PerformClinical((runtime, time) =>
            {
                runtime.RecordCommunication("HandoverTeam", summary, time, "Player");
                completed = runtime.Submit("PerformHandover", time) == "Accepted";
            });
            if (!completed) return false;
            Status = ClinicalHelpStage.HandoverCompleted;
            CurrentLine = "Relevo simulado: «Recibido. Nos hacemos cargo de la información que has comunicado». Puedes finalizar y revisar tu práctica.";
            Changed?.Invoke(); return true;
        }
        public static string SummarizeObserved(ObservedPatientDataStore store)
        {
            if (store == null || store.Count == 0) return "Hay una persona que necesita ayuda. Aún no he confirmado su respuesta ni su respiración.";
            var records = store.All;
            var parts = new System.Collections.Generic.List<string> { "Información obtenida hasta ahora:" };
            if (records.Any(x => x.type == "PatientResponsive" && x.valid)) parts.Add("He comprobado que responde.");
            if (records.Any(x => x.type == "BreathingNormal" && x.valid)) parts.Add("He observado respiración espontánea.");
            var symptom = store.Interviews.LastOrDefault(x => x.intent == DialogueIntent.CURRENT_STATUS) ?? store.Interviews.LastOrDefault(x => x.intent == DialogueIntent.MAIN_SYMPTOM);
            if (symptom != null) parts.Add("El paciente refiere: «" + symptom.text + "».");
            var onset = store.Interviews.LastOrDefault(x => x.intent == DialogueIntent.ONSET);
            if (onset != null) parts.Add("Inicio referido: «" + onset.text + "».");
            foreach (var reading in store.Measurements.Where(x => x.valid && x.hasValue).GroupBy(x => x.type).Select(x => x.Last()))
            {
                var when = TimeSpan.FromSeconds(reading.simulationTime).ToString(@"mm\:ss");
                parts.Add(reading.type == "BloodPressure" ? $"TA obtenida a las {when}: {reading.systolic:0}/{reading.diastolic:0} {reading.unit}." : $"SpO₂ obtenida a las {when}: {reading.value:0} {reading.unit}.");
            }
            if (parts.Count == 1) parts.Add("Todavía estoy valorando la situación y necesito ayuda.");
            return string.Join(" ", parts);
        }
    }
}
