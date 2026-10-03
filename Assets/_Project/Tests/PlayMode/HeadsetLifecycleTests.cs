#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EmergencyVR.Desktop;
using EmergencyVR.Medical.Interaction;
using EmergencyVR.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR;

namespace EmergencyVR.Tests
{
    [PrebuildSetup(SimulatedXRTestHooks.Setup), PostBuildCleanup(SimulatedXRTestHooks.Setup)]
    public sealed class HeadsetLifecycleTests
    {
        QuestLookSimulation simulation;
        TrainingExperience flow;

        [UnitySetUp] public IEnumerator Load()
        {
            Time.timeScale = 1; AudioListener.pause = false;
            SceneManager.sceneLoaded += Install;
            yield return SceneManager.LoadSceneAsync("TrainingRoom");
            SceneManager.sceneLoaded -= Install;
            yield return null; yield return null;
            flow = UnityEngine.Object.FindFirstObjectByType<TrainingExperience>();
        }
        void Install(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "TrainingRoom") return;
            simulation = QuestLookSimulation.CreateForValidation(); simulation.ManualControlsEnabled = false;
        }
        [UnityTearDown] public IEnumerator Restore()
        {
            SceneManager.sceneLoaded -= Install;
            if (flow != null) { flow.SendMessage("OnApplicationFocus", true); flow.Review.Manager.SetPaused(false); }
            if (simulation != null) UnityEngine.Object.Destroy(simulation.gameObject);
            Time.timeScale = 1; AudioListener.pause = false;
            yield return null;
        }
        void Begin()
        {
            flow.Prepare(Array.FindIndex(flow.Review.Catalog.entries, entry => entry.medical?.id == "gym-faint"));
            flow.BeginTraining();
        }

        [UnityTest] public IEnumerator FocusLossFreezesClockAndAudioAndReturnRequiresExplicitResume()
        {
            Begin(); yield return null;
            flow.SendMessage("OnApplicationFocus", false);
            double at = flow.Review.Manager.ElapsedSeconds;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(flow.Page, Is.EqualTo(ExperiencePage.Pause));
            Assert.That(flow.Review.Manager.ElapsedSeconds, Is.EqualTo(at).Within(.005));
            Assert.That(AudioListener.pause, Is.True);
            flow.Navigate(ExperiencePage.Training);
            Assert.That(flow.Review.Manager.IsPaused, Is.True, "Focus has not returned yet.");
            simulation.SetHeadPose(new Vector3(2, 1.1f, -1), Quaternion.Euler(0, 70, 0));
            flow.SendMessage("OnApplicationFocus", true);
            yield return null; yield return null; yield return null;
            Assert.That(flow.Review.Manager.IsPaused, Is.True);
            Assert.That(AudioListener.pause, Is.True);
            AssertCentered();
            flow.Navigate(ExperiencePage.Training); yield return null;
            Assert.That(flow.Review.Manager.IsPaused, Is.False);
            Assert.That(AudioListener.pause, Is.False);
        }

        [UnityTest] public IEnumerator ApplicationPauseReturnsToARecenteredPauseMenu()
        {
            Begin(); yield return null;
            flow.SendMessage("OnApplicationPause", true);
            Assert.That(flow.Review.Manager.IsPaused, Is.True);
            Assert.That(AudioListener.pause, Is.True);
            simulation.SetHeadPose(new Vector3(-1, 1.75f, 1), Quaternion.Euler(0, -40, 0));
            flow.SendMessage("OnApplicationPause", false);
            yield return null; yield return null; yield return null;
            Assert.That(flow.Page, Is.EqualTo(ExperiencePage.Pause)); AssertCentered();
            Assert.That(flow.Review.Manager.IsPaused, Is.True);
        }

