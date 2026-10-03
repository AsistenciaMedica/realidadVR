using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using EmergencyVR.Environment.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EmergencyVR.Editor.Environment
{
    /// <summary>Reproducible bake/export for procedural environments. This tool is intentionally
    /// explicit; a build hook must call EnsureBaked before it can claim baked environments.</summary>
    public static class EnvironmentLightingBaker
    {
        public const string Output = "Assets/_Project/Resources/BakedEnvironments";
        public const string Scenes = "Assets/_Project/Scenes/BakedEnvironments";
        public static readonly string[] EnvironmentIds = { "gym", "mall", "football" };
        public static string[] BakeScenePaths => EnvironmentIds.Select(id => Scenes + "/" + id + ".unity").ToArray();

        [MenuItem("Emergency VR/Art/Bake Quest environments")]
        public static void BakeAll() => EnsureBaked(true);

        public static void EnsureBaked(bool force = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Environment baking requires Edit Mode in the isolated lab project.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before running the environment baker.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Environment baking requires a graphics device; remove -nographics.");

            VisualMaterialsSetup.Ensure();
            QuestCharacterImportSetup.Ensure();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var previousPipeline = QualitySettings.renderPipeline;
            var quest = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/_Project/Settings/QuestURP.asset");
            if (quest == null) throw new InvalidOperationException("QuestURP asset is required before baking.");
            QualitySettings.renderPipeline = quest;
            string fingerprint = Fingerprint();
            try
            {
                foreach (var id in EnvironmentIds)
                {
                    var cached = AssetDatabase.LoadAssetAtPath<GameObject>(Output + "/" + id + "/Environment.prefab");
                    var lighting = cached == null ? null : cached.GetComponent<BakedEnvironmentLighting>();
                    if (!force && lighting != null && lighting.sourceFingerprint == fingerprint &&
                        lighting.HasVerifiedData(out _) && File.Exists(Scenes + "/" + id + ".unity")) continue;
                    Bake(id, fingerprint);
                }
                AssetDatabase.SaveAssets();
                Debug.Log("VITAL_ENVIRONMENTS_BAKED " + fingerprint);
            }
            finally
            {
                QualitySettings.renderPipeline = previousPipeline;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var saved = setup.Where(s => !string.IsNullOrEmpty(s.path)).ToArray();
                if (saved.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(saved);
            }
        }

        static void Bake(string id, string fingerprint)
        {
            string folder = Output + "/" + id;
            Directory.CreateDirectory(folder);
            Directory.CreateDirectory(Scenes);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = EnvironmentBakeSource.Create(id);
            var data = root.AddComponent<BakedEnvironmentLighting>();
            data.environmentId = id;
            data.sourceFingerprint = fingerprint;
            data.outdoor = id == "football";
            ConfigureAmbient(data);
            ConsolidateImportedProps(root);
            PrepareMeshesAndMaterials(root, folder, id);
            var settings = new LightingSettings {
                name = id + " Quest bake",
                bakedGI = true,
                realtimeGI = false,
                lightmapper = LightingSettings.Lightmapper.ProgressiveGPU,
                directionalityMode = LightmapsMode.NonDirectional,
                lightmapMaxSize = 2048,
                lightmapResolution = id == "football" ? 6 : id == "mall" ? 12 : 18,
                lightmapPadding = 4,
                lightmapCompression = LightmapCompression.HighQuality,
                directSampleCount = 32,
                indirectSampleCount = 128,
                environmentSampleCount = 64,
                minBounces = 2,
                maxBounces = 3,
                ao = true,
                aoMaxDistance = .3f,
                aoExponentDirect = .15f,
                aoExponentIndirect = .65f,
                lightProbeSampleCountMultiplier = 2,
                prioritizeView = false
            };
            settings = SaveReplacing(settings, folder + "/LightingSettings.asset");
            Lightmapping.lightingSettings = settings;
            AddBakeLights(root.transform, id);
            AddProbes(root.transform, id);
            var reflection = AddReflection(root.transform, id);
            string scenePath = Scenes + "/" + id + ".unity";
            if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new InvalidOperationException("Cannot save bake scene " + id);

            Debug.Log("VITAL_BAKE_START " + id + " backend=ProgressiveGPU");
            var backend = RunGpuBake(id);
            if (settings.lightmapper != LightingSettings.Lightmapper.ProgressiveGPU)
                throw new InvalidOperationException("The bake changed its requested GPU backend for " + id + "; inspect the editor log.");
            var maps = LightmapSettings.lightmaps;
            if (maps.Length < 1 || maps.Length > 2)
                throw new InvalidOperationException(id + " produced " + maps.Length + " lightmaps; budget is one or two. Adjust scaleInLightmap and rebake.");
            data.colorMaps = maps.Select(m => m.lightmapColor).ToArray();
            foreach (var map in data.colorMaps) ConfigureLightmap(map);
            var contributors = root.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, StaticEditorFlags.ContributeGI)).ToArray();
            var missing = contributors.Where(r => r.lightmapIndex < 0 || r.lightmapIndex >= maps.Length).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException(id + " has unbaked static surfaces: " + string.Join(", ", missing.Select(r => r.name).Take(8)));
            data.surfaces = contributors
                .Select(r => new BakedEnvironmentLighting.Surface { renderer = r, lightmap = r.lightmapIndex, scaleOffset = r.lightmapScaleOffset }).ToArray();
            if (LightmapSettings.lightProbes == null || LightmapSettings.lightProbes.count == 0)
                throw new InvalidOperationException(id + " did not produce baked Light Probes.");
            data.probes = Object.Instantiate(LightmapSettings.lightProbes);
            data.probes.name = id + " baked probes";
            data.probes = SaveReplacing(data.probes, folder + "/LightProbes.asset");
            data.ambientCoefficients = new float[27];
            var ambient = RenderSettings.ambientProbe;
            for (int channel = 0; channel < 3; channel++)
                for (int coefficient = 0; coefficient < 9; coefficient++)
                    data.ambientCoefficients[channel * 9 + coefficient] = ambient[channel, coefficient];
            string reflectionPath = folder + "/Reflection.exr";
            if (!Lightmapping.BakeReflectionProbe(reflection, reflectionPath))
                throw new InvalidOperationException("Reflection bake failed for " + id);
            AssetDatabase.ImportAsset(reflectionPath, ImportAssetOptions.ForceSynchronousImport);
            data.reflection = AssetDatabase.LoadAssetAtPath<Cubemap>(reflectionPath);
            reflection.mode = ReflectionProbeMode.Custom;
            reflection.customBakedTexture = data.reflection;
            data.bakedUtc = DateTime.UtcNow.ToString("O");
            data.requestedLightmapper = "ProgressiveGPU";
            data.observedLightmapper = backend.observed;
            if (!data.HasVerifiedData(out var reason)) throw new InvalidOperationException(id + ": " + reason);
            foreach (var light in root.GetComponentsInChildren<Light>(true)) light.enabled = false;
            if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new InvalidOperationException("Cannot save baked scene " + id);
            PrefabUtility.SaveAsPrefabAsset(root, folder + "/Environment.prefab");
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("TestResults/astra");
            File.WriteAllText("TestResults/astra/bake-" + id + ".json", JsonUtility.ToJson(new BakeEvidence {
                environment = id, fingerprint = fingerprint, requestedBackend = data.requestedLightmapper,
                observedBackend = data.observedLightmapper, backendMessages = backend.messages, bakedUtc = data.bakedUtc,
                lightmaps = data.colorMaps.Length, bindings = data.surfaces.Length, probes = data.probes.countSelf,
                reflectionResolution = data.reflection.width,
                maps = data.colorMaps.Select(m => AssetDatabase.GetAssetPath(m) + " " + m.width + "x" + m.height).ToArray()
            }, true));
            Debug.Log("VITAL_BAKE_COMPLETE " + id + " maps=" + data.colorMaps.Length + " surfaces=" + data.surfaces.Length + " probes=" + data.probes.countSelf);
        }

        sealed class BackendObservation { public string observed; public string[] messages; }

        static BackendObservation RunGpuBake(string id)
        {
            // The setting expresses intent only: Unity can retain it while falling back to CPU.
            // Capture messages from worker threads too, and always release the temporary callback.
            var messages = new ConcurrentQueue<string>();
            string nativeLog = Application.consoleLogPath;
            long logStart = string.IsNullOrEmpty(nativeLog) || !File.Exists(nativeLog) ? 0 : new FileInfo(nativeLog).Length;
            void Capture(string condition, string trace, LogType type)
            {
                string lower = condition.ToLowerInvariant();
                if (lower.StartsWith("vital_", StringComparison.Ordinal)) return;
                if (lower.Contains("lightmap") || lower.Contains("opencl") || lower.Contains("gpu") || lower.Contains("cpu"))
                    messages.Enqueue(condition);
            }
            bool completed;
            Application.logMessageReceivedThreaded += Capture;
            try { completed = Lightmapping.Bake(); }
            finally { Application.logMessageReceivedThreaded -= Capture; }
            // Native lightmapper diagnostics are not always forwarded to the managed event.
            // Audit only bytes appended by this bake, never an earlier bake's success/failure.
            if (!string.IsNullOrEmpty(nativeLog) && File.Exists(nativeLog))
            {
                using (var stream = new FileStream(nativeLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    stream.Seek(Math.Min(logStart, stream.Length), SeekOrigin.Begin);
                    using (var reader = new StreamReader(stream))
                        while (!reader.EndOfStream) Capture(reader.ReadLine(), "", LogType.Log);
                }
            }
            var captured = messages.ToArray();
            var fallback = captured.FirstOrDefault(IsCpuFallbackMessage);
            if (fallback != null)
                throw new InvalidOperationException(id + " rejected a CPU fallback during its requested GPU bake: " + fallback);
            if (!completed) throw new InvalidOperationException("Lighting bake failed for " + id + ". See the GPU lightmapper log.");
            bool observed = captured.Any(IsGpuExecutionMessage);
            return new BackendObservation {
                observed = observed ? "ProgressiveGPU (engine message)" : "Not exposed by Unity log events; inspect native Editor log",
                messages = captured.Distinct().Take(60).ToArray()
            };
        }

        public static bool IsCpuFallbackMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            string lower = message.ToLowerInvariant();
            if (!lower.Contains("cpu")) return false;
            return lower.Contains("falling back") || lower.Contains("fall back") || lower.Contains("fallback") ||
                Regex.IsMatch(lower, @"(using|switching to|switched to|starting|running|selected)\b.{0,80}\bcpu\b.{0,40}lightmap") ||
                Regex.IsMatch(lower, @"\bcpu\b.{0,40}lightmap.{0,80}(will be used|in use|enabled|selected|started)");
        }

        static bool IsGpuExecutionMessage(string message)
        {
            string lower = message.ToLowerInvariant();
            return lower.Contains("gpu") && lower.Contains("lightmap") &&
                (lower.Contains("using") || lower.Contains("baking") || lower.Contains("device") || lower.Contains("started")) &&
                !lower.Contains("failed") && !lower.Contains("unavailable") && !lower.Contains("unsupported");
        }

        static void PrepareMeshesAndMaterials(GameObject root, string folder, string id)
        {
            var meshes = new Dictionary<Mesh, Mesh>();
            var materials = new Dictionary<Material, Material>();
            int meshId = 0, materialId = 0;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue; // TextMesh and display-only signs stay unlit.
                if (!meshes.TryGetValue(filter.sharedMesh, out var mesh))
                {
                    mesh = Object.Instantiate(filter.sharedMesh);
                    mesh.name = id + " bake mesh " + meshId;
                    if (!Unwrapping.GenerateSecondaryUVSet(mesh)) throw new InvalidOperationException("Cannot unwrap " + filter.name);
                    mesh = SaveReplacing(mesh, folder + "/Mesh-" + meshId++ + ".asset");
                    meshes.Add(filter.sharedMesh, mesh);
                }
                filter.sharedMesh = mesh;
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null) continue;
                    if (!materials.TryGetValue(slots[i], out var material))
                    {
                        material = new Material(slots[i]) { enableInstancing = true };
                        material.globalIlluminationFlags = material.IsKeywordEnabled("_EMISSION")
                            ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.None;
                        material = SaveReplacing(material, folder + "/Material-" + materialId++ + ".mat");
                        materials.Add(slots[i], material);
                    }
                    slots[i] = material;
                }
                renderer.sharedMaterials = slots;
                bool receivesLighting = slots.Any(m => m != null && m.shader != null && !m.shader.name.Contains("Unlit"));
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic |
                    (receivesLighting ? StaticEditorFlags.ContributeGI : 0));
                renderer.receiveGI = ReceiveGI.Lightmaps;
                renderer.receiveShadows = true;
                // Display photographs outside windows are backdrops, not opaque walls
                // blocking the sun. Physical room/equipment geometry still casts both sides.
                renderer.shadowCastingMode = receivesLighting ? ShadowCastingMode.TwoSided : ShadowCastingMode.Off;
                renderer.scaleInLightmap = id == "football" && renderer.bounds.size.magnitude > 20 ? .35f : 1;
            }
        }

        static void ConsolidateImportedProps(GameObject root)
        {
            var props = root.transform.Find("Gym equipment (licensed models)");
            if (props == null) return;
            // Imported glTF can split a single elliptical into 151 meshes for only seven materials.
            // Combine within each prop, retaining separate culling between pieces of equipment.
            foreach (Transform prop in props)
            {
                var batches = new Dictionary<Material, List<CombineInstance>>();
                var sources = prop.GetComponentsInChildren<MeshRenderer>();
                long sourceIndices = 0;
                foreach (var renderer in sources)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null || !renderer.enabled) continue;
                    var mesh = filter.sharedMesh;
                    var materials = renderer.sharedMaterials;
                    if (materials.Length != mesh.subMeshCount)
                        throw new InvalidOperationException("Cannot consolidate mismatched submeshes in " + prop.name);
                    for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                    {
                        var material = materials[submesh];
                        if (material == null) throw new InvalidOperationException("Missing imported material in " + prop.name);
                        if (!batches.TryGetValue(material, out var batch)) batches[material] = batch = new List<CombineInstance>();
                        batch.Add(new CombineInstance { mesh = mesh, subMeshIndex = submesh,
                            transform = prop.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                        sourceIndices += mesh.GetIndexCount(submesh);
                    }
                }
                long combinedIndices = 0;
                foreach (var batch in batches)
                {
                    var mesh = new Mesh { name = prop.name + " / " + batch.Key.name, indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(batch.Value.ToArray(), true, true);
                    combinedIndices += mesh.GetIndexCount(0);
                    var combined = new GameObject("Bake geometry / " + batch.Key.name, typeof(MeshFilter), typeof(MeshRenderer));
                    combined.transform.SetParent(prop, false);
                    combined.GetComponent<MeshFilter>().sharedMesh = mesh;
                    combined.GetComponent<MeshRenderer>().sharedMaterial = batch.Key;
                }
                if (combinedIndices != sourceIndices) throw new InvalidOperationException("Consolidation lost triangles in " + prop.name);
                foreach (var renderer in sources)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null || !renderer.enabled) continue;
                    Object.DestroyImmediate(renderer);
                    Object.DestroyImmediate(filter);
                }
            }
        }

        static void AddBakeLights(Transform root, string id)
        {
            var sun = new GameObject("Baked key light").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.type = LightType.Directional;
            sun.lightmapBakeType = LightmapBakeType.Baked;
            sun.shadows = LightShadows.Soft;
            sun.intensity = id == "football" ? 1.1f : id == "gym" ? 1.5f : .25f;
            sun.color = new Color(1, .985f, .95f);
            sun.transform.rotation = Quaternion.Euler(id == "football" ? 48 : 36, id == "football" ? -38 : -100, 0);
            if (id == "football") return;
            foreach (float x in id == "gym" ? new[] { -1.6f, 1.6f } : new[] { 0f })
                foreach (float z in id == "gym" ? new[] { -1.4f, 1.2f, 3.6f } : new[] { -1.65f, .85f, 3.35f })
                {
                    var panel = new GameObject("Baked ceiling panel").AddComponent<Light>();
                    panel.transform.SetParent(root, false);
                    // Emit below the fixture, never inside its opaque lamp geometry.
                    // Mall lamp bottom is y=2.975; gym panels are recessed higher.
                    panel.transform.localPosition = new Vector3(x, id == "gym" ? 2.98f : 2.94f, z);
                    panel.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    panel.type = LightType.Rectangle;
                    panel.areaSize = id == "gym" ? new Vector2(1.10f, .50f) : new Vector2(1.18f, .36f);
                    panel.lightmapBakeType = LightmapBakeType.Baked;
                    panel.shadows = LightShadows.Soft;
                    panel.intensity = id == "gym" ? 10 : id == "mall" ? 16 : 8;
                    panel.color = new Color(1, .985f, .96f);
                    panel.range = 8;
                }
        }

        static void AddProbes(Transform root, string id)
        {
            var group = new GameObject("Baked character light probes").AddComponent<LightProbeGroup>();
            group.transform.SetParent(root, false);
            var points = new List<Vector3>();
            foreach (float x in new[] { -3f, -1.5f, 0f, 1.5f, 3f })
                foreach (float z in new[] { -2.5f, -.5f, 1.5f, 3f, 4.5f })
                    foreach (float y in new[] { .2f, 1f, 1.8f, 2.7f }) points.Add(new Vector3(x, y, z));
            if (id == "football")
                foreach (float x in new[] { -8f, 8f })
                    foreach (float z in new[] { -5f, 10f, 30f })
                        foreach (float y in new[] { .2f, 1.8f, 3f }) points.Add(new Vector3(x, y, z));
            group.probePositions = points.ToArray();
        }

        static ReflectionProbe AddReflection(Transform root, string id)
        {
            var probe = new GameObject("Baked environment reflection").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(root, false);
            probe.transform.localPosition = new Vector3(0, 1.5f, 1);
            probe.mode = ReflectionProbeMode.Baked;
            probe.resolution = 128;
            probe.hdr = true;
            probe.boxProjection = id != "football";
            probe.size = id == "football" ? new Vector3(36, 8, 52) : new Vector3(7, 3.2f, 8);
            probe.intensity = .85f;
            return probe;
        }

        static void ConfigureAmbient(BakedEnvironmentLighting data)
        {
            data.ambientSky = data.outdoor ? new Color(.63f, .73f, .82f) : new Color(.62f, .65f, .68f);
            data.ambientEquator = data.outdoor ? new Color(.36f, .44f, .48f) : new Color(.42f, .45f, .47f);
            data.ambientGround = data.outdoor ? new Color(.20f, .25f, .18f) : new Color(.28f, .30f, .31f);
            data.skybox = data.outdoor ? Resources.Load<Material>("Visual/Materials/FootballSky") : null;
            if (data.outdoor && (data.skybox == null || data.skybox.GetTexture("_MainTex") == null))
                throw new InvalidOperationException("Cannot bake outdoor lighting with a missing HDRI panorama.");
            RenderSettings.skybox = data.skybox;
            RenderSettings.ambientMode = data.skybox == null ? AmbientMode.Trilight : AmbientMode.Skybox;
            RenderSettings.ambientSkyColor = data.ambientSky;
            RenderSettings.ambientEquatorColor = data.ambientEquator;
            RenderSettings.ambientGroundColor = data.ambientGround;
            RenderSettings.ambientIntensity = 1;
            BakedEnvironmentLighting.ApplyDistanceFog(data.outdoor);
        }

        static void ConfigureLightmap(Texture2D texture)
        {
            if (texture == null || texture.width > 2048 || texture.height > 2048)
                throw new InvalidOperationException("Missing or oversized baked map.");
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Baked map has no persistent texture importer.");
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 2048;
            android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        static T SaveReplacing<T>(T value, string path) where T : Object
        {
            // Preserve GUIDs across rebakes so prefabs and saved bake scenes keep stable references.
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(value, path);
                return value;
            }
            else
            {
                EditorUtility.CopySerialized(value, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(value);
                return existing;
            }
        }

        public static string Fingerprint()
        {
            var paths = new List<string>();
            foreach (var directory in new[] { "Assets/_Project/Scripts/Environment", "Assets/_Project/Editor/Environment", "Assets/_Project/Resources/Visual/Materials", "Assets/_Project/Resources/Visual/Textures", "Assets/ThirdParty/GymModels/Resources/Gym" })
                if (Directory.Exists(directory)) paths.AddRange(Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)));
            using (var hash = SHA256.Create())
            {
                string inputs = string.Join("\n", paths.OrderBy(p => p, StringComparer.Ordinal).Select(p => p.Replace('\\', '/') + ":" + AssetDatabase.GetAssetDependencyHash(p.Replace('\\', '/'))));
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes("quest-bake-v1\n" + inputs))).Replace("-", "").ToLowerInvariant();
            }
        }

        [Serializable] sealed class BakeEvidence
        {
            public string environment, fingerprint, requestedBackend, observedBackend, bakedUtc;
            public string[] backendMessages;
            public int lightmaps, bindings, probes, reflectionResolution;
            public string[] maps;
        }
    }
}
