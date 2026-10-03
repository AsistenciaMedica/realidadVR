using System.Linq;
using EmergencyVR.Environment.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace EmergencyVR.Tests
{
    public sealed class EnvironmentTextureCoordinatesTests
    {
        [TestCase("football", "Pitch and treatment sideline/grass", 2f)]
        [TestCase("mall", "Room shell/mallFloor", 1f)]
        public void ProceduralMeshesCarryNormalMapTangentsAndSquareFloorTexels(string environment, string floorName, float metresPerTile)
        {
            var root = EnvironmentBakeSource.Create(environment);
            var filters = root.GetComponentsInChildren<MeshFilter>();
            var materials = root.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials)
                .Where(m => m != null && m.name.StartsWith("Vital environment /")).Distinct().ToArray();
            var meshes = filters.Select(f => f.sharedMesh).Where(m => m != null && m.name.StartsWith("Vital environment /")).Distinct().ToArray();
            try
            {
                foreach (var mesh in meshes)
                {
                    Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount), mesh.name);
                    Assert.That(mesh.tangents.Length, Is.EqualTo(mesh.vertexCount), mesh.name);
                    foreach (var tangent in mesh.tangents)
                    {
                        Assert.That(float.IsNaN(tangent.x) || float.IsInfinity(tangent.x), Is.False);
                        Assert.That(new Vector3(tangent.x, tangent.y, tangent.z).sqrMagnitude, Is.GreaterThan(.0001f), mesh.name);
                        Assert.That(Mathf.Abs(tangent.w), Is.EqualTo(1).Within(.001f), mesh.name);
                    }
                }
                var floor = filters.Single(f => f.name == floorName);
                var source = floor.sharedMesh;
                var top = Enumerable.Range(0, source.vertexCount).Where(i => source.normals[i].y > .99f).ToArray();
                float width = top.Max(i => source.vertices[i].x) - top.Min(i => source.vertices[i].x);
                float depth = top.Max(i => source.vertices[i].z) - top.Min(i => source.vertices[i].z);
                float u = top.Max(i => source.uv[i].x) - top.Min(i => source.uv[i].x);
                float v = top.Max(i => source.uv[i].y) - top.Min(i => source.uv[i].y);
                var repeats = floor.GetComponent<Renderer>().sharedMaterial.GetTextureScale("_BaseMap");
                Assert.That(width / (u * repeats.x), Is.EqualTo(metresPerTile).Within(.01f));
                Assert.That(depth / (v * repeats.y), Is.EqualTo(metresPerTile).Within(.01f));
                if (environment == "football")
                {
                    var stripe = filters.Single(f => f.name == "Pitch and treatment sideline/grassStripe").GetComponent<Renderer>().sharedMaterial;
                    Assert.That(31 / stripe.GetTextureScale("_BaseMap").x, Is.EqualTo(metresPerTile));
                    Assert.That(4 / stripe.GetTextureScale("_BaseMap").y, Is.EqualTo(metresPerTile));
                    Assert.That(stripe.GetTextureOffset("_BaseMap").x, Is.EqualTo(.25f));
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
                foreach (var material in materials) Object.DestroyImmediate(material);
            }
        }
    }
}
