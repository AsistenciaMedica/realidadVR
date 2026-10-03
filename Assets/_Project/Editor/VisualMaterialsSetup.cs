using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace EmergencyVR.Editor
{
    /// <summary>
    /// Saves the runtime environment/guide materials as assets under Resources so their shader keywords
    /// (normal maps, emission, unlit) survive player variant stripping. Runtime code loads them by name.
    /// </summary>
    public static class VisualMaterialsSetup
    {
        const string Folder = "Assets/_Project/Resources/Visual/Materials";
        public const string Textures = "Assets/_Project/Resources/Visual/Textures/";
        public const string ControllerOcclusionTexture = "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/Textures/DefaultMaterial_AO.png";

        [MenuItem("Emergency VR/Art/Create environment materials")]
        public static void Ensure()
        {
            Directory.CreateDirectory(Folder);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/_Project/Settings/QuestRenderer.asset");
            if (renderer == null) throw new System.InvalidOperationException("QuestRenderer is required before material setup.");
            // Match Android's default on Windows too; preserve four-sample MSAA and stencil UI.
            renderer.depthAttachmentFormat = DepthFormat.Depth_24_Stencil_8;
            renderer.intermediateTextureMode = IntermediateTextureMode.Auto;
            EditorUtility.SetDirty(renderer);
            // Import native images first, then each dependent GLB synchronously and reject missing bindings.
            QuestGymTextureSetup.ReimportAndValidate();
            Lit("GymFloor", "rubber_tiles", new Vector2(7, 8), new Color(.72f, .72f, .72f), .08f);
            Lit("GymWall", "plastered_wall", new Vector2(3, 1.5f), new Color(.93f, .94f, .93f), .06f);
            Lit("FootballGrass", "aerial_grass_rock", new Vector2(18, 26), new Color(.54f, .95f, .55f), .03f);
            Lit("MallFloor", "terrazzo_tiles", new Vector2(7, 8), Color.white, .55f);
            Lit("FootballStand", "concrete_floor_02", Vector2.one, new Color(.77f, .78f, .75f), .12f);
            ShopInterior();
            ConfigureQuestTextures();
            var controllerOcclusion = AssetImporter.GetAtPath(ControllerOcclusionTexture) as TextureImporter;
            if (controllerOcclusion == null) throw new System.InvalidOperationException("The XR controller occlusion texture is required.");
            string occlusionBefore = EditorJsonUtility.ToJson(controllerOcclusion);
            ConfigureControllerOcclusion(controllerOcclusion);
            if (EditorJsonUtility.ToJson(controllerOcclusion) != occlusionBefore) controllerOcclusion.SaveAndReimport();
            var sky = Material("FootballSky", "Skybox/Panoramic");
            var panorama = Texture("kloppenheim_05_1k.hdr");
            if (panorama == null) throw new System.InvalidOperationException("The football HDRI must import as a 2D panorama, not a cubemap.");
            sky.SetTexture("_MainTex", panorama);
            sky.SetFloat("_Exposure", 1);
            sky.SetFloat("_Rotation", 0);
            sky.SetColor("_Tint", new Color(.5f, .5f, .5f));
            EditorUtility.SetDirty(sky);
            Unlit("ExteriorView", Texture("exterior_street_view.jpg"), Color.white);
            Unlit("GuideGlow", null, new Color(.35f, .92f, .80f));
            var lamp = Material("CeilingLamp", "Universal Render Pipeline/Lit");
            lamp.SetColor("_BaseColor", new Color(.95f, .96f, .95f));
            lamp.SetColor("_EmissionColor", new Color(1, .97f, .92f) * 2.2f);
            lamp.EnableKeyword("_EMISSION");
            lamp.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
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

        // Original flat interior illustration; shelves, lamps and products are pixels,
        // not additional geometry or dynamic lights behind the shop windows.
        static void ShopInterior()
        {
            const int width = 512, height = 384;
            string path = Textures + "mall_shop_interior_color.png";
            if (!File.Exists(path))
            {
                var pixels = new Color32[width * height];
                void Rect(int x, int y, int w, int h, Color color)
                {
                    for (int py = Mathf.Max(0, y); py < Mathf.Min(height, y + h); py++)
                        for (int px = Mathf.Max(0, x); px < Mathf.Min(width, x + w); px++) pixels[py * width + px] = color;
                }
                Rect(0, 0, width, height, new Color(.31f, .28f, .24f));
                Rect(0, 75, width, 252, new Color(.55f, .48f, .37f));
                Rect(37, 90, 438, 231, new Color(.40f, .34f, .27f));
                Rect(0, 0, width, 74, new Color(.25f, .21f, .18f));
                for (int row = 0; row < 3; row++)
                {
                    int y = 115 + row * 65;
                    Rect(50, y - 7, 412, 8, new Color(.19f, .16f, .13f));
                    for (int column = 0; column < 8; column++)
                    {
                        int h = 25 + (column * 7 + row * 11) % 24;
                        Rect(61 + column * 51, y, 27, h, column % 3 == 0 ? new Color(.68f, .43f, .28f) :
                            column % 3 == 1 ? new Color(.33f, .48f, .43f) : new Color(.75f, .70f, .57f));
                    }
                }
                foreach (int x in new[] { 110, 256, 402 })
                {
                    Rect(x - 1, 339, 2, 45, new Color(.18f, .16f, .14f));
                    Rect(x - 29, 329, 58, 10, new Color(.9f, .81f, .61f));
                    Rect(x - 23, 325, 46, 4, new Color(1f, .92f, .73f));
                }
                Rect(178, 33, 156, 59, new Color(.37f, .27f, .19f));
                Rect(169, 89, 174, 8, new Color(.76f, .66f, .46f));
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.SetPixels32(pixels); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
            }
            var material = Material("MallShopInterior", "Universal Render Pipeline/Lit");
            var map = Texture("mall_shop_interior_color.png");
            material.SetTexture("_BaseMap", map);
            material.SetTexture("_EmissionMap", map);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_EmissionColor", Color.white * .65f);
            material.SetFloat("_Smoothness", .1f);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
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

        public static void ConfigureTexture(TextureImporter importer, string path)
        {
            bool normal = path.Contains("_normal");
            bool hdr = path.EndsWith(".hdr", System.StringComparison.OrdinalIgnoreCase);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal && !hdr;
            ConfigureCompression(importer, hdr);
        }

        // Keep the sample's authored AO channel and colour-space settings; only budget its storage.
        public static void ConfigureControllerOcclusion(TextureImporter importer) => ConfigureCompression(importer, false);

        static void ConfigureCompression(TextureImporter importer, bool hdr)
        {
            importer.textureShape = TextureImporterShape.Texture2D;
            // DXT cannot compress arbitrary NPOT dimensions. UVs preserve the image's
            // authored aspect ratio when its pixel grid is resampled to powers of two.
            importer.npotScale = TextureImporterNPOTScale.ToNearest;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Compressed;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 1024;
            android.format = hdr ? TextureImporterFormat.ASTC_HDR_6x6 : TextureImporterFormat.ASTC_6x6;
            android.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(android);
            var standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.maxTextureSize = 1024;
            standalone.format = hdr ? TextureImporterFormat.BC6H : TextureImporterFormat.Automatic;
            standalone.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(standalone);
        }

        static void ConfigureQuestTextures()
        {
            // Unity initially imports .hdr as a Cubemap, so a t:Texture2D query misses it.
            foreach (var source in Directory.GetFiles(Textures))
            {
                var path = source.Replace('\\', '/');
                if (path.EndsWith(".meta")) continue;
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                string before = EditorJsonUtility.ToJson(importer);
                ConfigureTexture(importer, path);
                if (EditorJsonUtility.ToJson(importer) != before) importer.SaveAndReimport();
            }
        }
    }
}
