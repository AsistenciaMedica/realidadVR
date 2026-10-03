using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEngine;

namespace EmergencyVR.Editor
{
    public static class Case01Build
    {
        public static void BuildDesktop()
        {
            var args=System.Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-case01-output");
            string output=index>=0&&index+1<args.Length?args[index+1]:"Builds/Case01/VITAL-VR.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            var settings=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            bool init=settings!=null&&settings.InitManagerOnStart;
            try
            {
                if(settings!=null)
                {
                    settings.InitManagerOnStart=false;
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                }
                PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
                DesktopRenderingSetup.Ensure();
                VisualMaterialsSetup.Ensure();
                CharacterAssetBuilder.Build();
                PatientRosterAssetBuilder.Build();
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes=new[]{DemoProjectBuilder.BootstrapPath,DemoProjectBuilder.TrainingPath},
                    locationPathName=output,target=BuildTarget.StandaloneWindows64,
                    extraScriptingDefines=new[]{"EMERGENCYVR_DESKTOP"},options=BuildOptions.None
                });
                if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("CASE01 desktop build failed: "+report.summary.result);
                Debug.Log("CASE01_DESKTOP_BUILT "+Path.GetFullPath(output));
            }
            finally
            {
                if(settings!=null)
                {
                    settings.InitManagerOnStart=init;
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                }
            }
        }
    }
}
