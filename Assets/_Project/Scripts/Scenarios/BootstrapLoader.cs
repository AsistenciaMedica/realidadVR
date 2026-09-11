using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EmergencyVR.Scenarios
{
    public sealed class BootstrapLoader : MonoBehaviour
    {
        [SerializeField] ScenarioDefinition scenario;
        public void Configure(ScenarioDefinition definition) { scenario = definition; }

        IEnumerator Start()
        {
            if (scenario == null || !Application.CanStreamedLevelBeLoaded(scenario.scenePath))
            {
                Debug.LogError("Training scene missing. Run Emergency VR > 2 - Generate demo and check Build Profiles.", this);
                yield break;
            }
            yield return SceneManager.LoadSceneAsync(scenario.scenePath, LoadSceneMode.Single);
        }
    }
}
