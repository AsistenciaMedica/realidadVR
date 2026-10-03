using UnityEditor;
using UnityEngine;

namespace EmergencyVR.Editor
{
    /// <summary>
    /// glTF models imported before the image-conversion modules existed stay failed (white) until reimported,
    /// and Unity never retries them on its own. Reimport the gym models once per project checkout and revision.
    /// </summary>
    [InitializeOnLoad]
    static class GymModelsReimport
    {
        const string Folder = "Assets/ThirdParty/GymModels/Resources/Gym";
        const string Revision = "gym-models-2";

        static GymModelsReimport() { EditorApplication.delayCall += Run; }

        static void Run()
        {
            string key = "VitalVR.GymModelsReimport." + Application.dataPath;
            if (EditorPrefs.GetString(key) == Revision || !AssetDatabase.IsValidFolder(Folder)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) { EditorApplication.delayCall += Run; return; }
            EditorPrefs.SetString(key, Revision);
            AssetDatabase.ImportAsset(Folder, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
            Debug.Log("VITAL VR: gym models reimported with texture support.");
        }
    }
}
