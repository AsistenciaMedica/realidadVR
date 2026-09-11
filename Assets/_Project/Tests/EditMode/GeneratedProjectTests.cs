using System.Linq;
using EmergencyVR.Core;
using EmergencyVR.Editor;
using EmergencyVR.Scenarios;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;

namespace EmergencyVR.Tests
{
    public sealed class GeneratedProjectTests
    {
        [Test]
        public void ScriptableObjectProducesIndependentSessionData()
        {
            var asset = ScriptableObject.CreateInstance<ClinicalCaseDefinition>();
            try
            {
                asset.initialState = PatientState.Normal;
                asset.steps.Add(new CaseStepData { actionId = "test", label = "Test",
                    fromState = PatientState.Normal, toState = PatientState.Recovered, points = 100 });
                var snapshot = asset.ToDomain();
                asset.steps[0].points = 1;
                asset.steps.Clear();
                var session = new CaseSession(snapshot, 0);
                session.Record("test", 1);
                Assert.That(session.Finish(2).ScorePercent, Is.EqualTo(100));
                Assert.That(snapshot.MaxPoints, Is.EqualTo(100));
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void GeneratedCaseIsExplicitlyATechnicalDemo()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ClinicalCaseDefinition>(DemoProjectBuilder.CasePath);
            Assert.That(asset, Is.Not.Null, "Run Emergency VR > 2 - Generate demo first.");
            Assert.That(asset.isTechnicalDemo, Is.True);
            Assert.That(asset.clinicallyApproved, Is.False);
            Assert.DoesNotThrow(() => asset.ToDomain());
        }

        [Test]
        public void GeneratedTrainingSceneContainsWiredInteractionSystems()
        {
            var scene = EditorSceneManager.OpenPreviewScene(DemoProjectBuilder.TrainingPath);
            try
            {
                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                        Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero, transform.name);
                var origin = roots.SelectMany(r => r.GetComponentsInChildren<XROrigin>(true)).Single();
                Assert.That(origin.Camera, Is.Not.Null);
                Assert.That(origin.RequestedTrackingOriginMode, Is.EqualTo(XROrigin.TrackingOriginMode.Floor));
                var inputs = origin.GetComponent<InputActionManager>();
                Assert.That(inputs, Is.Not.Null);
                Assert.That(inputs.actionAssets.Count, Is.GreaterThan(0));
                Assert.That(inputs.actionAssets.All(a => a != null), Is.True);
                Assert.That(origin.GetComponentInChildren<SnapTurnProvider>(true).turnAmount, Is.EqualTo(30));
                var pads = roots.SelectMany(r => r.GetComponentsInChildren<TeleportationArea>(true)).ToArray();
                Assert.That(pads.Length, Is.EqualTo(4));
                Assert.That(pads.All(p => p.teleportationProvider != null), Is.True);
                Assert.That(pads.All(p => p.interactionLayers.value == unchecked((int)0x80000000)), Is.True);
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<XRGrabInteractable>(true)).Count(), Is.EqualTo(1));
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<TrackedDeviceGraphicRaycaster>(true)).Count(), Is.EqualTo(1));
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<ScenarioManager>(true)).Single().Scenario, Is.Not.Null);
                var inputManagers = origin.GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(m => m.GetType().Name == "ControllerInputActionManager").ToArray();
                Assert.That(inputManagers.Length, Is.EqualTo(2));
                foreach (var inputManager in inputManagers)
                {
                    var serialized = new SerializedObject(inputManager);
                    Assert.That(serialized.FindProperty("m_SmoothMotionEnabled").boolValue, Is.False);
                    Assert.That(serialized.FindProperty("m_SmoothTurnEnabled").boolValue, Is.False);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
