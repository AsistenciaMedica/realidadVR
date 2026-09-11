using System;
using System.IO;
using System.Linq;
using EmergencyVR.Core;
using EmergencyVR.Evaluation;
using EmergencyVR.Patient;
using EmergencyVR.Scenarios;
using EmergencyVR.UI;
using EmergencyVR.XR;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;
using Object = UnityEngine.Object;

namespace EmergencyVR.Editor
{
    public static class DemoProjectBuilder
    {
        public const string Root = "Assets/_Project";
        public const string BootstrapPath = Root + "/Scenes/Bootstrap/Bootstrap.unity";
        public const string TrainingPath = Root + "/Scenes/Training/TrainingRoom.unity";
        public const string ScenarioPath = Root + "/ScriptableObjects/Scenarios/TrainingRoom.asset";
        public const string CasePath = Root + "/ScriptableObjects/Cases/TechnicalDemo.asset";
        const string StarterRoot = "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets";
        const string RigPath = StarterRoot + "/Prefabs/XR Origin (XR Rig).prefab";
        const int TeleportLayer = unchecked((int)0x80000000); // Official Starter Assets interaction bit 31.

        [MenuItem("Emergency VR/1 - Import Starter Assets")]
        public static void ImportStarterAssets()
        {
            var samples = Sample.FindByPackage("com.unity.xr.interaction.toolkit", "3.3.2");
            foreach (var sample in samples)
            {
                if (sample.displayName != "Starter Assets") continue;
                if (sample.isImported) { Debug.Log("Starter Assets already imported; preserved."); return; }
                if (!sample.Import(Sample.ImportOptions.None)) throw new InvalidOperationException("Starter Assets import failed.");
                Debug.Log("Wait for script compilation, then run Emergency VR > 2 - Generate demo.");
                return;
            }
            throw new InvalidOperationException("XRI 3.3.2 not resolved. Check Window > Package Management > Package Manager.");
        }

