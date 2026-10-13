using System.Collections;
using System.Linq;
using EmergencyVR.Medical.Interaction;
using EmergencyVR.Patient.Presentation;
using EmergencyVR.Scenarios;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EmergencyVR.Tests
{
#if UNITY_EDITOR
    [PrebuildSetup(SimulatedXRTestHooks.Setup), PostBuildCleanup(SimulatedXRTestHooks.Setup)]
#endif
    public sealed class ChokingInteractionTests
    {
        [UnityTest]
        public IEnumerator DesktopPointerGestureRequiresHContactAndReturnAndStopsAtPause()
        {
            EmergencyVR.Desktop.DesktopDemoController desktop = null;
            void Install(Scene scene, LoadSceneMode mode)
            {
                if (scene.name == "TrainingRoom") desktop = EmergencyVR.Desktop.DesktopDemoController.Create();
            }
            SceneManager.sceneLoaded += Install;
            try { yield return SceneManager.LoadSceneAsync("TrainingRoom"); }
            finally { SceneManager.sceneLoaded -= Install; }
            yield return null; yield return null;
            var inputSettings = UnityEngine.InputSystem.InputSystem.settings;
            var previousBackground = inputSettings.backgroundBehavior;
            bool previousRunInBackground = Application.runInBackground;
#if UNITY_EDITOR
            var previousEditorInput = inputSettings.editorInputBehaviorInPlayMode;
            // Batchmode has no focused Game View. Unity's default routes keyboard/mouse events
            // to the Editor; use the same routing as InputTestFixture for this real-provider test.
            inputSettings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            inputSettings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.runInBackground = true;
            var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            var flow = Object.FindFirstObjectByType<EmergencyVR.UI.TrainingExperience>();
            var rig = flow.Review.Procedures; var choking = rig.Choking;
            try
            {
                Assert.That(flow.IsDesktop, Is.True);
                int index = System.Array.FindIndex(flow.Review.Catalog.entries, e => e.medical?.id == "gym-choking");
                flow.Prepare(index); flow.BeginTraining();
                flow.Review.Submit("CheckSceneSafety"); flow.Review.Submit("CheckResponsiveness"); flow.Review.Submit("CheckBreathing");
                yield return new WaitForSeconds(1);
                var head = rig.Visuals.ChestRestPosition - choking.Anterior * .65f + choking.HeadDirection * .25f;
                desktop.Body.enabled = false; desktop.transform.position += head - desktop.View.transform.position; desktop.Body.enabled = true;
                // Let the walking capsule settle before projecting a world target into pixels.
                // A projection taken while the camera is still falling misses the contact plane.
                yield return new WaitForSeconds(.5f);
                desktop.View.transform.LookAt(choking.RestPosition);
                var screenPoint = (Vector2)desktop.View.WorldToScreenPoint(choking.RestPosition);
                Pointer(screenPoint, false); yield return null;
                Assert.That(desktop.PointerOverInterface, Is.False, "The contact target must be accessible through the training UI.");
                Assert.That(choking.CanAct, Is.True, "Assessment must unlock the physical dorsal action: " + flow.Review.Manager.Feedback);
                Assert.That(choking.InReach(desktop.View.transform.position), Is.True,
                    $"The walking capsule must remain behind the patient after grounding. Eye={desktop.View.transform.position}, chest={rig.Visuals.ChestRestPosition}, anterior={choking.Anterior}");
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.H));
                yield return null;
                Assert.That(choking.WindowsEngaged, Is.True,
                    $"H event: currentKeyboard={UnityEngine.InputSystem.Keyboard.current == keyboard}, held={keyboard.hKey.isPressed}, pressed={keyboard.hKey.wasPressedThisFrame}, canAct={choking.CanAct}, reach={choking.InReach(desktop.View.transform.position)}, blocked={desktop.WorldInputBlocked}, overUI={desktop.PointerOverInterface}, tool={desktop.HasHeldTool}");
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                Pointer(screenPoint, true); yield return null;
                Assert.That(choking.HasContact, Is.True,
                    $"The pointer must touch the target. mouse={UnityEngine.InputSystem.Mouse.current == mouse}, held={mouse.leftButton.isPressed}, pointer={mouse.position.ReadValue()}, target={desktop.View.WorldToScreenPoint(choking.RestPosition)}, engaged={choking.WindowsEngaged}, blocked={desktop.WorldInputBlocked}, ui={desktop.PointerOverInterface}");
                var feedback = choking.GetComponentsInChildren<TextMesh>().Single(t => t.name == "Choking gesture instructions");
                yield return null; // Read the label after its LateUpdate has presented contact.
                Assert.That(feedback.text, Does.Contain("Contacto activo"));
                var rotation = desktop.View.transform.rotation;
                Pointer(screenPoint + Vector2.up * 75, true, true); yield return new WaitForSeconds(.12f);
                Assert.That(Quaternion.Angle(rotation, desktop.View.transform.rotation), Is.LessThan(.01f), "Secondary mouse look cannot move the contact plane during a gesture.");
                Assert.That(choking.BackBlowGestures, Is.Zero, "A held stroke has not returned yet.");
                Assert.That(feedback.text, Does.Contain("vuelve al inicio"));
                Pointer(screenPoint, true); yield return null;
                Assert.That(choking.BackBlowGestures, Is.EqualTo(1));
                Pointer(screenPoint, false); yield return null;
                flow.TogglePause();
                Assert.That(choking.WindowsEngaged, Is.False);
                Assert.That(choking.HasContact, Is.False);
            }
            finally
            {
                flow.Review.Manager.SetPaused(false);
                UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard); UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
                inputSettings.backgroundBehavior = previousBackground;
#if UNITY_EDITOR
                inputSettings.editorInputBehaviorInPlayMode = previousEditorInput;
#endif
                Application.runInBackground = previousRunInBackground;
            }
            void Pointer(Vector2 position, bool held, bool secondary = false)
            {
                var state = new UnityEngine.InputSystem.LowLevel.MouseState { position = position };
                if (held) state = state.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left);
                if (secondary) state = state.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, state);
            }
        }
