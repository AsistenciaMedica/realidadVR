using System;
using UnityEngine;

namespace EmergencyVR.Scenarios
{
    [CreateAssetMenu(menuName="Emergency VR/Review case catalog")]
    public sealed class ReviewCaseCatalog : ScriptableObject
    {
        public ReviewCaseEntry[] entries = Array.Empty<ReviewCaseEntry>();
    }

    [Serializable]
    public sealed class ReviewCaseEntry
    {
        public ClinicalCaseDefinition definition;
        [TextArea] public string briefing;
        [TextArea] public string limitations;
        public string[] sourceUrls;
        public string reviewedOn;
        [NonSerialized] public EmergencyVR.Medical.MedicalScenarioDefinition medical;
    }
}