        [MenuItem("Emergency VR/2 - Generate demo")]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before generating assets.");
            var officialRig = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if (officialRig == null)
                throw new InvalidOperationException("Run Emergency VR > 1 - Import Starter Assets, wait for compilation, then retry.");
            EnsureFolders();
            ConfigureRendering();
            var clinicalCase = GetOrCreateCase();
            var scenario = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(ScenarioPath);
            if (scenario == null)
            {
                scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
                scenario.defaultCase = clinicalCase;
                scenario.scenePath = TrainingPath;
                AssetDatabase.CreateAsset(scenario, ScenarioPath);
            }
            // Each scene is created additively, saved and closed; existing user scenes stay open.
            if (!File.Exists(TrainingPath)) CreateTrainingRoom(officialRig, scenario);
            else Debug.Log("TrainingRoom already exists; preserved.");
            if (!File.Exists(BootstrapPath)) CreateBootstrap(scenario);
            else Debug.Log("Bootstrap already exists; preserved.");
            var extraScenes = EditorBuildSettings.scenes.Where(s => s.path != BootstrapPath && s.path != TrainingPath);
            EditorBuildSettings.scenes = new[] {
                new EditorBuildSettingsScene(BootstrapPath, true),
                new EditorBuildSettingsScene(TrainingPath, true)
            }.Concat(extraScenes).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Demo assets generated. Next: Emergency VR > 3 - Configure Android OpenXR. " +
                "Open Bootstrap from the Project window to run the demo. Hardware validation is still required.");
        }

        public static void EnsureFolders()
        {
            foreach (var path in new[] {
                "Art", "Audio", "Materials", "Settings", "Prefabs/Patient", "Prefabs/Medical",
                "Prefabs/Environment", "Prefabs/UI", "Prefabs/XR", "Scenes/Bootstrap",
                "Scenes/Training", "Scenes/Environments", "ScriptableObjects/Cases", "ScriptableObjects/Scenarios"
            }) Directory.CreateDirectory(Root + "/" + path);
            AssetDatabase.Refresh();
        }

        static void ConfigureRendering()
        {
            const string path = Root + "/Settings/QuestURP.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (pipeline == null)
            {
                const string rendererPath = Root + "/Settings/QuestRenderer.asset";
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(renderer, rendererPath);
                }
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.supportsHDR = false;
                pipeline.msaaSampleCount = 4;
                pipeline.renderScale = 1f;
                pipeline.supportsCameraDepthTexture = false;
                pipeline.supportsCameraOpaqueTexture = false;
                pipeline.shadowDistance = 0f;
                AssetDatabase.CreateAsset(pipeline, path);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            // All quality levels use the same initial mobile pipeline.
            var currentQuality = QualitySettings.GetQualityLevel();
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(currentQuality, false);
        }

        static ClinicalCaseDefinition GetOrCreateCase()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ClinicalCaseDefinition>(CasePath);
            if (definition != null) return definition;
            definition = ScriptableObject.CreateInstance<ClinicalCaseDefinition>();
            definition.caseId = "technical-demo-v1";
            definition.displayName = "Prueba técnica de estados";
            definition.description = "Transiciones arbitrarias para probar software. No es un protocolo clínico.";
            definition.isTechnicalDemo = true;
            definition.clinicallyApproved = false;
            definition.initialState = PatientState.UnconsciousBreathing;
            definition.steps.Add(new CaseStepData {
                actionId = "demo.inspect", label = "Selecciona el paciente con Grip",
                fromState = PatientState.UnconsciousBreathing, toState = PatientState.Recovering,
                points = 50, deadlineSeconds = 0
            });
            definition.steps.Add(new CaseStepData {
                actionId = "demo.confirm", label = "Pulsa Transición demo en el panel",
                fromState = PatientState.Recovering, toState = PatientState.Recovered,
                points = 50, deadlineSeconds = 0
            });
            definition.ToDomain(); // Fail before saving invalid data.
            AssetDatabase.CreateAsset(definition, CasePath);
            return definition;
        }

        static void CreateBootstrap(ScenarioDefinition scenario)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var loader = new GameObject("Bootstrap").AddComponent<BootstrapLoader>();
            loader.Configure(scenario);
            var camera = new GameObject("Loading Camera", typeof(Camera), typeof(AudioListener));
            camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camera.GetComponent<Camera>().backgroundColor = new Color(0.025f, 0.045f, 0.075f);
            SaveAndClose(scene, BootstrapPath);
        }

        static void CreateTrainingRoom(GameObject officialRig, ScenarioDefinition scenario)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.65f, 0.68f, 0.75f);
            RenderSettings.skybox = null;
            var light = new GameObject("Room light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            var floorMaterial = Material("Floor", new Color(0.12f, 0.17f, 0.23f));
            var wallMaterial = Material("Wall", new Color(0.35f, 0.43f, 0.5f));
            var accentMaterial = Material("Teleport", new Color(0.05f, 0.52f, 0.6f));
            Primitive("Floor", PrimitiveType.Cube, new Vector3(0, -0.1f, 1), new Vector3(8, 0.2f, 8), floorMaterial);
            Primitive("Back wall", PrimitiveType.Cube, new Vector3(0, 1.5f, 5), new Vector3(8, 3, 0.15f), wallMaterial);
            Primitive("Left wall", PrimitiveType.Cube, new Vector3(-4, 1.5f, 1), new Vector3(0.15f, 3, 8), wallMaterial);
            Primitive("Right wall", PrimitiveType.Cube, new Vector3(4, 1.5f, 1), new Vector3(0.15f, 3, 8), wallMaterial);
            new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(officialRig, scene);
            rig.name = "Quest Controller Rig";
            ConfigureRig(rig);
            // Save reusable project variant before positioning the scene instance.
            SavePrefabIfMissing(rig, Root + "/Prefabs/XR/QuestControllerRig.prefab");
            rig.transform.position = new Vector3(0, 0, -1.8f);
            var teleport = rig.GetComponentInChildren<TeleportationProvider>(true);
            if (teleport == null) throw new InvalidOperationException("Official rig has no TeleportationProvider.");
            foreach (var position in new[] { new Vector3(0, 0.01f, -1.8f), new Vector3(-1.5f, 0.01f, 0),
                new Vector3(1.5f, 0.01f, 0.5f), new Vector3(0, 0.01f, 3.1f) })
            {
                var pad = Primitive("Teleport pad", PrimitiveType.Cube, position, new Vector3(1.1f, 0.02f, 1.1f), accentMaterial);
                var area = pad.AddComponent<TeleportationArea>();
                area.interactionLayers = TeleportLayer;
                area.teleportationProvider = teleport;
                area.matchOrientation = MatchOrientation.WorldSpaceUp;
                area.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
            }

            var systems = new GameObject("Training Systems");
            var evaluation = systems.AddComponent<EvaluationManager>();
            var manager = systems.AddComponent<ScenarioManager>();
            var patientRoot = new GameObject("Patient Placeholder");
            var body = Primitive("Body", PrimitiveType.Capsule, new Vector3(1.55f, 1.05f, 2.1f),
                new Vector3(0.5f, 0.75f, 0.5f), Material("Patient", new Color(0.2f, 0.55f, 0.85f)));
            body.transform.rotation = Quaternion.Euler(90f, 0, 0);
            body.transform.SetParent(patientRoot.transform, true);
            var head = Primitive("Head", PrimitiveType.Sphere, new Vector3(1.55f, 1.05f, 2.95f),
                Vector3.one * 0.4f, wallMaterial);
            head.transform.SetParent(patientRoot.transform, true);
            var patient = patientRoot.AddComponent<PatientController>();
            patient.Configure(body.GetComponent<Renderer>());
            var interactable = patientRoot.AddComponent<XRSimpleInteractable>();
            interactable.colliders.Clear();
            interactable.colliders.Add(body.GetComponent<Collider>());
            interactable.colliders.Add(head.GetComponent<Collider>());
            var interaction = patientRoot.AddComponent<PatientInteraction>();
            // Prefab contains no cross-scene manager reference; scene instance gets it below.
            SavePrefabIfMissing(patientRoot, Root + "/Prefabs/Patient/PatientPlaceholder.prefab");
            interaction.Configure(manager);
            manager.Configure(scenario, patient, evaluation);
            Primitive("Patient platform", PrimitiveType.Cube, new Vector3(1.55f, 0.65f, 2.1f),
                new Vector3(1, 0.45f, 2.3f), floorMaterial);
            Primitive("Object table", PrimitiveType.Cube, new Vector3(-1.5f, 0.65f, 1.6f),
                new Vector3(1.2f, 0.3f, 0.75f), wallMaterial);
            var cube = Primitive("Grab me - Grip", PrimitiveType.Cube, new Vector3(-1.5f, 1.05f, 1.6f),
                Vector3.one * 0.18f, Material("Grab cube", new Color(0.95f, 0.55f, 0.13f)));
            var rigidbody = cube.AddComponent<Rigidbody>();
            rigidbody.mass = 0.2f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            cube.AddComponent<XRGrabInteractable>();
            cube.AddComponent<ResetDroppedObject>();
            SavePrefabIfMissing(cube, Root + "/Prefabs/Medical/InteractionCube.prefab");

            CreatePanel(manager, evaluation, rig.GetComponentInChildren<Camera>(true));
            SaveAndClose(scene, TrainingPath);
        }

        static void ConfigureRig(GameObject rig)
        {
            var origin = rig.GetComponent<XROrigin>();
            if (origin == null) throw new InvalidOperationException("Missing XROrigin on official prefab.");
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            origin.CameraYOffset = 1.6f; // Fallback only when runtime uses device origin.
            var managerCount = 0;
            // Sample assemblies do not exist until imported. Use verified serialized fields so
            // this editor assembly can compile BEFORE importing samples; fail loudly on changes.
            foreach (var behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) throw new InvalidOperationException("Missing script on XR rig.");
                if (behaviour.GetType().FullName != "UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets.ControllerInputActionManager") continue;
                var serialized = new SerializedObject(behaviour);
                var motion = serialized.FindProperty("m_SmoothMotionEnabled");
                var turn = serialized.FindProperty("m_SmoothTurnEnabled");
                if (motion == null || turn == null) throw new InvalidOperationException("Starter Assets schema changed; review locomotion configuration.");
                motion.boolValue = false;
                turn.boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                managerCount++;
            }
            if (managerCount != 2) throw new InvalidOperationException("Expected two controller input action managers.");
            foreach (var turn in rig.GetComponentsInChildren<SnapTurnProvider>(true))
            {
                turn.turnAmount = 30f;
                turn.enableTurnAround = false;
            }
            // Disable extra sample locomotion features (jump / grab movement / climbing).
            foreach (var child in rig.GetComponentsInChildren<Transform>(true))
                if (child.name == "Jump" || child.name == "Grab Move" || child.name == "Climb" || child.name == "Climb Teleport")
                    child.gameObject.SetActive(false);
            var camera = origin.Camera;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 30f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.045f, 0.075f);
        }

        static void CreatePanel(ScenarioManager manager, EvaluationManager evaluation, Camera camera)
        {
            new GameObject("XR EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
            var root = new GameObject("Training VR Panel", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1000, 680);
            rect.position = new Vector3(0, 1.65f, 1.05f);
            rect.localScale = Vector3.one * 0.0017f;
            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(root.transform, false);
            var bg = background.GetComponent<RectTransform>();
            bg.anchorMin = Vector2.zero;
            bg.anchorMax = Vector2.one;
            bg.sizeDelta = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0.025f, 0.055f, 0.085f, 0.97f);
            Label(root.transform, "Title", "EMERGENCY VR  /  DEMO TÉCNICA", new Vector2(0, 270), new Vector2(930, 60), 34);
            Label(root.transform, "Notice", "Prueba de software · Sin protocolo clínico validado", new Vector2(0, 211), new Vector2(930, 45), 25);
            Label(root.transform, "Controls", "Grip: agarrar / paciente · Trigger: botones\nStick arriba: apuntar teleport; soltar: viajar · Lateral: giro 30°",
                new Vector2(0, 136), new Vector2(930, 92), 25);
            var status = Label(root.transform, "Status", "Inicia un caso demo.", new Vector2(0, -28), new Vector2(930, 230), 28);
            var start = Button(root.transform, "Iniciar caso demo", new Vector2(-315, -230));
            var transition = Button(root.transform, "Transición demo", new Vector2(0, -230));
            var finish = Button(root.transform, "Finalizar", new Vector2(315, -230));
            var panel = root.AddComponent<TrainingPanel>();
            panel.Configure(manager, evaluation, status, start, transition, finish);
        }

        static Text Label(Transform parent, string name, string content, Vector2 position, Vector2 size, int fontSize)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(Text));
            label.transform.SetParent(parent, false);
            var rect = label.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        static Button Button(Transform parent, string text, Vector2 position)
        {
            var root = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(290, 86);
            var background = root.GetComponent<Image>();
            background.color = new Color(0.05f, 0.4f, 0.49f);
            var button = root.GetComponent<Button>();
            button.targetGraphic = background;
            Label(root.transform, "Label", text, Vector2.zero, new Vector2(275, 80), 26);
            return button;
        }

        static GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var instance = GameObject.CreatePrimitive(type);
            instance.name = name;
            instance.transform.position = position;
            instance.transform.localScale = scale;
            instance.GetComponent<Renderer>().sharedMaterial = material;
            return instance;
        }

        static Material Material(string name, Color color)
        {
            var path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) throw new InvalidOperationException("URP shader not available. Check Package Manager.");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void SavePrefabIfMissing(GameObject root, string path)
        {
            if (!File.Exists(path) && PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                throw new IOException("Could not save prefab: " + path);
        }

        static void SaveAndClose(Scene scene, string path)
        {
            if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Could not save scene: " + path);
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
