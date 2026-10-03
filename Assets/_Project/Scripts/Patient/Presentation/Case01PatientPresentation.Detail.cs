using System.Collections.Generic;
using UnityEngine;

namespace EmergencyVR.Patient.Presentation
{
    // Close-range presentation only. The authored bench/mat collider bounds and patient
    // position confirmation are unchanged; rounded surfaces replace their visible cube meshes.
    public sealed partial class Case01PatientPresentation
    {
        readonly Transform[] middleFingers = new Transform[2];
        readonly Transform[] indexFingers = new Transform[2];
        readonly Transform[] littleFingers = new Transform[2];
        float naturalBlinkAt = -1, nextNaturalBlink = 3.1f;
        int naturalBlinkSequence;
        static readonly float[] NaturalBlinkIntervals = { 4.1f, 3.2f, 5.3f, 2.8f, 4.7f };

        void PresentNaturalBlink()
        {
            // Preserve the medical presentation's sustained eye closure for drowsy/unresponsive states.
            if (visual.VisualState.Consciousness == PatientConsciousness.Drowsy ||
                visual.VisualState.Consciousness == PatientConsciousness.Unresponsive) return;
            if (naturalBlinkAt < 0 && performanceTime >= nextNaturalBlink) naturalBlinkAt = performanceTime;
            float closure = 0;
            if (naturalBlinkAt >= 0)
            {
                float t = performanceTime - naturalBlinkAt;
                closure = t < .055f ? Smooth(t / .055f) : 1 - Smooth((t - .065f) / .13f);
                if (t >= .20f)
                {
                    naturalBlinkAt = -1;
                    nextNaturalBlink = performanceTime + NaturalBlinkIntervals[naturalBlinkSequence++ % NaturalBlinkIntervals.Length];
                }
            }
            rig.SetBlendShape(rig.blinkLeft, closure * 100);
            rig.SetBlendShape(rig.blinkRight, closure * 97);
        }

        void CacheHandLandmarks()
        {
            for (int side = 0; side < 2; side++)
            {
                string prefix = side == 0 ? "Bip01 L " : "Bip01 R ";
                foreach (var bone in model.GetComponentsInChildren<Transform>(true))
                {
                    if (bone.name == prefix + "Finger2") middleFingers[side] = bone;
                    if (bone.name == prefix + "Finger1") indexFingers[side] = bone;
                    if (bone.name == prefix + "Finger4") littleFingers[side] = bone;
                }
            }
        }

        Vector3 SeatedHandTarget(int side)
        {
            if (side == 0) return Origin - Right * .34f + Forward * .075f + Vector3.up * .535f;
            // The free hand actually rests on the quadriceps instead of hovering in front of the abdomen.
            return Vector3.Lerp(body.thighs[side].position, body.calves[side].position, .57f) + Vector3.up * .083f;
        }

        void PoseDizzyGesture(float amount)
        {
            // Solve against the real head instead of adding a chair animation's arm angles
            // onto a different seat pose; that used to leave the hand floating in space.
            var eyeLine = rig.leftEye != null && rig.rightEye != null
                ? (rig.leftEye.position + rig.rightEye.position) * .5f : rig.head.position + Vector3.up * .10f;
            var temple = eyeLine + Right * .105f + Forward * .035f - Vector3.up * .035f;
            var target = Vector3.Lerp(SeatedHandTarget(1), temple, amount);
            SolveLimb(body.upperArms[1], body.forearms[1], body.hands[1], target,
                body.upperArms[1].position + Right * .30f - Vector3.up * .25f + Forward * .17f);
            OrientRightHand(Vector3.up + Forward * .2f, -Right, amount);
        }

        void PreserveSeatedHandContacts()
        {
            if (!HasVoluntaryPresence || busy || risePending || standing || regaining || progress > 0 || completed ||
                review.Procedures?.CPR?.Touching == true || visual.CompressionDepthMetres > .001f) return;
            for (int side = 0; side < 2; side++)
            {
                // During the occasional right-hand gesture the existing animation owns that hand.
                if (side == 1 && gestureAt >= 0) continue;
                var lateral = Right * (side == 0 ? -1 : 1);
                SolveLimb(body.upperArms[side], body.forearms[side], body.hands[side], SeatedHandTarget(side),
                    body.upperArms[side].position + lateral * .25f - Vector3.up * .34f + Forward * .15f);
                var middle = middleFingers[side]; var index = indexFingers[side]; var little = littleFingers[side];
                if (middle == null || index == null || little == null) continue;
                var fingerDirection = middle.position - body.hands[side].position;
                var palmNormal = Vector3.Cross(index.position - body.hands[side].position, little.position - body.hands[side].position);
                if (fingerDirection.sqrMagnitude < .00001f || palmNormal.sqrMagnitude < .00000001f) continue;
                var current = Quaternion.LookRotation(fingerDirection, palmNormal);
                // Index/little-finger winding is mirrored between hands. Using the same
                // normal for both would put the right palm face-up against the thigh.
                var desired = Quaternion.LookRotation((Forward + lateral * .12f - Vector3.up * .10f).normalized,
                    side == 0 ? Vector3.down : Vector3.up);
                body.hands[side].rotation = desired * Quaternion.Inverse(current) * body.hands[side].rotation;
            }
        }

