using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace EmergencyVR.Environment.Presentation
{
    /// <summary>Actual editor-baked data for a generated module. Applying it is explicit: an inactive
    /// cached environment must never replace the selected environment's lightmaps or probes.</summary>
    public sealed class BakedEnvironmentLighting : MonoBehaviour
    {
        [Serializable]
        public sealed class Surface
        {
            public Renderer renderer;
            public int lightmap;
            public Vector4 scaleOffset;
        }

        public string environmentId, sourceFingerprint, bakedUtc, requestedLightmapper, observedLightmapper;
        public Texture2D[] colorMaps = Array.Empty<Texture2D>();
        public Surface[] surfaces = Array.Empty<Surface>();
        public LightProbes probes;
        public Cubemap reflection;
        public Material skybox;
        public Color ambientSky, ambientEquator, ambientGround;
        public float[] ambientCoefficients = Array.Empty<float>();
        public bool outdoor;
        bool batched;

        public bool HasVerifiedData(out string reason)
        {
            if (colorMaps == null || colorMaps.Length < 1 || colorMaps.Length > 2)
            { reason = "Expected one or two baked lightmaps."; return false; }
            foreach (var map in colorMaps)
                if (map == null || map.width > 2048 || map.height > 2048)
                { reason = "Missing or oversized baked lightmap."; return false; }
            if (probes == null || probes.countSelf == 0 || reflection == null)
            { reason = "Baked light probes and reflection cubemap are required."; return false; }
            if (ambientCoefficients == null || ambientCoefficients.Length != 27)
            { reason = "Baked ambient spherical harmonics are missing."; return false; }
            if (outdoor && (skybox == null || !skybox.HasProperty("_MainTex") || skybox.GetTexture("_MainTex") == null))
            { reason = "Outdoor lighting requires an assigned HDRI panorama."; return false; }
            if (surfaces == null || surfaces.Length == 0)
            { reason = "No renderer/lightmap bindings were exported."; return false; }
            foreach (var surface in surfaces)
                if (surface == null || surface.renderer == null || surface.lightmap < 0 ||
                    surface.lightmap >= colorMaps.Length || surface.scaleOffset.x <= 0 || surface.scaleOffset.y <= 0)
                { reason = "Invalid renderer/lightmap binding."; return false; }
            reason = "";
            return true;
        }

        public void Apply()
        {
            if (!HasVerifiedData(out var reason)) throw new InvalidOperationException(environmentId + ": " + reason);
            var maps = new LightmapData[colorMaps.Length];
            for (int i = 0; i < maps.Length; i++) maps[i] = new LightmapData { lightmapColor = colorMaps[i] };
            LightmapSettings.lightmapsMode = LightmapsMode.NonDirectional;
            LightmapSettings.lightmaps = maps;
            LightmapSettings.lightProbes = probes;
            foreach (var surface in surfaces)
            {
                surface.renderer.lightmapIndex = surface.lightmap;
                if (!surface.renderer.isPartOfStaticBatch)
                    surface.renderer.lightmapScaleOffset = surface.scaleOffset;
            }
            if (!batched && Application.isPlaying)
            {
                StaticBatchingUtility.Combine(gameObject);
                // Static batching may bake atlas transforms into UV2. Preserve the resulting values
                // for this instance so selecting a cached environment never applies the transform twice.
                foreach (var surface in surfaces) surface.scaleOffset = surface.renderer.lightmapScaleOffset;
                batched = true;
            }
            RenderSettings.ambientMode = skybox == null ? AmbientMode.Trilight : AmbientMode.Skybox;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            var ambient = new SphericalHarmonicsL2();
            for (int channel = 0; channel < 3; channel++)
                for (int coefficient = 0; coefficient < 9; coefficient++)
                    ambient[channel, coefficient] = ambientCoefficients[channel * 9 + coefficient];
            RenderSettings.ambientProbe = ambient;
            RenderSettings.skybox = skybox;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = reflection;
            ApplyDistanceFog(outdoor);
        }

        public static void ApplyDistanceFog(bool outdoor)
        {
            RenderSettings.fog = outdoor;
            if (!outdoor) return;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.68f, .76f, .78f);
            RenderSettings.fogStartDistance = 18;
            RenderSettings.fogEndDistance = 70;
        }
    }
}
