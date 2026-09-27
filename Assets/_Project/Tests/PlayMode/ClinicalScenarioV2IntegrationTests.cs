using System;
using System.Collections;
using System.IO;
using System.Linq;
using EmergencyVR.Medical;
using EmergencyVR.Patient;
using EmergencyVR.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace EmergencyVR.Tests
{
    public sealed class ClinicalScenarioV2IntegrationTests
    {
        TrainingExperience flow;
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
            flow.Prepare(CaseIndex); flow.BeginTraining();
            yield return null;
            Assert.That(flow.Review.Manager.IsRunning, Is.True);
            Assert.That(flow.Review.Manager.MedicalSession.ClinicalState, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            if (flow != null && flow.Review.Manager != null)
            {
                flow.Review.Manager.SetPaused(false);
                if (flow.Review.Manager.IsRunning) flow.Review.Manager.FinishCase();
            }
            Time.timeScale = 1; AudioListener.pause = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClinicalMonitorDoesNotRevealInternalValuesInEitherTrainingMode()
        {
            foreach (bool guided in new[] { true, false })
            {
                flow.Review.Procedures.TrainingMode = guided;
                yield return null;
                Assert.That(flow.MonitorValue("bp"), Is.EqualTo("—"));
                Assert.That(flow.MonitorValue("spo2"), Is.EqualTo("—"));
                Assert.That(flow.Review.Manager.MedicalSession.Observations.All, Is.Empty);
                Assert.That(flow.GetComponentsInChildren<Text>().Any(t => t.text.Contains("85/55")), Is.False);
            }
            var response = flow.Review.Manager.Dialogue.Ask(DialogueIntent.MAIN_SYMPTOM);
            yield return null;
            Assert.That(response, Is.Not.Null);
            Assert.That(flow.Review.Manager.MedicalSession.Observations.Interviews.Count, Is.EqualTo(1));
            Assert.That(flow.Review.Manager.MedicalSession.Observations.Measurements, Is.Empty);
            Assert.That(flow.MonitorValue("bp"), Is.EqualTo("—"));
        }

        [UnityTest]
        public IEnumerator PausingFreezesRuntimeAndPhysicalTransitionUntilResume()
        {
            var manager = flow.Review.Manager;
            var position = manager.PositionTransitions;
            Assert.That(position.Request(PhysicalPosition.Supine), Is.True);
            string ticket = position.TransitionId;
            Assert.That(position.BeginPreparing(ticket), Is.True);
            Assert.That(position.BeginTransition(ticket), Is.True);
            manager.SetPaused(true);
            var before = manager.MedicalSession.ClinicalState;
            var presented = UnityEngine.Object.FindFirstObjectByType<PatientController>().ClinicalState;
            Assert.That(presented, Is.Not.Null);
            Assert.That(presented.SimulationTime, Is.EqualTo(before.SimulationTime),
                "The last tick at pause must be published to patient presentation immediately.");
            Assert.That(presented.RespiratoryPhase, Is.EqualTo(before.RespiratoryPhase));
            int eventsBefore = manager.MedicalSession.ClinicalEvents.Count;
            Assert.That(position.Confirm(ticket, true), Is.False);
            Assert.That(manager.Dialogue.Ask(DialogueIntent.MAIN_SYMPTOM), Is.Null);
            Assert.That(manager.PerformClinical((runtime, time) => runtime.RequestHelp(time)), Is.False);
            yield return new WaitForSecondsRealtime(.2f);
            var paused = manager.MedicalSession.ClinicalState;
            Assert.That(paused.SimulationTime, Is.EqualTo(before.SimulationTime));
            Assert.That(paused.RespiratoryPhase, Is.EqualTo(before.RespiratoryPhase));
            Assert.That(paused.ClinicalPosition, Is.EqualTo(ClinicalPosition.SeatedSupported));
            Assert.That(manager.MedicalSession.ClinicalEvents.Count, Is.EqualTo(eventsBefore));
            Assert.That(position.State, Is.EqualTo(PositionTransitionPhase.Transitioning));
            manager.SetPaused(false);
            Assert.That(position.Confirm(ticket, true), Is.True);
            Assert.That(manager.MedicalSession.ClinicalState.ClinicalPosition, Is.EqualTo(ClinicalPosition.Supine));
            Assert.That(manager.MedicalSession.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_01_SUPPORTED_OBSERVATION"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator FailedOrCancelledPhysicalTransitionCannotImprovePatient()
        {
            var manager = flow.Review.Manager;
            var position = manager.PositionTransitions;
            Assert.That(position.Request(PhysicalPosition.Supine), Is.True);
            var firstTicket = position.TransitionId;
            Assert.That(position.Confirm(firstTicket, true), Is.False, "A requested transition is not yet physically completed.");
            Assert.That(position.BeginPreparing(firstTicket), Is.True);
            Assert.That(position.BeginTransition(firstTicket), Is.True);
            Assert.That(position.Confirm(firstTicket, false), Is.False);
            Assert.That(position.State, Is.EqualTo(PositionTransitionPhase.Failed));
            Assert.That(manager.MedicalSession.ClinicalState.ClinicalPosition, Is.EqualTo(ClinicalPosition.SeatedSupported));
            Assert.That(position.Request(PhysicalPosition.Supine), Is.True);
            Assert.That(position.TransitionId, Is.Not.EqualTo(firstTicket));
            Assert.That(position.Confirm(firstTicket, true), Is.False, "A stale transition ticket is never accepted.");
            Assert.That(position.Cancel("TrackingLost"), Is.True);
            Assert.That(position.State, Is.EqualTo(PositionTransitionPhase.Cancelled));
            manager.AdvanceTrainingTime(10);
            Assert.That(manager.MedicalSession.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_00_INITIAL_PRESYNCOPE"));
            Assert.That(manager.MedicalSession.Patient.systolic, Is.EqualTo(85));
            yield return null;
        }

        [UnityTest]
        public IEnumerator NewAttemptClearsDialogueObservationsHelpAndRejectsOldPositionTicket()
        {
            var manager = flow.Review.Manager;
            var oldRuntime = manager.MedicalSession;
            string oldAttemptId = oldRuntime.AttemptId;
            Assert.That(manager.Dialogue.Ask(DialogueIntent.MAIN_SYMPTOM), Is.Not.Null);
            Assert.That(manager.PerformClinical((runtime, time) => runtime.RequestHelp(time)), Is.True);
            Assert.That(manager.PositionTransitions.Request(PhysicalPosition.Supine), Is.True);
            string oldTicket = manager.PositionTransitions.TransitionId;
            manager.PositionTransitions.BeginPreparing(oldTicket);
            manager.PositionTransitions.BeginTransition(oldTicket);
            flow.FinishTraining(); yield return null;
            flow.Prepare(CaseIndex); flow.BeginTraining(); yield return null;
            var fresh = manager.MedicalSession;
            Assert.That(fresh, Is.Not.SameAs(oldRuntime));
            Assert.That(fresh.AttemptId, Is.Not.EqualTo(oldAttemptId));
            Assert.That(fresh.Observations.All, Is.Empty);
            Assert.That(fresh.HelpState, Is.EqualTo(HelpRequestState.None));
            Assert.That(fresh.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_00_INITIAL_PRESYNCOPE"));
            Assert.That(fresh.ObjectiveProgress.All(o => o.result == ObjectiveResult.NotEvaluated), Is.True);
            Assert.That(fresh.ClinicalEvents.All(e => e.attemptId == fresh.AttemptId), Is.True);
            Assert.That(manager.Dialogue.LastResponse, Is.Null);
            Assert.That(manager.PositionTransitions.State, Is.EqualTo(PositionTransitionPhase.Idle));
            Assert.That(manager.PositionTransitions.Confirm(oldTicket, true), Is.False);
            Assert.That(flow.MonitorValue("bp"), Is.EqualTo("—"));
        }

        [UnityTest]
        public IEnumerator ManagerUsesOneClockForSemanticActionsAndIncludesActualBuildVersion()
        {
            var manager = flow.Review.Manager;
            yield return new WaitForSeconds(.08f);
            Assert.That(manager.PerformClinical((runtime, time) => runtime.RequestHelp(time)), Is.True);
            var requested = manager.MedicalSession.ClinicalEvents.Last(e => e.eventType == "HelpRequested");
            Assert.That(requested.simulationTime, Is.EqualTo(manager.MedicalSession.Elapsed).Within(.001));
            Assert.That(requested.buildVersion, Is.EqualTo(Application.version));
            Assert.That(requested.scenarioVersion, Is.EqualTo("2.0.0"));
            Assert.That(requested.clinicalSpecVersion, Is.EqualTo("0.1"));
            Assert.That(requested.trainingProfile, Is.EqualTo("I0_FIRST_RESPONDER"));
        }

        [UnityTest]
        public IEnumerator ExportIncludesActualKnowledgeAndVersionsWithoutLegacyPercentage()
        {
            var manager = flow.Review.Manager;
            manager.Dialogue.Ask(DialogueIntent.MAIN_SYMPTOM);
            manager.PerformClinical((runtime, time) => runtime.RequestHelp(time));
            string attemptId = manager.MedicalSession.AttemptId;
            flow.FinishTraining(); yield return null;
            string path = flow.Review.ExportResult();
            try
            {
                Assert.That(File.Exists(path), Is.True);
                string json = File.ReadAllText(path);
                Assert.That(json, Does.Not.Contain("\"scorePercent\""));
                var report = JsonUtility.FromJson<V2ExportHeader>(json);
                Assert.That(report.attemptId, Is.EqualTo(attemptId));
                Assert.That(report.scenarioVersion, Is.EqualTo("2.0.0"));
                Assert.That(report.clinicalSpecVersion, Is.EqualTo("0.1"));
                Assert.That(report.trainingProfile, Is.EqualTo("I0_FIRST_RESPONDER"));
                Assert.That(report.buildVersion, Is.EqualTo(Application.version));
                Assert.That(report.interviews.Length, Is.EqualTo(1));
                Assert.That(report.interviews[0].intent, Is.EqualTo(DialogueIntent.MAIN_SYMPTOM));
                Assert.That(report.measurements, Is.Empty);
                Assert.That(report.objectives.Length, Is.EqualTo(5));
                Assert.That(report.clinicalEvents.Any(e => e.eventType == "HelpRequested"), Is.True);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [UnityTest]
        public IEnumerator RejectedRepeatDoesNotReportTheSuccessOfAnEarlierAcceptedAction()
        {
            var manager = flow.Review.Manager;
            var rig = flow.Review.Procedures;
            Assert.That(rig.SubmitNext("RequestHelp"), Is.True);
            bool confirmed = false;
            manager.PerformClinical((runtime, time) => confirmed = runtime.ConfirmHelp(time));
            Assert.That(confirmed, Is.True);
            int requests = manager.MedicalSession.ClinicalEvents.Count(e => e.eventType == "HelpRequested");
            int achieved = manager.MedicalSession.ClinicalEvents.Count(e => e.eventType == "LearningObjectiveAchieved");
            Assert.That(rig.SubmitNext("RequestHelp"), Is.False,
                "Completed contains the earlier request; the adapter must report this occurrence's rejection.");
            Assert.That(manager.MedicalSession.HelpState, Is.EqualTo(HelpRequestState.Confirmed));
            Assert.That(manager.MedicalSession.ClinicalEvents.Count(e => e.eventType == "HelpRequested"), Is.EqualTo(requests));
            Assert.That(manager.MedicalSession.ClinicalEvents.Count(e => e.eventType == "LearningObjectiveAchieved"), Is.EqualTo(achieved));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RuntimeHandoverCancellationInvalidatesThePhysicalFacadeTicket()
        {
            var manager = flow.Review.Manager;
            var position = manager.PositionTransitions;
            Assert.That(position.Request(PhysicalPosition.Supine), Is.True);
            string ticket = position.TransitionId;
            position.BeginPreparing(ticket); position.BeginTransition(ticket);
            manager.PerformClinical((runtime, time) => runtime.RequestHelp(time));
            manager.PerformClinical((runtime, time) => runtime.ConfirmHelp(time));
            manager.PerformClinical((runtime, time) => runtime.CompleteHandover(time));
            Assert.That(manager.MedicalSession.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_07_HANDOVER"));
            Assert.That(position.State, Is.EqualTo(PositionTransitionPhase.Cancelled));
            Assert.That(position.TransitionId, Is.Null);
            Assert.That(position.Confirm(ticket, true), Is.False);
            Assert.That(position.Request(PhysicalPosition.Supine), Is.False);
            Assert.That(manager.MedicalSession.ClinicalState.ClinicalPosition, Is.EqualTo(ClinicalPosition.SeatedSupported));
            yield return null;
        }

        [Serializable]
        sealed class V2ExportHeader
        {
            public string attemptId, scenarioVersion, clinicalSpecVersion, trainingProfile, buildVersion;
            public InterviewObservation[] interviews;
            public MeasurementObservation[] measurements;
            public LearningObjectiveProgress[] objectives;
            public ClinicalEvent[] clinicalEvents;
        }
    }
}
