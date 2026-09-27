using System.Collections.Generic;
using EmergencyVR.Environment.Presentation;
using EmergencyVR.Patient;
using UnityEngine;
using UnityEngine.Rendering;

namespace EmergencyVR.Environment
{
    /// <summary>Cached visual modules layered over the original, regenerable TrainingRoom.</summary>
    public sealed class ScenarioEnvironmentPresenter : MonoBehaviour
    {
        readonly Dictionary<string, GameObject> modules = new Dictionary<string, GameObject>();
        readonly Dictionary<GameObject, bool> hidden = new Dictionary<GameObject, bool>();
        readonly Dictionary<Renderer, bool> hiddenTechnicalRenderers = new Dictionary<Renderer, bool>();
        readonly Dictionary<Camera, Color> cameraBackgrounds = new Dictionary<Camera, Color>();
        readonly List<Mesh> meshes = new List<Mesh>();
        GeneratedEnvironment original;
        PatientController patient;
        Vector3 originalPatientPosition;
        float originalTorsoHeight;
        EnvironmentPalette palette;
        EnvironmentPrefabLibrary prefabLibrary;
        Light roomLight;
        float originalIntensity;
        Color originalLightColor;
        Quaternion originalLightRotation;
        LightShadows originalShadows;
        AmbientMode originalAmbientMode;
        Color originalAmbientSky, originalAmbientEquator, originalAmbientGround;
        SphericalHarmonicsL2 originalAmbientProbe;
        bool initialized;
        bool ownsLight;

        public string CurrentEnvironment { get; private set; }
        public Transform PortableEquipmentAnchor { get; private set; }
        public int CachedEnvironmentCount => modules.Count;

        public static void Apply(string environment, PatientController patient)
        {
            if (patient == null) return;
            var presenter = FindFirstObjectByType<ScenarioEnvironmentPresenter>();
            if (presenter == null)
            {
                presenter = new GameObject("VITAL VR environment modules").AddComponent<ScenarioEnvironmentPresenter>();
                presenter.Initialize(patient);
            }
            presenter.Select(environment);
        }

        void Initialize(PatientController target)
        {
            patient = target;
            originalPatientPosition = target.transform.position;
            var torso = target.transform.Find("Body");
            originalTorsoHeight = torso == null ? originalPatientPosition.y : torso.position.y;
            original = FindFirstObjectByType<GeneratedEnvironment>();
            var renderer = original == null ? null : original.GetComponentInChildren<Renderer>(true);
            palette = new EnvironmentPalette(renderer == null ? null : renderer.sharedMaterial);
            prefabLibrary = Resources.Load<EnvironmentPrefabLibrary>("EnvironmentPrefabLibrary");
            if (original != null)
            {
                hidden[original.gameObject] = original.gameObject.activeSelf;
                foreach (var go in original.ownedObjects)
                    if (go != null && (go.name == "Lighting" || go.name == "PatientArea")) hidden[go] = go.activeSelf;
            }
            // Teleport colliders stay operational. Other technical props are disabled as complete objects,
            // avoiding invisible obstacles and invisible grabbables; all are restored for the technical case.
            foreach (var source in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (source.gameObject.scene != target.gameObject.scene) continue;
                if (source.gameObject.name == "Teleport pad") hiddenTechnicalRenderers[source] = source.enabled;
                else if (source.gameObject.name == "Grab me - Grip" || source.gameObject.name == "Object table")
                    hidden[source.gameObject] = source.gameObject.activeSelf;
            }
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                cameraBackgrounds[camera] = camera.backgroundColor;
            // Reuse the directional already lighting TrainingRoom; no light per cached module.
            foreach (var source in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (source.type != LightType.Directional || !source.enabled) continue;
                bool isHiddenWithRoom = false;
                foreach (var pair in hidden)
                    if (source.transform.IsChildOf(pair.Key.transform)) { isHiddenWithRoom = true; break; }
                if (!isHiddenWithRoom) { roomLight = source; break; }
            }
            if (roomLight == null)
            {
                var source = new GameObject("Shared environment key light");
                source.transform.SetParent(transform, false);
                roomLight = source.AddComponent<Light>();
                roomLight.type = LightType.Directional;
                roomLight.intensity = .85f;
                roomLight.shadows = LightShadows.None;
                ownsLight = true;
            }
            originalIntensity = roomLight.intensity;
            originalLightColor = roomLight.color;
            originalLightRotation = roomLight.transform.rotation;
            originalShadows = roomLight.shadows;
            originalAmbientMode = RenderSettings.ambientMode;
            originalAmbientSky = RenderSettings.ambientSkyColor;
            originalAmbientEquator = RenderSettings.ambientEquatorColor;
            originalAmbientGround = RenderSettings.ambientGroundColor;
            originalAmbientProbe = RenderSettings.ambientProbe;
            initialized = true;
        }

