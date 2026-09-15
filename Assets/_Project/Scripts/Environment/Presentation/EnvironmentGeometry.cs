using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EmergencyVR.Environment.Presentation
{
    // Author simple structural forms, then combine only within local furniture/architecture clusters.
    // Meshes remain separately culled between clusters, unlike one room-wide combined mesh.
    internal sealed class EnvironmentGeometry
    {
        readonly Transform root;
        readonly EnvironmentPalette palette;
        readonly List<Mesh> ownedMeshes;
        readonly Dictionary<string, List<CombineInstance>> batches = new Dictionary<string, List<CombineInstance>>();
        readonly Dictionary<string, Mesh> primitives = new Dictionary<string, Mesh>();
        string cluster = "Architecture";
        public EnvironmentGeometry(Transform root, EnvironmentPalette palette, List<Mesh> ownedMeshes)
        {
            this.root = root; this.palette = palette; this.ownedMeshes = ownedMeshes;
            primitives["box"] = Box(0);
            primitives["soft"] = Box(.12f);
            primitives["cylinder"] = Round(1);
            primitives["cone"] = Round(.09f);
            primitives["sphere"] = Sphere();
        }

        public void Cluster(string name) { cluster = name; }
        public void Shape(string shape, Vector3 position, Vector3 size, string material, Quaternion rotation = default)
        {
            // Quaternion.operator== compares rotational similarity; a zero quaternion is not equal even to itself.
            if (Quaternion.Dot(rotation, rotation) < .000001f) rotation = Quaternion.identity;
            string key = cluster + "/" + material;
            if (!batches.TryGetValue(key, out var instances)) { instances = new List<CombineInstance>(); batches.Add(key, instances); }
            instances.Add(new CombineInstance { mesh = primitives[shape], transform = Matrix4x4.TRS(position, rotation, size) });
        }
        public void Tube(Vector3 from, Vector3 to, float radius, string material)
        {
            var delta = to - from;
            if (delta.sqrMagnitude < .000001f) return;
            Shape("cylinder", (from + to) * .5f, new Vector3(radius * 2, delta.magnitude, radius * 2), material,
                Quaternion.FromToRotation(Vector3.up, delta.normalized));
        }
        public void Solid(string name, Vector3 position, Vector3 size, Quaternion rotation = default)
        {
            var item = new GameObject(name + " collider", typeof(BoxCollider));
            item.transform.SetParent(root, false);
            item.transform.localPosition = position;
            item.transform.localRotation = Quaternion.Dot(rotation, rotation) < .000001f ? Quaternion.identity : rotation;
            item.GetComponent<BoxCollider>().size = size;
        }
        public void Text(string text, Vector3 position, float size = .018f, float yaw = 0, bool light = false)
        {
            var item = new GameObject(text, typeof(TextMesh));
            item.transform.SetParent(root, false);
            item.transform.localPosition = position;
            item.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var mesh = item.GetComponent<TextMesh>();
            mesh.text = text; mesh.characterSize = size; mesh.fontSize = 48;
            mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center;
            mesh.color = light ? new Color(.87f, .91f, .90f) : new Color(.055f, .10f, .14f);
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var renderer = item.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mesh.font.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        public void BrandLockup(Vector3 position, float width)
        {
            float height = width * .245f;
            Shape("box", position, new Vector3(width, height, .025f), "brandNavy");
            var panel = new GameObject("Vital VR corporate sign", typeof(RectTransform), typeof(Canvas));
            panel.transform.SetParent(root, false);
            panel.transform.localPosition = position + Vector3.back * .014f;
            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            var canvas = panel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            // Display only: no raycaster, interaction target, or controller blocking collider.
            EmergencyVR.UI.VitalBrand.AddLockup(panel.transform, Vector2.zero, new Vector2(width * .90f, height * .82f));
        }
        public void Finish()
        {
            foreach (var batch in batches)
            {
                var mesh = new Mesh { name = "Vital environment / " + batch.Key };
                mesh.CombineMeshes(batch.Value.ToArray(), true, true);
                var item = new GameObject(batch.Key, typeof(MeshFilter), typeof(MeshRenderer));
                item.transform.SetParent(root, false);
                item.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = item.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = palette[batch.Key.Substring(batch.Key.LastIndexOf('/') + 1)];
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                ownedMeshes.Add(mesh);
            }
            foreach (var primitive in primitives.Values) Object.Destroy(primitive);
            batches.Clear(); primitives.Clear();
        }

        static Mesh Box(float radius)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var triangles = new List<int>();
            var directions = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            float[] grid = radius == 0 ? new[] { -.5f, .5f } : new[] { -.5f, -.5f + radius, .5f - radius, .5f };
            foreach (var normal in directions)
            {
                var tangent = Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.up;
                var bitangent = Vector3.Cross(normal, tangent);
                int start = vertices.Count;
                foreach (float y in grid) foreach (float x in grid)
                {
                    var point = normal * .5f + tangent * x + bitangent * y;
                    var inner = new Vector3(Mathf.Clamp(point.x, -.5f + radius, .5f - radius), Mathf.Clamp(point.y, -.5f + radius, .5f - radius), Mathf.Clamp(point.z, -.5f + radius, .5f - radius));
                    var n = radius == 0 ? normal : (point - inner).normalized;
                    vertices.Add(radius == 0 ? point : inner + n * radius); normals.Add(n);
                }
                for (int y = 0; y < grid.Length - 1; y++) for (int x = 0; x < grid.Length - 1; x++)
                {
                    int a = start + y * grid.Length + x, b = a + 1, c = a + grid.Length, d = c + 1;
                    triangles.AddRange(new[] { a, b, c, b, d, c });
                }
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); return mesh;
        }

        static Mesh Round(float topRadius)
        {
            const int segments = 12;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var normal = (radial + Vector3.up * ((1 - topRadius) * .5f)).normalized;
                v.Add(radial * .5f - Vector3.up * .5f); n.Add(normal);
                v.Add(radial * (.5f * topRadius) + Vector3.up * .5f); n.Add(normal);
                if (i < segments) { int a = i * 2; t.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 }); }
            }
            for (int cap = 0; cap < 2; cap++)
            {
                float y = cap == 0 ? -.5f : .5f, radius = cap == 0 ? .5f : topRadius * .5f;
                int center = v.Count; v.Add(Vector3.up * y); n.Add(cap == 0 ? Vector3.down : Vector3.up);
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2 / segments;
                    v.Add(new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius)); n.Add(cap == 0 ? Vector3.down : Vector3.up);
                    if (i < segments) t.AddRange(cap == 0 ? new[] { center, center + i + 1, center + i + 2 } : new[] { center, center + i + 2, center + i + 1 });
                }
            }
            var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(t, 0); mesh.RecalculateBounds(); return mesh;
        }

        static Mesh Sphere()
        {
            const int rings = 8, slices = 12;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            for (int r = 0; r <= rings; r++) for (int s = 0; s <= slices; s++)
            {
                float phi = r * Mathf.PI / rings, theta = s * Mathf.PI * 2 / slices;
                var point = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                v.Add(point * .5f); n.Add(point);
                if (r < rings && s < slices)
                {
                    int a = r * (slices + 1) + s, b = a + 1, c = a + slices + 1;
                    t.AddRange(new[] { a, b, c, b, c + 1, c });
                }
            }
            var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(t, 0); mesh.RecalculateBounds(); return mesh;
        }
    }
}
