using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EmergencyVR.Patient.Presentation;
using UnityEditor;
using UnityEngine;

namespace EmergencyVR.Editor
{
    /// <summary>
    /// Builds CASE 01 people from Microsoft Rocketbox (MIT): Daniel's clothed skin for the shared patient rig,
    /// the gym colleague and paramedic prefabs, and the motion-capture clip library.
    /// </summary>
    public static class CharacterAssetBuilder
    {
        const string Rocketbox = "Assets/ThirdParty/Rocketbox/";
        const string Characters = "Assets/_Project/Resources/Visual/Characters/";
        const string Appearances = "Assets/_Project/Resources/Visual/Appearances/";
        static readonly string[] FacialChannels =
            { "AK_09_EyeBlinkLeft", "AK_10_EyeBlinkRight", "AK_25_JawOpen", "AU_04_BrowLowerer", "AU_01_InnerBrowRaiser", "AU_20_LipStretcher" };

        [MenuItem("Emergency VR/Art/Build case characters")]
        public static void Build()
        {
            Directory.CreateDirectory(Characters); Directory.CreateDirectory(Appearances);
            AssetDatabase.Refresh();
            BuildDanielAppearance();
            BuildCharacter("Paramedic", "m154", "body_color_blue");
            BuildCharacter("GymStaff", "f013", "body_color");
            BuildAnimationLibrary();
            AssetDatabase.SaveAssets();
            Debug.Log("VITAL_CASE_CHARACTERS_BUILT");
        }

