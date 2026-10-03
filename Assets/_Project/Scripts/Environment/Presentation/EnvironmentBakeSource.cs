using System.Collections.Generic;
using UnityEngine;

namespace EmergencyVR.Environment.Presentation
{
    /// <summary>Editor-facing entry point into the same procedural geometry used at runtime.
    /// It is never invoked automatically and does not activate baked rendering by itself.</summary>
    public static class EnvironmentBakeSource
    {
        public static GameObject Create(string id)
        {
            var root = new GameObject("Environment_" + id);
            var meshes = new List<Mesh>();
            var palette = new EnvironmentPalette(null);
            new EnvironmentModuleBuilder(root.transform, palette, meshes, true).Build(id);
            var anchor = new GameObject("PortableEquipmentAnchor").transform;
            anchor.SetParent(root.transform, false);
            anchor.localPosition = new Vector3(-1.8f, .98f, 3.75f);
            return root;
        }
    }
}
