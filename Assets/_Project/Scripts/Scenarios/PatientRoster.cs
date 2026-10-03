using System;
using System.Linq;
using EmergencyVR.Medical;
using UnityEngine;

namespace EmergencyVR.Scenarios
{
    [Serializable]
    public sealed class PatientRoster
    {
        public int schemaVersion;
        public PatientIdentity[] profiles = Array.Empty<PatientIdentity>();

        public static PatientRoster Load()
        {
            var source = Resources.Load<TextAsset>("PatientRoster");
            if (source == null) throw new InvalidOperationException("Missing PatientRoster.json.");
            var roster = JsonUtility.FromJson<PatientRoster>(source.text);
            if (roster == null) throw new InvalidOperationException("Invalid PatientRoster.json.");
            return roster;
        }

        public void Apply(MedicalLibrary library, ReleaseScope scope)
        {
            if (library == null || scope == null) throw new ArgumentNullException("Patient roster requires a library and release scope.");
            var ids = scope.ScenarioIds;
            if (schemaVersion != 1 || profiles == null || profiles.Length != ids.Length ||
                profiles.Any(p => p == null) || profiles.Select(p => p.id).Distinct().Count() != profiles.Length ||
                profiles.Select(p => p.scenarioId).Distinct().Count() != profiles.Length ||
                profiles.Any(p => !ids.Contains(p.scenarioId)))
                throw new ArgumentException("The release needs one distinct patient per case.");
            // Validate every row before changing the composed library.
            foreach (var profile in profiles)
            {
                profile.Validate(profile.scenarioId);
                var scenario = library.scenarios.Single(s => s.id == profile.scenarioId);
                var clinical = scenario.clinicalV2?.patientProfile;
                if (clinical != null && (clinical.age != profile.age || clinical.sex != profile.sex || clinical.name != profile.displayName))
                    throw new ArgumentException("Patient roster disagrees with the clinical V2 authoring data.");
                if (scenario.initialState.consciousness == "Unresponsive" && !string.IsNullOrWhiteSpace(profile.patientOpeningLine))
                    throw new ArgumentException("An unresponsive patient must not have an opening speech line.");
            }
            foreach (var source in profiles)
            {
                var scenario = library.scenarios.Single(s => s.id == source.scenarioId);
                var profile = source.Copy();
                scenario.patientIdentity = profile;
                scenario.initialState.patientId = profile.id;
                scenario.initialState.patientName = profile.displayName;
                scenario.initialState.age = profile.age;
                scenario.initialState.sex = profile.sex;
                scenario.initialState.appearance = profile.clothingDescription;
                scenario.variation.minAge = scenario.variation.maxAge = profile.age;
                scenario.variation.sexes = new[] { profile.sex };
                scenario.variation.witnessStatements = (string[])profile.witnessLines.Clone();
                // Keep the initial authored posture; randomisation must not turn a seated
                // breathless person or an unresponsive person into a different setup.
                scenario.variation.positions = new[] { scenario.initialState.position };
                if (scenario.clinicalV2 == null)
                {
                    scenario.initialDialogue = profile.patientOpeningLine ?? "";
                    scenario.initialState.dialogue = scenario.initialDialogue;
                }
            }
        }
    }
}