        static void BuildDanielAppearance()
        {
            var patient = Resources.Load<GameObject>("Visual/Patient");
            var target = patient == null ? null : patient.GetComponent<PatientRigAdapter>()?.face as SkinnedMeshRenderer;
            if (target == null) throw new InvalidOperationException("Build the realistic patient before Daniel's appearance.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Rocketbox + "Daniel/Daniel.fbx");
            if (source == null) throw new InvalidOperationException("Daniel.fbx is missing.");
            var instance = UnityEngine.Object.Instantiate(source);
            try
            {
                var renderer = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(r => r.sharedMesh.vertexCount).First();
                var original = renderer.sharedMesh;
                var mesh = UnityEngine.Object.Instantiate(original);
                mesh.name = "DanielMesh";
                mesh.ClearBlendShapes();
                var delta = new Vector3[mesh.vertexCount]; var normals = new Vector3[mesh.vertexCount]; var tangents = new Vector3[mesh.vertexCount];
                foreach (var channel in FacialChannels)
                {
                    string name = "blendShape1." + channel; int shape = original.GetBlendShapeIndex(name);
                    if (shape < 0) { Debug.LogWarning("Daniel lacks facial channel " + name); continue; }
                    for (int f = 0; f < original.GetBlendShapeFrameCount(shape); f++)
                    {
                        original.GetBlendShapeFrameVertices(shape, f, delta, normals, tangents);
                        mesh.AddBlendShapeFrame(name, original.GetBlendShapeFrameWeight(shape, f), delta, normals, tangents);
                    }
                }
                // Same breathing/compression surfaces as the default patient (authored in the model's rest pose).
                var vertices = mesh.vertices; var breath = new Vector3[vertices.Length]; var compress = new Vector3[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    var p = renderer.transform.TransformPoint(vertices[i]);
                    float front = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.015f, .10f, p.z));
                    float thorax = Mathf.Exp(-Mathf.Pow(p.x / .17f, 2) - Mathf.Pow((p.y - 1.28f) / .22f, 2)) * front;
                    float sternum = Mathf.Exp(-Mathf.Pow(p.x / .10f, 2) - Mathf.Pow((p.y - 1.30f) / .15f, 2)) * front;
                    breath[i] = renderer.transform.InverseTransformVector(Vector3.forward * .011f * thorax);
                    compress[i] = renderer.transform.InverseTransformVector(Vector3.back * .08f * sternum);
                }
                mesh.AddBlendShapeFrame("VitalBreath", 100, breath, null, null);
                mesh.AddBlendShapeFrame("VitalCompression", 100, compress, null, null);

                // Remap to the patient's bone order so the mesh drives the existing articulated skeleton.
                var targetIndex = new Dictionary<string, int>();
                for (int i = 0; i < target.bones.Length; i++) targetIndex[target.bones[i].name] = i;
                var remap = new int[renderer.bones.Length];
                for (int i = 0; i < remap.Length; i++)
                    if (!targetIndex.TryGetValue(renderer.bones[i].name, out remap[i]))
                        throw new InvalidOperationException("Daniel bone missing from patient skeleton: " + renderer.bones[i].name);
                var bindposes = target.sharedMesh.bindposes.ToArray();
                for (int i = 0; i < remap.Length; i++) bindposes[remap[i]] = original.bindposes[i];
                mesh.boneWeights = original.boneWeights.Select(w => new BoneWeight
                {
                    boneIndex0 = remap[w.boneIndex0], weight0 = w.weight0, boneIndex1 = remap[w.boneIndex1], weight1 = w.weight1,
                    boneIndex2 = remap[w.boneIndex2], weight2 = w.weight2, boneIndex3 = remap[w.boneIndex3], weight3 = w.weight3
                }).ToArray();
                mesh.bindposes = bindposes;
                mesh.RecalculateBounds();

                var appearance = ScriptableObject.CreateInstance<PatientAppearance>();
                appearance.mesh = Save(mesh, Appearances + "DanielMesh.asset");
                var body = Lit(Appearances + "DanielBody.mat", Rocketbox + "Daniel/Textures/m026_body_color.png", Rocketbox + "Daniel/Textures/m026_body_normal.png");
                var head = Lit(Appearances + "DanielHead.mat", Rocketbox + "Daniel/Textures/m026_head_color.png", Rocketbox + "Daniel/Textures/m026_head_normal.png");
                appearance.materials = renderer.sharedMaterials.Select(m => m != null && m.name.ToLowerInvariant().Contains("head") ? head : body).ToArray();
                Save(appearance, Appearances + "Daniel.asset");
                Debug.Log("DANIEL_APPEARANCE vertices=" + mesh.vertexCount + " materials=" + string.Join(",", renderer.sharedMaterials.Select(m => m == null ? "null" : m.name)));
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        static void BuildCharacter(string name, string prefix, string bodyColor)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Rocketbox + name + "/" + name + ".fbx");
            if (source == null) throw new InvalidOperationException(name + ".fbx is missing.");
            string textures = Rocketbox + name + "/Textures/" + prefix + "_";
            var body = Lit(Characters + name + "Body.mat", textures + bodyColor + ".png", textures + "body_normal.png");
            var head = Lit(Characters + name + "Head.mat", textures + "head_color.png", textures + "head_normal.png");
            var cutout = Cutout(Characters + name + "Hair.mat", textures + "opacity_color.png");
            var instance = UnityEngine.Object.Instantiate(source);
            try
            {
                instance.name = name;
                foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                    {
                        string n = m == null ? "" : m.name.ToLowerInvariant();
                        return n.Contains("opacity") || n.Contains("hair") || n.Contains("lash") ? cutout : n.Contains("head") ? head : body;
                    }).ToArray();
                    renderer.updateWhenOffscreen = true;
                }
                // Unity's fake-null objects defeat '??'; check explicitly.
                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = null; animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                PrefabUtility.SaveAsPrefabAsset(instance, Characters + name + ".prefab");
                Debug.Log("CHARACTER " + name + " materials=" + string.Join(",", source.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.sharedMaterials).Select(m => m == null ? "null" : m.name)));
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        static void BuildAnimationLibrary()
        {
            var entries = new List<CharacterAnimationLibrary.Entry>();
            foreach (var path in Directory.GetFiles(Rocketbox + "Animations", "*.fbx"))
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(path.Replace('\\', '/')).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip != null) entries.Add(new CharacterAnimationLibrary.Entry { name = Path.GetFileNameWithoutExtension(path), clip = clip });
            }
            var library = ScriptableObject.CreateInstance<CharacterAnimationLibrary>();
            library.clips = entries.ToArray();
            Save(library, Characters + "AnimationLibrary.asset");
            Debug.Log("ANIMATION_LIBRARY clips=" + entries.Count);
        }

        static Material Lit(string path, string color, string normal)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(color));
            var bump = AssetDatabase.LoadAssetAtPath<Texture2D>(normal);
            if (bump != null) { material.SetTexture("_BumpMap", bump); material.EnableKeyword("_NORMALMAP"); material.SetFloat("_BumpScale", .6f); }
            material.SetFloat("_Smoothness", .28f);
            return Save(material, path);
        }

        static Material Cutout(string path, string color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(color));
            material.SetFloat("_AlphaClip", 1); material.SetFloat("_Cutoff", .45f); material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", 0); material.SetFloat("_Smoothness", .2f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            return Save(material, path);
        }

        static T Save<T>(T value, string path) where T : UnityEngine.Object
        {
            var previous = AssetDatabase.LoadAssetAtPath<T>(path);
            if (previous == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, previous); UnityEngine.Object.DestroyImmediate(value);
            return previous;
        }
    }
}
