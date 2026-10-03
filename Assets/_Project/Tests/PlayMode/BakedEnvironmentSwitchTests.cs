using System.Collections;
using EmergencyVR.Environment;
using EmergencyVR.Scenarios;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EmergencyVR.Tests
{
    public sealed class BakedEnvironmentSwitchTests
    {
        [UnityTest]
        public IEnumerator CachedBakesKeepOwnBindingsAndRestoreTheTechnicalRoom()
        {
            if (Resources.Load<GameObject>("BakedEnvironments/gym/Environment") == null)
                Assert.Ignore("Requires actual generated lightmaps; never fabricates a baked test fixture.");
            yield return SceneManager.LoadSceneAsync("TrainingRoom"); yield return null; yield return null;
            var review = Object.FindFirstObjectByType<ReviewCaseSession>();
            var presenter = Object.FindFirstObjectByType<ScenarioEnvironmentPresenter>();
            if (presenter == null)
            {
                review.Select(System.Array.FindIndex(review.Catalog.entries, e => e.medical?.environment == "gym"));
                presenter = Object.FindFirstObjectByType<ScenarioEnvironmentPresenter>();
            }
            presenter.Select(null);
            var maps = LightmapSettings.lightmaps;
            var probes = LightmapSettings.lightProbes;
            var sky = RenderSettings.skybox;
            bool fog = RenderSettings.fog;
            var reflection = RenderSettings.customReflectionTexture;
            foreach (string id in new[] { "gym", "football", "mall", "gym" })
            {
                presenter.Select(id); yield return null;
                var data = presenter.ActiveBakedLighting;
                Assert.That(data, Is.Not.Null);
                Assert.That(LightmapSettings.lightProbes, Is.SameAs(data.probes));
                Assert.That(RenderSettings.customReflectionTexture, Is.SameAs(data.reflection));
                Assert.That(RenderSettings.fog, Is.EqualTo(id == "football"));
                foreach (var surface in data.surfaces)
                {
                    Assert.That(surface.renderer.lightmapIndex, Is.EqualTo(surface.lightmap));
                    Assert.That(surface.renderer.lightmapScaleOffset, Is.EqualTo(surface.scaleOffset));
                    Assert.That(LightmapSettings.lightmaps[surface.lightmap].lightmapColor, Is.SameAs(data.colorMaps[surface.lightmap]));
                }
            }
            presenter.Select(null);
            Assert.That(LightmapSettings.lightmaps.Length, Is.EqualTo(maps.Length));
            for (int i = 0; i < maps.Length; i++) Assert.That(LightmapSettings.lightmaps[i].lightmapColor, Is.SameAs(maps[i].lightmapColor));
            Assert.That(LightmapSettings.lightProbes, Is.SameAs(probes));
            Assert.That(RenderSettings.skybox, Is.SameAs(sky));
            Assert.That(RenderSettings.customReflectionTexture, Is.SameAs(reflection));
            Assert.That(RenderSettings.fog, Is.EqualTo(fog));
        }
    }
}
