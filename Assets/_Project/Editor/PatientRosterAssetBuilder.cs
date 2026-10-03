using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EmergencyVR.Patient.Presentation;
using UnityEditor;
using UnityEngine;

namespace EmergencyVR.Editor
{
    // The source FBX files are authoring assets. Only the compact appearance selected at runtime is loaded.
    public static class PatientRosterAssetBuilder
    {
        public const string SourceManifest = "Assets/ThirdParty/Rocketbox/Roster/sources.json";
        const string Output = "Assets/_Project/Resources/Visual/Appearances/";
        static readonly string[] Channels = { "AK_09_EyeBlinkLeft", "AK_10_EyeBlinkRight", "AK_25_JawOpen",
            "AU_04_BrowLowerer", "AU_01_InnerBrowRaiser", "AU_20_LipStretcher" };
        [Serializable] public sealed class Manifest { public string commit; public ModelSource[] models; }
        [Serializable] public sealed class ModelSource { public string id, upstreamModel, modelPath, textureDirectory; }

        [MenuItem("Emergency VR/Art/Build release patient roster")]
        public static void Build()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(SourceManifest));
            if (manifest?.models == null || manifest.models.Length != 15) throw new InvalidOperationException("Expected fifteen roster model sources.");
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            var target = Resources.Load<GameObject>("Visual/Patient")?.GetComponent<PatientRigAdapter>()?.face;
            if (target == null) throw new InvalidOperationException("The shared patient rig must exist before generating the roster.");
            foreach (var source in manifest.models)
            {
                // Preserve Daniel's authored mesh, materials and choreography; add source provenance only.
                if (source.id == "Daniel")
                {
                    var daniel = Resources.Load<PatientAppearance>("Visual/Appearances/Daniel");
                    if (daniel == null) throw new InvalidOperationException("Missing existing Daniel appearance.");
                    daniel.patientId = source.id; daniel.sourceModel = source.upstreamModel; daniel.sourceCommit = manifest.commit;
                    daniel.boneNames = target.bones.Select(b => b.name).ToArray();
                    EditorUtility.SetDirty(daniel);
                    continue;
                }
                BuildAppearance(source, manifest.commit, target);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("VITAL_PATIENT_ROSTER_BUILT 15 distinct model sources, one active patient renderer.");
        }

        static void BuildAppearance(ModelSource source, string commit, SkinnedMeshRenderer target)
        {
            // Correct assets first imported by the older, global 2K Rocketbox importer.
            foreach (var path in Directory.GetFiles(source.textureDirectory).Where(p => !p.EndsWith(".meta")))
            {
                var importer = AssetImporter.GetAtPath(path.Replace('\\', '/')) as TextureImporter;
                if (importer == null || importer.maxTextureSize == 1024) continue;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source.modelPath);
            if (prefab == null) throw new InvalidOperationException("Missing roster source " + source.modelPath);
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh != null).ToArray();
                var primary = renderers.OrderByDescending(r => r.sharedMesh.vertexCount).First();
                var boneIndices = target.bones.Select((bone, i) => new { bone.name, index = i }).ToDictionary(x => x.name, x => x.index);
                var bindposes = target.sharedMesh.bindposes.ToArray();
                var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var tangents = new List<Vector4>();
                var uv = new List<Vector2>(); var weights = new List<BoneWeight>();
                var groups = new List<List<int>>(); var materials = new List<Material>();
                var shapes = Channels.Select(_ => new List<Vector3>()).ToArray();
                var foundChannels = new bool[Channels.Length];
                var body = Material(source, "Body", "body_color", "body_normal", false);
                var head = Material(source, "Head", "head_color", "head_normal", false);
                Material hair = null;

