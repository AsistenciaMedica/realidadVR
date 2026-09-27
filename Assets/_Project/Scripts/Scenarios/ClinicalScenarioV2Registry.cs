using System;
using UnityEngine;

namespace EmergencyVR.Scenarios
{
    [Serializable]
    public sealed class ClinicalScenarioV2Registration
    {
        public TextAsset definition;
        public ScriptableObject presentationAssets;
    }

    /// <summary>Build-reachable Unity references connecting clinical JSON and presentation resources.</summary>
    [CreateAssetMenu(menuName = "VITAL VR/Cases/Clinical scenario V2 registry")]
    public sealed class ClinicalScenarioV2Registry : ScriptableObject
    {
        public ClinicalScenarioV2Registration[] entries = Array.Empty<ClinicalScenarioV2Registration>();
    }
}
