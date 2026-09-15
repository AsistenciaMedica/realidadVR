using System.Collections.Generic;
using UnityEngine;

namespace EmergencyVR.Environment.Presentation
{
    /// <summary>Small shared opaque URP palette. Owned runtime materials never mutate imported assets.</summary>
    internal sealed class EnvironmentPalette : System.IDisposable
    {
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        public Material this[string name] => materials[name];

        public EnvironmentPalette(Material template)
        {
            Add("plaster", new Color(.77f, .81f, .80f));
            Add("white", new Color(.88f, .91f, .90f));
            Add("stone", new Color(.49f, .53f, .51f), smooth: .18f);
            Add("navy", new Color(.055f, .10f, .14f), smooth: .13f);
            Add("blue", new Color(.19f, .34f, .39f), smooth: .22f);
            Add("rubber", new Color(.065f, .071f, .073f), smooth: .05f);
            Add("metal", new Color(.46f, .51f, .54f), metallic: .45f, smooth: .35f);
            Add("wood", new Color(.42f, .30f, .19f), smooth: .18f);
            Add("glass", new Color(.23f, .37f, .42f), metallic: .15f, smooth: .48f);
            Add("green", new Color(.14f, .28f, .12f), smooth: .04f);
            Add("grass", new Color(.22f, .34f, .15f), smooth: .02f);
            Add("accent", new Color(.60f, .15f, .13f), smooth: .17f);
            Add("lamp", new Color(.82f, .88f, .86f), emission: new Color(.32f, .36f, .34f));
            // Corporate signage has its own token; clinical furniture and surfaces retain their existing palette.
            Add("brandNavy", EmergencyVR.UI.VitalBrand.Navy);

            void Add(string name, Color color, float metallic = 0, float smooth = .12f, Color emission = default)
            {
                var shader = template == null ? Shader.Find("Universal Render Pipeline/Lit") : template.shader;
                var material = new Material(shader) { name = "Vital environment / " + name, enableInstancing = true };
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Metallic", metallic);
                material.SetFloat("_Smoothness", smooth);
                if (emission.maxColorComponent > 0)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", emission);
                }
                materials.Add(name, material);
            }
        }

        public void Dispose()
        {
            foreach (var material in materials.Values) if (material != null) Object.Destroy(material);
            materials.Clear();
        }
    }
}
