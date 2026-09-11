using UnityEngine;

namespace EmergencyVR.Scenarios
{
    [CreateAssetMenu(menuName = "Emergency VR/Scenario", fileName = "NewScenario")]
    public sealed class ScenarioDefinition : ScriptableObject
    {
        public string scenarioId = "training-room";
        public string displayName = "Training Room";
        [Tooltip("Full scene asset path, included in Build Profiles scene list.")]
        public string scenePath = "Assets/_Project/Scenes/Training/TrainingRoom.unity";
        public ClinicalCaseDefinition defaultCase;
    }
}
