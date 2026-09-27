using System;
using System.Collections;
using System.Linq;
using EmergencyVR.Medical;
using EmergencyVR.Patient.Presentation;
using EmergencyVR.Scenarios;
using EmergencyVR.UI;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EmergencyVR.Tests
{
    public sealed class Case01RecurrenceIntegrationTests
    {
        TrainingExperience flow;
        Case01PatientPresentation body;
        ScenarioManager Manager => flow.Review.Manager;

        [UnitySetUp]
        public IEnumerator LoadCase()
        {
            Time.timeScale = 1; AudioListener.pause = false;
            yield return SceneManager.LoadSceneAsync("Assets/_Project/Scenes/Training/TrainingRoom.unity");
            yield return null; yield return null;
            flow = UnityEngine.Object.FindFirstObjectByType<TrainingExperience>();
            Assert.That(flow, Is.Not.Null);
            int index = Array.FindIndex(flow.Review.Catalog.entries, e => e.medical?.id == "review-hypotension-v2");
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            flow.Review.Procedures.TrainingMode = false;
            flow.Prepare(index); flow.BeginTraining();
            yield return null; yield return null;
            body = flow.Review.GetComponent<Case01PatientPresentation>();
            Assert.That(body, Is.Not.Null); Assert.That(body.IsActive, Is.True);
            // Speed up presentation only. The clinical trajectory still uses its authored times.
            body.assistanceSeconds = 2; body.riseWarningSeconds = 2;
            PositionLearner(); yield return null;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            if (flow != null)
            {
                Manager.SetPaused(false);
                if (Manager.IsRunning) Manager.FinishCase();
            }
            Time.timeScale = 1; AudioListener.pause = false;
            yield return null;
        }

        void PositionLearner()
        {
            var camera = Camera.main; Assert.That(camera, Is.Not.Null);
            var target = body.AssistanceAnchor.position;
            var desktop = UnityEngine.Object.FindFirstObjectByType<EmergencyVR.Desktop.DesktopDemoController>();
            var origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
            if (desktop != null)
            {
                bool enabled = desktop.Body.enabled; desktop.Body.enabled = false;
                desktop.transform.position += target - camera.transform.position;
                desktop.Body.enabled = enabled;
            }
            else if (origin != null && camera.transform.IsChildOf(origin.transform)) origin.transform.position += target - camera.transform.position;
            else camera.transform.position = target;
            Physics.SyncTransforms();
        }

        IEnumerator WaitFor(Func<bool> condition, float maximumSeconds, string expectation)
        {
            float deadline = Time.realtimeSinceStartup + maximumSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, expectation + " | " + body.Instruction + " | " + body.LastValidationFailure);
        }

        IEnumerator AssistUntilImproved()
        {
            Assert.That(body.BeginRiseAttempt(), Is.False, "The initial presyncope state does not permit a rise transition.");
            Assert.That(body.BeginAssistance(), Is.True, body.LastValidationFailure);
            body.SetSupportHeld(true);
            yield return WaitFor(() => body.FinalPositionValidated, 8, "The first support must be reached through the physical route.");
            Assert.That(Manager.MedicalSession.ClinicalState.ClinicalPosition, Is.EqualTo(ClinicalPosition.Supine));
            var definition = flow.Review.Selected.medical.clinicalV2;
            double duration = definition.clinicalStates.Single(s => s.id == "HYP_01_SUPPORTED_OBSERVATION")
                .vitalTrajectories.Max(t => t.durationSeconds);
            Manager.AdvanceTrainingTime(duration + .1);
            yield return null;
            Assert.That(Manager.MedicalSession.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_02_IMPROVING"));
        }

        [UnityTest]
        public IEnumerator ValidatedSeatSupportAchievesProtectionWithoutForcingThePatientOntoTheFloor()
        {
            var before = body.PelvisPosition;
            Assert.That(body.MaintainSupportedPosition(), Is.True, body.LastValidationFailure);
            Assert.That(Manager.MedicalSession.ObjectiveProgress.Single(o => o.objectiveId == "OBJ_02").result,
                Is.EqualTo(ObjectiveResult.AchievedIndependently));
            Assert.That(Manager.MedicalSession.ClinicalState.ClinicalPosition, Is.EqualTo(ClinicalPosition.SeatedSupported));
            Assert.That(Manager.MedicalSession.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_00_INITIAL_PRESYNCOPE"));
            Assert.That(Vector3.Distance(body.PelvisPosition, before), Is.LessThan(.001f));
            int evidenceCount = Manager.MedicalSession.ClinicalEvents.Count;
            Assert.That(body.MaintainSupportedPosition(), Is.True);
            Assert.That(Manager.MedicalSession.ClinicalEvents.Count, Is.EqualTo(evidenceCount));
            Assert.That(Manager.MedicalSession.ClinicalEvents.Any(e => e.eventType == "PhysicalPositionConfirmed"), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PreventingTheWarnedRisePreservesSupportedPositionAndCancelsThePendingTransition()
        {
            yield return AssistUntilImproved();
            var runtime = Manager.MedicalSession;
            var supported = body.PelvisPosition;
            int confirmations = runtime.ClinicalEvents.Count(e => e.eventType == "PhysicalPositionConfirmed");
            Assert.That(body.BeginRiseAttempt(), Is.True, body.LastValidationFailure);
            Assert.That(body.IsRiseWarning, Is.True);
            Assert.That(body.CanPreventRise, Is.True);
            Assert.That(runtime.RequestedPosition, Is.EqualTo(PhysicalPosition.Standing));
            Assert.That(body.PreventRiseAttempt(), Is.True);
            yield return null;
            Assert.That(body.IsAttemptingRise, Is.False);
            Assert.That(body.IsStanding, Is.False);
            Assert.That(body.FinalPositionValidated, Is.True);
            Assert.That(runtime.RequestedPosition, Is.EqualTo(PhysicalPosition.Unknown));
            Assert.That(runtime.ClinicalState.ClinicalPosition, Is.EqualTo(ClinicalPosition.Supine));
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_02_IMPROVING"));
            Assert.That(Vector3.Distance(body.PelvisPosition, supported), Is.LessThan(.001f));
            Assert.That(runtime.ClinicalEvents.Count(e => e.eventType == "PhysicalPositionConfirmed"), Is.EqualTo(confirmations));
            Assert.That(runtime.ClinicalEvents.Any(e => e.eventType == "PositionAssistanceCancelled"), Is.True);
        }

        [UnityTest]
        public IEnumerator PausingAndPreventingARiseInProgressReturnsThroughTheValidatedRoute()
        {
            yield return AssistUntilImproved();
            Assert.That(body.BeginRiseAttempt(), Is.True, body.LastValidationFailure);
            yield return WaitFor(() => body.Progress > .15f, 6, "The patient must actually begin rising before cancellation.");
            Manager.SetPaused(true);
            float progress = body.Progress; var pelvis = body.PelvisPosition;
            Assert.That(body.PreventRiseAttempt(), Is.False);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(body.Progress, Is.EqualTo(progress));
            Assert.That(Vector3.Distance(body.PelvisPosition, pelvis), Is.LessThan(.001f));
            Manager.SetPaused(false);
            Assert.That(body.PreventRiseAttempt(), Is.True);
            yield return WaitFor(() => !body.IsAttemptingRise, 6, "Prevention must physically return to supported rest.");
            Assert.That(body.FinalPositionValidated, Is.True);
            Assert.That(body.IsStanding, Is.False);
            Assert.That(Manager.MedicalSession.ClinicalState.ClinicalPosition, Is.EqualTo(ClinicalPosition.Supine));
            Assert.That(Manager.MedicalSession.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_02_IMPROVING"));
        }

        [UnityTest]
        public IEnumerator ActualStandingProducesRecurrenceAndHeldAssistancePhysicallyRestoresSupport()
        {
            yield return AssistUntilImproved();
            var runtime = Manager.MedicalSession;
            var supported = body.PelvisPosition;
            Assert.That(body.BeginRiseAttempt(), Is.True, body.LastValidationFailure);
            Assert.That(runtime.ClinicalState.ClinicalPosition, Is.EqualTo(ClinicalPosition.Supine));
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_02_IMPROVING"));
            yield return WaitFor(() => body.IsStanding, 9, "Only the completed physical rise may cause recurrent presyncope.");
            Assert.That(runtime.ClinicalState.ClinicalPosition, Is.EqualTo(ClinicalPosition.Standing));
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_04_RECURRENT_PRESYNCOPE"));
            Assert.That(body.PelvisPosition.y, Is.GreaterThan(.65f));
            Assert.That(body.HeadPosition.y, Is.GreaterThan(body.PelvisPosition.y + .25f));
            Assert.That(body.FinalPositionValidated, Is.False);
            Assert.That(body.BeginAssistance(), Is.True, body.LastValidationFailure);
            body.SetSupportHeld(true);
            yield return WaitFor(() => body.FinalPositionValidated, 8, "Recurrent symptoms require another physically completed support.");
            Assert.That(body.IsStanding, Is.False);
            Assert.That(runtime.ClinicalState.ClinicalPosition, Is.EqualTo(ClinicalPosition.Supine));
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_01_SUPPORTED_OBSERVATION"));
            Assert.That(Vector3.Distance(body.PelvisPosition, supported), Is.LessThan(.02f));
            Assert.That(runtime.ClinicalEvents.Count(e => e.eventType == "PhysicalPositionConfirmed"), Is.EqualTo(3));
            Assert.That(runtime.Observations.Measurements, Is.Empty);
        }
    }
}
