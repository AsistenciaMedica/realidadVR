using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.Management;
using EmergencyVR.Desktop;

namespace EmergencyVR.Editor
{
    public static class Case01Build
    {
        public static void BuildDesktop()
        {
            var args=System.Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-case01-output");
            string output=index>=0&&index+1<args.Length?args[index+1]:"Builds/Case01/VITAL-VR.exe";
            bool validationBuild = args.Contains("-vital-validation-build");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            var settings=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            bool init=settings!=null&&settings.InitManagerOnStart;
            var loaders=settings?.Manager?.activeLoaders.ToList();
            try
            {
                if(settings!=null)
                {
                    settings.InitManagerOnStart=false;
                    // The native pre-init build hook ignores InitManagerOnStart. A Windows
                    // simulator has Input System devices, not a native OpenXR runtime.
                    if(settings.Manager!=null && !settings.Manager.TrySetLoaders(new List<XRLoader>()))
                        throw new InvalidOperationException("Cannot disable native XR loaders for the Windows simulator build.");
                    if(settings.Manager!=null)EditorUtility.SetDirty(settings.Manager);
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                }
                PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
                PlayerSettings.resizableWindow=true;
                // D3D12 crashes in the Windows capture player's native shutdown after
                // SubmitRenderRequest. D3D11 completes the same 15-case walkthrough cleanly.
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                    new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
                DesktopRenderingSetup.Ensure();
                VisualMaterialsSetup.Ensure();
                CharacterAssetBuilder.Build();
                PatientRosterAssetBuilder.Build();
                EmergencyVR.Editor.Environment.EnvironmentLightingBaker.EnsureBaked();
                var fingerprint = ValidationSourceFingerprint.Compute(Path.GetFullPath("."), validationBuild);
                Directory.CreateDirectory(Path.GetDirectoryName(ValidationSourceFingerprint.ResourceFile));
                string fingerprintJson = JsonUtility.ToJson(fingerprint, true);
                File.WriteAllText(ValidationSourceFingerprint.ResourceFile, fingerprintJson);
                AssetDatabase.ImportAsset(ValidationSourceFingerprint.ResourceFile, ImportAssetOptions.ForceSynchronousImport);
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes=new[]{DemoProjectBuilder.BootstrapPath,DemoProjectBuilder.TrainingPath}
                        .Concat(EmergencyVR.Editor.Environment.EnvironmentLightingBaker.BakeScenePaths).ToArray(),
                    locationPathName=output,target=BuildTarget.StandaloneWindows64,
                    extraScriptingDefines=new[]{"EMERGENCYVR_DESKTOP"},options=validationBuild ? BuildOptions.Development : BuildOptions.None
                });
                if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("CASE01 desktop build failed: "+report.summary.result);
                File.WriteAllText(output + ".fingerprint.json", fingerprintJson);
                Debug.Log("VITAL_BUILD_FINGERPRINT " + fingerprint.sourceSha256 + " development=" + validationBuild);
                Debug.Log("CASE01_DESKTOP_BUILT "+Path.GetFullPath(output));
            }
            finally
            {
                if(settings!=null)
                {
                    settings.InitManagerOnStart=init;
                    if(settings.Manager!=null && loaders!=null)settings.Manager.TrySetLoaders(loaders);
                    if(settings.Manager!=null)EditorUtility.SetDirty(settings.Manager);
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                }
            }
        }
    }
}
