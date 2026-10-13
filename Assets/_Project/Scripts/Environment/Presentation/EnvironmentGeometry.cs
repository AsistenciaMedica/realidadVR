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
        readonly List<Mesh> mappedPrimitives = new List<Mesh>();
        readonly Dictionary<Mesh, Mesh> modelMeshes = new Dictionary<Mesh, Mesh>();
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
            var primitive = primitives[shape];
            if (material == "footballStand" || material == "gymWood" || material == "wood" || material == "plaster" || material == "displayStone")
            {
                // World-sized UVs keep the grain vertical on timber, and concrete tiles at metre scale.
                // A thin slat must sample a narrow part of the grain rather than stretch a whole board.
                primitive = Object.Instantiate(primitive);
                var vertices = primitive.vertices; var normals = primitive.normals; var uv = new Vector2[vertices.Length];
                for (int i = 0; i < uv.Length; i++)
                {
                    var p = Vector3.Scale(vertices[i], size); var n = normals[i];
                    uv[i] = Mathf.Abs(n.y) > .5f ? new Vector2(p.x, p.z) :
                        Mathf.Abs(n.x) > .5f ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
                }
                primitive.uv = uv; primitive.RecalculateTangents(); mappedPrimitives.Add(primitive);
            }
            instances.Add(new CombineInstance { mesh = primitive, transform = Matrix4x4.TRS(position, rotation, size) });
        }
        /// <summary>
        /// Adds a licensed model (Resources path) to the current cluster with palette materials, resting on
        /// <paramref name="position"/>. Zero components of <paramref name="size"/> follow the model's proportions.
        /// Returns false when the model is unavailable so the caller can keep its procedural form.
        /// </summary>
        public bool Model(string resource, Vector3 position, float yaw, Vector3 size, System.Func<string, string> material)
        {
            var prefab = Resources.Load<GameObject>(resource);
            if (prefab == null) return false;
            var parts = new List<(Mesh mesh, Matrix4x4 local, Material[] materials)>();
            var bounds = new Bounds(); bool any = false;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh; var renderer = filter.GetComponent<MeshRenderer>();
                // Players can only combine readable meshes; the Editor (baking, tests) always can.
                if (mesh == null || renderer == null || (!Application.isEditor && !mesh.isReadable)) continue;
                var local = filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = local.MultiplyPoint3x4(mesh.bounds.center + Vector3.Scale(mesh.bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!any) { bounds = new Bounds(corner, Vector3.zero); any = true; } else bounds.Encapsulate(corner);
                }
                parts.Add((mesh, local, renderer.sharedMaterials));
            }
            if (!any || bounds.size.x <= 0 || bounds.size.y <= 0 || bounds.size.z <= 0) return false;
            var ratio = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
            int given = (size.x > 0 ? 1 : 0) + (size.y > 0 ? 1 : 0) + (size.z > 0 ? 1 : 0);
            float uniform = given == 0 ? 1 : (ratio.x + ratio.y + ratio.z) / given;
            var scale = new Vector3(size.x > 0 ? ratio.x : uniform, size.y > 0 ? ratio.y : uniform, size.z > 0 ? ratio.z : uniform);
            var place = Matrix4x4.TRS(position, Quaternion.Euler(0, yaw, 0), scale) *
                Matrix4x4.Translate(-new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            foreach (var part in parts)
            {
                var mesh = WithTangents(part.mesh);
                for (int submesh = 0; submesh < mesh.subMeshCount && submesh < part.materials.Length; submesh++)
                {
                    string key = cluster + "/" + material(part.materials[submesh] == null ? "" : part.materials[submesh].name);
                    if (!batches.TryGetValue(key, out var instances)) { instances = new List<CombineInstance>(); batches.Add(key, instances); }
                    instances.Add(new CombineInstance { mesh = mesh, subMeshIndex = submesh, transform = place * part.local });
                }
            }
            return true;
        }

        // Imported props may omit tangents or carry degenerate UVs; every environment mesh keeps a valid basis.
        Mesh WithTangents(Mesh source)
        {
            if (modelMeshes.TryGetValue(source, out var mesh)) return mesh;
            mesh = Object.Instantiate(source);
            if (mesh.uv.Length != mesh.vertexCount) mesh.uv = new Vector2[mesh.vertexCount];
            mesh.RecalculateTangents();
            var normals = mesh.normals; var tangents = mesh.tangents;
            if (tangents.Length != mesh.vertexCount) tangents = new Vector4[mesh.vertexCount];
            for (int i = 0; i < tangents.Length; i++)
            {
                var t = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                if (float.IsNaN(t.x) || float.IsNaN(t.y) || float.IsNaN(t.z) || t.sqrMagnitude < .01f)
                {
                    var n = i < normals.Length ? normals[i] : Vector3.up;
                    t = Vector3.Cross(n, Mathf.Abs(n.y) > .9f ? Vector3.right : Vector3.up).normalized;
                    if (t.sqrMagnitude < .01f) t = Vector3.right;
                }
                tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w < 0 ? -1 : 1);
            }
            mesh.tangents = tangents;
            modelMeshes.Add(source, mesh);
            return mesh;
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
        /// <summary>Textured plane (Unity quad: UVs + tangents) for surfaces that need a tiled material.</summary>
        public void Surface(string name, Vector3 center, Quaternion rotation, Vector2 size, string material)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Dispose(item.GetComponent<Collider>());
            item.name = name;
            item.transform.SetParent(root, false);
            item.transform.localPosition = center;
            item.transform.localRotation = rotation;
            item.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = item.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = palette[material];
            renderer.shadowCastingMode = ShadowCastingMode.Off;
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
            var panel = new GameObject("VITAL VR corporate sign", typeof(RectTransform), typeof(Canvas));
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
                long vertices = 0;
                foreach (var instance in batch.Value) vertices += instance.mesh.vertexCount;
                if (vertices > 65000) mesh.indexFormat = IndexFormat.UInt32; // dense licensed props, e.g. goal nets
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
            foreach (var primitive in primitives.Values) Dispose(primitive);
            foreach (var primitive in mappedPrimitives) Dispose(primitive);
            foreach (var model in modelMeshes.Values) Dispose(model);
            mappedPrimitives.Clear(); modelMeshes.Clear();
            batches.Clear(); primitives.Clear();
        }

        static void Dispose(Object value)
        {
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }

        static Mesh Box(float radius)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var triangles = new List<int>();
            var uv = new List<Vector2>();
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
                    uv.Add(new Vector2(x + .5f, y + .5f));
                }
                for (int y = 0; y < grid.Length - 1; y++) for (int x = 0; x < grid.Length - 1; x++)
                {
                    int a = start + y * grid.Length + x, b = a + 1, c = a + grid.Length, d = c + 1;
                    triangles.AddRange(new[] { a, b, c, b, d, c });
                }
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
        }

        static Mesh Round(float topRadius)
        {
            const int segments = 12;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            var uv = new List<Vector2>(); var tangents = new List<Vector4>();
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var normal = (radial + Vector3.up * ((1 - topRadius) * .5f)).normalized;
                v.Add(radial * .5f - Vector3.up * .5f); n.Add(normal);
                v.Add(radial * (.5f * topRadius) + Vector3.up * .5f); n.Add(normal);
                uv.Add(new Vector2((float)i / segments, 0)); uv.Add(new Vector2((float)i / segments, 1));
                var tangent = new Vector4(-Mathf.Sin(angle), 0, Mathf.Cos(angle), -1);
                tangents.Add(tangent); tangents.Add(tangent);
                if (i < segments) { int a = i * 2; t.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 }); }
            }
            for (int cap = 0; cap < 2; cap++)
            {
                float y = cap == 0 ? -.5f : .5f, radius = cap == 0 ? .5f : topRadius * .5f;
                int center = v.Count; v.Add(Vector3.up * y); n.Add(cap == 0 ? Vector3.down : Vector3.up);
                uv.Add(new Vector2(.5f, .5f)); tangents.Add(new Vector4(1, 0, 0, cap == 0 ? 1 : -1));
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2 / segments;
                    v.Add(new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius)); n.Add(cap == 0 ? Vector3.down : Vector3.up);
                    uv.Add(new Vector2(.5f + Mathf.Cos(angle) * radius, .5f + Mathf.Sin(angle) * radius));
                    tangents.Add(new Vector4(1, 0, 0, cap == 0 ? 1 : -1));
                    if (i < segments) t.AddRange(cap == 0 ? new[] { center, center + i + 1, center + i + 2 } : new[] { center, center + i + 2, center + i + 1 });
                }
            }
            var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTangents(tangents);
            mesh.SetTriangles(t, 0); mesh.RecalculateBounds(); return mesh;
        }

        static Mesh Sphere()
        {
            const int rings = 8, slices = 12;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            var uv = new List<Vector2>(); var tangents = new List<Vector4>();
            for (int r = 0; r <= rings; r++) for (int s = 0; s <= slices; s++)
            {
                float phi = r * Mathf.PI / rings, theta = s * Mathf.PI * 2 / slices;
                var point = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                v.Add(point * .5f); n.Add(point);
                uv.Add(new Vector2((float)s / slices, (float)r / rings));
                tangents.Add(new Vector4(-Mathf.Sin(theta), 0, Mathf.Cos(theta), -1));
                if (r < rings && s < slices)
                {
                    int a = r * (slices + 1) + s, b = a + 1, c = a + slices + 1;
                    t.AddRange(new[] { a, b, c, b, c + 1, c });
                }
            }
            var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTangents(tangents);
            mesh.SetTriangles(t, 0); mesh.RecalculateBounds(); return mesh;
        }
    }
}