        public void Select(string id)
        {
            if (!initialized) return;
            if (!string.IsNullOrEmpty(id) && id != "gym" && id != "football" && id != "mall" && id != "dental")
            {
                Debug.LogWarning("Unknown environment '" + id + "'; showing the original TrainingRoom.");
                id = null;
            }
            CurrentEnvironment = id;
            foreach (var module in modules.Values) module.SetActive(false);
            foreach (var pair in hidden)
                if (pair.Key != null) pair.Key.SetActive(string.IsNullOrEmpty(id) && pair.Value);
            foreach (var pair in hiddenTechnicalRenderers)
                if (pair.Key != null) pair.Key.enabled = string.IsNullOrEmpty(id) && pair.Value;
            patient.transform.position = originalPatientPosition;
            PortableEquipmentAnchor = null;
            if (string.IsNullOrEmpty(id)) { RestoreLighting(); return; }
            if (!modules.TryGetValue(id, out var root))
            {
                root = Build(id);
                modules.Add(id, root);
            }
            root.SetActive(true);
            PortableEquipmentAnchor = root.transform.Find("PortableEquipmentAnchor");
            if (id != "dental")
                patient.transform.position = originalPatientPosition + Vector3.up * (.16f - originalTorsoHeight);
            ApplyLighting(id);
            // Capture reflections once the module and its equipment are visible.
            foreach (var probe in root.GetComponentsInChildren<ReflectionProbe>()) probe.RenderProbe();
        }

        GameObject Build(string id)
        {
            var root = new GameObject("Environment_" + id);
            root.transform.SetParent(transform, false);
            var builder = new EnvironmentModuleBuilder(root.transform, palette, meshes);
            builder.Build(id);
            ReuseEquipment(root.transform, id);
            var anchor = new GameObject("PortableEquipmentAnchor").transform;
            anchor.SetParent(root.transform, false);
            anchor.localPosition = new Vector3(-1.8f, .98f, 3.75f);
            return root;
        }

        void ReuseEquipment(Transform root, string id)
        {
            if (original == null && prefabLibrary == null) return;
            var reused = new GameObject("Reused medical equipment").transform;
            reused.SetParent(root, false);
            // Carry the support furniture with every supported object; the old modules floated the defibrillator/packs.
            Copy("Furniture/MedicalCart", new Vector3(-1.8f, 0, 3.75f));
            Copy("MedicalEquipment/DefibrillatorPlaceholder", new Vector3(-1.8f, .955f, 3.75f));
            if (id != "dental") return;
            Copy("Furniture/MedicalCabinet", new Vector3(-2.65f, 0, 4.67f));
            Copy("Furniture/Stool", new Vector3(2.85f, 0, 3.8f));
            Copy("Furniture/SideTable", new Vector3(2.8f, 0, 1.4f));
            Copy("MedicalEquipment/Supplies", new Vector3(2.78f, .8f, 1.4f));
            Copy("MedicalEquipment/PatientMonitor", new Vector3(2.8f, 0, 2.65f), 12);
            Copy("MedicalEquipment/OxygenTank", new Vector3(3.03f, 0, 4.5f));

            void Copy(string path, Vector3 position, float yaw = 0)
            {
                var prefab = prefabLibrary == null ? null : prefabLibrary.Find(path);
                var source = original == null ? null : original.transform.Find(path);
                if (prefab == null && source == null) return;
                // Prefab assets contain local meshes; cloning a build-time static-batched scene renderer can retain world vertices.
                var copy = Instantiate(prefab != null ? prefab : source.gameObject, reused);
                copy.name = path.Substring(path.LastIndexOf('/') + 1);
                copy.transform.localPosition = position;
                copy.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                copy.SetActive(true);
            }
        }

