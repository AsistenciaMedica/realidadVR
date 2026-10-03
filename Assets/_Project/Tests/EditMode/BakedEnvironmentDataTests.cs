using System;
using System.Linq;
using EmergencyVR.Environment.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace EmergencyVR.Tests
{
    public sealed class BakedEnvironmentDataTests
    {
        [TestCase("Falling back to Progressive CPU Lightmapper", true)]
        [TestCase("GPU device unavailable. The bake will fall back to CPU.", true)]
        [TestCase("Using the Progressive CPU Lightmapper", true)]
        [TestCase("Progressive CPU Lightmapper will be used", true)]
        [TestCase("Using Progressive GPU Lightmapper device NVIDIA", false)]
        [TestCase("CPU threads: 12; GPU Lightmapper requested", false)]
        public void CpuFallbackDiagnosticsCannotBeMisreportedAsGpu(string message, bool fallback)
        {
            Assert.That(EmergencyVR.Editor.Environment.EnvironmentLightingBaker.IsCpuFallbackMessage(message), Is.EqualTo(fallback));
        }

        [Test]
        public void MissingBakeCannotModifyGlobalLighting()
        {
            var root = new GameObject("Invalid bake test");
            var before = LightmapSettings.lightmaps;
            var probes = LightmapSettings.lightProbes;
            var sky = RenderSettings.skybox;
            try
            {
                var data = root.AddComponent<BakedEnvironmentLighting>();
                Assert.That(data.HasVerifiedData(out var reason), Is.False);
                Assert.That(reason, Does.Contain("lightmaps"));
                Assert.Throws<InvalidOperationException>(() => data.Apply());
                Assert.That(LightmapSettings.lightmaps.Length, Is.EqualTo(before.Length));
                Assert.That(LightmapSettings.lightProbes, Is.SameAs(probes));
                Assert.That(RenderSettings.skybox, Is.SameAs(sky));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void GeneratedReleasePrefabsHaveRealMapsProbesAndRendererBindings()
        {
            var prefabs = new[] { "gym", "mall", "football" }.Select(id => Resources.Load<GameObject>("BakedEnvironments/" + id + "/Environment")).ToArray();
            if (prefabs.All(p => p == null)) Assert.Ignore("Run EnvironmentLightingBaker once; this test validates its real artifacts without invoking a GPU bake.");
            foreach (var prefab in prefabs)
            {
                Assert.That(prefab, Is.Not.Null, "A release must contain all three baked environments.");
                var data = prefab.GetComponent<BakedEnvironmentLighting>();
                Assert.That(data, Is.Not.Null);
                Assert.That(data.HasVerifiedData(out var reason), Is.True, reason);
                Assert.That(data.sourceFingerprint.Length, Is.EqualTo(64));
                Assert.That(data.bakedUtc, Is.Not.Empty);
                Assert.That(data.requestedLightmapper, Is.EqualTo("ProgressiveGPU"));
                Assert.That(data.observedLightmapper, Is.Not.Empty);
                Assert.That(data.probes.bakedProbes.Length, Is.GreaterThan(0));
                Assert.That(data.probes.cellCountSelf, Is.GreaterThan(0), "Saved probes must retain their interpolation tetrahedra.");
                var previousProbes = LightmapSettings.lightProbes;
                try
                {
                    LightmapSettings.lightProbes = data.probes;
                    LightProbes.GetInterpolatedProbe(new Vector3(0, 1.2f, 1), null, out var lighting);
                    float energy = 0;
                    for (int channel = 0; channel < 3; channel++)
                        for (int coefficient = 0; coefficient < 9; coefficient++)
                        {
                            float value = lighting[channel, coefficient];
                            Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False);
                            energy += Mathf.Abs(value);
                        }
                    Assert.That(energy, Is.GreaterThan(.001f), "The saved probes must actually illuminate a character in the patient area.");
                }
                finally { LightmapSettings.lightProbes = previousProbes; }
                Assert.That(data.surfaces.Select(s => s.renderer).Distinct().Count(), Is.EqualTo(data.surfaces.Length));
                foreach (var surface in data.surfaces)
                    Assert.That(surface.renderer.transform.IsChildOf(prefab.transform), Is.True, "Bindings must refer to this prefab's own static geometry.");
                if (data.outdoor)
                {
                    Assert.That(data.skybox, Is.Not.Null);
                    Assert.That(data.skybox.GetTexture("_MainTex"), Is.TypeOf<Texture2D>(), "Panoramic shader cannot use a missing/Cubemap source.");
                    Assert.That(data.skybox.GetTexture("_MainTex").width, Is.LessThanOrEqualTo(1024));
                }
            }
        }
    }
}
