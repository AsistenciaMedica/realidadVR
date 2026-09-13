using System.Linq;
using EmergencyVR.Editor;
using EmergencyVR.Editor.Environment;
using EmergencyVR.Environment;
using EmergencyVR.Patient;
using EmergencyVR.Scenarios;
using EmergencyVR.XR;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using Unity.XR.CoreUtils;
using Object = UnityEngine.Object;

namespace EmergencyVR.Tests
{
    public sealed class TrainingRoomEnvironmentTests
    {
        Scene scene;
        SceneSetup[] setup;

        [SetUp]
        public void OpenIsolatedSavedScene()
        {
            // Never discard unsaved user scenes when run interactively.
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) Assert.Ignore("Save open scenes before running environment mutation tests.");
            setup = EditorSceneManager.GetSceneManagerSetup();
            scene = EditorSceneManager.OpenScene(DemoProjectBuilder.TrainingPath,OpenSceneMode.Single);
        }

        [TearDown]
        public void DiscardTestChanges()
        {
            if(setup==null) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            // Batch test runners start with an untitled empty scene, which cannot be restored by path.
            var savedScenes=setup.Where(s=>!string.IsNullOrEmpty(s.path)).ToArray();
            if(savedScenes.Length>0) EditorSceneManager.RestoreSceneManagerSetup(savedScenes);
            setup=null;
        }

        T[] Find<T>() where T:Component { return scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray(); }

        [Test]
        public void GeneratedRoomHasRealScaleAndReplaceableVisuals()
        {
            var manifest = Find<GeneratedEnvironment>().Single();
            var floor = manifest.transform.Find("Architecture/Floor").GetComponentInChildren<BoxCollider>();
            Assert.That(floor.size,Is.EqualTo(new Vector3(7,.2f,8)));
            Assert.That(manifest.transform.Find("Architecture/Ceiling"),Is.Not.Null);
            Assert.That(manifest.preservedObjects.Count,Is.EqualTo(6));
            Assert.That(manifest.preservedObjects.All(s=>s.target!=null && !s.target.activeSelf),Is.True);
            var bed = manifest.transform.Find("Furniture/HospitalBed");
            Assert.That(bed.position,Is.EqualTo(new Vector3(1.55f,0,2.1f)));
            Assert.That(bed.Find("Visual").GetComponentsInChildren<Collider>().Length,Is.Zero);
            Assert.That(bed.Find("Physics").GetComponentsInChildren<BoxCollider>().Length,Is.EqualTo(1));
            Assert.That(Find<MeshCollider>().Where(c=>manifest.ownedObjects.Contains(c.gameObject)),Is.Empty);
            Assert.That(manifest.ownedObjects.SelectMany(o=>o.GetComponents<Renderer>()).Count(),Is.LessThan(120));
            Assert.That(Find<Light>().Count(l=>l.isActiveAndEnabled && l.lightmapBakeType!=LightmapBakeType.Baked),Is.EqualTo(1));
            Assert.That(Find<Light>().All(l=>l.shadows==LightShadows.None),Is.True);
            Assert.That(manifest.transform.Find("Architecture/WallFinish"),Is.Not.Null);
            Assert.That(manifest.transform.Find("Decoration/RoomDetails"),Is.Not.Null);
            Assert.That(manifest.presentation.Count,Is.EqualTo(1));
            Assert.That(manifest.materialOverrides.Count,Is.EqualTo(4));
            Assert.That(manifest.presentation[0].target.position.x,Is.LessThan(-3f),"UI should be on the side wall, clear of the patient.");
        }

