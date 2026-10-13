using System.Collections.Generic;
using UnityEngine;

namespace EmergencyVR.UI
{
    /// <summary>Physical signs are depth-tested; UI text retains its normal overlay behavior.</summary>
    public sealed class WorldSignage : MonoBehaviour
    {
        readonly Dictionary<Font, Material> materials = new Dictionary<Font, Material>();
        readonly Dictionary<MeshRenderer, Material> signs = new Dictionary<MeshRenderer, Material>();

        void Awake()
        {
            var shader = Resources.Load<Shader>("Visual/WorldSignage");
            if (shader == null) throw new System.InvalidOperationException("Missing world signage shader.");
            foreach (var text in GetComponentsInChildren<TextMesh>(true))
            {
                if (text.font == null) continue;
                if (!materials.TryGetValue(text.font, out var material))
                {
                    material = new Material(shader) { name = "Depth-tested world lettering" };
                    material.mainTexture = text.font.material.mainTexture;
                    materials.Add(text.font, material);
                }
                var renderer = text.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                signs.Add(renderer, material);
            }
            Font.textureRebuilt += RefreshAtlas;
        }

        void RefreshAtlas(Font font)
        {
            if (materials.TryGetValue(font, out var material)) material.mainTexture = font.material.mainTexture;
        }

        void LateUpdate()
        {
            // Legacy TextMesh may restore its font material when an atlas is rebuilt.
            foreach (var pair in signs)
                if (pair.Key != null && pair.Key.sharedMaterial != pair.Value) pair.Key.sharedMaterial = pair.Value;
        }

        void OnDestroy()
        {
            Font.textureRebuilt -= RefreshAtlas;
            foreach (var material in materials.Values) Destroy(material);
            materials.Clear();
            signs.Clear();
        }
    }
}