#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator ControllerInputCountsAContactStrokeAndTrackingLossCancelsTheNextOne()
        {
            yield return SceneManager.LoadSceneAsync("TrainingRoom"); yield return null;
            var simulation = EmergencyVR.Desktop.QuestLookSimulation.CreateForValidation();
            simulation.ManualControlsEnabled = false;
            var review = Object.FindFirstObjectByType<ReviewCaseSession>();
            var rig = review.Procedures;
            var choking = rig.Choking;
            try
            {
                int index = System.Array.FindIndex(review.Catalog.entries, e => e.medical?.id == "gym-choking");
                Assert.That(review.Select(index), Is.True); review.Manager.StartCase();
                review.Submit("CheckSceneSafety"); review.Submit("CheckResponsiveness"); review.Submit("CheckBreathing");
                yield return new WaitForSeconds(1);
                var flow = Object.FindFirstObjectByType<EmergencyVR.UI.TrainingExperience>();
                var guidance = flow.GetComponentsInChildren<UnityEngine.UI.Text>().Single(t => t.name == "Consultation guidance");
                Assert.That(guidance.text, Does.Contain("ambos grips").And.Contain("vuelve"), "The actual VR side panel must explain the physical controls.");
                simulation.SetHeadPose(rig.Visuals.ChestRestPosition - choking.Anterior * .65f + choking.HeadDirection * .2f,
                    Quaternion.LookRotation(choking.Anterior, choking.HeadDirection));
                Pose(0); simulation.SetGrip(UnityEngine.XR.XRNode.LeftHand, true); simulation.SetGrip(UnityEngine.XR.XRNode.RightHand, true);
                yield return null; yield return null;
                Pose(.05f); yield return new WaitForSeconds(.12f);
                Pose(0); yield return null; yield return null;
                Assert.That(choking.BackBlowGestures, Is.EqualTo(1), "Both providers remain installed; desktop must not cancel XR samples.");
                Pose(.05f); yield return new WaitForSeconds(.1f);
                simulation.SetControllerPose(UnityEngine.XR.XRNode.LeftHand, choking.SupportPosition, Quaternion.identity, false);
                yield return null; yield return null;
                Pose(0); simulation.SetGrip(UnityEngine.XR.XRNode.LeftHand, true);
                yield return null; yield return null;
                Assert.That(choking.BackBlowGestures, Is.EqualTo(1), "Losing the supporting hand cancels the unfinished gesture.");
                flow.Navigate(EmergencyVR.UI.ExperiencePage.Help); yield return null;
                Assert.That(flow.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t => t.name == "Help instructions" &&
                    t.text.Contains("ambos grips") && t.text.Contains("vuelve")), Is.True, "The reachable VR help must also explain grip and recoil.");
            }
            finally
            {
                review.Manager.SetPaused(false); Object.Destroy(simulation.gameObject);
            }
            void Pose(float travel)
            {
                simulation.SetControllerPose(UnityEngine.XR.XRNode.LeftHand, choking.SupportPosition, Quaternion.identity);
                simulation.SetControllerPose(UnityEngine.XR.XRNode.RightHand, choking.RestPosition + choking.GestureDirection * travel, Quaternion.identity);
            }
            yield return null;
        }
