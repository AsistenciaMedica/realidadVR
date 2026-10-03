using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EmergencyVR.Editor
{
    /// <summary>One import policy for patient/civilian/paramedic texture budgets on Quest.</summary>
    public static class QuestCharacterImportSetup
    {
        const string Source = "Assets/ThirdParty/Rocketbox";
        // Reserve 2K for facial colour detail; normal maps use the shared 1K texture budget.
        public static int MaximumSize(string path) => path.Contains("_head_color.") ? 2048 : 1024;

        public static void Configure(TextureImporter importer, string path)
        {
            bool normal = path.Contains("_normal.");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.maxTextureSize = MaximumSize(path);
            importer.mipmapEnabled = true; importer.isReadable = false;
            importer.sRGBTexture = !normal;
            importer.alphaIsTransparency = path.Contains("_opacity_color.");
            importer.textureCompression = TextureImporterCompression.Compressed;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true; android.maxTextureSize = MaximumSize(path);
            android.format = TextureImporterFormat.ASTC_6x6;
            android.textureCompression = TextureImporterCompression.Compressed;
            android.compressionQuality = 50;
            importer.SetPlatformTextureSettings(android);
        }

        [MenuItem("Emergency VR/Art/Configure Quest character textures")]
        public static void Ensure()
        {
            var evidence = new List<TextureEvidence>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Source }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                var android = importer.GetPlatformTextureSettings("Android");
                int maximum = MaximumSize(path);
                bool normal = path.Contains("_normal.");
                bool changed = importer.maxTextureSize != maximum || !importer.mipmapEnabled || importer.isReadable ||
                    importer.sRGBTexture == normal || !android.overridden || android.maxTextureSize != maximum || android.format != TextureImporterFormat.ASTC_6x6;
                Configure(importer, path);
                if (changed) importer.SaveAndReimport();
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                int width = texture.width, height = texture.height;
                long bytes = 0;
                while (true)
                {
                    bytes += ((width + 5) / 6) * ((height + 5) / 6) * 16L;
                    if (width == 1 && height == 1) break;
                    width = Mathf.Max(1, width / 2); height = Mathf.Max(1, height / 2);
                }
                evidence.Add(new TextureEvidence { path = path, maximum = maximum, importedWidth = texture.width,
                    importedHeight = texture.height, estimatedAndroidBytesWithMips = bytes });
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project/Resources/Visual" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || !(name.EndsWith("Head") || name.EndsWith("Body") || name.EndsWith("Hair"))) continue;
                material.name = name;
                material.enableInstancing = true;
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", material.name.EndsWith("Head") ? .18f : .22f);
                if (material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap") != null) material.EnableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("TestResults/astra");
            File.WriteAllText("TestResults/astra/character-textures.json", JsonUtility.ToJson(new TextureReport {
                format = "ASTC 6x6, mipmaps; face <=2048, other textures <=1024", textures = evidence.ToArray(),
                totalEstimatedAndroidBytesWithMips = evidence.Sum(e => e.estimatedAndroidBytesWithMips)
            }, true));
            Debug.Log("VITAL_QUEST_CHARACTER_TEXTURES " + evidence.Count + " textures; estimateMiB=" + evidence.Sum(e => e.estimatedAndroidBytesWithMips) / 1048576d);
        }

        [Serializable] sealed class TextureEvidence { public string path; public int maximum, importedWidth, importedHeight; public long estimatedAndroidBytesWithMips; }
        [Serializable] sealed class TextureReport { public string format; public TextureEvidence[] textures; public long totalEstimatedAndroidBytesWithMips; }
    }

    // Execute after both existing Rocketbox authoring importers so regeneration preserves the release policy.
    public sealed class QuestCharacterTexturePostprocessor : AssetPostprocessor
    {
        public override int GetPostprocessOrder() => 1000;
        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith("Assets/ThirdParty/Rocketbox/", StringComparison.Ordinal))
                QuestCharacterImportSetup.Configure((TextureImporter)assetImporter, assetPath);
        }
    }
}
