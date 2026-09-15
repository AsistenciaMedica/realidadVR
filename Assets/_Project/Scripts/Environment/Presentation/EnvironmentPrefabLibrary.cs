using System;
using UnityEngine;

namespace EmergencyVR.Environment.Presentation
{
    /// <summary>Prefab asset references, independent of the scene's build-time static batches.</summary>
    public sealed class EnvironmentPrefabLibrary : ScriptableObject
    {
        [Serializable] public sealed class Entry { public string path; public GameObject prefab; }
        public Entry[] entries = Array.Empty<Entry>();
        public GameObject Find(string path)
        {
            foreach (var entry in entries) if (entry != null && entry.path == path) return entry.prefab;
            return null;
        }
    }
}