#endif
        [UnityTest]
        public IEnumerator EachEnvironmentRequiresContactAndRecoilThenUsesTheAuthoredRecoveryTimeline()
        {
            yield return SceneManager.LoadSceneAsync("TrainingRoom"); yield return null;
            var review = Object.FindFirstObjectByType<ReviewCaseSession>();
            var rig = review.Procedures;
            var choking = rig.Choking;
            choking.GetComponent<WindowsChokingInput>().enabled = false;
            choking.GetComponent<VRChokingInput>().enabled = false;
            var entries = review.Catalog.entries.Select((entry, index) => new { entry, index })
                .Where(e => e.entry.medical?.actions.Any(a => a.action == ChokingInteractionController.BackBlows) == true).ToArray();
            Assert.That(entries.Length, Is.EqualTo(3));
            foreach (var entry in entries)
            {
                Assert.That(review.Select(entry.index), Is.True);
                Assert.That(rig.IsPhysicalAction(ChokingInteractionController.BackBlows), Is.True);
                Assert.That(rig.IsPhysicalAction(ChokingInteractionController.AbdominalThrusts), Is.True);
                review.Manager.StartCase();
                Assert.That(choking.CanAct, Is.False, "Assessment prerequisites must be satisfied first.");
                review.Submit("CheckSceneSafety"); review.Submit("CheckResponsiveness"); review.Submit("CheckBreathing");
                yield return new WaitForSeconds(.6f);
                Assert.That(rig.Visuals.EffectivePosture, Is.EqualTo(PatientPosture.Standing));
                Assert.That(choking.CanAct, Is.True);
                Assert.That(choking.InReach(rig.Visuals.ChestRestPosition + choking.Anterior), Is.False,
                    "The learner must approach from behind.");
                Feed(0, true); Feed(.05f, true); yield return new WaitForSeconds(.1f); Feed(0, true);
                Assert.That(choking.BackBlowGestures, Is.Zero, "Remote gestures do not count.");
                Feed(0); Feed(.05f); review.Manager.SetPaused(true);
                Feed(0); review.Manager.SetPaused(false); Feed(0);
                Assert.That(choking.BackBlowGestures, Is.Zero, "Pause cancels an incomplete stroke.");
                choking.CancelContact();
                for (int i = 0; i < 5; i++) yield return Stroke();
                Assert.That(review.Manager.MedicalSession.Completed, Does.Contain(ChokingInteractionController.BackBlows));
                Assert.That(choking.BackBlowGestures, Is.EqualTo(5));
                Assert.That(choking.PendingAction, Is.EqualTo(ChokingInteractionController.AbdominalThrusts));
                Assert.That(choking.ObjectExpelled, Is.False);
                for (int i = 0; i < 5; i++) yield return Stroke();
                Assert.That(review.Manager.MedicalSession.Completed, Does.Contain(ChokingInteractionController.AbdominalThrusts));
                Assert.That(choking.AbdominalGestures, Is.EqualTo(5));
                Assert.That(choking.ObjectExpelled, Is.False, "Gestures cannot bypass the clinical timeline.");
                review.Manager.AdvanceTrainingTime(11); yield return null;
                Assert.That(choking.ObjectExpelled, Is.True);
                Assert.That(review.Manager.MedicalSession.Patient.flags, Does.Not.Contain("airway obstruction"));
                Assert.That(choking.CanAct, Is.False);
                review.Manager.FinishCase();
                Assert.That(review.Manager.MedicalResult.procedures.chokingAbdominalThrusts, Is.EqualTo(5));
                Assert.That(review.Manager.MedicalResult.procedures.chokingObjectExpelled, Is.True);
                Assert.That(review.Select(0), Is.True);
                Assert.That(choking.ObjectExpelled, Is.False);
                Assert.That(choking.BackBlowGestures, Is.Zero);
                Assert.That(choking.AbdominalGestures, Is.Zero);
            }
            IEnumerator Stroke()
            {
                Feed(0); Feed(.05f); yield return new WaitForSeconds(.10f); Feed(0); choking.CancelContact();
            }
            void Feed(float travel, bool distant = false)
            {
                var hand = choking.RestPosition + choking.GestureDirection * travel;
                var support = choking.IsBackBlow ? choking.SupportPosition : hand;
                var learner = rig.Visuals.ChestRestPosition - choking.Anterior * (distant ? 5 : .65f) + choking.HeadDirection * .2f;
                choking.FeedHands(support, hand, learner, true);
            }
        }
    }
}
