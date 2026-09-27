using UnityEngine;

namespace EmergencyVR.Environment.Presentation
{
    /// <summary>
    /// Licensed gym models (Assets/ThirdParty/GymModels, CC BY 4.0) placed at real-world scale.
    /// Source files keep their authored units; each prop is normalised here to a measured dimension.
    /// </summary>
    public static class GymPropSet
    {
        enum Fit { Height, Length }

        /// <summary>Returns false when the imported models are unavailable, so the procedural fallback is kept.</summary>
        public static bool Available => Resources.Load<GameObject>("Gym/treadmill_a") != null;

        public static void Build(Transform root)
        {
            var parent = new GameObject("Gym equipment (licensed models)").transform;
            parent.SetParent(root, false);

            // Back wall: strength area. The patient lies around (1.55, 0, 2.1); keep a 1 m working margin.
            Prop(parent, "power_rack_bench", new Vector3(0, 0, 4.05f), 180, 2.2f, Fit.Height);
            Prop(parent, "dumbbell_rack", new Vector3(2.35f, 0, 4.45f), 180, 1.9f, Fit.Length);
            Prop(parent, "weight_plates", new Vector3(-.95f, 0, 4.55f), 0, .9f, Fit.Length, solid: false);
            Prop(parent, "kettlebell", new Vector3(1.0f, 0, 4.5f), 20, .28f, Fit.Height);
            Prop(parent, "kettlebell", new Vector3(1.05f, 0, 4.12f), -35, .24f, Fit.Height);
            Prop(parent, "barbell", new Vector3(-.2f, 0, 3.0f), 0, 2.0f, Fit.Length, solid: false);

            // Right wall: cardio line facing the wall.
            Prop(parent, "treadmill_a", new Vector3(2.45f, 0, -.35f), 180, 1.95f, Fit.Length);
            Prop(parent, "treadmill_b", new Vector3(2.55f, 0, -1.7f), -90, 1.9f, Fit.Length);
            Prop(parent, "elliptical", new Vector3(2.6f, 0, -2.6f), -90, 1.65f, Fit.Height);

            // Left wall (mirror side): selectorised machines.
            Prop(parent, "lat_pulldown", new Vector3(-2.75f, 0, 1.45f), 90, 2.1f, Fit.Height);
            Prop(parent, "body_solid_machine", new Vector3(-2.7f, 0, -.55f), 90, 1.95f, Fit.Height);

            // Entrance wall: lockers, mats, hydration.
            Prop(parent, "lockers", new Vector3(-2.2f, 0, -2.62f), 0, 1.85f, Fit.Height);
            Prop(parent, "first_aid_kit", new Vector3(-2.2f, 1.86f, -2.66f), 0, .38f, Fit.Length, solid: false);
            Prop(parent, "yoga_mat", new Vector3(-1.2f, 0, -2.8f), 0, .62f, Fit.Height);
            Prop(parent, "yoga_mat", new Vector3(-1.02f, 0, -2.78f), 25, .62f, Fit.Height);
            Prop(parent, "water_cooler", new Vector3(1.15f, 0, -2.72f), 0, 1.1f, Fit.Height);

            // Corner details.
            Prop(parent, "punching_bag", new Vector3(3.05f, .55f, 1.0f), 0, 1.25f, Fit.Height);
            Prop(parent, "wall_clock", new Vector3(-3.47f, 2.35f, -1.6f), 90, .36f, Fit.Length, solid: false, onFloor: false);
            if (!Application.isMobilePlatform) Lighting(root);
        }

        /// <summary>Desktop: warm-neutral spot lights under the six LED panels and a box-projected reflection probe.</summary>
        static void Lighting(Transform root)
        {
            var lights = new GameObject("Ceiling lights").transform;
            lights.SetParent(root, false);
            foreach (float x in new[] { -1.6f, 1.6f })
                foreach (float z in new[] { -1.4f, 1.2f, 3.6f })
                {
                    var light = new GameObject("Panel light").AddComponent<Light>();
                    light.transform.SetParent(lights, false);
                    light.transform.localPosition = new Vector3(x, 3.0f, z);
                    light.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    light.type = LightType.Spot; light.spotAngle = 150; light.innerSpotAngle = 70;
                    light.range = 6.5f; light.intensity = 2.1f; light.color = new Color(1, .96f, .9f);
                    // Two panels over the incident area cast soft shadows; the rest only fill.
                    light.shadows = z > 0 && z < 2 ? LightShadows.Soft : LightShadows.None;
                    light.shadowStrength = .55f;
                }
            var probe = new GameObject("Room reflections").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(root, false);
            probe.transform.localPosition = new Vector3(0, 1.55f, 1);
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.boxProjection = true; probe.size = new Vector3(7, 3.2f, 8); probe.resolution = 256;
            probe.intensity = .9f;
        }

        /// <summary>Instantiates a licensed model at real scale (largest horizontal extent), resting on its base.</summary>
        public static Transform Spawn(string id, float length, Transform parent) =>
            Prop(parent, id, Vector3.zero, 0, length, Fit.Length, solid: false);

        static Transform Prop(Transform parent, string id, Vector3 position, float yaw, float size, Fit fit,
            bool solid = true, bool onFloor = true)
        {
            var prefab = Resources.Load<GameObject>("Gym/" + id);
            if (prefab == null) return null;
            var holder = new GameObject(id).transform;
            holder.SetParent(parent, false);
            var model = Object.Instantiate(prefab, holder, false).transform;
            model.name = "Model";
            // Imported glTF scenes can carry authoring cameras/lights; only geometry belongs in the room.
            foreach (var extra in model.GetComponentsInChildren<Camera>(true)) Object.Destroy(extra.gameObject);
            foreach (var extra in model.GetComponentsInChildren<Light>(true)) Object.Destroy(extra.gameObject);

            // Measure unrotated, scale to the real dimension, then rest the base on the floor at the anchor.
            var bounds = LocalBounds(holder, model);
            if (bounds.size == Vector3.zero) { Object.Destroy(holder.gameObject); return null; }
            float measured = fit == Fit.Height ? bounds.size.y : Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float scale = size / Mathf.Max(measured, .0001f);
            model.localScale *= scale;
            bounds = LocalBounds(holder, model);
            model.localPosition -= new Vector3(bounds.center.x, onFloor ? bounds.min.y : bounds.center.y, bounds.center.z);

            if (solid)
            {
                var box = holder.gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0, onFloor ? bounds.size.y * .5f : 0, 0);
                box.size = bounds.size;
            }
            holder.localPosition = position;
            holder.localRotation = Quaternion.Euler(0, yaw, 0);
            return holder;
        }

        static Bounds LocalBounds(Transform space, Transform model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            bool any = false;
            var result = new Bounds();
            foreach (var renderer in renderers)
            {
                var world = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = world.center + Vector3.Scale(world.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var local = space.InverseTransformPoint(corner);
                    if (!any) { result = new Bounds(local, Vector3.zero); any = true; }
                    else result.Encapsulate(local);
                }
            }
            return result;
        }
    }
}
