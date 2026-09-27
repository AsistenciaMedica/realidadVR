using System;
using System.IO;
using EmergencyVR.Desktop;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.Management;

namespace EmergencyVR.Editor
{
    public static class DemoDistributionBuilder
    {
        [MenuItem("Emergency VR/Demo/Play with keyboard and mouse")]
        public static void PreviewDesktop()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ReviewCaseCatalogBuilder.Generate();
            SessionState.SetBool(DesktopDemoController.PreviewKey,true);
            EditorSceneManager.OpenScene(DemoProjectBuilder.BootstrapPath);
            EditorApplication.isPlaying=true;
        }

        [InitializeOnLoadMethod]
        static void ResetPreviewAfterPlay()
        {
            EditorApplication.playModeStateChanged-=PlayChanged;
            EditorApplication.playModeStateChanged+=PlayChanged;
        }
        static void PlayChanged(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredEditMode) SessionState.SetBool(DesktopDemoController.PreviewKey,false);
        }

        [MenuItem("Emergency VR/Demo/Build Windows client demo")]
        public static void BuildWindows()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.StandaloneWindows64)
                throw new InvalidOperationException("Switch to Windows first, or invoke Unity with -buildTarget Win64. Android settings are preserved.");
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Windows Build Support is unavailable.");
            ReviewCaseCatalogBuilder.Generate();
            var general=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            var medical=EmergencyVR.Medical.MedicalLibraryLoader.Load();
            File.Copy("Assets/_Project/Resources/MedicalScenarios.json","demo/public/scenarios.json",true);
            bool previousInit=general!=null && general.InitManagerOnStart;
            var oldFullscreen=PlayerSettings.fullScreenMode;
            var oldPreloaded=PlayerSettings.GetPreloadedAssets();
            var projectSettings=File.ReadAllBytes("ProjectSettings/ProjectSettings.asset");
            var directory="Builds/Windows/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(directory);
            try
            {
                // Only the distributed desktop player skips XR startup. Restore the Editor configuration afterward.
                if(general!=null) { general.InitManagerOnStart=false; EditorUtility.SetDirty(general); }
                PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
                AssetDatabase.SaveAssets();
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes=new[]{DemoProjectBuilder.BootstrapPath,DemoProjectBuilder.TrainingPath},
                    locationPathName=directory+"/EmergencyVR.exe", target=BuildTarget.StandaloneWindows64,
                    extraScriptingDefines=new[]{"EMERGENCYVR_DESKTOP"}, options=BuildOptions.None
                });
                if(report.summary.result!=BuildResult.Succeeded) throw new InvalidOperationException("Windows build failed: "+report.summary.result);
                File.WriteAllText(directory+"/LEEME.txt",
                    "VITAL VR - Simulaci?n y entrenamiento cl?nico inmersivo.\nExtraer todo el ZIP y abrir EmergencyVR.exe.\n"+
                    "Bienvenida > elegir entorno y entrenamiento > preparaci?n > iniciar > resultados.\n"+
                    "Pr?ctica guiada: ayudas y valores simulados. Evaluaci?n: consultar los instrumentos.\n"+
                    "WASD: caminar. Bot?n derecho: mirar. E: coger/soltar. Q: usar. Z/X: orientar. R: volver al punto inicial.\n"+
                    "C: RCP manual cerca del paciente. Apuntar al t?rax y arrastrar el rat?n; soltar para retroceso.\n"+
                    "Escape: pausa real / continuar. Acciones y ficha se abren sin ocultar el monitor.\n"+
                    "Finalizar abre el resumen, las acciones, la cronolog?a, las m?tricas y las referencias.\n"+
                    "Guardar informe exporta JSON a AppData/LocalLow/EmergencyVR/VITAL VR/ReviewResults.\n"+
                    "Los informes de la demo anterior permanecen en su carpeta original.\n"+
                    "Cat?logo: "+medical.scenarios.Length+" variantes. Contenido en revisi?n cl?nica; m?tricas RCP virtuales sin calibraci?n de maniqu?.\n");
                File.WriteAllText("Builds/Windows/latest-build.json",JsonUtility.ToJson(new Release {directory=directory,createdUtc=DateTime.UtcNow.ToString("O"),unityVersion=Application.unityVersion},true));
                Debug.Log("WINDOWS_DEMO_BUILT "+directory);
            }
            finally
            {
                if(general!=null) { general.InitManagerOnStart=previousInit; EditorUtility.SetDirty(general); }
                PlayerSettings.fullScreenMode=oldFullscreen;
                PlayerSettings.SetPreloadedAssets(oldPreloaded);
                AssetDatabase.SaveAssets();
                // Unity's player build can also serialize a new Standalone batching entry.
                // This export must preserve the exact authoring configuration, including XR preloaded assets.
                File.WriteAllBytes("ProjectSettings/ProjectSettings.asset",projectSettings);
            }
        }
        [Serializable] sealed class Release {public string directory,createdUtc,unityVersion;}
    }
}