                foreach (var renderer in renderers)
                {
                    var input = renderer.sharedMesh;
                    int start = vertices.Count;
                    var matrix = primary.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                    var sourceVertices = input.vertices; var sourceNormals = input.normals; var sourceTangents = input.tangents; var sourceUv = input.uv;
                    for (int i = 0; i < input.vertexCount; i++)
                    {
                        vertices.Add(matrix.MultiplyPoint3x4(sourceVertices[i]));
                        normals.Add(sourceNormals.Length == input.vertexCount ? matrix.MultiplyVector(sourceNormals[i]).normalized : Vector3.up);
                        var tangent = sourceTangents.Length == input.vertexCount ? sourceTangents[i] : new Vector4(1, 0, 0, 1);
                        var direction = matrix.MultiplyVector(new Vector3(tangent.x, tangent.y, tangent.z)).normalized;
                        tangents.Add(new Vector4(direction.x, direction.y, direction.z, tangent.w));
                        uv.Add(sourceUv.Length == input.vertexCount ? sourceUv[i] : Vector2.zero);
                    }
                    var remap = new int[renderer.bones.Length];
                    for (int i = 0; i < remap.Length; i++)
                    {
                        if (!boneIndices.TryGetValue(renderer.bones[i].name, out remap[i]))
                            throw new InvalidOperationException(source.id + " has an unmapped bone: " + renderer.bones[i].name);
                        bindposes[remap[i]] = input.bindposes[i] * matrix.inverse;
                    }
                    foreach (var weight in input.boneWeights)
                        weights.Add(new BoneWeight { boneIndex0 = remap[weight.boneIndex0], weight0 = weight.weight0,
                            boneIndex1 = remap[weight.boneIndex1], weight1 = weight.weight1,
                            boneIndex2 = remap[weight.boneIndex2], weight2 = weight.weight2,
                            boneIndex3 = remap[weight.boneIndex3], weight3 = weight.weight3 });
                    if (input.boneWeights.Length != input.vertexCount) throw new InvalidOperationException("Missing skin weights for " + source.id);

                    for (int submesh = 0; submesh < input.subMeshCount; submesh++)
                    {
                        string name = submesh < renderer.sharedMaterials.Length && renderer.sharedMaterials[submesh] != null
                            ? renderer.sharedMaterials[submesh].name.ToLowerInvariant() : "body";
                        bool alpha = name.Contains("opacity") || name.Contains("hair") || name.Contains("lash");
                        if (alpha && hair == null) hair = Material(source, "Hair", "opacity_color", null, true);
                        var material = alpha ? hair : name.Contains("head") ? head : body;
                        int group = materials.IndexOf(material);
                        if (group < 0) { group = materials.Count; materials.Add(material); groups.Add(new List<int>()); }
                        groups[group].AddRange(input.GetTriangles(submesh).Select(index => index + start));
                    }
                    for (int channel = 0; channel < Channels.Length; channel++)
                    {
                        var delta = new Vector3[input.vertexCount];
                        int shape = Enumerable.Range(0, input.blendShapeCount).Where(i => input.GetBlendShapeName(i).EndsWith(Channels[channel])).DefaultIfEmpty(-1).First();
                        if (shape >= 0)
                        {
                            input.GetBlendShapeFrameVertices(shape, input.GetBlendShapeFrameCount(shape) - 1, delta, null, null);
                            foundChannels[channel] = true;
                        }
                        shapes[channel].AddRange(delta.Select(matrix.MultiplyVector));
                    }
                }
                if (foundChannels.Any(found => !found)) throw new InvalidOperationException("Missing facial channels in " + source.id);
                var mesh = new Mesh { name = source.id + "Mesh" };
                if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTangents(tangents); mesh.SetUVs(0, uv);
                mesh.boneWeights = weights.ToArray(); mesh.bindposes = bindposes; mesh.subMeshCount = groups.Count;
                for (int i = 0; i < groups.Count; i++) mesh.SetTriangles(groups[i], i);
                for (int i = 0; i < Channels.Length; i++) mesh.AddBlendShapeFrame("blendShape1." + Channels[i], 100, shapes[i].ToArray(), null, null);
                var breath = new Vector3[vertices.Count]; var compression = new Vector3[vertices.Count];
                for (int i = 0; i < vertices.Count; i++)
                {
                    var p = primary.transform.TransformPoint(vertices[i]);
                    float front = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.015f, .10f, p.z));
                    float thorax = Mathf.Exp(-Mathf.Pow(p.x / .17f, 2) - Mathf.Pow((p.y - 1.28f) / .22f, 2)) * front;
                    float sternum = Mathf.Exp(-Mathf.Pow(p.x / .10f, 2) - Mathf.Pow((p.y - 1.30f) / .15f, 2)) * front;
                    breath[i] = primary.transform.InverseTransformVector(Vector3.forward * .011f * thorax);
                    compression[i] = primary.transform.InverseTransformVector(Vector3.back * .08f * sternum);
                }
                mesh.AddBlendShapeFrame("VitalBreath", 100, breath, null, null);
                mesh.AddBlendShapeFrame("VitalCompression", 100, compression, null, null);
                mesh.RecalculateBounds();
                var appearance = ScriptableObject.CreateInstance<PatientAppearance>();
                appearance.patientId = source.id; appearance.sourceModel = source.upstreamModel; appearance.sourceCommit = commit;
                appearance.mesh = Save(mesh, Output + source.id + "Mesh.asset");
                appearance.materials = materials.ToArray(); appearance.boneNames = target.bones.Select(b => b.name).ToArray();
                appearance = Save(appearance, Output + source.id + ".asset");
                Debug.Log("PATIENT_APPEARANCE " + source.id + " model=" + source.upstreamModel + " vertices=" + appearance.mesh.vertexCount + " materials=" + materials.Count);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        static Material Material(ModelSource source, string label, string colorSuffix, string normalSuffix, bool cutout)
        {
            string Find(string suffix) => Directory.GetFiles(source.textureDirectory).FirstOrDefault(p =>
                !p.EndsWith(".meta") && Path.GetFileNameWithoutExtension(p).EndsWith("_" + suffix))?.Replace('\\', '/');
            var color = Find(colorSuffix);
            if (color == null) throw new InvalidOperationException("Missing " + colorSuffix + " for " + source.id);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = source.id + label };
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(color));
            if (normalSuffix != null)
            {
                var normal = Find(normalSuffix);
                if (normal == null) throw new InvalidOperationException("Missing " + normalSuffix + " for " + source.id);
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
                material.EnableKeyword("_NORMALMAP"); material.SetFloat("_BumpScale", .6f);
            }
            material.SetFloat("_Smoothness", .25f);
            if (cutout)
            {
                material.SetFloat("_AlphaClip", 1); material.SetFloat("_Cutoff", .45f); material.EnableKeyword("_ALPHATEST_ON");
                material.SetFloat("_Cull", 0); material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            return Save(material, Output + source.id + label + ".mat");
        }

        static T Save<T>(T value, string path) where T : UnityEngine.Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, existing); UnityEngine.Object.DestroyImmediate(value); return existing;
        }
    }

    // Apply bounded import settings before first import, rather than allocating full 2K textures first.
    sealed class PatientRosterImportSettings : AssetPostprocessor
    {
        bool IsRoster => assetPath.StartsWith("Assets/ThirdParty/Rocketbox/Roster/", StringComparison.Ordinal);
        void OnPreprocessModel()
        {
            if (!IsRoster) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = false; importer.importBlendShapes = true;
            importer.isReadable = true; importer.optimizeGameObjects = false;
        }
        void OnPreprocessTexture()
        {
            if (!IsRoster) return;
            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 1024; importer.mipmapEnabled = true; importer.isReadable = false;
            importer.textureType = assetPath.Contains("_normal.") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.alphaIsTransparency = assetPath.Contains("_opacity_color.");
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
                name = "Android", overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.ASTC_6x6,
                textureCompression = TextureImporterCompression.Compressed, compressionQuality = 50 });
        }
    }
}
