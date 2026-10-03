using System;
using System.Collections.Generic;
using System.Linq;
using EmergencyVR.Medical;
using UnityEngine;

namespace EmergencyVR.Scenarios
{
    /// <summary>Product selection only; the complete authored medical library remains intact.</summary>
    [Serializable]
    public sealed class ReleaseScope
    {
        public const int EnvironmentCount = 3;
        public const int CasesPerEnvironment = 5;
        public int schemaVersion;
        public string releaseId;
        public ReleaseEnvironment[] environments = Array.Empty<ReleaseEnvironment>();

        public string[] ScenarioIds => environments.SelectMany(e => e.scenarioIds).ToArray();

        public static ReleaseScope Load(MedicalLibrary library)
        {
            var source = Resources.Load<TextAsset>("ReleaseScope");
            if (source == null) throw new InvalidOperationException("Missing ReleaseScope.json.");
            var scope = JsonUtility.FromJson<ReleaseScope>(source.text);
            if (scope == null) throw new InvalidOperationException("Invalid ReleaseScope.json.");
            scope.Validate(library);
            return scope;
        }

        public void Validate(MedicalLibrary library)
        {
            if (library?.scenarios == null) throw new ArgumentNullException(nameof(library));
            if (schemaVersion != 1 || string.IsNullOrWhiteSpace(releaseId))
                throw new ArgumentException("Release scope requires schema version 1 and a release ID.");
            if (environments == null || environments.Length != EnvironmentCount)
                throw new ArgumentException("Release scope must contain exactly three environments.");
            var environmentIds = new HashSet<string>(StringComparer.Ordinal);
            var scenarioIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var environment in environments)
            {
                if (environment == null || string.IsNullOrWhiteSpace(environment.id) ||
                    string.IsNullOrWhiteSpace(environment.name) || !environmentIds.Add(environment.id))
                    throw new ArgumentException("Release environments need unique IDs and display names.");
                if (environment.id != "gym" && environment.id != "mall" && environment.id != "football")
                    throw new ArgumentException("This release contains only gym, mall and football environments.");
                if (environment.scenarioIds == null || environment.scenarioIds.Length != CasesPerEnvironment)
                    throw new ArgumentException("Each release environment must contain exactly five cases.");
                foreach (var id in environment.scenarioIds)
                {
                    if (string.IsNullOrWhiteSpace(id) || !scenarioIds.Add(id))
                        throw new ArgumentException("Release case IDs must be non-empty and unique.");
                    var matches = library.scenarios.Where(s => s.id == id).ToArray();
                    if (matches.Length != 1 || matches[0].environment != environment.id || matches[0].availability != "AVAILABLE")
                        throw new ArgumentException("Release case is missing, unavailable or assigned to a different environment: " + id);
                }
            }
        }
    }

    [Serializable]
    public sealed class ReleaseEnvironment
    {
        public string id, name;
        public string[] scenarioIds = Array.Empty<string>();
    }
}
