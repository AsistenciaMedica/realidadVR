#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using EmergencyVR.Desktop;
using EmergencyVR.Medical;
using EmergencyVR.Medical.Interaction;
using EmergencyVR.Patient.Presentation;
using EmergencyVR.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EmergencyVR.Tests
{
    [PrebuildSetup(SimulatedXRTestHooks.Setup), PostBuildCleanup(SimulatedXRTestHooks.Setup)]
    public sealed class CharacterPresenceTests
    {
        QuestLookSimulation simulation;
        TrainingExperience flow;
        Camera portrait;
        float previousCaptureDelta;

        [UnitySetUp]
        public IEnumerator Load()
        {
            Time.timeScale = 1; AudioListener.pause = false;
            previousCaptureDelta = Time.captureDeltaTime;
            SceneManager.sceneLoaded += Install;
            yield return SceneManager.LoadSceneAsync("TrainingRoom");
            SceneManager.sceneLoaded -= Install;
            yield return null; yield return null;
            flow = Object.FindFirstObjectByType<TrainingExperience>();
            flow.Prepare(Array.FindIndex(flow.Review.Catalog.entries, e => e.medical?.id == "review-hypotension-v2"));
            flow.BeginTraining();
            yield return null; yield return null;
        }

        void Install(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "TrainingRoom") return;
            simulation = QuestLookSimulation.CreateForValidation();
            simulation.ManualControlsEnabled = false;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            SceneManager.sceneLoaded -= Install;
            if (flow != null) { flow.Review.Manager.SetPaused(false); flow.Review.Manager.FinishCase(); }
            if (portrait != null) Object.Destroy(portrait.gameObject);
            if (simulation != null) Object.Destroy(simulation.gameObject);
            Time.captureDeltaTime = previousCaptureDelta;
            Time.timeScale = 1; AudioListener.pause = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SeatedActingMovesWithoutSlidingSupportsOrAdvancingClinicalActions()
        {
            var presentation = flow.Review.GetComponent<Case01PatientPresentation>();
            var rig = flow.Review.Procedures.Visuals.Rig;
            Assert.That(presentation.IsActive, Is.True);
            var pelvis = presentation.PelvisPosition;
            var leftFoot = presentation.LeftFootPosition;
            var rightFoot = presentation.RightFootPosition;
            var initialHand = presentation.RightHandPosition;
            float movement = 0;
            for (int i = 0; i < 24; i++)
            {
                yield return new WaitForSeconds(.2f);
                movement = Mathf.Max(movement, Vector3.Distance(initialHand, presentation.RightHandPosition));
                Assert.That(Vector3.Distance(pelvis, presentation.PelvisPosition), Is.LessThan(.005f));
                Assert.That(Vector3.Distance(leftFoot, presentation.LeftFootPosition), Is.LessThan(.008f));
                Assert.That(Vector3.Distance(rightFoot, presentation.RightFootPosition), Is.LessThan(.008f));
            }
            Assert.That(movement, Is.GreaterThan(.12f), "The seated patient should visibly act without user input.");
            Assert.That(flow.Review.Manager.MedicalSession.Completed, Is.Empty);
            flow.Review.Manager.SetPaused(true);
            yield return null;
            var head = rig.head.rotation;
            var hand = presentation.RightHandPosition;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(Quaternion.Angle(head, rig.head.rotation), Is.LessThan(.1f));
            Assert.That(Vector3.Distance(hand, presentation.RightHandPosition), Is.LessThan(.001f));
        }

        // Optional visual evidence: run this exact test with -vital-presence-directory PATH.
        [UnityTest]
        public IEnumerator RecordOptionalAllScenarioInspection()
        {
            var args = System.Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args, "-vital-presence-directory");
            if (flag < 0 || flag + 1 >= args.Length) yield break;
            string directory = Path.GetFullPath(args[flag + 1]);
            Directory.CreateDirectory(directory);
            portrait = new GameObject("All scenario inspection camera").AddComponent<Camera>();
            portrait.CopyFrom(simulation.View); portrait.enabled = false;
            portrait.fieldOfView = 65; portrait.aspect = 16f / 9;
            portrait.cullingMask &= ~(1 << 5);
            var captured = new System.Collections.Generic.List<string>();
            foreach (var id in flow.Review.Scope.ScenarioIds)
            {
                if (flow.Review.Manager.IsRunning) flow.FinishTraining();
                flow.Prepare(Array.FindIndex(flow.Review.Catalog.entries, e => e.medical?.id == id));
                flow.BeginTraining();
                yield return new WaitForSeconds(2);
                Assert.That(flow.Review.Selected.medical.id, Is.EqualTo(id), "Each image must show the requested case, not a previously running attempt.");
                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
                // This camera is an inspection view, away from the tracked learner. Hide only
                // the learner's hands so they do not appear as detached arms in the room view.
                foreach (var hand in Object.FindObjectsByType<ArticulatedHand>(FindObjectsSortMode.None))
                    foreach (var renderer in hand.GetComponentsInChildren<Renderer>()) renderer.forceRenderingOff = true;
                var environment = Object.FindFirstObjectByType<EmergencyVR.Environment.ScenarioEnvironmentPresenter>();
                EmergencyVR.Environment.ScenarioEnvironmentPresenter.GetCaptureView(environment.CurrentEnvironment, out var position, out var target);
                Assert.That(environment.CurrentEnvironment, Is.EqualTo(flow.Review.Selected.medical.environment));
                portrait.farClipPlane = simulation.View.farClipPlane; // open-air modules extend the learner's view
                foreach (var text in environment.GetComponentsInChildren<TextMesh>())
                    Assert.That(text.GetComponent<MeshRenderer>().sharedMaterial.shader.name, Is.EqualTo("VITAL VR/World Signage"), text.name);
                portrait.clearFlags = environment.ActiveBakedLighting != null && environment.ActiveBakedLighting.skybox != null
                    ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                portrait.backgroundColor = simulation.View.backgroundColor;
                if (environment.CurrentEnvironment != "football" && environment.CurrentEnvironment != "street")
                {
                    position = new Vector3(.1f, 1.65f, -2.35f);
                    target = new Vector3(.3f, 1.15f, 2.3f);
                }
                portrait.transform.position = position; portrait.transform.LookAt(target);
                RuntimeCapture.Save(portrait, Path.Combine(directory, id + "-room.png"), 1440, 810);
                var head = flow.Review.Procedures.Visuals.Rig.head.position;
                portrait.transform.position = head + (head.y < .6f ? new Vector3(.7f, 1.05f, -.75f) : new Vector3(.5f, .18f, -1.5f));
                portrait.transform.LookAt(head + Vector3.down * .22f);
                RuntimeCapture.Save(portrait, Path.Combine(directory, id + "-patient.png"), 1440, 810);
                captured.Add(id + " | " + environment.CurrentEnvironment);
                Assert.That(flow.Review.Manager.MedicalSession, Is.Not.Null);
            }
            Assert.That(captured.Count, Is.EqualTo(flow.Review.Scope.ScenarioIds.Length));
            File.WriteAllLines(Path.Combine(directory, "cases.txt"), captured);
        }

        // This is a silent fixed-step animation inspection, never headset or FPS evidence.
        [UnityTest]
        public IEnumerator RecordOptionalCharacterInspection()
        {
            var args = System.Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args, "-vital-presence-directory");
            if (flag < 0 || flag + 1 >= args.Length) yield break;
            string directory = Path.GetFullPath(args[flag + 1]);
            Directory.CreateDirectory(directory);
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Rocketbox/Daniel/Daniel.fbx");
            var mesh = source.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(r => r.sharedMesh.vertexCount).First().sharedMesh;
            File.WriteAllLines(Path.Combine(directory, "available-facial-channels.txt"), Enumerable.Range(0, mesh.blendShapeCount).Select(mesh.GetBlendShapeName));
            portrait = new GameObject("Character inspection camera").AddComponent<Camera>();
            portrait.CopyFrom(simulation.View); portrait.enabled = false;
            portrait.fieldOfView = 58; portrait.aspect = 16f / 9;
            portrait.transform.position = new Vector3(.05f, 1.43f, -1.9f);
            portrait.transform.LookAt(new Vector3(.65f, 1.10f, .6f));
            portrait.cullingMask &= ~(1 << 5);
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
            Time.captureDeltaTime = 1f / 12;
            for (int i = 0; i < 216; i++)
            {
                if (i == 100) flow.Review.Manager.Dialogue.Ask(DialogueIntent.MAIN_SYMPTOM);
                yield return null;
                RuntimeCapture.Save(portrait, Path.Combine(directory, "frame-" + i.ToString("D4") + ".png"), 1280, 720);
            }
            File.WriteAllText(Path.Combine(directory, "capture.txt"), "216 rendered frames; 12 animation steps/second; 18 seconds. Silent Unity Quest-profile character inspection. No headset validation.\n");
        }
    }
}
#endif
