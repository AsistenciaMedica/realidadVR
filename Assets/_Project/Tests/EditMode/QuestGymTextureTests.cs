using System.IO;
using System.Linq;
using EmergencyVR.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace EmergencyVR.Tests
{
    public sealed class QuestGymTextureTests
    {
        [Test]
        public void ImportedGymMaterialsUseSharedCompressedImagesWithCorrectColorAndNormalChannels()
        {
            int boundTextures = 0;
            foreach (var source in Directory.GetFiles(QuestGymTextureSetup.ModelFolder, "*.glb"))
            {
                string path = source.Replace('\\', '/');
                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                Assert.That(assets.OfType<Texture2D>(), Is.Empty, path + " must not embed an uncompressed duplicate");
                foreach (var material in assets.OfType<Material>())
                    foreach (var property in material.GetTexturePropertyNames())
                    {
                        var texture = material.GetTexture(property) as Texture2D;
                        if (texture == null) continue;
                        boundTextures++;
                        var imagePath = AssetDatabase.GetAssetPath(texture);
                        Assert.That(imagePath, Does.StartWith(QuestGymTextureSetup.TextureFolder + "/"), path + " / " + property);
                        var importer = AssetImporter.GetAtPath(imagePath) as TextureImporter;
                        Assert.That(importer, Is.Not.Null, imagePath);
                        bool normal = Path.GetFileNameWithoutExtension(imagePath).EndsWith("-normal");
                        int maximum = normal ? 512 : 1024;
                        Assert.That(importer.textureShape, Is.EqualTo(TextureImporterShape.Texture2D), imagePath);
                        Assert.That(Mathf.Max(texture.width, texture.height), Is.LessThanOrEqualTo(maximum), imagePath);
                        Assert.That(GraphicsFormatUtility.IsCompressedFormat(texture.graphicsFormat), Is.True, imagePath + " must be compressed after actual import");
                        Assert.That(importer.mipmapEnabled, Is.True, imagePath);
                        Assert.That(importer.isReadable, Is.False, imagePath);
                        Assert.That(importer.maxTextureSize, Is.EqualTo(maximum), imagePath);
                        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Compressed), imagePath);
                        var android = importer.GetPlatformTextureSettings("Android");
                        Assert.That(android.overridden && android.maxTextureSize == maximum, Is.True, imagePath);
                        Assert.That(android.format, Is.EqualTo(TextureImporterFormat.ASTC_6x6), imagePath);
                        Assert.That(android.textureCompression, Is.EqualTo(TextureImporterCompression.Compressed), imagePath);
                        var standalone = importer.GetPlatformTextureSettings("Standalone");
                        Assert.That(standalone.overridden && standalone.maxTextureSize == maximum, Is.True, imagePath);
                        Assert.That(standalone.format, Is.EqualTo(TextureImporterFormat.Automatic), imagePath);
                        Assert.That(standalone.textureCompression, Is.EqualTo(TextureImporterCompression.Compressed), imagePath);
                        Assert.That(importer.textureType, Is.EqualTo(normal ? TextureImporterType.NormalMap : TextureImporterType.Default), imagePath);
                        Assert.That(importer.sRGBTexture, Is.EqualTo(Path.GetFileNameWithoutExtension(imagePath).EndsWith("-color")), imagePath);
                    }
            }
            Assert.That(boundTextures, Is.GreaterThan(30), "The real imported gym material bindings must be present.");
        }

        [Test]
        public void EnvironmentAndControllerTexturesReallyImportWithinCompressedBudget()
        {
            var paths = Directory.GetFiles(VisualMaterialsSetup.Textures).Where(p => !p.EndsWith(".meta"))
                .Append(VisualMaterialsSetup.ControllerOcclusionTexture);
            foreach (var source in paths)
            {
                string path = source.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(texture, Is.Not.Null, path + " must import as a 2D image");
                Assert.That(Mathf.Max(texture.width, texture.height), Is.LessThanOrEqualTo(1024), path);
                Assert.That(Mathf.IsPowerOfTwo(texture.width) && Mathf.IsPowerOfTwo(texture.height), Is.True, path);
                Assert.That(GraphicsFormatUtility.IsCompressedFormat(texture.graphicsFormat), Is.True, path + " must not silently fall back to RGB storage");
                Assert.That(importer.mipmapEnabled && !importer.isReadable, Is.True, path);
                var standalone = importer.GetPlatformTextureSettings("Standalone");
                var android = importer.GetPlatformTextureSettings("Android");
                Assert.That(standalone.overridden && standalone.maxTextureSize == 1024, Is.True, path);
                Assert.That(standalone.textureCompression, Is.EqualTo(TextureImporterCompression.Compressed), path);
                Assert.That(android.overridden && android.maxTextureSize == 1024, Is.True, path);
                bool hdr = path.EndsWith(".hdr");
                Assert.That(android.format, Is.EqualTo(hdr ? TextureImporterFormat.ASTC_HDR_6x6 : TextureImporterFormat.ASTC_6x6), path);
                if (hdr) Assert.That(importer.sRGBTexture, Is.False, "The sky remains linear HDR");
            }
        }
    }
}
