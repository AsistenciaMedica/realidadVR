using System;
using System.Linq;

namespace EmergencyVR.Medical
{
    [Serializable] public sealed class DialogueIntentDefinition
    {
        public DialogueIntent intent;
        public DialogueResponseDefinition[] responses=Array.Empty<DialogueResponseDefinition>();
        public DialogueIntentDefinition Copy() { return new DialogueIntentDefinition {intent=intent,responses=responses.Select(r=>r.Copy()).ToArray()}; }
    }
    [Serializable] public sealed class DialogueResponseDefinition
    {
        public string id, text, audioReference, subtitle;
        public DialogueIntent intent;
        public string[] allowedClinicalStates=Array.Empty<string>();
        public bool requiresCanSpeak=true, marksObservation=true;
        public double minStateSeconds;
        public DialogueResponseDefinition Copy() { var x=(DialogueResponseDefinition)MemberwiseClone(); x.allowedClinicalStates=(string[])allowedClinicalStates.Clone(); return x; }
    }
}
