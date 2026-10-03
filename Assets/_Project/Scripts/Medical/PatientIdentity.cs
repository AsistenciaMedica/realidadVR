using System;
using System.Linq;

namespace EmergencyVR.Medical
{
    // Fictional authored identity. Clinical state is owned by the scenario runtime.
    [Serializable]
    public sealed class PatientIdentity
    {
        public string scenarioId, id, displayName, sex, appearanceResource, role, context, presentingComplaint;
        public string patientOpeningLine, historyReply, clothingDescription;
        public int age;
        public string[] witnessLines = Array.Empty<string>();

        public PatientIdentity Copy()
        {
            var copy = (PatientIdentity)MemberwiseClone();
            copy.witnessLines = (string[])witnessLines.Clone();
            return copy;
        }

        public void Validate(string expectedScenarioId)
        {
            if (scenarioId != expectedScenarioId || string.IsNullOrWhiteSpace(id) ||
                id.Any(c => !char.IsLetterOrDigit(c)) || string.IsNullOrWhiteSpace(displayName) ||
                age < 18 || age > 100 || (sex != "female" && sex != "male") ||
                appearanceResource != "Visual/Appearances/" + id)
                throw new ArgumentException("Invalid patient identity for " + expectedScenarioId);
            if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(context) ||
                string.IsNullOrWhiteSpace(presentingComplaint) || string.IsNullOrWhiteSpace(clothingDescription) ||
                witnessLines == null || witnessLines.Length == 0 || witnessLines.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Incomplete patient context for " + expectedScenarioId);
        }
    }
}
