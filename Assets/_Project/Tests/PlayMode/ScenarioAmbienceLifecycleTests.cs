#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using EmergencyVR.Audio;
using EmergencyVR.Desktop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EmergencyVR.Tests
{
    [PrebuildSetup(SimulatedXRTestHooks.Setup), PostBuildCleanup(SimulatedXRTestHooks.Setup)]
    public sealed class ScenarioAmbienceLifecycleTests
    {
        [UnityTest]
        public IEnumerator DisableAndRemovalReleaseScheduledLoopVoicesAndWhistle()
        {
            var root = new GameObject("Ambience lifecycle fixture");
            try
            {
                var ambience = root.AddComponent<ScenarioAmbience>();
                ambience.SelectEnvironment("gym");
                var gym = root.GetComponentsInChildren<AudioSource>();
                Assert.That(gym, Has.Length.EqualTo(4), "Both sources of both crossfaded loops must exist.");
                foreach (var source in gym) source.PlayScheduled(AudioSettings.dspTime + .1);
                ambience.enabled = false;
                Assert.That(root.GetComponentsInChildren<AudioSource>(), Is.Empty, "OnDisable must stop every source immediately.");
                Assert.That(gym.All(source => !source.gameObject.activeInHierarchy && !source.isPlaying), Is.True);
                Assert.That(ambience.CurrentEnvironment, Is.Null);
                yield return null;
                Assert.That(gym.All(source => source == null), Is.True, "Deferred destruction must leave no inactive source objects.");
                Assert.That(root.GetComponentsInChildren<AudioSource>(true), Is.Empty);

                ambience.enabled = true;
                ambience.SelectEnvironment("football");
                var field = root.GetComponentsInChildren<AudioSource>();
                Assert.That(field, Has.Length.EqualTo(5), "Two loop pairs and the scheduled whistle must be covered.");
                foreach (var source in field) source.PlayScheduled(AudioSettings.dspTime + .1);
                Object.Destroy(ambience);
                yield return null;
                yield return null;
                Assert.That(field.All(source => source == null), Is.True, "Removing only the ambience component must also release its child sources.");
                Assert.That(root.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            }
            finally { Object.Destroy(root); }
        }
    }
}
#endif
