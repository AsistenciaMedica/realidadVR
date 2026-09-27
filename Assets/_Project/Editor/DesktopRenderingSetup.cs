using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EmergencyVR.Editor
{
    /// <summary>
    /// Creates a desktop-only URP quality level (HDR, soft shadows, SSAO) next to the untouched Quest pipeline.
    /// The level is excluded from Android so Quest builds keep their variants and budget.
    /// </summary>
    public static class DesktopRenderingSetup
    {
        public const string QualityName = "Desktop";
        const string PipelinePath = "Assets/_Project/Settings/DesktopURP.asset";
        const string RendererPath = "Assets/_Project/Settings/DesktopRenderer.asset";
        const string QuestPipelinePath = "Assets/_Project/Settings/QuestURP.asset";
        const string QuestRendererPath = "Assets/_Project/Settings/QuestRenderer.asset";

        [MenuItem("Emergency VR/Art/Create desktop rendering quality")]
        public static void Ensure()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                if (!AssetDatabase.CopyAsset(QuestRendererPath, RendererPath)) throw new InvalidOperationException("Cannot copy the Quest renderer.");
                renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            }
            EnsureAmbientOcclusion(renderer);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                if (!AssetDatabase.CopyAsset(QuestPipelinePath, PipelinePath)) throw new InvalidOperationException("Cannot copy the Quest pipeline.");
                pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            }
            var settings = new SerializedObject(pipeline);
            settings.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            Set(settings, "m_SupportsHDR", true);
            Set(settings, "m_MSAA", 4);
            Set(settings, "m_MainLightShadowsSupported", true);
            Set(settings, "m_MainLightShadowmapResolution", 4096);
            Set(settings, "m_AdditionalLightsRenderingMode", 1);
            Set(settings, "m_AdditionalLightsPerObjectLimit", 8);
            Set(settings, "m_AdditionalLightShadowsSupported", true);
            Set(settings, "m_AdditionalLightsShadowmapResolution", 2048);
            Set(settings, "m_ShadowDistance", 22f);
            Set(settings, "m_ShadowCascadeCount", 2);
            Set(settings, "m_SoftShadowsSupported", true);
            Set(settings, "m_ColorGradingMode", 1);
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);

            EnsureQualityLevel(pipeline);
            AssetDatabase.SaveAssets();
            Debug.Log("VITAL_DESKTOP_RENDERING_READY");
        }

        static void EnsureAmbientOcclusion(UniversalRendererData renderer)
        {
            if (renderer.rendererFeatures.Any(x => x != null && x.GetType().Name == "ScreenSpaceAmbientOcclusion")) return;
            var type = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
            var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
            feature.name = "Ambient Occlusion";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
            var data = new SerializedObject(renderer);
            var features = data.FindProperty("m_RendererFeatures");
            var map = data.FindProperty("m_RendererFeatureMap");
            features.arraySize++; features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            data.ApplyModifiedPropertiesWithoutUndo();
            // Moderate, contact-scale occlusion: grounds equipment and the patient without darkening the room.
            var ao = new SerializedObject(feature);
            Set(ao, "m_Settings.Intensity", 1.4f);
            Set(ao, "m_Settings.Radius", .3f);
            Set(ao, "m_Settings.DirectLightingStrength", .2f);
            ao.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
        }

        static void EnsureQualityLevel(RenderPipelineAsset pipeline)
        {
            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = quality.FindProperty("m_QualitySettings");
            SerializedProperty level = null;
            for (int i = 0; i < levels.arraySize; i++)
                if (levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == QualityName) level = levels.GetArrayElementAtIndex(i);
            if (level == null)
            {
                int current = quality.FindProperty("m_CurrentQuality").intValue;
                levels.InsertArrayElementAtIndex(current);
                levels.MoveArrayElement(current + 1, levels.arraySize - 1);
                level = levels.GetArrayElementAtIndex(levels.arraySize - 1);
                level.FindPropertyRelative("name").stringValue = QualityName;
            }
            level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
            var excluded = level.FindPropertyRelative("excludedTargetPlatforms");
            excluded.ClearArray(); excluded.InsertArrayElementAtIndex(0); excluded.GetArrayElementAtIndex(0).stringValue = "Android";
            Set(level, "shadows", 2);
            Set(level, "shadowResolution", 3);
            Set(level, "antiAliasing", 4);
            Set(level, "anisotropicTextures", 2);
            Set(level, "realtimeReflectionProbes", true);
            quality.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Set(SerializedObject target, string path, object value)
        {
            var property = target.FindProperty(path);
            if (property == null) { Debug.LogWarning("Desktop rendering: missing setting " + path); return; }
            Assign(property, value);
        }
        static void Set(SerializedProperty parent, string path, object value)
        {
            var property = parent.FindPropertyRelative(path);
            if (property == null) { Debug.LogWarning("Desktop rendering: missing quality setting " + path); return; }
            Assign(property, value);
        }
        static void Assign(SerializedProperty property, object value)
        {
            switch (value)
            {
                case bool b: property.boolValue = b; break;
                case int i: property.intValue = i; break;
                case float f: property.floatValue = f; break;
            }
        }
    }
}
