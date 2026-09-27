using System.Linq;
using EmergencyVR.Medical;
using NUnit.Framework;

namespace EmergencyVR.Tests
{
    public sealed class Case01CompletionTests
    {
        static MedicalScenarioRuntime Runtime()
        {
            var library = MedicalLibraryLoader.Load();
            return new MedicalScenarioRuntime(library.scenarios.Single(s => s.id == "review-hypotension-v2"), library, 42, "completion-tests");
        }

        static LearningObjectiveProgress Objective(MedicalScenarioRuntime runtime, string id) =>
            runtime.ObjectiveProgress.Single(o => o.objectiveId == id);

        [Test]
        public void RequestedGuidanceAffectsOnlyItsObjectiveAndCannotAwardOrImproveThePatientByItself()
        {
            var runtime = Runtime();
            Assert.That(runtime.RecordGuidance("OBJ_03", 0), Is.True);
            Assert.That(runtime.ObjectiveProgress.All(o => o.result == ObjectiveResult.NotEvaluated), Is.True);
            Assert.That(runtime.Observations.All, Is.Empty);
            Assert.That(runtime.Patient.systolic, Is.EqualTo(85));
            runtime.Ask(DialogueIntent.MAIN_SYMPTOM, 1);
            runtime.Ask(DialogueIntent.ONSET, 2);
            Assert.That(Objective(runtime, "OBJ_03").result, Is.EqualTo(ObjectiveResult.AchievedWithGuidance));
            runtime.ObservePhysical("PatientResponsive", 3);
            runtime.ObservePhysical("BreathingNormal", 4);
            Assert.That(Objective(runtime, "OBJ_01").result, Is.EqualTo(ObjectiveResult.AchievedIndependently));
            Assert.That(runtime.Patient.heartRate, Is.EqualTo(88));
            Assert.That(runtime.Patient.systolic, Is.EqualTo(85));
            Assert.That(runtime.ClinicalEvents.Count(e => e.eventType == "GuidanceUsed"), Is.EqualTo(1));
        }

        [Test]
        public void GuidanceRequestedAfterIndependentAchievementDoesNotRewritePastEvidence()
        {
            var runtime = Runtime();
            runtime.ObservePhysical("PatientResponsive", 1);
            runtime.ObservePhysical("BreathingNormal", 2);
            var evidence = Objective(runtime, "OBJ_01").evidenceEventIds;
            Assert.That(runtime.RecordGuidance("OBJ_01", 3), Is.True);
            Assert.That(Objective(runtime, "OBJ_01").result, Is.EqualTo(ObjectiveResult.AchievedIndependently));
            Assert.That(Objective(runtime, "OBJ_01").evidenceEventIds, Is.EqualTo(evidence));
        }

        [Test]
        public void ResetRemovesGuidanceFromThePreviousAttempt()
        {
            var runtime = Runtime();
            runtime.RecordGuidance("OBJ_03", 1);
            runtime.Reset();
            runtime.Ask(DialogueIntent.MAIN_SYMPTOM, 1);
            runtime.Ask(DialogueIntent.ONSET, 2);
            Assert.That(Objective(runtime, "OBJ_03").result, Is.EqualTo(ObjectiveResult.AchievedIndependently));
            Assert.That(runtime.ClinicalEvents.Any(e => e.eventType == "GuidanceUsed"), Is.False);
        }

        [Test]
        public void ReassessmentRequiresAnEarlierObservationButCanBuildItsOwnBaseline()
        {
            var runtime = Runtime();
            Assert.That(runtime.Submit("ReassessPatient", 1), Is.EqualTo("Accepted"));
            Assert.That(Objective(runtime, "OBJ_04").result, Is.EqualTo(ObjectiveResult.NotEvaluated));
            Assert.That(runtime.Submit("ReassessPatient", 1), Is.EqualTo("Accepted"));
            Assert.That(Objective(runtime, "OBJ_04").result, Is.EqualTo(ObjectiveResult.NotEvaluated),
                "Two identical instants do not demonstrate observation over time.");
            Assert.That(runtime.Submit("ReassessPatient", 2), Is.EqualTo("Accepted"));
            Assert.That(Objective(runtime, "OBJ_04").result, Is.EqualTo(ObjectiveResult.AchievedIndependently));
            Assert.That(runtime.ClinicalEvents.Count(e => e.eventType == "LearningObjectiveAchieved" && e.result == "OBJ_04"), Is.EqualTo(1));
        }

        [Test]
        public void CommunicationLogsExactlyWhatWasCommunicatedWithoutAcquiringHiddenVitals()
        {
            var runtime = Runtime();
            const string message = "Estamos en la zona de cardio. El paciente está sentado y responde.";
            Assert.That(runtime.RecordCommunication("112 simulado", message, 3), Is.True);
            var entry = runtime.ClinicalEvents.Single(e => e.eventType == "CommunicationRecorded");
            Assert.That(entry.simulationTime, Is.EqualTo(3));
            Assert.That(entry.metadata.Single(m => m.key == "audience").value, Is.EqualTo("112 simulado"));
            Assert.That(entry.metadata.Single(m => m.key == "message").value, Is.EqualTo(message));
            Assert.That(runtime.Observations.All, Is.Empty);
            Assert.That(runtime.HelpState, Is.EqualTo(HelpRequestState.None), "Speaking is not operator confirmation.");
            Assert.That(runtime.Patient.systolic, Is.EqualTo(85));
        }

        [Test]
        public void InvalidCommunicationOrUnknownGuidanceDoesNotCreateEvidence()
        {
            var runtime = Runtime();
            int events = runtime.ClinicalEvents.Count;
            Assert.That(runtime.RecordGuidance("UNKNOWN_OBJECTIVE", 0), Is.False);
            Assert.That(runtime.RecordCommunication("", "message", 0), Is.False);
            Assert.That(runtime.RecordCommunication("112 simulado", "  ", 0), Is.False);
            Assert.That(runtime.RecordCommunication("112 simulado", new string('x', 2001), 0), Is.False);
            Assert.That(runtime.ClinicalEvents.Count, Is.EqualTo(events));
        }

        [Test]
        public void PausedOrFinishedAttemptsRejectNewEducationalEvidence()
        {
            var runtime = Runtime();
            runtime.Tick(1); runtime.SetPaused(true);
            int events = runtime.ClinicalEvents.Count;
            Assert.That(runtime.RecordGuidance("OBJ_01", 2), Is.False);
            Assert.That(runtime.RecordCommunication("112 simulado", "Responde", 2), Is.False);
            Assert.That(runtime.ClinicalEvents.Count, Is.EqualTo(events));
            Assert.That(runtime.Elapsed, Is.EqualTo(1));
            runtime.SetPaused(false); runtime.Finish(2);
            events = runtime.ClinicalEvents.Count;
            Assert.That(runtime.RecordGuidance("OBJ_01", 3), Is.False);
            Assert.That(runtime.RecordCommunication("112 simulado", "Responde", 3), Is.False);
            Assert.That(runtime.ClinicalEvents.Count, Is.EqualTo(events));
        }
    }
}
