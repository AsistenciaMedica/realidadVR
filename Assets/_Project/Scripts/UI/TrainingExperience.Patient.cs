using EmergencyVR.Dialogue;
using EmergencyVR.Medical;
using UnityEngine;

namespace EmergencyVR.UI
{
    public sealed partial class TrainingExperience
    {
        public PatientConversationController PatientConversation { get; private set; }

        public static string LearnerTitle(MedicalScenarioDefinition definition)
        {
            var identity = definition?.patientIdentity;
            if (identity == null) return definition?.name ?? "Práctica de controles";
            return identity.displayName + (string.IsNullOrWhiteSpace(identity.role) ? "" : " · " + identity.role);
        }

        static string LearnerContext(MedicalScenarioDefinition definition, string fallback)
        {
            var identity = definition?.patientIdentity;
            return identity == null ? fallback : identity.context + "\n\n" + identity.presentingComplaint;
        }

        string PatientDetails()
        {
            var definition = Review.Manager.MedicalDefinition;
            var identity = definition?.patientIdentity;
            if (identity == null) return "Práctica de familiarización con el paciente.";
            string details = identity.displayName + " · " + identity.role;
            var patient = Review.Manager.MedicalSession?.Patient;
            if (patient != null && (Review.Procedures.TrainingMode || PatientConversation.IdentityObtained))
                details += "\n" + patient.age + " años";
            return details;
        }

        void DrawLegacyConversation()
        {
            var questions = new[] { PatientQuestion.NameAndAge, PatientQuestion.WhatHappened, PatientQuestion.History, PatientQuestion.Witness };
            var names = new[] { "Patient name", "Patient situation", "Patient history", "Witness account" };
            var labels = new[] { "Nombre y edad", "¿Qué ha ocurrido?", "Antecedentes", "Escuchar al testigo" };
            for (int i = 0; i < questions.Length; i++)
            {
                var question = questions[i];
                var button = Button(content, names[i], labels[i], 44 + i % 2 * 242, 344 + i / 2 * 58, 230, 48,
                    () => { PatientConversation.Ask(question); nextRefresh = 0; });
                button.GetComponentInChildren<UnityEngine.UI.Text>().fontSize = 18;
                live.Add(() => button.interactable = PatientConversation.CanAsk);
            }
            var body = ScrollArea(45, 465, 468, 247, 247);
            var transcript = Label(body, "Patient conversation", "", 0, 0, 431, 247, 19, Soft);
            live.Add(() =>
            {
                transcript.text = PatientConversation.Transcript;
                if (string.IsNullOrWhiteSpace(transcript.text))
                    transcript.text = "Pregunta y escucha. Aquí se conservarán las respuestas de esta conversación.\n\nLas mediciones se consultan en el monitor.";
                float height = Mathf.Max(247, transcript.preferredHeight + 20);
                transcript.rectTransform.sizeDelta = new Vector2(431, height);
                ((RectTransform)body).sizeDelta = new Vector2(450, height);
            });
        }
    }
}