        [UnityTest] public IEnumerator EitherSecondaryButtonRecentersAtSeatedOrStandingEyeHeight()
        {
            foreach (var hand in new[] { XRNode.LeftHand, XRNode.RightHand })
            {
                float eyeHeight = hand == XRNode.LeftHand ? 1.05f : 1.8f;
                simulation.SetHeadPose(new Vector3(1, eyeHeight, -1), Quaternion.Euler(15, 100, 0));
                yield return null; yield return null;
                simulation.SetSecondaryButton(hand, true);
                yield return null; yield return null;
                AssertCentered();
                Assert.That(flow.InterfaceCanvas.transform.position.y, Is.EqualTo(eyeHeight).Within(.01));
                simulation.SetSecondaryButton(hand, false);
                yield return null; yield return null;
            }
        }

        [UnityTest] public IEnumerator OneTrackedControllerPreservesTrainingAndLosingBothPausesSafely()
        {
            Begin(); yield return null;
            simulation.SetControllerPose(XRNode.LeftHand, Vector3.zero, Quaternion.identity, false);
            yield return null; yield return null;
            Assert.That(flow.Review.Manager.IsPaused, Is.False);
            simulation.SetControllerPose(XRNode.RightHand, Vector3.zero, Quaternion.identity, false);
            yield return null; yield return null;
            Assert.That(flow.Review.Manager.IsPaused, Is.True);
            Assert.That(AudioListener.pause, Is.True);
            simulation.SetControllerPose(XRNode.RightHand, simulation.View.transform.position + Vector3.forward * .3f, Quaternion.identity);
            yield return null; yield return null;
            Assert.That(flow.Review.Manager.IsPaused, Is.True, "Tracking recovery must not resume the clinical clock automatically.");
            var resume = flow.GetComponentsInChildren<Button>().Single(button => button.name == "Resume training");
            var center = resume.transform.TransformPoint(((RectTransform)resume.transform).rect.center);
            var forward = flow.InterfaceCanvas.transform.forward;
            simulation.SetControllerPose(XRNode.RightHand, center - forward * .8f, Quaternion.LookRotation(forward));
            yield return null; yield return null; yield return null;
            simulation.SetTrigger(XRNode.RightHand, true); yield return null; yield return null;
            simulation.SetTrigger(XRNode.RightHand, false); yield return new WaitForSecondsRealtime(.35f);
            Assert.That(flow.Page, Is.EqualTo(ExperiencePage.Training));
            Assert.That(flow.Review.Manager.IsPaused, Is.False);
        }

