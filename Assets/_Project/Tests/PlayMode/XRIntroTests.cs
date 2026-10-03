#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using EmergencyVR.Desktop;
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
    public sealed class XRIntroTests
    {
        QuestLookSimulation simulation;
        TrainingExperience flow;

        [UnitySetUp]
        public IEnumerator Load()
        {
            Time.timeScale = 1; AudioListener.pause = false;
            SceneManager.sceneLoaded += Install;
            yield return SceneManager.LoadSceneAsync("TrainingRoom");
            SceneManager.sceneLoaded -= Install;
            yield return new WaitForSecondsRealtime(.4f);
            flow = Object.FindFirstObjectByType<TrainingExperience>();
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
            if (flow != null && flow.Review.Manager != null) flow.Review.Manager.SetPaused(false);
            if (simulation != null) Object.Destroy(simulation.gameObject);
            Time.timeScale = 1; AudioListener.pause = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TutorialCanBeSkippedImmediatelyWithoutClaimingControlCompletion()
        {
            yield return Select("Learn controls");
            Assert.That(flow.Page, Is.EqualTo(ExperiencePage.Tutorial));
            Assert.That(flow.Tutorial.RemainingSeconds, Is.InRange(28, 30));
            Assert.That(flow.WorldVisible, Is.True);
            Assert.That(flow.Review.Manager.IsRunning, Is.False);
            yield return Select("Skip tutorial");
            Assert.That(flow.Page, Is.EqualTo(ExperiencePage.Welcome));
            Assert.That(flow.Tutorial.Skipped, Is.True);
            Assert.That(flow.Tutorial.Pointed || flow.Tutorial.Grabbed || flow.Tutorial.Teleported || flow.Tutorial.Recentered, Is.False);
            Assert.That(flow.Review.Manager.IsRunning, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TutorialTracksActualTriggerAndGripReleaseBeforeSkip()
        {
            yield return Select("Learn controls");
            yield return Select("Tutorial target");
            Assert.That(flow.Tutorial.Pointed, Is.True);
            Assert.That(flow.Tutorial.Step, Is.EqualTo(XRIntroStep.Grab));
            var cube = flow.Tutorial.PracticeObject;
            var forward = simulation.View.transform.forward;
            simulation.SetControllerPose(XRNode.RightHand, cube.transform.position - forward * .3f, Quaternion.LookRotation(forward));
            yield return new WaitForSecondsRealtime(.15f);
            simulation.SetGrip(XRNode.RightHand, true);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(cube.isSelected, Is.True, "The authored XR interactor must actually select the practice object.");
            simulation.SetGrip(XRNode.RightHand, false);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(flow.Tutorial.Grabbed, Is.True);
            Assert.That(flow.Tutorial.Step, Is.EqualTo(XRIntroStep.Teleport));
            yield return Select("Skip tutorial");
            Assert.That(flow.Tutorial.Skipped, Is.True);
            Assert.That(flow.Tutorial.Teleported || flow.Tutorial.Recentered, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RecommendedDemoContinuesFromDanielDebriefToAndresBriefing()
        {
            yield return Select("Recommended demo");
            AssertPreparedCase("review-hypotension-v2", "Daniel", "gym");
            yield return Select("Begin training");
            Assert.That(flow.Page, Is.EqualTo(ExperiencePage.Training));
            Assert.That(flow.Review.Manager.IsRunning, Is.True);
            Assert.That(flow.Review.Selected.medical.id, Is.EqualTo("review-hypotension-v2"));
            Assert.That(flow.Review.Manager.MedicalDefinition.id, Is.EqualTo("review-hypotension-v2"));
            var danielAttempt = flow.Review.Manager.MedicalSession;
            yield return Select("Finish training");
            yield return Select("Confirm finish");
            Assert.That(flow.Page, Is.EqualTo(ExperiencePage.Results));
            var danielResult = flow.Review.Manager.MedicalResult;
            Assert.That(danielResult, Is.Not.Null);
            yield return Select("Next recommended demo");
            AssertPreparedCase("arrest-witnessed", "Andrés", "football");
            // Preparing the next briefing must not erase the completed Daniel attempt.
            // Selected/MedicalDefinition become the new case only after Begin training.
            Assert.That(flow.Review.Manager.MedicalSession, Is.SameAs(danielAttempt));
            Assert.That(flow.Review.Manager.MedicalResult, Is.SameAs(danielResult));
            yield return Select("Begin training");
            Assert.That(flow.Page, Is.EqualTo(ExperiencePage.Training));
            Assert.That(flow.Review.Manager.IsRunning, Is.True);
            Assert.That(flow.Review.Selected.medical.id, Is.EqualTo("arrest-witnessed"));
            Assert.That(flow.Review.Manager.MedicalDefinition.id, Is.EqualTo("arrest-witnessed"));
            Assert.That(flow.Review.Manager.MedicalSession, Is.Not.SameAs(danielAttempt));
            Assert.That(flow.Review.Manager.MedicalSession.Completed, Is.Empty);
            yield return Select("Finish training");
            yield return Select("Confirm finish");
            Assert.That(flow.Page, Is.EqualTo(ExperiencePage.Results));
            Assert.That(flow.GetComponentsInChildren<Button>().Any(button => button.name == "Next recommended demo"), Is.False);
            yield return Select("Return catalog");
            Assert.That(flow.Page, Is.EqualTo(ExperiencePage.Catalog));
            Assert.That(flow.SelectedEnvironment, Is.EqualTo("football"));
            LogAssert.NoUnexpectedReceived();
        }

        void AssertPreparedCase(string id, string patient, string environment)
        {
            Assert.That(flow.Page, Is.EqualTo(ExperiencePage.Briefing));
            Assert.That(flow.Review.Manager.IsRunning, Is.False, "Opening a briefing must not start or reset a clinical attempt.");
            Assert.That(flow.PendingCaseIndex, Is.InRange(0, flow.Review.Catalog.entries.Length - 1));
            var prepared = flow.Review.Catalog.entries[flow.PendingCaseIndex].medical;
            Assert.That(prepared, Is.Not.Null);
            Assert.That(prepared.id, Is.EqualTo(id));
            Assert.That(flow.SelectedEnvironment, Is.EqualTo(environment));
            var title = flow.GetComponentsInChildren<Text>().Single(label => label.name == "Page title");
            Assert.That(title.text, Does.Contain(patient), "The learner must see the prepared patient's actual briefing.");
        }

        IEnumerator Select(string name)
        {
            var button = flow.GetComponentsInChildren<Button>().Single(b => b.name == name);
            var center = button.transform.TransformPoint(((RectTransform)button.transform).rect.center);
            var forward = flow.InterfaceCanvas.transform.forward;
            simulation.SetControllerPose(XRNode.RightHand, center - forward * .8f, Quaternion.LookRotation(forward, Vector3.up));
            yield return new WaitForSecondsRealtime(.12f);
            simulation.SetTrigger(XRNode.RightHand, true);
            yield return new WaitForSecondsRealtime(.08f);
            simulation.SetTrigger(XRNode.RightHand, false);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(flow.IsTransitioning, Is.False);
        }
    }
}
#endif