        void ApplyLighting(string id)
        {
            var outdoor = id == "football";
            if (ownsLight) roomLight.gameObject.SetActive(true);
            // Restrained hemispherical fill provides depth without extra realtime lights or transparent surfaces.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = outdoor ? new Color(.63f, .73f, .82f) : new Color(.66f, .70f, .73f);
            RenderSettings.ambientEquatorColor = outdoor ? new Color(.36f, .44f, .48f) : new Color(.39f, .43f, .44f);
            RenderSettings.ambientGroundColor = outdoor ? new Color(.20f, .25f, .18f) : new Color(.22f, .25f, .25f);
            // Runtime Trilight colours do not rebuild the baked ambient SH probe used by these URP renderers.
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(outdoor ? new Color(.46f, .53f, .58f) : new Color(.48f, .51f, .52f));
            RenderSettings.ambientProbe = probe;
            foreach (var pair in cameraBackgrounds)
                if (pair.Key != null) pair.Key.backgroundColor = outdoor ? new Color(.50f, .67f, .77f) : pair.Value;
            bool gym = id == "gym";
            roomLight.intensity = outdoor ? .92f : gym ? 1.5f : .7f;
            roomLight.color = outdoor ? new Color(1, .96f, .87f) : gym ? new Color(1, .95f, .86f) : new Color(.94f, .98f, 1);
            // Gym: afternoon sun through the street windows (+X); walls and ceiling shade the rest of the room.
            roomLight.transform.rotation = Quaternion.Euler(outdoor ? 48 : gym ? 36 : 62, outdoor ? -38 : gym ? -100 : -25, 0);
            // Other interiors have no openings, so a shadowing sun would leave them dark.
            roomLight.shadows = outdoor || gym ? LightShadows.Soft : LightShadows.None;
            roomLight.shadowStrength = .85f;
        }

        void RestoreLighting()
        {
            if (roomLight != null)
            {
                roomLight.intensity = originalIntensity;
                roomLight.color = originalLightColor;
                roomLight.transform.rotation = originalLightRotation;
                roomLight.shadows = originalShadows;
                if (ownsLight) roomLight.gameObject.SetActive(false);
            }
            RenderSettings.ambientMode = originalAmbientMode;
            RenderSettings.ambientSkyColor = originalAmbientSky;
            RenderSettings.ambientEquatorColor = originalAmbientEquator;
            RenderSettings.ambientGroundColor = originalAmbientGround;
            RenderSettings.ambientProbe = originalAmbientProbe;
            foreach (var pair in cameraBackgrounds) if (pair.Key != null) pair.Key.backgroundColor = pair.Value;
        }

        public static void GetCaptureView(string id, out Vector3 position, out Vector3 lookAt)
        {
            position = id == "football" ? new Vector3(-3.2f, 2.3f, -3.9f) : new Vector3(-2.8f, 1.9f, -2.55f);
            lookAt = id == "football" ? new Vector3(1.4f, 1.3f, 6) : new Vector3(.5f, 1.1f, 2.25f);
        }

        void OnDestroy()
        {
            if (initialized)
            {
                RestoreLighting();
                // Select(null) restores the technical room. During scene teardown, re-enabling an
                // XR interactable can recreate its already destroyed XRInteractionManager.
                foreach (var pair in hiddenTechnicalRenderers) if (pair.Key != null) pair.Key.enabled = pair.Value;
                if (patient != null) patient.transform.position = originalPatientPosition;
            }
            palette?.Dispose();
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
        }
    }
}

