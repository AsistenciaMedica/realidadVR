using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Management;

namespace EmergencyVR.Tests
{
    public sealed class SimulatedXRTestSetup : IPrebuildSetup, IPostBuildCleanup
    {
        const string SnapshotKey = "VITAL.SimulatedXRTestSetup.Standalone";
        [Serializable] sealed class Snapshot { public bool initialize; public string[] loaders; }

        public void Setup()
        {
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (settings == null || settings.Manager == null) return;
            if (string.IsNullOrEmpty(SessionState.GetString(SnapshotKey, "")))
            {
                var snapshot = new Snapshot {
                    initialize = settings.InitManagerOnStart,
                    loaders = settings.Manager.activeLoaders.Select(loader => GlobalObjectId.GetGlobalObjectIdSlow(loader).ToString()).ToArray()
                };
                SessionState.SetString(SnapshotKey, JsonUtility.ToJson(snapshot));
            }
            settings.InitManagerOnStart = false;
            if (!settings.Manager.TrySetLoaders(new List<XRLoader>()))
                throw new InvalidOperationException("Cannot disable native Standalone XR loaders before simulated PlayMode tests.");
            Save(settings);
            Debug.Log("SIMULATED_XR_TEST_SETUP: native Standalone loaders disabled before PlayMode; Input System simulation only.");
        }

        public void Cleanup()
        {
            string json = SessionState.GetString(SnapshotKey, "");
            if (string.IsNullOrEmpty(json)) return;
            var snapshot = JsonUtility.FromJson<Snapshot>(json);
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (settings == null || settings.Manager == null) throw new InvalidOperationException("Cannot restore Standalone XR settings after tests.");
            var loaders = new List<XRLoader>();
            foreach (var value in snapshot.loaders)
            {
                if (!GlobalObjectId.TryParse(value, out var id) || !(GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) is XRLoader loader))
                    throw new InvalidOperationException("Cannot restore the original XR loader: " + value);
                loaders.Add(loader);
            }
            if (!settings.Manager.TrySetLoaders(loaders)) throw new InvalidOperationException("Could not restore Standalone XR loaders after tests.");
            settings.InitManagerOnStart = snapshot.initialize;
            Save(settings);
            SessionState.EraseString(SnapshotKey);
            Debug.Log("SIMULATED_XR_TEST_CLEANUP: original Standalone XR settings restored.");
        }

        static void Save(XRGeneralSettings settings)
        {
            EditorUtility.SetDirty(settings); EditorUtility.SetDirty(settings.Manager);
            AssetDatabase.SaveAssets();
        }
    }
}