        void RefineCloseRangeSurfaces()
        {
            // Fine neutral upholstery reflects the room lighting without the old teal plastic appearance.
            benchMaterial.SetColor("_BaseColor", new Color(.24f, .25f, .255f));
            benchMaterial.SetFloat("_Smoothness", .23f);
            benchMaterial.SetTexture("_BaseMap", UpholsteryGrain());
            metalMaterial.SetColor("_BaseColor", new Color(.12f, .135f, .145f));
            metalMaterial.SetFloat("_Metallic", .72f);
            metalMaterial.SetFloat("_Smoothness", .34f);
            matMaterial.SetColor("_BaseColor", new Color(.22f, .235f, .24f));
            matMaterial.SetFloat("_Smoothness", .08f);
            var rubber = Resources.Load<Texture2D>("Visual/Textures/rubber_tiles_color");
            if (rubber != null) { matMaterial.SetTexture("_BaseMap", rubber); matMaterial.SetTextureScale("_BaseMap", new Vector2(2, 3)); }
            RoundSurface(assembly.transform.Find("Stable cardio bench seat"), .018f);
            RoundSurface(assembly.transform.Find("Bench back support"), .020f);
            RoundSurface(assembly.transform.Find("Clear assistance mat"), .009f);
            // Small functional furniture details give the contact area a human scale.
            var feetMaterial = Surface("CASE 01 bench rubber feet", new Color(.045f, .048f, .05f));
            feetMaterial.SetFloat("_Smoothness", .09f);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var cap = Box("Bench non-slip foot", Origin + new Vector3(x * .47f, .019f, z * .14f),
                        new Vector3(.073f, .036f, .073f), feetMaterial);
                    cap.GetComponent<Collider>().enabled = false;
                    RoundSurface(cap.transform, .007f);
                }
            var lowerRail = Box("Bench lower frame rail", Origin + new Vector3(0, .16f, .14f),
                new Vector3(.94f, .032f, .035f), metalMaterial);
            lowerRail.GetComponent<Collider>().enabled = false;
            RoundSurface(lowerRail.transform, .006f);
        }

        Texture2D UpholsteryGrain()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true) {
                name = "Daniel bench fine upholstery grain", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear
            };
            var pixels = new Color32[size * size];
            // Deterministic texture: never touch Unity's global Random stream used by other presentation layers.
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    uint hash = (uint)(x * 374761393 + y * 668265263);
                    hash = (hash ^ (hash >> 13)) * 1274126177u;
                    byte shade = (byte)(205 + (hash & 15) + ((x + y) % 3 == 0 ? 8 : 0));
                    pixels[y * size + x] = new Color32(shade, shade, shade, 255);
                }
            texture.SetPixels32(pixels); texture.Apply(true, true); owned.Add(texture); return texture;
        }

        void RoundSurface(Transform item, float radius)
        {
            if (item == null) return;
            // Keep Transform scale and its BoxCollider intact. Mesh positions are normalized afterward.
            var size = item.localScale; var half = size * .5f;
            radius = Mathf.Min(radius, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * .95f);
            var core = half - Vector3.one * radius;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var uv = new List<Vector2>(); var triangles = new List<int>();
            var axes = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            var horizontal = new[] { Vector3.back, Vector3.forward, Vector3.right, Vector3.right, Vector3.right, Vector3.left };
            var vertical = new[] { Vector3.up, Vector3.up, Vector3.back, Vector3.forward, Vector3.up, Vector3.up };
            float Extent(Vector3 axis) => Mathf.Abs(axis.x) * half.x + Mathf.Abs(axis.y) * half.y + Mathf.Abs(axis.z) * half.z;
            float Coordinate(int i, float extent) => i == 0 ? -extent : i == 1 ? -extent + radius * .293f :
                i == 2 ? -extent + radius : i == 3 ? 0 : i == 4 ? extent - radius : i == 5 ? extent - radius * .293f : extent;
            for (int face = 0; face < 6; face++)
            {
                int start = vertices.Count;
                for (int y = 0; y < 7; y++)
                    for (int x = 0; x < 7; x++)
                    {
                        float u = Coordinate(x, Extent(horizontal[face])), v = Coordinate(y, Extent(vertical[face]));
                        var p = axes[face] * Extent(axes[face]) + horizontal[face] * u + vertical[face] * v;
                        var centre = new Vector3(Mathf.Clamp(p.x, -core.x, core.x), Mathf.Clamp(p.y, -core.y, core.y), Mathf.Clamp(p.z, -core.z, core.z));
                        var normal = (p - centre).normalized; p = centre + normal * radius;
                        vertices.Add(new Vector3(p.x / size.x, p.y / size.y, p.z / size.z));
                        normals.Add(Vector3.Scale(normal, size).normalized);
                        uv.Add(new Vector2(u * 8, v * 8));
                    }
                for (int y = 0; y < 6; y++)
                    for (int x = 0; x < 6; x++)
                    {
                        int a = start + y * 7 + x;
                        triangles.Add(a); triangles.Add(a + 1); triangles.Add(a + 7);
                        triangles.Add(a + 1); triangles.Add(a + 8); triangles.Add(a + 7);
                    }
            }
            var mesh = new Mesh { name = item.name + " rounded visible surface" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds(); owned.Add(mesh); item.GetComponent<MeshFilter>().sharedMesh = mesh;
        }
    }
}