        [Test]
        public void RegenerationPreservesExistingObjectsAndSerializedClinicalXrReferences()
        {
            var original = Find<GeneratedEnvironment>().Single();
            var owned = original.ownedObjects.ToArray();
            var protectedObjects = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true))
                .Where(t=>!owned.Contains(t.gameObject)).Select(t=>t.gameObject).ToArray();
            var scripts = protectedObjects.SelectMany(o=>o.GetComponents<MonoBehaviour>()).Where(c=>c!=null).ToArray();
            var before = scripts.Select(s=>EditorJsonUtility.ToJson(s)).ToArray();
            var transforms = protectedObjects.Select(o=>EditorJsonUtility.ToJson(o.transform)).ToArray();
            var guid = AssetDatabase.AssetPathToGUID("Assets/_Project/Art/Prefabs/Generated/Furniture/HospitalBed.prefab");
            TrainingRoomEnvironmentBuilder.Generate(scene);
            TrainingRoomEnvironmentBuilder.Generate(scene);
            Assert.That(Find<GeneratedEnvironment>().Length,Is.EqualTo(1));
            Assert.That(Find<GeneratedEnvironment>().Single().ownedObjects.Count,Is.EqualTo(owned.Length));
            for(int i=0;i<scripts.Length;i++) Assert.That(EditorJsonUtility.ToJson(scripts[i]),Is.EqualTo(before[i]),scripts[i].name);
            for(int i=0;i<protectedObjects.Length;i++) Assert.That(EditorJsonUtility.ToJson(protectedObjects[i].transform),Is.EqualTo(transforms[i]),protectedObjects[i].name);
            Assert.That(AssetDatabase.AssetPathToGUID("Assets/_Project/Art/Prefabs/Generated/Furniture/HospitalBed.prefab"),Is.EqualTo(guid));
            Assert.That(Find<XROrigin>().Length,Is.EqualTo(1));
            Assert.That(Find<PatientController>().Length,Is.EqualTo(1));
            Assert.That(Find<PatientInteraction>().Length,Is.EqualTo(1));
            Assert.That(Find<ScenarioManager>().Length,Is.EqualTo(1));
        }

        [Test]
        public void ClearRestoresOriginalsAndPreservesUserAdditionsEvenUnderGeneratedObjects()
        {
            var manifest=Find<GeneratedEnvironment>().Single();
            var saved=manifest.preservedObjects.ToArray();
            var presentation=manifest.presentation.ToArray();
            var materials=manifest.materialOverrides.ToArray();
            var light=manifest.existingRoomLight;
            var intensity=manifest.previousLightIntensity;
            var patient=Find<PatientController>().Single();
            var origin=Find<XROrigin>().Single();
            var custom=new GameObject("User equipment");
            custom.transform.SetParent(manifest.transform.Find("Furniture/HospitalBed/Visual"),false);
            custom.transform.position=new Vector3(2,1,2);
            var position=custom.transform.position;
            var customRoot=new GameObject("Environment"); // Matching names never grant ownership.
            TrainingRoomEnvironmentBuilder.Clear(scene);
            Assert.That(custom!=null && customRoot!=null,Is.True);
            Assert.That(custom.transform.position,Is.EqualTo(position));
            Assert.That(custom.transform.parent,Is.Null);
            Assert.That(patient!=null && origin!=null,Is.True);
            Assert.That(saved.All(s=>s.target!=null && s.target.activeSelf==s.activeSelf),Is.True);
            Assert.That(Find<GeneratedEnvironment>(),Is.Empty);
            foreach(var prior in presentation)
            {
                Assert.That(prior.target.localPosition,Is.EqualTo(prior.localPosition));
                Assert.That(prior.target.localRotation,Is.EqualTo(prior.localRotation));
                Assert.That(prior.target.localScale,Is.EqualTo(prior.localScale));
            }
            foreach(var prior in materials) Assert.That(prior.target.sharedMaterials,Is.EqualTo(prior.materials));
            Assert.That(light.intensity,Is.EqualTo(intensity));
            Assert.DoesNotThrow(()=>TrainingRoomEnvironmentBuilder.Clear(scene));
            Object.DestroyImmediate(custom); Object.DestroyImmediate(customRoot);
        }

        [Test]
        public void TeleportDestinationsAndEntryHaveStandingClearance()
        {
            Physics.SyncTransforms();
            var manifest=Find<GeneratedEnvironment>().Single();
            var colliders=manifest.ownedObjects.SelectMany(o=>o.GetComponents<Collider>()).Where(c=>c.enabled && !c.isTrigger).ToArray();
            foreach(var pad in Find<TeleportationArea>())
            {
                Assert.That(pad.teleportationProvider,Is.Not.Null);
                var standing=new Bounds(pad.transform.position+Vector3.up*1.05f,new Vector3(.7f,1.9f,.7f));
                foreach(var collider in colliders) Assert.That(collider.bounds.Intersects(standing),Is.False,pad.transform.position + " blocked by " + collider.transform.parent.parent.name);
            }
            var doorway=new Bounds(new Vector3(0,1.04f,-3),new Vector3(1.18f,2.06f,.3f));
            foreach(var collider in colliders) Assert.That(collider.bounds.Intersects(doorway),Is.False,"Door opening blocked by " + collider.name);
        }

        [Test]
        public void GenerationRejectsOtherScenesWithoutMutation()
        {
            var other=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                Assert.Throws<System.InvalidOperationException>(()=>TrainingRoomEnvironmentBuilder.Generate(other));
                Assert.That(other.GetRootGameObjects(),Is.Empty);
            }
            finally { EditorSceneManager.CloseScene(other,true); }
        }
    }
}
