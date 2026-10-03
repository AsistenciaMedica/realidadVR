using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace EmergencyVR.Editor
{
    /// <summary>External GLB images use native importers; GLB materials retain their
    /// original property bindings, UV transforms, packed data channels and normal scale.</summary>
    public static class QuestGymTextureSetup
    {
        public const string TextureFolder = "Assets/ThirdParty/GymModels/Textures";
        public const string ModelFolder = "Assets/ThirdParty/GymModels/Resources/Gym";

        public static void Configure(TextureImporter importer, string path)
        {
            bool normal = Path.GetFileNameWithoutExtension(path).EndsWith("-normal", StringComparison.Ordinal);
            bool color = Path.GetFileNameWithoutExtension(path).EndsWith("-color", StringComparison.Ordinal);
            int maximum = normal ? 512 : 1024;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            // glTFast's editor provider resolves external images as Texture2D.
            // A guid-only meta can inherit Cubemap defaults on the first import.
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = color;
            importer.maxTextureSize = maximum;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Compressed;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true; android.maxTextureSize = maximum;
            android.format = TextureImporterFormat.ASTC_6x6;
            android.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(android);
            var standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true; standalone.maxTextureSize = maximum;
            standalone.format = TextureImporterFormat.Automatic;
            standalone.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(standalone);
        }

        public static void Ensure()
        {
            if (!Directory.Exists(TextureFolder))
                throw new InvalidOperationException("Extract gym images with tools/Externalize-GymTextures.py before building.");
            AssetDatabase.ImportAsset(TextureFolder, ImportAssetOptions.ImportRecursive);
            foreach (var path in Directory.GetFiles(TextureFolder).Where(p => !p.EndsWith(".meta")))
            {
                var importer = AssetImporter.GetAtPath(path.Replace('\\', '/')) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Gym texture has no native importer: " + path);
                var before = EditorJsonUtility.ToJson(importer);
                Configure(importer, path);
                if (EditorJsonUtility.ToJson(importer) != before) importer.SaveAndReimport();
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(path.Replace('\\', '/')) == null)
                    throw new InvalidOperationException("Gym image did not import as Texture2D: " + path);
            }
        }

        public static void Validate()
        {
            foreach (var path in Directory.GetFiles(ModelFolder, "*.glb"))
            {
                string assetPath = path.Replace('\\', '/');
                var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                if (assets.OfType<Texture2D>().Any())
                    throw new InvalidOperationException("Embedded uncompressed gym texture remains: " + path);
                var bytes = File.ReadAllBytes(path);
                var source = JsonUtility.FromJson<ImageSource>(Encoding.UTF8.GetString(bytes, 20, BitConverter.ToInt32(bytes, 12)));
                if (source.images == null || source.images.Length == 0) continue;
                int bindings = assets.OfType<Material>().Sum(material => material.GetTexturePropertyNames().Count(property => material.GetTexture(property) != null));
                if (bindings < source.images.Length)
                    throw new InvalidOperationException("Gym images are missing from imported material bindings: " + assetPath +
                        "; expected at least " + source.images.Length + ", got " + bindings + ". Import report: " +
                        EditorJsonUtility.ToJson(AssetImporter.GetAtPath(assetPath), true));
            }
        }

        public static void ReimportAndValidate()
        {
            Ensure();
            foreach (var path in Directory.GetFiles(ModelFolder, "*.glb"))
                AssetDatabase.ImportAsset(path.Replace('\\', '/'), ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Validate();
            Debug.Log("VITAL_GYM_TEXTURE_BINDINGS_VERIFIED");
        }

        [Serializable] sealed class ImageSource { public ImageEntry[] images; }
        [Serializable] sealed class ImageEntry { public string uri; }
    }

    sealed class QuestGymTexturePostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(QuestGymTextureSetup.TextureFolder + "/", StringComparison.Ordinal))
                QuestGymTextureSetup.Configure((TextureImporter)assetImporter, assetPath);
        }
    }
}
