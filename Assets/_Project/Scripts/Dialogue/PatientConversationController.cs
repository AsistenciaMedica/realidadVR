using System;
using System.Collections.Generic;
using System.Linq;
using EmergencyVR.Medical;

namespace EmergencyVR.Dialogue
{
    public enum PatientQuestion { NameAndAge, WhatHappened, History, Witness }

    public sealed class PatientConversationLine
    {
        public string Speaker { get; }
        public string Text { get; }
        public bool SpokenByPatient { get; }
        public PatientConversationLine(string speaker, string text, bool spokenByPatient = false)
        { Speaker = speaker; Text = text; SpokenByPatient = spokenByPatient; }
        public override string ToString() => Speaker + ": " + Text;
    }

    // Authored conversation for legacy cases. Reading a line never submits an action,
    // reveals unmeasured vital signs or changes the clinical engine or its score.
    public sealed class PatientConversationController
    {
        readonly Func<MedicalScenarioRuntime> currentAttempt;
        readonly Func<MedicalScenarioDefinition> currentDefinition;
        readonly Func<bool> acceptsInput;
        readonly List<PatientConversationLine> lines = new List<PatientConversationLine>();
        MedicalScenarioRuntime attempt;
        string attemptId;
        int witnessLine;
        bool identityObtained;

        public PatientConversationController(Func<MedicalScenarioRuntime> currentAttempt,
            Func<MedicalScenarioDefinition> currentDefinition, Func<bool> acceptsInput)
        {
            this.currentAttempt = currentAttempt ?? throw new ArgumentNullException(nameof(currentAttempt));
            this.currentDefinition = currentDefinition ?? throw new ArgumentNullException(nameof(currentDefinition));
            this.acceptsInput = acceptsInput ?? throw new ArgumentNullException(nameof(acceptsInput));
        }

        void Synchronize()
        {
            var active = currentAttempt();
            if (ReferenceEquals(active, attempt) && attemptId == active?.AttemptId) return;
            attempt = active; attemptId = active?.AttemptId;
            lines.Clear(); witnessLine = 0; identityObtained = false;
        }

        public bool CanAsk
        {
            get
            {
                Synchronize();
                var definition = currentDefinition();
                return attempt != null && !attempt.IsFinished && !attempt.IsPaused && acceptsInput() &&
                    definition?.patientIdentity != null && definition.clinicalV2 == null;
            }
        }
        public bool IdentityObtained { get { Synchronize(); return identityObtained; } }
        public PatientConversationLine[] Lines { get { Synchronize(); return lines.ToArray(); } }
        public string Transcript { get { Synchronize(); return string.Join("\n\n", lines.Select(line => line.ToString())); } }

        public PatientConversationLine Ask(PatientQuestion question)
        {
            if (!CanAsk) return null;
            var identity = currentDefinition().patientIdentity;
            var patient = attempt.Patient;
            PatientConversationLine response;
            if (question == PatientQuestion.Witness)
            {
                var accounts = identity.witnessLines ?? Array.Empty<string>();
                response = accounts.Length == 0
                    ? new PatientConversationLine("Entorno", "No hay un testigo disponible que pueda aportar información.")
                    : new PatientConversationLine("Testigo", accounts[witnessLine++ % accounts.Length]);
            }
            else if (patient.consciousness == "Unresponsive" || patient.respiration == "absent" ||
                patient.respiration == "agonal" || patient.circulation == "pulseless")
                response = new PatientConversationLine("Observación", "El paciente no responde a la pregunta.");
            else if (patient.consciousness == "Drowsy")
                response = new PatientConversationLine("Observación", "Intenta responder, pero no consigue completar una frase.");
            else if (patient.consciousness == "Confused" && question != PatientQuestion.WhatHappened)
                response = new PatientConversationLine("Observación", "Le cuesta mantener una respuesta clara; no confirma esa información.");
            else if (question == PatientQuestion.WhatHappened && !string.IsNullOrWhiteSpace(patient.dialogue) &&
                patient.dialogue != currentDefinition().initialDialogue)
                response = new PatientConversationLine("Observación", patient.dialogue);
            else
            {
                string reply;
                switch (question)
                {
                    case PatientQuestion.NameAndAge:
                        reply = "Me llamo " + identity.displayName + ". Tengo " + patient.age + " años.";
                        identityObtained = true;
                        break;
                    case PatientQuestion.WhatHappened:
                        reply = identity.patientOpeningLine;
                        break;
                    case PatientQuestion.History:
                        reply = identity.historyReply;
                        break;
                    default: throw new ArgumentOutOfRangeException(nameof(question));
                }
                if (string.IsNullOrWhiteSpace(reply))
                    response = new PatientConversationLine("Observación", "No aporta más información a esta pregunta.");
                else if (patient.respiration == "fast" || patient.respiration == "labored" ||
                    patient.consciousness == "Confused" || patient.flags.Contains("labored breathing"))
                    response = new PatientConversationLine(identity.displayName,
                        question == PatientQuestion.NameAndAge ? identity.displayName + "… " + patient.age + " años…" : ShortReply(reply) + "…", true);
                else response = new PatientConversationLine(identity.displayName, reply, true);
            }
            lines.Add(response);
            if (lines.Count > 6) lines.RemoveAt(0);
            return response;
        }

        static string ShortReply(string text)
        {
            var phrase = text.Split(new[] { '.', ';', '!', '?' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? text;
            var words = phrase.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", words.Take(9));
        }
    }
}
