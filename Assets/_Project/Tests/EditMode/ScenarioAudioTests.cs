using System.Linq;
using EmergencyVR.Audio;
using EmergencyVR.Medical.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace EmergencyVR.Tests
{
    public sealed class ScenarioAudioTests
    {
        [Test]
        public void EveryDeviceMessageHasASpanishRecordingAndIdleIsSilent()
        {
            var source = Resources.Load<TextAsset>("Audio/AEDVoices");
            Assert.That(source, Is.Not.Null);
            var manifest = JsonUtility.FromJson<AEDVoiceController.Manifest>(source.text);
            Assert.That(manifest.lines.Length, Is.EqualTo(11));
            Assert.That(manifest.lines.Select(l => l.display).Distinct().Count(), Is.EqualTo(11));
            Assert.That(manifest.lines.Any(l => l.display == "DEA\nABRIR / ENCENDER"), Is.False);
            foreach (var line in manifest.lines)
            {
                var clip = Resources.Load<AudioClip>(line.resource);
                Assert.That(clip, Is.Not.Null, line.resource);
                Assert.That(clip.length, Is.GreaterThan(.2f), line.resource);
                Assert.That(clip.channels, Is.EqualTo(1), line.resource);
                Assert.That(line.text, Is.Not.Empty);
            }
            Assert.That(manifest.lines.Any(l => l.display == "CONTACTO DETECTADO\nRETIRAR MANOS"), Is.True);
            Assert.That(manifest.lines.Any(l => l.display == "NO DESCARGAR\nREANUDAR RCP"), Is.True);
        }

        [Test]
        public void ChangingEnvironmentReleasesPreviousSourcesAndKeepsAmbienceQuietAndSpatial()
        {
            var root = new GameObject("Audio integration fixture");
            try
            {
                var ambience = root.AddComponent<ScenarioAmbience>();
                foreach (var id in new[] { "gym", "mall", "football" })
                {
                    ambience.SelectEnvironment(id);
                    var sources = root.GetComponentsInChildren<AudioSource>();
                    Assert.That(sources.Length, Is.EqualTo(id == "football" ? 5 : 4), id);
                    foreach (var source in sources)
                    {
                        Assert.That(source.clip, Is.Not.Null, source.name);
                        Assert.That(source.clip.channels, Is.EqualTo(1), source.name);
                        Assert.That(source.clip.frequency, Is.LessThanOrEqualTo(24000), source.name);
                        Assert.That(source.spatialBlend, Is.EqualTo(1));
                        Assert.That(source.volume, Is.InRange(.001f, .065f));
                        Assert.That(source.dopplerLevel, Is.Zero);
                        Assert.That(source.ignoreListenerPause, Is.False);
                        Assert.That(source.priority, Is.GreaterThanOrEqualTo(180));
                    }
                }
                ambience.SelectEnvironment(null);
                Assert.That(root.GetComponentsInChildren<AudioSource>(), Is.Empty);
                // Native OnDisable and deferred Destroy belong to PlayMode; exercised by
                // ScenarioAmbienceLifecycleTests with both voices in each crossfaded loop.
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
