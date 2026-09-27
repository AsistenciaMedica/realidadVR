using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace EmergencyVR.Editor
{
    public static class QuestProjectSetup
    {
        const string Loader = "UnityEngine.XR.OpenXR.OpenXRLoader";
        const string MetaFeature = "com.unity.openxr.feature.metaquest";
        const string TouchFeature = "com.unity.openxr.feature.input.oculustouch";

        [MenuItem("Emergency VR/3 - Configure Android OpenXR")]
        public static void ConfigureAndroid()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            DemoProjectBuilder.EnsureFolders();
            PlayerSettings.companyName = "EmergencyVR";
            PlayerSettings.productName = "VITAL VR";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.emergencyvr.trainingdemo");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EditorSettings.serializationMode = SerializationMode.ForceText;

            // Public PlayerSettings has no setter for Active Input Handling in this version.
            // Validate the serialized key instead of silently relying on an unverified field.
            var settingsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settingsAssets.Length == 0) throw new InvalidOperationException("PlayerSettings asset is not available.");
            var settings = new SerializedObject(settingsAssets[0]);
            var input = settings.FindProperty("activeInputHandler");
            if (input == null) throw new InvalidOperationException("Set Player > Other Settings > Active Input Handling to Input System Package (New), restart Editor, then retry.");
            var changedInputBackend = input.intValue != 1;
            input.intValue = 1;
            settings.ApplyModifiedPropertiesWithoutUndo();

            XRGeneralSettingsPerBuildTarget targets;
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out targets) || targets == null)
            {
                const string path = DemoProjectBuilder.Root + "/Settings/XRGeneralSettings.asset";
                targets = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
                if (targets == null)
                {
                    targets = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(targets, path);
                }
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, targets, true);
            }
            if (!targets.HasSettingsForBuildTarget(BuildTargetGroup.Android))
                targets.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!targets.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                targets.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            var general = targets.SettingsForBuildTarget(BuildTargetGroup.Android);
            general.InitManagerOnStart = true;
            if (!XRPackageMetadataStore.AssignLoader(general.Manager, Loader, BuildTargetGroup.Android))
                throw new InvalidOperationException("OpenXR loader assignment failed. Check XR Plug-in Management > Android.");
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            EnableFeature(MetaFeature);
            EnableFeature(TouchFeature);
            var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (openxr == null) throw new InvalidOperationException("Android OpenXR settings missing.");
            openxr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            EditorUtility.SetDirty(openxr);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(general.Manager);
            EditorUtility.SetDirty(targets);
            AssetDatabase.SaveAssets();
            Debug.Log("Android configured: IL2CPP, ARM64, Vulkan, Linear, OpenXR, Meta Quest Support, Touch profile. " +
                "Run XR Plug-in Management > Project Validation > Android before building.");
            if (changedInputBackend)
                Debug.LogWarning("Active Input Handling changed. Save, close and reopen Unity before Play Mode or building.");
        }

        static void EnableFeature(string id)
        {
            var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, id);
            if (feature == null) throw new InvalidOperationException("OpenXR feature unavailable: " + id);
            feature.enabled = true;
            EditorUtility.SetDirty(feature);
        }

        [MenuItem("Emergency VR/4 - Validate project setup")]
        public static void ValidateMenu()
        {
            var issues = FindIssues();
            if (issues.Count != 0) throw new InvalidOperationException(string.Join("\n", issues));
            Debug.Log("Project configuration checks passed. This does not validate runtime XR, package Project Validation rules or headset performance.");
        }

        public static List<string> FindIssues()
        {
            var issues = new List<string>();
            if (Application.unityVersion != "6000.3.23f1") issues.Add("Expected Editor 6000.3.23f1. Review and update version baseline before using another Editor.");
            if (!File.Exists(DemoProjectBuilder.BootstrapPath) || !File.Exists(DemoProjectBuilder.TrainingPath))
                issues.Add("Generate Bootstrap and TrainingRoom with Emergency VR > 2 - Generate demo.");
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length < 2 || scenes[0] != DemoProjectBuilder.BootstrapPath || scenes[1] != DemoProjectBuilder.TrainingPath)
                issues.Add("Build scene list must start with Bootstrap then TrainingRoom.");
            if (GraphicsSettings.defaultRenderPipeline == null) issues.Add("URP asset is missing.");
            if (PlayerSettings.colorSpace != ColorSpace.Linear) issues.Add("Color Space must be Linear.");
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
                issues.Add("Android scripting backend must be IL2CPP.");
            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64) issues.Add("Android architecture must be ARM64 only.");
            var graphics = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android) || graphics.Length != 1 || graphics[0] != GraphicsDeviceType.Vulkan)
                issues.Add("Set Android graphics API explicitly to Vulkan.");
            var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (general == null || !general.InitManagerOnStart || general.Manager == null ||
                !general.Manager.activeLoaders.Any(l => l != null && l.GetType().FullName == Loader))
                issues.Add("OpenXR loader must be enabled and initialized on startup for Android.");
            foreach (var id in new[] { MetaFeature, TouchFeature })
            {
                var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, id);
                if (feature == null || !feature.enabled) issues.Add("Enable OpenXR feature: " + id);
            }
            var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (openxr == null || openxr.renderMode != OpenXRSettings.RenderMode.SinglePassInstanced)
                issues.Add("Android OpenXR Render Mode must be Single Pass Instanced.");
            var playerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (playerAssets.Length > 0)
            {
                var input = new SerializedObject(playerAssets[0]).FindProperty("activeInputHandler");
                if (input == null || input.intValue != 1) issues.Add("Active Input Handling must be Input System Package (New); restart after changing it.");
            }
            else issues.Add("PlayerSettings asset unavailable; input backend could not be validated.");
            return issues;
        }

        [MenuItem("Emergency VR/5 - Build development APK")]
        public static void BuildDevelopmentApk()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("File > Build Profiles > Android > Switch Platform first; wait for compilation.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Install Android Build Support, SDK & NDK Tools and OpenJDK in Unity Hub.");
            ValidateMenu();
            Directory.CreateDirectory("Builds/Android");
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { DemoProjectBuilder.BootstrapPath, DemoProjectBuilder.TrainingPath },
                locationPathName = "Builds/Android/EmergencyVR-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".apk",
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("APK build failed: " + report.summary.result);
            Debug.Log("APK built: " + report.summary.outputPath + ". Install and test on Quest 3; build success alone is not device validation.");
        }
    }
}
