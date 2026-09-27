using System;
using System.Collections;
using System.Linq;
using EmergencyVR.Medical;
using EmergencyVR.Scenarios;
using EmergencyVR.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace EmergencyVR.Tests
{
    public sealed class Case01VariationTests
    {
        TrainingExperience flow;
        Case01VariationController variation;
        int caseIndex;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            Time.timeScale = 1; AudioListener.pause = false;
            yield return SceneManager.LoadSceneAsync("Assets/_Project/Scenes/Training/TrainingRoom.unity");
            yield return null; yield return null;
            flow = UnityEngine.Object.FindFirstObjectByType<TrainingExperience>();
            variation = flow.Review.GetComponent<Case01VariationController>();
            caseIndex = Array.FindIndex(flow.Review.Catalog.entries, x => x.medical?.id == "review-hypotension-v2");
            Assert.That(variation, Is.Not.Null); Assert.That(caseIndex, Is.GreaterThanOrEqualTo(0));
        }
        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (flow != null && flow.Review != null)
            {
                flow.Review.Manager.SetPaused(false);
                if (flow.Review.Manager.IsRunning) flow.Review.Manager.FinishCase();
            }
            Time.timeScale = 1; AudioListener.pause = false;
            yield return null;
        }
        void Begin()
        {
            flow.Prepare(caseIndex); flow.BeginTraining();
            Assert.That(flow.Review.Manager.IsRunning, Is.True);
        }
        [UnityTest]
        public IEnumerator SelectionIsLockedDuringAttemptAndRecordedWithoutGuidanceOrCredit()
        {
            Assert.That(variation.Configure(Case01Variation.Persistent), Is.True);
            flow.Review.Procedures.TrainingMode = true; Begin(); yield return null;
            Assert.That(variation.ActiveVariant, Is.EqualTo(Case01Variation.Persistent));
            Assert.That(variation.Configure(Case01Variation.Normal), Is.False);
            Assert.That(variation.ConfigureEvaluation(Case01Variation.Recurrence), Is.False);
            var runtime = flow.Review.Manager.MedicalSession;
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_00_INITIAL_PRESYNCOPE"));
            Assert.That(runtime.Observations.All, Is.Empty);
            Assert.That(runtime.ObjectiveProgress.All(x => x.result == ObjectiveResult.NotEvaluated), Is.True);
            Assert.That(runtime.ClinicalEvents.Count(x => x.eventType == "CommunicationRecorded" && x.source == "ScenarioConfiguration"), Is.EqualTo(1));
            Assert.That(runtime.ClinicalEvents.Any(x => x.eventType == "GuidanceUsed"), Is.False);
        }
        [UnityTest]
        public IEnumerator PersistenceWaitsForPhysicalConfirmationAndDoesNotRunDuringPause()
        {
            variation.Configure(Case01Variation.Persistent); flow.Review.Procedures.TrainingMode = true; Begin(); yield return null;
            var manager = flow.Review.Manager;
            manager.AdvanceTrainingTime(10); variation.UpdatePresentation();
            Assert.That(variation.HasTriggered, Is.False);
            var position = manager.PositionTransitions;
            Assert.That(position.Request(PhysicalPosition.Supine), Is.True);
            string ticket = position.TransitionId;
            Assert.That(position.BeginPreparing(ticket), Is.True); Assert.That(position.BeginTransition(ticket), Is.True);
            manager.SetPaused(true); variation.UpdatePresentation();
            Assert.That(position.Confirm(ticket, true), Is.False);
            Assert.That(manager.MedicalSession.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_00_INITIAL_PRESYNCOPE"));
            manager.SetPaused(false);
            Assert.That(position.Confirm(ticket, true), Is.True); variation.UpdatePresentation();
            Assert.That(variation.HasTriggered, Is.True);
            Assert.That(manager.MedicalSession.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_03_PERSISTENT_SYMPTOMS"));
            Assert.That(manager.MedicalSession.Patient.systolic, Is.EqualTo(85).Within(.01));
            Assert.That(manager.MedicalSession.ClinicalEvents.Count(x => x.eventType == "ClinicalSignalReceived" && x.result == "NoMeaningfulImprovement"), Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator EvaluationHasSeparateConfigurationAndDoesNotRevealVariantInBriefing()
        {
            variation.Configure(Case01Variation.Recurrence); variation.ConfigureEvaluation(Case01Variation.Persistent);
            flow.Review.Procedures.TrainingMode = false; flow.Prepare(caseIndex);
            yield return null; yield return null;
            Assert.That(flow.GetComponentsInChildren<Button>().Any(x => x.name.StartsWith("Practice variation ", StringComparison.Ordinal)), Is.False);
            Assert.That(flow.GetComponentsInChildren<Text>().Any(x => x.text.Contains("Síntomas persistentes") || x.text.Contains("Intento de incorporación")), Is.False);
            flow.BeginTraining(); yield return null;
            Assert.That(variation.ActiveVariant, Is.EqualTo(Case01Variation.Persistent));
            Assert.That(variation.SelectedVariant, Is.EqualTo(Case01Variation.Recurrence));
        }
        [UnityTest]
        public IEnumerator RestartUsesNewSelectionAndDoesNotCarryThePreviousTrigger()
        {
            variation.Configure(Case01Variation.Persistent); flow.Review.Procedures.TrainingMode = true; Begin(); yield return null;
            string previousAttempt = flow.Review.Manager.MedicalSession.AttemptId;
            flow.FinishTraining();
            Assert.That(variation.Configure(Case01Variation.Normal), Is.True);
            Begin(); yield return null;
            var runtime = flow.Review.Manager.MedicalSession;
            Assert.That(runtime.AttemptId, Is.Not.EqualTo(previousAttempt));
            Assert.That(variation.ActiveVariant, Is.EqualTo(Case01Variation.Normal));
            Assert.That(variation.HasTriggered, Is.False);
            Assert.That(runtime.ClinicalEvents.Count(x => x.eventType == "CommunicationRecorded" && x.source == "ScenarioConfiguration"), Is.EqualTo(1));
            Assert.That(runtime.ClinicalEvents.All(x => x.attemptId == runtime.AttemptId), Is.True);
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_00_INITIAL_PRESYNCOPE"));
        }
    }
}
