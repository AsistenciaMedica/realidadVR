using System;
using System.Linq;
using EmergencyVR.Medical;
using UnityEngine;

namespace EmergencyVR.Scenarios
{
    [Serializable]
    public sealed class ReleaseCaseVariant
    {
        public string id, sourceId, trainingCase, environment, name, description, history, incident, position;
        public string[] symptoms = Array.Empty<string>(), visibleSigns = Array.Empty<string>();
    }

    /// <summary>Three shared exercises contextualized for each environment, without duplicating clinical rules.</summary>
    [Serializable]
    public sealed class ReleaseCaseVariants
    {
        public int schemaVersion;
        public ReleaseCaseVariant[] cases = Array.Empty<ReleaseCaseVariant>();
        public static readonly string[] CaseTypes = { "dizziness", "cardiac-arrest", "choking" };
        public static readonly string[] SourceIds = { "review-hypotension-v2", "arrest-witnessed", "choking-complete" };

        public static ReleaseCaseVariants Load()
        {
            var source = Resources.Load<TextAsset>("ReleaseCases");
            if (source == null) throw new InvalidOperationException("Missing ReleaseCases.json.");
            var variants = JsonUtility.FromJson<ReleaseCaseVariants>(source.text);
            if (variants == null || variants.schemaVersion != 1 || variants.cases == null ||
                variants.cases.Length != 9 || variants.cases.Any(c => c == null) ||
                variants.cases.Select(c => c.id).Distinct().Count() != variants.cases.Length)
                throw new ArgumentException("Release requires nine unique exercise variants.");
            return variants;
        }

        public static void LoadInto(MedicalLibrary library)
        {
            var variants = Load();
            var authored = library.scenarios.ToDictionary(s => s.id);
            var composed = library.scenarios.ToList();
            foreach (var variant in variants.cases)
            {
                if (!authored.TryGetValue(variant.sourceId, out var source) ||
                    !CaseTypes.Contains(variant.trainingCase) || string.IsNullOrWhiteSpace(variant.name) ||
                    string.IsNullOrWhiteSpace(variant.description) || string.IsNullOrWhiteSpace(variant.incident) ||
                    (authored.ContainsKey(variant.id) && variant.id != variant.sourceId))
                    throw new ArgumentException("Invalid release exercise: " + variant.id);
                if (variant.sourceId != SourceIds[Array.IndexOf(CaseTypes, variant.trainingCase)] ||
                    variant.position != (variant.trainingCase == "dizziness" ? "seated" : variant.trainingCase == "choking" ? "standing" : "supine"))
                    throw new ArgumentException("The shared exercise must retain its clinical source and starting posture.");
                var scenario = source.Copy();
                scenario.id = variant.id;
                scenario.sourceScenarioId = variant.sourceId;
                scenario.trainingCase = variant.trainingCase;
                scenario.environment = variant.environment;
                scenario.name = variant.name;
                scenario.description = variant.description;
                scenario.history = variant.history ?? "";
                scenario.incident = variant.incident;
                scenario.symptoms = (string[])variant.symptoms.Clone();
                scenario.visibleSigns = (string[])variant.visibleSigns.Clone();
                scenario.initialState.position = variant.position;
                scenario.variation.positions = new[] { variant.position };
                if (scenario.clinicalV2 != null)
                {
                    var metadata = scenario.clinicalV2.metadata;
                    metadata.scenarioId = scenario.id;
                    metadata.environment = scenario.environment;
                    metadata.displayName = scenario.name;
                    metadata.description = scenario.description;
                    metadata.history = scenario.history;
                    metadata.incident = scenario.incident;
                    EmergencyVR.Dialogue.ClinicalScenarioLocation.ApplyDialogueContext(scenario.clinicalV2, scenario.environment);
                }
                scenario.Validate(library);
                var index = composed.FindIndex(s => s.id == scenario.id);
                if (index < 0) composed.Add(scenario); else composed[index] = scenario;
            }
            library.scenarios = composed.ToArray();
        }

        public static string SourceId(string id)
        {
            return Load().cases.FirstOrDefault(c => c.id == id)?.sourceId ?? id;
        }
    }
}
