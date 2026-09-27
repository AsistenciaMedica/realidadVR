using System.IO;
using UnityEditor;
using UnityEngine;

namespace EmergencyVR.Editor
{
    /// <summary>
    /// Saves the runtime environment/guide materials as assets under Resources so their shader keywords
    /// (normal maps, emission, unlit) survive player variant stripping. Runtime code loads them by name.
    /// </summary>
    public static class VisualMaterialsSetup
    {
        const string Folder = "Assets/_Project/Resources/Visual/Materials";
        const string Textures = "Assets/_Project/Resources/Visual/Textures/";

        [MenuItem("Emergency VR/Art/Create environment materials")]
        public static void Ensure()
        {
            Directory.CreateDirectory(Folder);
            // glTF files that failed before their texture modules existed are not retried by Unity on their own.
            AssetDatabase.ImportAsset("Assets/ThirdParty/GymModels/Resources/Gym", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
            Lit("GymFloor", "rubber_tiles", new Vector2(7, 8), new Color(.72f, .72f, .72f), .08f);
            Lit("GymWall", "plastered_wall", new Vector2(3, 1.5f), new Color(.93f, .94f, .93f), .06f);
            Unlit("ExteriorView", Texture("exterior_street_view.jpg"), Color.white);
            Unlit("GuideGlow", null, new Color(.35f, .92f, .80f));
            var lamp = Material("CeilingLamp", "Universal Render Pipeline/Lit");
            lamp.SetColor("_BaseColor", new Color(.95f, .96f, .95f));
            lamp.SetColor("_EmissionColor", new Color(1, .97f, .92f) * 2.2f);
            lamp.EnableKeyword("_EMISSION");
            lamp.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(lamp);
            AssetDatabase.SaveAssets();
            Debug.Log("VITAL_ENVIRONMENT_MATERIALS_READY");
        }

        static void Lit(string name, string map, Vector2 tiling, Color color, float smoothness)
        {
            var material = Material(name, "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            var albedo = Texture(map + "_color.jpg");
            var normal = Texture(map + "_normal.jpg");
            if (albedo != null) { material.SetTexture("_BaseMap", albedo); material.SetTextureScale("_BaseMap", tiling); }
            if (normal != null) { material.SetTexture("_BumpMap", normal); material.EnableKeyword("_NORMALMAP"); }
            EditorUtility.SetDirty(material);
        }

        static void Unlit(string name, Texture2D texture, Color color)
        {
            var material = Material(name, "Universal Render Pipeline/Unlit");
            material.SetColor("_BaseColor", color);
            if (texture != null) material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);
        }

        static Material Material(string name, string shader)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find(shader)) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Texture2D Texture(string file) => AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + file);
    }
}
