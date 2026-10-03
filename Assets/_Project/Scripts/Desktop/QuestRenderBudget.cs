using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering.Universal;

namespace EmergencyVR.Desktop
{
    // Windows/editor counters are conservative evidence, never an estimate of Quest FPS.
    public sealed class QuestRenderBudget : IDisposable
    {
        public const int MaxBatches = 100, MaxTriangles = 300000, MaxRealtimeLights = 1;
        public const long MaxTextureBytes = 250L * 1024 * 1024;
        ProfilerRecorder batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 64);
        ProfilerRecorder triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 64);

        [Serializable]
        public sealed class Sample
        {
            public string scenario, quality, pipeline, capturedUtc, sourceSha256, runId;
            public string validation = "Windows XR simulation; not Quest hardware performance";
            public bool countersAvailable, postProcessing, passed, player, developmentBuild, cameraUsesBackbuffer;
            public long peakBatches, peakTriangles, textureBytes;
            public long renderTargetBytes, assetTextureBytes;
            public int realtimeLights, sampleFrames, msaaSamples, renderWidth, renderHeight;
            public string[] failures;
            public TextureSample[] largestTextures;
        }

        [Serializable] public sealed class TextureSample
        {
            public string name, kind, format;
            public int width, height;
            public long bytes;
        }

        public Sample Read(Camera view, string scenario)
        {
            var textures = Resources.FindObjectsOfTypeAll<Texture>();
            var sample = new Sample {
                scenario = scenario,
                quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
                pipeline = QualitySettings.renderPipeline == null ? "default" : QualitySettings.renderPipeline.name,
                capturedUtc = DateTime.UtcNow.ToString("O"),
                player = !Application.isEditor, developmentBuild = Debug.isDebugBuild,
                cameraUsesBackbuffer = view.targetTexture == null && view.enabled,
                msaaSamples = view.allowMSAA && QualitySettings.renderPipeline is UniversalRenderPipelineAsset pipeline ? pipeline.msaaSampleCount : 1,
                renderWidth = view.pixelWidth, renderHeight = view.pixelHeight,
                countersAvailable = batches.Valid && triangles.Valid && batches.Count > 0 && triangles.Count > 0,
                sampleFrames = Math.Min(batches.Count, triangles.Count),
                postProcessing = view.GetUniversalAdditionalCameraData().renderPostProcessing,
                realtimeLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                    .Count(l => l.enabled && l.gameObject.activeInHierarchy && l.bakingOutput.lightmapBakeType != LightmapBakeType.Baked),
                // Includes all resident texture assets, not just visible materials.
                textureBytes = textures.Sum(t => Profiler.GetRuntimeMemorySizeLong(t)),
                renderTargetBytes = textures.OfType<RenderTexture>().Sum(t => Profiler.GetRuntimeMemorySizeLong(t)),
                assetTextureBytes = textures.Where(t => !(t is RenderTexture)).Sum(t => Profiler.GetRuntimeMemorySizeLong(t)),
                largestTextures = textures.Select(t => new TextureSample { name = t.name, kind = t.GetType().Name,
                    format = t.graphicsFormat.ToString(), width = t.width, height = t.height,
                    bytes = Profiler.GetRuntimeMemorySizeLong(t) }).OrderByDescending(t => t.bytes).Take(64).ToArray()
            };
            sample.peakBatches = Peak(batches);
            sample.peakTriangles = Peak(triangles);
            sample.failures = Failures(sample).ToArray();
            sample.passed = sample.failures.Length == 0;
            return sample;
        }

        static long Peak(ProfilerRecorder recorder)
        {
            if (!recorder.Valid || recorder.Count == 0) return -1;
            var samples = new List<ProfilerRecorderSample>();
            recorder.CopyTo(samples);
            return samples.Max(s => s.Value);
        }

        public static IEnumerable<string> Failures(Sample sample)
        {
            if (!sample.countersAvailable || sample.sampleFrames < 30 || sample.peakBatches <= 0 || sample.peakTriangles <= 0)
                yield return "At least 30 rendered frames with valid nonzero counters are required.";
            if (sample.peakBatches > MaxBatches) yield return "Batches exceed 100: " + sample.peakBatches;
            if (sample.peakTriangles > MaxTriangles) yield return "Triangles exceed 300000: " + sample.peakTriangles;
            if (sample.realtimeLights > MaxRealtimeLights) yield return "More than one realtime light: " + sample.realtimeLights;
            if (sample.textureBytes > MaxTextureBytes) yield return "Resident texture memory exceeds 250 MiB: " + sample.textureBytes;
            if (sample.textureBytes <= 0 || sample.assetTextureBytes <= 0 || sample.renderTargetBytes < 0 || sample.textureBytes != sample.assetTextureBytes + sample.renderTargetBytes)
                yield return "Resident texture accounting must contain valid asset and render-target totals.";
            if (sample.postProcessing) yield return "Post-processing must be disabled.";
            if (sample.pipeline != "QuestURP" && sample.pipeline != "QuestURP (runtime)") yield return "Quest rendering pipeline is required.";
            if (sample.quality != QuestLookSimulation.QualityName) yield return "Android-default Quest quality is required.";
            if (!sample.player || !sample.developmentBuild || !sample.cameraUsesBackbuffer) yield return "A Development player rendering to the normal backbuffer is required.";
            if (sample.msaaSamples != 4) yield return "MSAA4 must remain enabled.";
            if (sample.renderWidth != QuestLookSimulation.CaptureWidth || sample.renderHeight != QuestLookSimulation.CaptureHeight)
                yield return "The camera must render at 2064x2208.";
        }

        public void Dispose() { batches.Dispose(); triangles.Dispose(); }
    }
}
