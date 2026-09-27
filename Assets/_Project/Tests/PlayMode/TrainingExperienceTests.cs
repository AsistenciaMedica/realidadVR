using System;
using System.Collections;
using System.Linq;
using EmergencyVR.Medical;
using EmergencyVR.Medical.Interaction;
using EmergencyVR.Scenarios;
using EmergencyVR.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace EmergencyVR.Tests
{
    public sealed class TrainingExperienceTests
    {
        TrainingExperience flow;
        [UnitySetUp] public IEnumerator Load()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("Assets/_Project/Scenes/Training/TrainingRoom.unity");
            yield return null; yield return null;
            flow = UnityEngine.Object.FindFirstObjectByType<TrainingExperience>();
            Assert.That(flow, Is.Not.Null);
        }
        [UnityTearDown] public IEnumerator Restore()
        {
            if (flow != null && flow.Review.Manager != null) { flow.Review.Manager.SetPaused(false); if (flow.Review.Manager.IsRunning) flow.Review.Manager.FinishCase(); }
            Time.timeScale = 1; yield return null;
        }
        int MedicalIndex => Array.FindIndex(flow.Review.Catalog.entries, e => e.medical != null);

        [UnityTest] public IEnumerator WelcomeCatalogAndBriefingDoNotStartOrChangeTheClinicalWorld()
        {
            var original = flow.Review.SelectedIndex;
            Assert.That(flow.Page, Is.EqualTo(ExperiencePage.Welcome));
            Assert.That(flow.WorldVisible, Is.False);
            Assert.That(flow.GetComponentsInChildren<Text>().Any(t => t.text == VitalBrand.Tagline), Is.True);
            flow.Browse("gym"); yield return null;
            flow.Prepare(MedicalIndex); yield return null;
            Assert.That(flow.Page, Is.EqualTo(ExperiencePage.Briefing));
            Assert.That(flow.Review.SelectedIndex, Is.EqualTo(original));
            Assert.That(flow.Review.Manager.IsRunning, Is.False);
            Assert.That(flow.WorldVisible, Is.False);
            flow.BeginTraining(); yield return null;
            Assert.That(flow.WorldVisible, Is.True);
            Assert.That(flow.Page, Is.EqualTo(ExperiencePage.Training));
            Assert.That(flow.Review.SelectedIndex, Is.EqualTo(MedicalIndex));
            Assert.That(flow.Review.Manager.IsRunning, Is.True);
        }

        [UnityTest] public IEnumerator PauseFreezesPatientTimelineClockAndRejectsClinicalActions()
        {
            flow.Prepare(MedicalIndex); flow.BeginTraining(); yield return null;
            var manager = flow.Review.Manager;
            flow.TogglePause(); yield return null;
            double at = manager.ElapsedSeconds; var before = JsonUtility.ToJson(manager.MedicalSession.Patient);
            int actions = manager.MedicalSession.Completed.Length;
            manager.SubmitAction(flow.Review.ActionIds[0]); manager.AdvanceTrainingTime(60);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(manager.ElapsedSeconds, Is.EqualTo(at).Within(.005));
            Assert.That(JsonUtility.ToJson(manager.MedicalSession.Patient), Is.EqualTo(before));
            Assert.That(manager.MedicalSession.Completed.Length, Is.EqualTo(actions));
            Assert.That(Time.timeScale, Is.EqualTo(0));
            Assert.That(flow.Review.Select(MedicalIndex + 1), Is.False);
            flow.TogglePause(); yield return new WaitForSecondsRealtime(.08f);
            Assert.That(manager.IsPaused, Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(manager.ElapsedSeconds - at, Is.GreaterThan(.03).And.LessThan(.2));
        }

        [UnityTest] public IEnumerator AssessmentReadingsAreAcquiredSnapshotsAndResetBetweenAttempts()
        {
            flow.Review.Procedures.TrainingMode = false;
            flow.Prepare(MedicalIndex); flow.BeginTraining(); yield return null;
            Assert.That(flow.MonitorValue("spo2"), Is.EqualTo("—"));
            var sample = flow.Review.Manager.MedicalSession.Patient; sample.spo2 = 93;
            flow.Review.Procedures.RecordMeasurement(MedicalToolKind.Oximeter, sample);
            sample.spo2 = 80;
            Assert.That(flow.MonitorValue("spo2"), Is.EqualTo("93"));
            Assert.That(flow.MonitorValue("bp"), Is.EqualTo("—"));
            flow.FinishTraining(); yield return null;
            flow.Prepare(MedicalIndex); flow.BeginTraining(); yield return null;
            Assert.That(flow.MonitorValue("spo2"), Is.EqualTo("—"));
        }

        [UnityTest] public IEnumerator PausingMidCompressionDoesNotInventACompletedCycle()
        {
            flow.Prepare(Array.FindIndex(flow.Review.Catalog.entries, e => e.medical?.id == "arrest-witnessed"));
            flow.BeginTraining(); yield return null;
            var cpr = flow.Review.Procedures.CPR;
            cpr.Feed(.055f, 0, 0, true, "TEST");
            flow.TogglePause(); cpr.ReleaseContact();
            Assert.That(cpr.Metrics.compressions, Is.Zero);
            Assert.That(AudioListener.pause, Is.True);
            flow.TogglePause(); yield return null;
            cpr.Feed(.055f, 0, 0, true, "TEST"); cpr.ReleaseContact();
            Assert.That(cpr.Metrics.compressions, Is.EqualTo(1));
            Assert.That(AudioListener.pause, Is.False);
        }

        [UnityTest] public IEnumerator ProductFlowPreservesScoringAndDebriefAcrossAllEnvironments()
        {
            foreach (var environment in new[] { "gym", "mall", "dental", "football" })
            {
                flow.Browse(environment);
                flow.Prepare(Array.FindIndex(flow.Review.Catalog.entries, e => e.medical?.environment == environment));
                flow.BeginTraining(); yield return null;
                foreach (var id in flow.Review.Selected.medical.recommendedSequence)
                {
                    var earliest = flow.Review.Manager.MedicalSession.EarliestTime(id);
                    if (earliest > flow.Review.Manager.MedicalSession.Elapsed) flow.Review.Manager.AdvanceTrainingTime(earliest - flow.Review.Manager.MedicalSession.Elapsed + .01);
                    flow.Review.Submit(id);
                }
                flow.FinishTraining(); yield return null;
                Assert.That(flow.Page, Is.EqualTo(ExperiencePage.Results));
                Assert.That(flow.Review.Score, Is.EqualTo(100), environment);
                Assert.That(flow.Review.Manager.MedicalResult.timeline.Length, Is.GreaterThan(0));
                Assert.That(flow.Review.Manager.MedicalResult.procedures, Is.Not.Null);
            }
        }
    }
}
