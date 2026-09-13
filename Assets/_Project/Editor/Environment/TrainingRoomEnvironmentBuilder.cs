using System;
using System.Collections.Generic;
using System.Linq;
using EmergencyVR.Environment;
using EmergencyVR.Patient;
using EmergencyVR.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EmergencyVR.Editor.Environment
{
    public static class TrainingRoomEnvironmentBuilder
    {
        public const string Menu = "Emergency VR/Environment/";

        [MenuItem(Menu + "Generate Training Room")]
        public static void GenerateTrainingRoom() { EditTrainingScene(Generate); }

        [MenuItem(Menu + "Clear Generated Environment")]
        public static void ClearGeneratedEnvironment() { EditTrainingScene(Clear); }

        static void EditTrainingScene(Action<Scene> action)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before changing the environment.");
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(DemoProjectBuilder.TrainingPath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(DemoProjectBuilder.TrainingPath, OpenSceneMode.Additive);
            bool wasDirty = scene.isDirty;
            try
            {
                SceneManager.SetActiveScene(scene);
                action(scene);
                // Do not silently save unrelated unsaved user changes.
                if (!wasDirty)
                {
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save TrainingRoom.");
                }
                else Debug.Log("Environment changed. TrainingRoom had unsaved changes; save the scene when ready.");
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        public static void Generate(Scene scene)
        {
            ValidateScene(scene);
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Generate Training Room environment");
            var previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try
            {
                // Clear before upgrading prefab visuals: newly imported children must not be mistaken
                // for user additions in the previous generation's ownership manifest.
                Clear(scene);
                var factory = new EnvironmentPrefabFactory();
                var prefabs = new Dictionary<string, GameObject>();
                foreach (var pair in new[] {
                    "Architecture/Floor", "Architecture/Ceiling", "Architecture/Wall", "Architecture/Skirting",
                    "Architecture/Door", "Architecture/Window", "Architecture/CeilingFixture", "Architecture/WallFinish",
                    "Furniture/HospitalBed", "Furniture/MedicalCabinet", "Furniture/MedicalCart",
                    "Furniture/Stool", "Furniture/SideTable", "Medical/PatientMonitor", "Medical/IVStand",
                    "Medical/OxygenTank", "Medical/DefibrillatorPlaceholder", "Props/Supplies", "Props/WallServicePanel", "Props/RoomDetails" })
                {
                    var split = pair.Split('/');
                    prefabs.Add(split[1], factory.Get(split[0], split[1]));
                }
                var originalRoots = new HashSet<GameObject>(scene.GetRootGameObjects());
                var environment = Root(scene,"Environment");
                var manifest = Undo.AddComponent<GeneratedEnvironment>(environment);
                manifest.previousAmbientMode = RenderSettings.ambientMode;
                manifest.previousAmbientLight = RenderSettings.ambientLight;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.7f,.73f,.72f);
                PolishExistingPresentation(scene,manifest);

                var architecture = Group(environment.transform,"Architecture");
                var walls = Group(architecture,"Walls");
                var doors = Group(architecture,"Doors");
                var windows = Group(architecture,"Windows");
                var trim = Group(architecture,"Skirting");
                var furniture = Group(environment.transform,"Furniture");
                var equipment = Group(environment.transform,"MedicalEquipment");
                var decoration = Group(environment.transform,"Decoration");
                var lighting = Root(scene,"Lighting").transform;
                var patientArea = Root(scene,"PatientArea").transform;

                GameObject Place(string prefab, Transform parent, Vector3 position, Vector3? scale = null, float yaw = 0, string name = null)
                {
                    var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[prefab],scene);
                    obj.name = name ?? prefab;
                    obj.transform.SetParent(parent,false);
                    obj.transform.localPosition = position;
                    obj.transform.localRotation = Quaternion.Euler(0,yaw,0);
                    if (scale.HasValue) obj.transform.localScale = scale.Value;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);
                    Undo.RegisterCreatedObjectUndo(obj,"Place " + obj.name);
                    return obj;
                }

                // Interior bounds x [-3.5,3.5], z [-3,5], y [0,3]. Existing rig/patient/pads stay put.
                Place("Floor",architecture,new Vector3(0,0,1));
                Place("Ceiling",architecture,new Vector3(0,0,1));
                Place("WallFinish",architecture,Vector3.zero);
                Place("Wall",walls,new Vector3(0,1.5f,5.075f),new Vector3(7,3,.15f),name:"Back");
                Place("Wall",walls,new Vector3(-3.575f,1.5f,1),new Vector3(.15f,3,8.3f),name:"Left");
                // Front opening centered on x=0. Window opening on the right at z=-.9, y=1.65.
                Place("Wall",walls,new Vector3(-2.05f,1.5f,-3.075f),new Vector3(2.9f,3,.15f),name:"FrontLeft");
                Place("Wall",walls,new Vector3(2.05f,1.5f,-3.075f),new Vector3(2.9f,3,.15f),name:"FrontRight");
                Place("Wall",walls,new Vector3(0,2.55f,-3.075f),new Vector3(1.2f,.9f,.15f),name:"FrontLintel");
                Place("Door",doors,new Vector3(0,0,-3));
                Place("Wall",walls,new Vector3(3.575f,1.5f,-2.325f),new Vector3(.15f,3,1.35f),name:"RightFront");
                Place("Wall",walls,new Vector3(3.575f,1.5f,2.425f),new Vector3(.15f,3,5.15f),name:"RightBack");
                Place("Wall",walls,new Vector3(3.575f,.6f,-.9f),new Vector3(.15f,1.2f,1.5f),name:"WindowSillWall");
                Place("Wall",walls,new Vector3(3.575f,2.55f,-.9f),new Vector3(.15f,.9f,1.5f),name:"WindowLintelWall");
                Place("Window",windows,new Vector3(3.55f,1.65f,-.9f),new Vector3(1.15f,1,1),90);
                Place("Skirting",trim,new Vector3(0,.08f,4.985f),new Vector3(7,.16f,.03f),name:"BackTrim");
                for(int i=-1;i<=1;i+=2)
                {
                    Place("Skirting",trim,new Vector3(i*3.485f,.08f,1),new Vector3(.03f,.16f,8),name:"SideTrim_"+i);
                    Place("Skirting",trim,new Vector3(i*2.05f,.08f,-2.985f),new Vector3(2.9f,.16f,.03f),name:"FrontTrim_"+i);
                }

                Place("HospitalBed",furniture,new Vector3(1.55f,0,2.1f));
                Place("SideTable",furniture,new Vector3(-1.5f,0,1.6f));
                Place("MedicalCabinet",furniture,new Vector3(-2.45f,0,4.68f));
                Place("MedicalCart",furniture,new Vector3(-2.78f,0,2.85f),yaw:270);
                Place("Stool",furniture,new Vector3(2.7f,0,3.98f));
                Place("PatientMonitor",equipment,new Vector3(2.58f,0,2.98f),yaw:25);
                Place("IVStand",equipment,new Vector3(2.6f,0,1.74f));
                Place("OxygenTank",equipment,new Vector3(3.08f,0,4.38f));
                Place("DefibrillatorPlaceholder",equipment,new Vector3(-2.78f,.955f,2.85f),yaw:270);
                Place("Supplies",equipment,new Vector3(-1.87f,.8f,1.75f));
                Place("WallServicePanel",decoration,new Vector3(1.55f,1.35f,4.91f));
                Place("RoomDetails",decoration,Vector3.zero);

                Group(patientArea,"PatientSpawnPoint").position = new Vector3(1.55f,1.05f,2.1f);
                var zone = Group(patientArea,"TreatmentZone");
                zone.position = new Vector3(1.55f,0,2.1f);
                // Visual boundary only: never a trigger or interaction target.
                for(int i=-1;i<=1;i+=2)
                {
                    Place("Skirting",zone,new Vector3(i*.8f,.004f,0),new Vector3(.025f,.008f,2.85f),name:"ZoneSide_"+i);
                    Place("Skirting",zone,new Vector3(0,.004f,i*1.425f),new Vector3(1.6f,.008f,.025f),name:"ZoneEnd_"+i);
                }
                for(int i=0;i<3;i++) Place("CeilingFixture",lighting,new Vector3(0,2.94f,-1.6f+i*2.65f));
                for(int i=0;i<2;i++)
                {
                    var light = Group(lighting,"BakedCeilingLight_"+i).gameObject.AddComponent<Light>();
                    light.type = LightType.Point; light.lightmapBakeType = LightmapBakeType.Baked;
                    light.transform.position = new Vector3(0,2.65f,-.4f+i*3.3f);
                    light.range = 5; light.intensity = .35f; light.color = new Color(.94f,.98f,1);
                    light.shadows = LightShadows.None;
                }
                if (!originalRoots.SelectMany(r=>r.GetComponentsInChildren<Light>()).Any(l=>l.enabled && l.type==LightType.Directional))
                {
                    var fill = Group(lighting,"RoomFill").gameObject.AddComponent<Light>();
                    fill.type = LightType.Directional; fill.intensity = 1; fill.shadows = LightShadows.None;
                    fill.transform.rotation = Quaternion.Euler(45,-30,0);
                }
                PreserveLegacyPlaceholders(scene,manifest);
                foreach(var root in scene.GetRootGameObjects().Where(r=>!originalRoots.Contains(r)))
                    manifest.ownedObjects.AddRange(root.GetComponentsInChildren<Transform>(true).Select(t=>t.gameObject));
                EditorUtility.SetDirty(manifest);
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log("Polished TrainingRoom generated: 7 x 8 x 3 m, 20 reusable prefabs. XR, UI and patient references preserved.");
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
            finally { if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }

        public static void Clear(Scene scene)
        {
            ValidateScene(scene);
            var previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try
            {
                foreach(var manifest in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GeneratedEnvironment>(true)).ToArray())
                {
                    if(manifest.generatorId != GeneratedEnvironment.GeneratorId) continue;
                    var owned = new HashSet<GameObject>(manifest.ownedObjects.Where(o=>o!=null && o.scene==scene));
                    // Refuse malformed/copied manifests instead of adopting arbitrary roots by name.
                    if(!owned.Contains(manifest.gameObject)) throw new InvalidOperationException("Invalid environment ownership manifest.");
                    foreach(var saved in manifest.preservedObjects)
                        if(saved.target!=null)
                        {
                            Undo.RecordObject(saved.target,"Restore original placeholder");
                            saved.target.SetActive(saved.activeSelf);
                        }
                    RenderSettings.ambientMode = manifest.previousAmbientMode;
                    RenderSettings.ambientLight = manifest.previousAmbientLight;
                    foreach(var saved in manifest.presentation)
                        if(saved.target!=null)
                        {
                            Undo.RecordObject(saved.target,"Restore presentation");
                            saved.target.localPosition=saved.localPosition;
                            saved.target.localRotation=saved.localRotation;
                            saved.target.localScale=saved.localScale;
                        }
                    foreach(var saved in manifest.materialOverrides)
                        if(saved.target!=null)
                        {
                            Undo.RecordObject(saved.target,"Restore renderer materials");
                            saved.target.sharedMaterials=saved.materials;
                        }
                    if(manifest.existingRoomLight!=null)
                    {
                        Undo.RecordObject(manifest.existingRoomLight,"Restore room light");
                        manifest.existingRoomLight.intensity=manifest.previousLightIntensity;
                    }
                    // User-added objects (even nested beneath generated visuals) always survive clear.
                    foreach(var obj in owned)
                        foreach(Transform child in obj.transform.Cast<Transform>().ToArray())
                            if(!owned.Contains(child.gameObject)) Undo.SetTransformParent(child,null,"Preserve user object");
                    foreach(var obj in owned.Where(o=>o.transform.parent==null || !owned.Contains(o.transform.parent.gameObject)).ToArray())
                        Undo.DestroyObjectImmediate(obj);
                }
                EditorSceneManager.MarkSceneDirty(scene);
            }
            finally { if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }

        static void ValidateScene(Scene scene)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if(!scene.IsValid() || !scene.isLoaded || scene.path != DemoProjectBuilder.TrainingPath)
                throw new InvalidOperationException("Environment generation is restricted to the existing TrainingRoom scene.");
            if(scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PatientController>(true)).Count()!=1)
                throw new InvalidOperationException("Expected one existing patient. Restore TrainingRoom before generating.");
        }

        static void PolishExistingPresentation(Scene scene,GeneratedEnvironment manifest)
        {
            var roots=scene.GetRootGameObjects();
            var panel=roots.SelectMany(r=>r.GetComponentsInChildren<TrainingPanel>(true)).SingleOrDefault();
            if(panel!=null)
            {
                var t=panel.transform;
                manifest.presentation.Add(new PreservedPresentation {target=t,localPosition=t.localPosition,localRotation=t.localRotation,localScale=t.localScale});
                Undo.RecordObject(t,"Place existing UI on wall");
                var rect=t as RectTransform;
                if(rect!=null) rect.anchoredPosition=new Vector2(-3.445f,1.55f);
                t.localPosition=new Vector3(t.localPosition.x,t.localPosition.y,.85f);
                t.localRotation=Quaternion.Euler(0,-90,0);
                t.localScale=Vector3.one*.00115f;
            }
            var padMaterial=AssetDatabase.LoadAssetAtPath<Material>(EnvironmentPrefabFactory.Materials+"/M_Teleport.mat");
            foreach(var pad in roots.SelectMany(r=>r.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea>(true)))
            {
                var renderer=pad.GetComponent<Renderer>();
                if(renderer==null) continue;
                manifest.materialOverrides.Add(new PreservedMaterials {target=renderer,materials=renderer.sharedMaterials});
                Undo.RecordObject(renderer,"Desaturate teleport pad");
                renderer.sharedMaterial=padMaterial;
            }
            var roomLight=roots.SelectMany(r=>r.GetComponentsInChildren<Light>()).FirstOrDefault(l=>l.type==LightType.Directional && l.enabled);
            if(roomLight!=null)
            {
                manifest.existingRoomLight=roomLight; manifest.previousLightIntensity=roomLight.intensity;
                Undo.RecordObject(roomLight,"Balance clinical lighting");
                roomLight.intensity=.8f;
            }
        }

        static GameObject Root(Scene scene,string name)
        {
            var obj = new GameObject(scene.GetRootGameObjects().Any(r=>r.name==name) ? name + " (Generated)" : name);
            SceneManager.MoveGameObjectToScene(obj,scene);
            Undo.RegisterCreatedObjectUndo(obj,"Create " + obj.name);
            return obj;
        }

        static Transform Group(Transform parent,string name)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent,false);
            Undo.RegisterCreatedObjectUndo(obj,"Create " + name);
            return obj.transform;
        }

        static void PreserveLegacyPlaceholders(Scene scene,GeneratedEnvironment manifest)
        {
            var expected = new[] {
                ("Floor",new Vector3(0,-.1f,1),new Vector3(8,.2f,8)),
                ("Back wall",new Vector3(0,1.5f,5),new Vector3(8,3,.15f)),
                ("Left wall",new Vector3(-4,1.5f,1),new Vector3(.15f,3,8)),
                ("Right wall",new Vector3(4,1.5f,1),new Vector3(.15f,3,8)),
                ("Patient platform",new Vector3(1.55f,.65f,2.1f),new Vector3(1,.45f,2.3f)),
                ("Object table",new Vector3(-1.5f,.65f,1.6f),new Vector3(1.2f,.3f,.75f)) };
            foreach(var spec in expected)
                foreach(var obj in scene.GetRootGameObjects().Where(r=>r.name==spec.Item1))
                {
                    var components = obj.GetComponents<Component>();
                    bool original = obj.transform.childCount==0 && Vector3.Distance(obj.transform.position,spec.Item2)<.001f &&
                        Vector3.Distance(obj.transform.localScale,spec.Item3)<.001f && Quaternion.Angle(obj.transform.rotation,Quaternion.identity)<.01f &&
                        components.Length==4 && components.All(c=>c is Transform || c is MeshFilter || c is MeshRenderer || c is BoxCollider);
                    if(!original) { Debug.LogWarning("Custom object preserved: " + obj.name + ". Review overlap with the generated room."); continue; }
                    manifest.preservedObjects.Add(new PreservedObject { target=obj,activeSelf=obj.activeSelf });
                    Undo.RecordObject(obj,"Preserve demo placeholder");
                    obj.SetActive(false);
                }
        }
    }
}