        [UnityTest] public IEnumerator VrMenusUseLargerTypeAndTheConsultationPanelLeavesPatientViewClear()
        {
            var rect = (RectTransform)flow.InterfaceCanvas.transform;
            Assert.That(rect.rect.width * rect.lossyScale.x, Is.EqualTo(1.3f).Within(.001f));
            AssertCentered();
            var start = flow.GetComponentsInChildren<Button>().Single(button => button.name == "Start learning").GetComponentInChildren<Text>();
            Assert.That(start.fontSize, Is.GreaterThanOrEqualTo(30));
            Assert.That(start.fontSize * rect.lossyScale.x / TrainingExperience.VrMenuDistance,
                Is.GreaterThan(22 * .00165f / 2.2f), "The glyph angular size must grow; shrinking the old canvas is insufficient.");
            Begin(); yield return null;
            simulation.FocusPatient(flow.Review.Procedures.Visuals.ChestAnchor.position);
            yield return null; yield return null;
            flow.Recenter(); yield return null;
            Assert.That(flow.UsesSidePanel, Is.True);
            Assert.That(rect.rect.width * rect.lossyScale.x, Is.EqualTo(.68f).Within(.001f));
            var patientScreen = simulation.View.WorldToScreenPoint(flow.Review.Procedures.Visuals.ChestAnchor.position);
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(rect, patientScreen, simulation.View), Is.False,
                "The patient's chest must remain outside the consultation panel's projected rectangle.");
            Assert.That(flow.GetComponentsInChildren<Text>().Any(label => label.name == "Value spo2" || label.name == "Value bp"), Is.False);
            flow.TogglePause(); yield return null;
            AssertCentered();
            Assert.That(rect.rect.width * rect.lossyScale.x, Is.EqualTo(1.3f).Within(.001f));
        }

        [UnityTest] public IEnumerator MenuFeedbackUsesOnlyThePointingHandWithoutStartingClinicalActions()
        {
            var requests = new List<MedicalHaptics.Request>();
            Action<MedicalHaptics.Request> record = request => requests.Add(request);
            MedicalHaptics.Requested += record;
            try
            {
                var button = flow.GetComponentsInChildren<Button>().Single(item => item.name == "Start learning");
                var center = button.transform.TransformPoint(((RectTransform)button.transform).rect.center);
                var forward = flow.InterfaceCanvas.transform.forward;
                simulation.SetControllerPose(XRNode.LeftHand, Vector3.zero, Quaternion.identity, false);
                simulation.SetControllerPose(XRNode.RightHand, center - forward * .8f, Quaternion.LookRotation(forward));
                yield return null; yield return null; yield return null;
                simulation.SetTrigger(XRNode.RightHand, true); yield return null; yield return null;
                simulation.SetTrigger(XRNode.RightHand, false); yield return new WaitForSecondsRealtime(.35f);
                Assert.That(flow.Page, Is.EqualTo(ExperiencePage.Environments));
                var feedback = requests.Where(request => request.Reason == "UIHover" || request.Reason == "UIClick").ToArray();
                Assert.That(feedback.Any(request => request.Reason == "UIHover"), Is.True);
                Assert.That(feedback.Any(request => request.Reason == "UIClick"), Is.True);
                Assert.That(feedback.All(request => request.Hand == XRNode.RightHand && request.Simulated), Is.True);
                Assert.That(feedback.All(request => request.Seconds <= .04f && request.Amplitude <= .12f), Is.True);
                Assert.That(flow.Review.Manager.IsRunning, Is.False);
            }
            finally { MedicalHaptics.Requested -= record; }
        }

        [UnityTest] public IEnumerator ContactGrabAndReleaseRequestBriefFeedbackWithoutSubmittingAnAssessment()
        {
            Begin(); yield return null;
            var requests = new List<MedicalHaptics.Request>();
            Action<MedicalHaptics.Request> record = request => requests.Add(request);
            MedicalHaptics.Requested += record;
            try
            {
                var rig = flow.Review.Procedures;
                simulation.SetControllerPose(XRNode.LeftHand, rig.CPR.ChestRestPosition, Quaternion.identity);
                simulation.SetControllerPose(XRNode.RightHand, simulation.View.transform.position + Vector3.back * 2, Quaternion.identity);
                yield return null; yield return null;
                Assert.That(requests.Count(request => request.Reason == "PatientContact" && request.Hand == XRNode.LeftHand), Is.EqualTo(1));
                yield return null; yield return null; yield return null;
                Assert.That(requests.Count(request => request.Reason == "PatientContact"), Is.EqualTo(1), "Sustained contact must not vibrate every frame.");
                var meter = UnityEngine.Object.FindObjectsByType<MedicalPhysicalTool>(FindObjectsSortMode.None).Single(tool => tool.Kind == MedicalToolKind.Glucose);
                meter.OnGrabbed();
                meter.transform.position = Vector3.up * 2;
                meter.OnReleased();
                Assert.That(requests.Any(request => request.Reason == "Grab"), Is.True);
                Assert.That(requests.Any(request => request.Reason == "Release"), Is.True);
                int beforeReset = requests.Count;
                meter.ResetTool();
                Assert.That(requests.Count, Is.EqualTo(beforeReset), "Returning equipment between attempts is not a learner release.");
                Assert.That(flow.Review.Manager.MedicalSession.Completed, Is.Empty);
                Assert.That(rig.CPR.Metrics.compressions, Is.Zero);
            }
            finally { MedicalHaptics.Requested -= record; }
        }

        void AssertCentered()
        {
            var head = simulation.View.transform;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            var delta = flow.InterfaceCanvas.transform.position - head.position;
            Assert.That(Mathf.Abs(delta.y), Is.LessThan(.01f));
            Assert.That(Vector3.Angle(delta, forward), Is.LessThan(.1f));
            Assert.That(delta.magnitude, Is.EqualTo(TrainingExperience.VrMenuDistance).Within(.01f));
        }
    }
}
#endif
