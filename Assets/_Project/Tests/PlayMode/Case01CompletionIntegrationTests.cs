using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EmergencyVR.Dialogue;
using EmergencyVR.Medical;
using EmergencyVR.Patient.Presentation;
using EmergencyVR.Scenarios;
using EmergencyVR.UI;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace EmergencyVR.Tests
{
    public sealed class Case01CompletionIntegrationTests
    {
        TrainingExperience flow;
        Case01PatientPresentation body;
        ClinicalHelpController help;
        readonly List<GameObject> obstacles = new List<GameObject>();
        ScenarioManager Manager => flow.Review.Manager;
        int CaseIndex => Array.FindIndex(flow.Review.Catalog.entries, e => e.medical?.id == "review-hypotension-v2");

        [UnitySetUp]
        public IEnumerator LoadCase()
        {
            Time.timeScale = 1; AudioListener.pause = false;
            yield return SceneManager.LoadSceneAsync("Assets/_Project/Scenes/Training/TrainingRoom.unity");
            yield return null; yield return null;
            flow = UnityEngine.Object.FindFirstObjectByType<TrainingExperience>();
            Assert.That(flow, Is.Not.Null);
            Assert.That(CaseIndex, Is.GreaterThanOrEqualTo(0));
            flow.Review.Procedures.TrainingMode = false;
            flow.Prepare(CaseIndex); flow.BeginTraining();
            yield return null; yield return null;
            body = flow.Review.GetComponent<Case01PatientPresentation>();
            help = flow.Review.GetComponent<ClinicalHelpController>();
            Assert.That(body, Is.Not.Null); Assert.That(body.IsActive, Is.True);
            Assert.That(help, Is.Not.Null); Assert.That(help.IsActive, Is.True);
            PositionLearnerAtAssistanceAnchor();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            foreach (var obstacle in obstacles) if (obstacle != null) UnityEngine.Object.Destroy(obstacle);
            obstacles.Clear();
            if (flow != null && flow.Review.Manager != null)
            {
                Manager.SetPaused(false);
                if (Manager.IsRunning) Manager.FinishCase();
            }
            Time.timeScale = 1; AudioListener.pause = false;
            yield return null;
        }

        void PositionLearnerAtAssistanceAnchor()
        {
            Assert.That(body.AssistanceAnchor, Is.Not.Null);
            var camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            var origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
            var desktop = UnityEngine.Object.FindFirstObjectByType<EmergencyVR.Desktop.DesktopDemoController>();
            var target = body.AssistanceAnchor.position;
            if (desktop != null)
            {
                bool enabled = desktop.Body.enabled;
                desktop.Body.enabled = false;
                desktop.transform.position += target - camera.transform.position;
                desktop.Body.enabled = enabled;
            }
            else if (origin != null && camera.transform.IsChildOf(origin.transform))
                origin.transform.position += target - camera.transform.position;
            else camera.transform.position = target;
            Physics.SyncTransforms();
        }

        IEnumerator WaitFor(Func<bool> condition, float maximumSeconds, string requirement)
        {
            float deadline = Time.realtimeSinceStartup + maximumSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, requirement + " | " + body.Instruction + " | " + body.LastValidationFailure);
        }

        [UnityTest]
        public IEnumerator I0CompletesThroughValidatedBodyDialogueCommunicationAndEvidenceWithoutHiddenMeasurements()
        {
            var runtime = Manager.MedicalSession;
            Assert.That(runtime.Observations.All, Is.Empty);
            Assert.That(body.HeadPosition.y, Is.GreaterThan(body.PelvisPosition.y + .25f));
            Assert.That(body.LeftFootPosition.y, Is.InRange(-.01f, .30f));
            Assert.That(body.RightFootPosition.y, Is.InRange(-.01f, .30f));
            Assert.That(Manager.TrySubmitAction("AssessResponsiveness"), Is.True);
            Assert.That(Manager.TrySubmitAction("ObserveBreathing"), Is.True);
            Assert.That(Manager.Dialogue.Ask(DialogueIntent.MAIN_SYMPTOM), Is.Not.Null);
            Assert.That(Manager.Dialogue.Ask(DialogueIntent.ONSET), Is.Not.Null);
            Assert.That(help.RequestCall(false), Is.True);
            Manager.AdvanceTrainingTime(help.ContactPresentationSeconds + .1);
            yield return null;
            Assert.That(help.CommunicateLocation(), Is.True);
            Assert.That(help.CommunicateSituation(), Is.True);
            Assert.That(help.ConfirmOperator(), Is.True);

            var seatedPelvis = body.PelvisPosition;
            Assert.That(body.BeginAssistance(), Is.True, body.LastValidationFailure);
            body.SetSupportHeld(true);
            yield return WaitFor(() => body.Progress > .15f, 5, "Physical assistance must advance gradually.");
            Assert.That(runtime.ClinicalState.ClinicalPosition, Is.EqualTo(ClinicalPosition.SeatedSupported),
                "Movement in progress is not proof of reaching the final supported position.");
            flow.TogglePause();
            var pausedState = runtime.ClinicalState;
            float progress = body.Progress; var pelvis = body.PelvisPosition;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(body.Progress, Is.EqualTo(progress));
            Assert.That(Vector3.Distance(body.PelvisPosition, pelvis), Is.LessThan(.0001f));
            Assert.That(runtime.ClinicalState.SimulationTime, Is.EqualTo(pausedState.SimulationTime));
            Assert.That(runtime.ClinicalState.RespiratoryPhase, Is.EqualTo(pausedState.RespiratoryPhase));
            flow.TogglePause(); body.SetSupportHeld(true);
            yield return WaitFor(() => body.FinalPositionValidated, body.assistanceSeconds + 6, "Only geometric validation may confirm physical support.");
            Assert.That(body.IsAssisting, Is.False);
            Assert.That(runtime.ClinicalState.ClinicalPosition, Is.EqualTo(ClinicalPosition.Supine));
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_01_SUPPORTED_OBSERVATION"));
            Assert.That(Vector3.Distance(body.PelvisPosition, seatedPelvis), Is.GreaterThan(.5f));
            Assert.That(body.PelvisPosition.y, Is.GreaterThan(0));
            Assert.That(body.HeadPosition.y, Is.GreaterThan(0));
            Manager.AdvanceTrainingTime(1);
            Assert.That(Manager.TrySubmitAction("ReassessPatient"), Is.True);
            Assert.That(help.PerformHandover(), Is.True);
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_07_HANDOVER"));
            Assert.That(runtime.ObjectiveProgress.Count, Is.EqualTo(5));
            Assert.That(runtime.ObjectiveProgress.All(o => o.result == ObjectiveResult.AchievedIndependently), Is.True);
            Assert.That(runtime.Observations.Measurements, Is.Empty);
            Assert.That(flow.MonitorValue("bp"), Is.EqualTo("—"));
            Assert.That(flow.MonitorValue("spo2"), Is.EqualTo("—"));
            flow.FinishTraining(); yield return null;
            string path = flow.Review.ExportResult();
            try
            {
                string json = File.ReadAllText(path);
                Assert.That(json, Does.Not.Contain("\"scorePercent\""));
                Assert.That(json, Does.Not.Contain("\"initialPatient\""));
                Assert.That(json, Does.Not.Contain("\"finalPatient\""));
                var report = JsonUtility.FromJson<ClinicalV2Report>(json);
                Assert.That(report.objectives.All(o => o.result == ObjectiveResult.AchievedIndependently), Is.True);
                Assert.That(report.measurements, Is.Empty);
                Assert.That(report.clinicalEvents.Any(e => e.eventType == "PhysicalPositionConfirmed"), Is.True);
                Assert.That(report.clinicalEvents.Any(e => e.eventType == "CommunicationRecorded"), Is.True);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [UnityTest]
        public IEnumerator ObstacleOnTheRoutePreventsStartingAndCannotRegisterPosturalSuccess()
        {
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Test obstruction in assistance area";
            blocker.transform.position = body.TransitionAreaCenter + Vector3.up * .15f;
            blocker.transform.localScale = new Vector3(.8f, .7f, .8f);
            obstacles.Add(blocker); Physics.SyncTransforms();
            var before = body.PelvisPosition;
            Assert.That(body.BeginAssistance(), Is.False);
            Assert.That(body.LastValidationFailure, Is.Not.Empty);
            Assert.That(body.IsAssisting, Is.False);
            Assert.That(Manager.MedicalSession.ClinicalState.ClinicalPosition, Is.EqualTo(ClinicalPosition.SeatedSupported));
            Assert.That(Manager.MedicalSession.ClinicalEvents.Any(e => e.eventType == "PhysicalPositionConfirmed"), Is.False);
            Assert.That(Vector3.Distance(body.PelvisPosition, before), Is.LessThan(.0001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReleasingSupportStopsMovementAndCancellationReturnsToTheValidatedSeat()
        {
            var seated = body.PelvisPosition;
            Assert.That(body.BeginAssistance(), Is.True, body.LastValidationFailure);
            body.SetSupportHeld(true);
            yield return WaitFor(() => body.Progress > .12f, 5, "Assistance must begin before testing release.");
            body.SetSupportHeld(false);
            float stopped = body.Progress;
            yield return new WaitForSeconds(.2f);
            Assert.That(body.Progress, Is.EqualTo(stopped));
            Assert.That(Manager.MedicalSession.ClinicalState.ClinicalPosition, Is.EqualTo(ClinicalPosition.SeatedSupported));
            body.CancelAssistance();
            yield return WaitFor(() => !body.IsAssisting, 6, "Cancellation must return through the safe path.");
            Assert.That(body.Progress, Is.Zero);
            Assert.That(body.FinalPositionValidated, Is.False);
            Assert.That(Vector3.Distance(body.PelvisPosition, seated), Is.LessThan(.01f));
            Assert.That(Manager.MedicalSession.ClinicalEvents.Any(e => e.eventType == "PositionAssistanceCancelled"), Is.True);
            Assert.That(Manager.MedicalSession.ClinicalEvents.Any(e => e.eventType == "PhysicalPositionConfirmed"), Is.False);
        }

        [UnityTest]
        public IEnumerator ResetDuringAssistanceClearsPhysicalAndHelpProgressForTheNewAttempt()
        {
            string oldAttempt = Manager.MedicalSession.AttemptId;
            Assert.That(help.RequestCall(true), Is.True);
            Assert.That(body.BeginAssistance(), Is.True, body.LastValidationFailure);
            body.SetSupportHeld(true);
            yield return WaitFor(() => body.Progress > .1f, 5, "The reset test must interrupt actual movement.");
            flow.FinishTraining(); yield return null;
            flow.Prepare(CaseIndex); flow.BeginTraining();
            yield return null; yield return null;
            PositionLearnerAtAssistanceAnchor();
            Assert.That(Manager.MedicalSession.AttemptId, Is.Not.EqualTo(oldAttempt));
            Assert.That(body.Progress, Is.Zero);
            Assert.That(body.IsAssisting, Is.False);
            Assert.That(body.FinalPositionValidated, Is.False);
            Assert.That(help.Status, Is.EqualTo(ClinicalHelpStage.Idle));
            Assert.That(Manager.MedicalSession.HelpState, Is.EqualTo(HelpRequestState.None));
            Assert.That(Manager.MedicalSession.Observations.All, Is.Empty);
            Assert.That(Manager.MedicalSession.ClinicalEvents.All(e => e.attemptId == Manager.MedicalSession.AttemptId), Is.True);
        }

        [UnityTest]
        public IEnumerator DirectAndDelegatedHelpRequireCommunicationAndUseThePausedClinicalClock()
        {
            foreach (bool delegated in new[] { false, true })
            {
                Assert.That(help.RequestCall(delegated), Is.True);
                Assert.That(help.ConfirmOperator(), Is.False);
                Assert.That(help.PerformHandover(), Is.False);
                Assert.That(help.CommunicateSituation(), Is.False);
                flow.TogglePause();
                double at = Manager.MedicalSession.Elapsed;
                Manager.AdvanceTrainingTime(10);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(Manager.MedicalSession.Elapsed, Is.EqualTo(at));
                Assert.That(help.Status, Is.EqualTo(ClinicalHelpStage.Connecting));
                flow.TogglePause();
                Manager.AdvanceTrainingTime(help.ContactPresentationSeconds + .1);
                yield return null;
                Assert.That(help.CommunicateLocation(), Is.True);
                Assert.That(help.ConfirmOperator(), Is.False, "Location alone is not the completed conversation.");
                Assert.That(help.CommunicateSituation(), Is.True);
                Assert.That(help.ConfirmOperator(), Is.True);
                Assert.That(Manager.MedicalSession.HelpState, Is.EqualTo(HelpRequestState.Confirmed));
                Assert.That(Manager.MedicalSession.Observations.All, Is.Empty);
                Assert.That(help.SituationSummary, Does.Not.Contain("85/55"));
                Assert.That(help.SituationSummary, Does.Not.Contain("97"));
                Assert.That(help.SituationSummary, Does.Not.Contain("Daniel"));
                if (!delegated)
                {
                    flow.FinishTraining(); yield return null;
                    flow.Prepare(CaseIndex); flow.BeginTraining();
                    yield return null; yield return null;
                }
            }
        }

        [UnityTest]
        public IEnumerator Case01KeepsTheCanvasInputAdapterForTheActualDesktopOrXrMode()
        {
            var canvas = flow.InterfaceCanvas;
            if (flow.IsDesktop)
            {
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
                Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            }
            else
            {
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
                Assert.That(canvas.GetComponent<TrackedDeviceGraphicRaycaster>(), Is.Not.Null);
            }
            Assert.That(Manager.TrySubmitAction("AssessResponsiveness"), Is.True);
            yield return null;
            Assert.That(Manager.MedicalSession.Observations.Physical.Any(o => o.type == "PatientResponsive"), Is.True);
            Assert.That(Manager.MedicalSession.Observations.Measurements, Is.Empty);
            Assert.That(flow.MonitorValue("bp"), Is.EqualTo("—"));
        }
    }
}
