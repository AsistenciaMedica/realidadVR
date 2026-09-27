using System.Collections.Generic;
using UnityEngine;

namespace EmergencyVR.Patient.Presentation
{
    // Own derived garment mesh with copied skin weights, attached to the existing licensed skeleton.
    // No replacement avatar, simulated cloth, external tooling or new package is required.
    internal static class Case01SportsShirt
    {
        public static GameObject Create(SkinnedMeshRenderer skin, Transform model, List<Object> owned)
        {
            var source = skin.sharedMesh;
            var vertices = source.vertices; var normals = source.normals; var weights = source.boneWeights;
            var indices = source.triangles;
            var map = new Dictionary<int, int>();
            var points = new List<Vector3>(); var outputNormals = new List<Vector3>();
            var outputWeights = new List<BoneWeight>(); var triangles = new List<int>(); var originals = new List<int>();
            bool Inside(Vector3 p)
            {
                // Bounds are garment authoring coordinates of this fixed Rocketbox sports male, not clinical parameters.
                p = model.InverseTransformPoint(skin.transform.TransformPoint(p));
                return p.y > .94f && p.y < 1.485f && Mathf.Abs(p.x) < .40f;
            }
            int Copy(int index)
            {
                if (map.TryGetValue(index, out var found)) return found;
                int next = points.Count; map[index] = next; originals.Add(index);
                // A small shell clearance prevents skin z fighting; the same bones deform shirt and body.
                points.Add(vertices[index] + normals[index] * .009f);
                outputNormals.Add(normals[index]); outputWeights.Add(weights[index]); return next;
            }
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                if (!Inside(vertices[a]) || !Inside(vertices[b]) || !Inside(vertices[c])) continue;
                triangles.Add(Copy(a)); triangles.Add(Copy(b)); triangles.Add(Copy(c));
            }
            if (triangles.Count == 0) return null;
            var mesh = new Mesh { name = "CASE 01 Daniel fitted sports shirt" };
            mesh.SetVertices(points); mesh.SetNormals(outputNormals); mesh.SetTriangles(triangles, 0);
            mesh.boneWeights = outputWeights.ToArray(); mesh.bindposes = source.bindposes;
            int breath = source.GetBlendShapeIndex("VitalBreath");
            if (breath >= 0)
            {
                var delta = new Vector3[vertices.Length]; source.GetBlendShapeFrameVertices(breath, 0, delta, null, null);
                var selected = new Vector3[points.Count]; for (int i = 0; i < selected.Length; i++) selected[i] = delta[originals[i]];
                mesh.AddBlendShapeFrame("VitalBreath", 100, selected, null, null);
            }
            mesh.RecalculateBounds(); owned.Add(mesh);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "CASE 01 Daniel sports navy" };
            material.SetColor("_BaseColor", new Color(.025f, .17f, .23f)); material.SetFloat("_Smoothness", .15f);
            material.SetFloat("_Cull", 0); owned.Add(material);
            var item = new GameObject("Daniel sports shirt", typeof(SkinnedMeshRenderer));
            item.transform.SetParent(skin.transform, false);
            var renderer = item.GetComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh; renderer.bones = skin.bones;
            renderer.rootBone = skin.rootBone; renderer.sharedMaterial = material; renderer.localBounds = skin.localBounds;
            renderer.updateWhenOffscreen = false;
            return item;
        }
    }
}
