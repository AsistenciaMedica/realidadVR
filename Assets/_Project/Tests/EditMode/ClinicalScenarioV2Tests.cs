using System;
using System.IO;
using System.Linq;
using EmergencyVR.Medical;
using NUnit.Framework;
using UnityEngine;

namespace EmergencyVR.Tests
{
    // Authoring data is loaded through the same composition path as the player.
    // The legacy JSON remains an independent reference for all 44 original cases.
    public sealed class ClinicalScenarioV2Tests
    {
        const string CaseId = "review-hypotension-v2";
        const string Initial = "HYP_00_INITIAL_PRESYNCOPE";
        const string Supported = "HYP_01_SUPPORTED_OBSERVATION";

        static MedicalLibrary Library() => MedicalLibraryLoader.Load();
        static MedicalScenarioRuntime Runtime(out MedicalScenarioDefinition definition, int seed = 1234)
        {
            var library = Library();
            definition = library.scenarios.Single(s => s.id == CaseId);
            return new MedicalScenarioRuntime(definition, library, seed, "stage-b-tests");
        }

        static MedicalScenarioRuntime MeasurementRuntime()
        {
            var library = Library();
            var definition = library.scenarios.Single(s => s.id == CaseId).Copy();
            var profile = definition.clinicalV2.trainingProfiles.Single().Copy();
            profile.id = TrainingProfileId.I1_NonInvasiveEquipment;
            profile.allowedMeasurements.AddRange(new[] { "BloodPressure", "SpO2" });
            profile.allowedEquipment.AddRange(new[] { "BloodPressure", "SpO2" });
            definition.clinicalV2.trainingProfiles = new[] { profile };
            definition.clinicalV2.metadata.trainingProfile = "I1_NON_INVASIVE_EQUIPMENT";
            definition.clinicalV2.capabilities.usesAdvancedMeasurements = true;
            return new MedicalScenarioRuntime(definition, library, 1234, "stage-b-tests", TrainingProfileId.I1_NonInvasiveEquipment);
        }

        [Test]
        public void ComposedLibraryPreservesEveryLegacyDefinitionAndAddsExactlyOneVersion()
        {
            var original = JsonUtility.FromJson<MedicalLibrary>(File.ReadAllText("Assets/_Project/Resources/MedicalScenarios.json"));
            // This contract covers V2 registration before the explicit release identity overlay.
            // PatientRosterTests separately verifies that overlay and preservation of archived cases.
            var composed = JsonUtility.FromJson<MedicalLibrary>(JsonUtility.ToJson(original));
            EmergencyVR.Scenarios.ClinicalScenarioV2Catalog.LoadInto(composed);
            original.Validate(); composed.Validate();
            Assert.That(original.scenarios.Length, Is.EqualTo(44));
            Assert.That(composed.scenarios.Length, Is.EqualTo(45));
            foreach (var oldCase in original.scenarios)
            {
                var preserved = composed.scenarios.Single(s => s.id == oldCase.id);
                Assert.That(JsonUtility.ToJson(preserved), Is.EqualTo(JsonUtility.ToJson(oldCase)), oldCase.id);
                var runtime = new MedicalScenarioRuntime(preserved, composed, 1234);
                var action = preserved.recommendedSequence.First();
                Assert.That(runtime.Submit(action, 1), Is.EqualTo("Accepted"), oldCase.id);
                Assert.That(runtime.Submit(action, 2), Is.EqualTo("Duplicate"), oldCase.id);
            }
            Assert.That(composed.scenarios.Count(s => s.id == CaseId), Is.EqualTo(1));
            Assert.That(composed.scenarios.Count(s => s.id == "review-hypotension-v1"), Is.EqualTo(1));
        }

        [Test]
        public void LegacyJsonDoesNotMaterializeOrSerializeAnAbsentClinicalV2Definition()
        {
            var legacy = JsonUtility.FromJson<MedicalLibrary>(File.ReadAllText("Assets/_Project/Resources/MedicalScenarios.json"));
            Assert.That(legacy.scenarios.Length, Is.EqualTo(44));
            Assert.That(legacy.scenarios.All(s => s.clinicalV2 == null), Is.True);
            Assert.DoesNotThrow(() => legacy.Validate());
            Assert.That(JsonUtility.ToJson(legacy), Does.Not.Contain("\"clinicalV2\""));
            foreach (var scenario in legacy.scenarios)
                Assert.That(scenario.Copy().clinicalV2, Is.Null, scenario.id);
        }

        [Test]
        public void ExplicitlyComposedIncompleteV2DefinitionIsRejectedInsteadOfTreatedAsAbsent()
        {
            var legacy = JsonUtility.FromJson<MedicalLibrary>(File.ReadAllText("Assets/_Project/Resources/MedicalScenarios.json"));
            var definition = legacy.scenarios.Single(s => s.id == "review-hypotension-v1").Copy();
            definition.clinicalV2 = new ClinicalScenarioV2Definition();
            Assert.Throws<ArgumentException>(() => definition.Validate(legacy));
            definition.clinicalV2.capabilities.usesObservedPatientData = true;
            Assert.Throws<ArgumentException>(() => definition.Validate(legacy));
        }

        [TestCase(1)]
        [TestCase(1234)]
        [TestCase(98765)]
        public void Case01StartsWithAuthoredDanielAndNoAcquiredDataAcrossSeeds(int seed)
        {
            var runtime = Runtime(out _, seed);
            var state = runtime.ClinicalState;
            Assert.That(state.ScenarioId, Is.EqualTo(CaseId));
            Assert.That(state.ClinicalStateId, Is.EqualTo(Initial));
            Assert.That(state.Consciousness, Is.EqualTo(ConsciousnessState.Alert));
            Assert.That(state.Orientation, Is.EqualTo(OrientationState.Oriented));
            Assert.That(state.ClinicalPosition, Is.EqualTo(ClinicalPosition.SeatedSupported));
            Assert.That(state.HeartRate, Is.EqualTo(88).Within(.001));
            Assert.That(state.RespiratoryRate, Is.EqualTo(18).Within(.001));
            Assert.That(state.SpO2, Is.EqualTo(97).Within(.001));
            Assert.That(state.CanSpeak, Is.True);
            Assert.That(state.CanCooperate, Is.True);
            Assert.That(runtime.Patient.age, Is.EqualTo(40));
            Assert.That(runtime.Patient.sex, Is.EqualTo("male"));
            Assert.That(runtime.Patient.systolic, Is.EqualTo(85));
            Assert.That(runtime.Patient.diastolic, Is.EqualTo(55));
            Assert.That(runtime.Observations.All, Is.Empty, "Knowing internal BP does not mean the learner measured it.");
        }

        [Test]
        public void ProjectionAndPatientCopiesCannotChangeRuntimePhysiology()
        {
            var runtime = Runtime(out _);
            var oldProjection = runtime.ClinicalState;
            var copy = runtime.Patient;
            copy.heartRate = 200; copy.systolic = 0; copy.flags = new[] { "external-mutation" };
            runtime.Tick(1);
            Assert.That(runtime.Patient.heartRate, Is.EqualTo(88));
            Assert.That(runtime.Patient.systolic, Is.EqualTo(85));
            Assert.That(runtime.Patient.flags, Does.Not.Contain("external-mutation"));
            Assert.That(oldProjection.SimulationTime, Is.Zero, "A previously returned projection is a detached snapshot.");
            Assert.That(runtime.ClinicalState.SimulationTime, Is.EqualTo(1));
            Assert.That(runtime.ClinicalState.HeartRate, Is.EqualTo(runtime.Patient.heartRate).Within(.001));
        }

        [Test]
        public void RequestedAndCancelledPositionNeverBecomeConfirmedClinicalPosition()
        {
            var runtime = Runtime(out _);
            Assert.That(runtime.RequestPosition(PhysicalPosition.Supine, 1), Is.True);
            Assert.That(runtime.ClinicalState.ClinicalPosition, Is.EqualTo(ClinicalPosition.SeatedSupported));
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo(Initial));
            Assert.That(runtime.Patient.systolic, Is.EqualTo(85));
            Assert.That(runtime.CancelPosition(2, "TrackingLost"), Is.True);
            runtime.Tick(10);
            Assert.That(runtime.ClinicalState.ClinicalPosition, Is.EqualTo(ClinicalPosition.SeatedSupported));
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo(Initial));
            Assert.That(runtime.Patient.systolic, Is.EqualTo(85));
        }

        [Test]
        public void ValidPositionConfirmationChangesStateButDoesNotInstantlyRecoverPatient()
        {
            var runtime = Runtime(out _);
            Assert.That(runtime.RequestPosition(PhysicalPosition.Supine, 1), Is.True);
            Assert.That(runtime.ConfirmPhysicalPosition(PhysicalPosition.Supine, 2), Is.True);
            Assert.That(runtime.ClinicalState.ClinicalPosition, Is.EqualTo(ClinicalPosition.Supine));
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo(Supported));
            Assert.That(runtime.Patient.systolic, Is.EqualTo(85).Within(.001));
            runtime.Tick(3);
            Assert.That(runtime.Patient.systolic, Is.GreaterThan(85).And.LessThan(95));
            Assert.That(runtime.Patient.spo2, Is.EqualTo(97));
        }

        [Test]
        public void InvalidPositionConfirmationLeavesClinicalPositionUnchanged()
        {
            var runtime = Runtime(out _);
            runtime.RequestPosition(PhysicalPosition.Supine, 1);
            Assert.That(runtime.ConfirmPhysicalPosition(PhysicalPosition.Supine, 2, "Presentation", false), Is.False);
            Assert.That(runtime.ClinicalState.ClinicalPosition, Is.EqualTo(ClinicalPosition.SeatedSupported));
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo(Initial));
        }

        [Test]
        public void HelpCanBeRequestedAtTheBeginningWithoutInterviewsOrMeasurements()
        {
            var runtime = Runtime(out _);
            Assert.That(runtime.RequestHelp(0), Is.True);
            Assert.That(runtime.HelpState, Is.EqualTo(HelpRequestState.Requested));
            Assert.That(runtime.Observations.Measurements, Is.Empty);
            Assert.That(runtime.Observations.Interviews, Is.Empty);
            Assert.That(runtime.Patient.systolic, Is.EqualTo(85));
        }

        [Test]
        public void RuntimePauseFreezesClinicalTimePhaseAndRejectsInput()
        {
            var runtime = Runtime(out _);
            runtime.RequestPosition(PhysicalPosition.Supine, 1);
            runtime.ConfirmPhysicalPosition(PhysicalPosition.Supine, 2);
            runtime.Tick(3);
            var before = runtime.ClinicalState;
            var eventCount = runtime.ClinicalEvents.Count;
            runtime.SetPaused(true);
            runtime.Tick(30);
            Assert.That(runtime.RequestHelp(30), Is.False);
            Assert.That(runtime.ClinicalState.SimulationTime, Is.EqualTo(before.SimulationTime));
            Assert.That(runtime.ClinicalState.RespiratoryPhase, Is.EqualTo(before.RespiratoryPhase));
            Assert.That(runtime.ClinicalState.HeartRate, Is.EqualTo(before.HeartRate));
            Assert.That(runtime.ClinicalEvents.Count, Is.EqualTo(eventCount));
            runtime.SetPaused(false);
            runtime.Tick(4);
            Assert.That(runtime.ClinicalState.SimulationTime, Is.EqualTo(4));
        }

        [Test]
        public void V2MetadataAndCapabilitiesSurviveJsonRoundTrip()
        {
            Runtime(out var definition);
            var copy = JsonUtility.FromJson<ClinicalScenarioV2Definition>(JsonUtility.ToJson(definition.clinicalV2));
            copy.Validate(CaseId);
            Assert.That(JsonUtility.ToJson(copy), Is.EqualTo(JsonUtility.ToJson(definition.clinicalV2)),
                "The independently serialized clinical document preserves all configured data.");
            var composedCopy = definition.Copy();
            Assert.That(composedCopy.clinicalV2, Is.Not.SameAs(definition.clinicalV2));
            Assert.That(JsonUtility.ToJson(composedCopy.clinicalV2), Is.EqualTo(JsonUtility.ToJson(definition.clinicalV2)),
                "The nonserialized adapter reference is preserved by explicit composition and copy.");
            Assert.That(copy.metadata.scenarioId, Is.EqualTo(CaseId));
            Assert.That(copy.metadata.scenarioVersion, Is.EqualTo("2.0.0"));
            Assert.That(copy.metadata.clinicalSpecVersion, Is.EqualTo("0.1"));
            Assert.That(copy.metadata.trainingProfile, Is.EqualTo("I0_FIRST_RESPONDER"));
            Assert.That(copy.metadata.clinicalReviewRequired, Is.True);
            Assert.That(copy.capabilities.usesClinicalStateMachine, Is.True);
            Assert.That(copy.capabilities.usesObservedPatientData, Is.True);
            Assert.That(copy.capabilities.usesPhysicalPositionConfirmation, Is.True);
            Assert.That(copy.capabilities.usesIntentDialogue, Is.True);
            Assert.That(copy.capabilities.usesAdvancedMeasurements, Is.False);
            Assert.That(copy.capabilities.usesObjectiveBasedEvaluation, Is.True);
            Assert.That(copy.clinicalStates.Select(s => s.id), Is.EquivalentTo(new[]
            {
                Initial, Supported, "HYP_02_IMPROVING", "HYP_03_PERSISTENT_SYMPTOMS",
                "HYP_04_RECURRENT_PRESYNCOPE", "HYP_07_HANDOVER"
            }));
            Assert.That(copy.clinicalStates.SelectMany(s => s.vitalTrajectories).All(t => t.clinicalReviewRequired), Is.True);
        }

        [TestCase("HYP_05_BRIEF_TLOC")]
        [TestCase("HYP_06_EARLY_RECOVERY_AFTER_TLOC")]
        public void ReservedLossOfConsciousnessStatesAreRejectedFromSerializedDefinitions(string reserved)
        {
            Runtime(out var definition);
            var data = definition.clinicalV2.Copy();
            data.clinicalStates = data.clinicalStates.Concat(new[] { new ClinicalStateDefinition { id = reserved } }).ToArray();
            var loaded = JsonUtility.FromJson<ClinicalScenarioV2Definition>(JsonUtility.ToJson(data));
            Assert.Throws<ArgumentException>(() => loaded.Validate(CaseId));
        }

        [TestCase("HYP_05_BRIEF_TLOC")]
        [TestCase("HYP_06_EARLY_RECOVERY_AFTER_TLOC")]
        public void TransitionsCannotTargetReservedUnimplementedStates(string reserved)
        {
            Runtime(out var definition);
            var data = definition.clinicalV2.Copy();
            data.transitions[0].to = reserved;
            var loaded = JsonUtility.FromJson<ClinicalScenarioV2Definition>(JsonUtility.ToJson(data));
            Assert.Throws<ArgumentException>(() => loaded.Validate(CaseId));
        }

        [Test]
        public void I0DoesNotAcquireMeasurementsAndI2DoesNotImplicitlyGrantPermissions()
        {
            var runtime = Runtime(out var definition);
            Assert.That(runtime.RecordMeasurement("BloodPressure", true, "Valid", 1, "AutomaticBloodPressureMonitor"), Is.False);
            Assert.That(runtime.RecordMeasurement("SpO2", true, "Valid", 2, "PulseOximeter"), Is.False);
            Assert.That(runtime.Observations.Measurements, Is.Empty);
            var library = Library();
            var restricted = definition.Copy();
            restricted.clinicalV2.trainingProfiles = new[]
            {
                new TrainingProfileDefinition { id = TrainingProfileId.I2_HealthcareProfessional }
            };
            restricted.clinicalV2.metadata.trainingProfile = "I2_HEALTHCARE_PROFESSIONAL";
            var professional = new MedicalScenarioRuntime(restricted, library, 1234, "tests", TrainingProfileId.I2_HealthcareProfessional);
            Assert.That(professional.RequestHelp(1), Is.False, "I2 is a configured scope, not a universal permission bypass.");
            Assert.That(professional.RecordMeasurement("BloodPressure", true, "Valid", 2, "AutomaticBloodPressureMonitor"), Is.False);
        }

        [Test]
        public void AskingOneIntentRevealsOnlyThatResponseWithoutChangingPhysiology()
        {
            var runtime = Runtime(out _);
            var before = runtime.Patient;
            var response = runtime.Ask(DialogueIntent.MAIN_SYMPTOM, 1);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.intent, Is.EqualTo(DialogueIntent.MAIN_SYMPTOM));
            Assert.That(runtime.Observations.Interviews.Count, Is.EqualTo(1));
            Assert.That(runtime.Observations.Measurements, Is.Empty);
            Assert.That(runtime.Patient.heartRate, Is.EqualTo(before.heartRate));
            Assert.That(runtime.Patient.systolic, Is.EqualTo(before.systolic));
            Assert.That(runtime.Patient.diastolic, Is.EqualTo(before.diastolic));
            Assert.That(runtime.Patient.respiratoryRate, Is.EqualTo(before.respiratoryRate));
            Assert.That(runtime.Patient.spo2, Is.EqualTo(before.spo2));
            Assert.That(runtime.Patient.consciousness, Is.EqualTo(before.consciousness));
            response.text = "external mutation";
            var repeated = runtime.Ask(DialogueIntent.MAIN_SYMPTOM, 2);
            Assert.That(repeated.text, Is.Not.EqualTo("external mutation"));
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo(Initial));
        }

        [Test]
        public void TrajectoryAndRespiratoryPhaseAreIndependentOfTickSubdivision()
        {
            var coarse = Runtime(out _);
            var fine = Runtime(out _);
            foreach (var runtime in new[] { coarse, fine })
            {
                runtime.RequestPosition(PhysicalPosition.Supine, 1);
                runtime.ConfirmPhysicalPosition(PhysicalPosition.Supine, 2);
            }
            coarse.Tick(202.37);
            foreach (double time in new[] { 2.01, 2.7, 3.2, 8.8, 16.13, 32.09, 61.8, 90.111, 132.7, 181.06, 202.37 }) fine.Tick(time);
            Assert.That(fine.ClinicalState.ClinicalStateId, Is.EqualTo(coarse.ClinicalState.ClinicalStateId));
            Assert.That(fine.Patient.heartRate, Is.EqualTo(coarse.Patient.heartRate).Within(.0001));
            Assert.That(fine.Patient.systolic, Is.EqualTo(coarse.Patient.systolic).Within(.0001));
            Assert.That(fine.Patient.respiratoryRate, Is.EqualTo(coarse.Patient.respiratoryRate).Within(.0001));
            Assert.That(fine.ClinicalState.RespiratoryPhase, Is.EqualTo(coarse.ClinicalState.RespiratoryPhase).Within(.0001));
        }

        [Test]
        public void RespiratoryPhaseHasTheAuthoredFrequencyAndDoesNotResetAtPositionConfirmation()
        {
            var runtime = Runtime(out _);
            runtime.Tick(1);
            Assert.That(runtime.ClinicalState.RespiratoryPhase, Is.EqualTo(.3).Within(.0001), "18 breaths/min produces 0.3 cycles in one second.");
            runtime.RequestPosition(PhysicalPosition.Supine, 1);
            var before = runtime.ClinicalState.RespiratoryPhase;
            runtime.ConfirmPhysicalPosition(PhysicalPosition.Supine, 1);
            Assert.That(runtime.ClinicalState.RespiratoryPhase, Is.EqualTo(before).Within(.0001));
            runtime.Tick(1.001);
            Assert.That(runtime.ClinicalState.RespiratoryPhase, Is.GreaterThan(before));
            Assert.That(runtime.ClinicalState.RespiratoryPhase, Is.LessThan(before + .001));
        }

        [Test]
        public void RepeatableReassessmentRecordsEachAttemptWithoutDuplicateOrExtraObjective()
        {
            var runtime = Runtime(out _);
            // Reassessment now needs an earlier assessment to compare against; repeating
            // a button at the initial instant is not evidence of following evolution.
            runtime.ObservePhysical("PatientResponsive", .1);
            runtime.ObservePhysical("BreathingNormal", .2);
            Assert.That(runtime.Submit("ReassessPatient", 1), Is.EqualTo("Accepted"));
            var first = runtime.ObjectiveProgress.Single(o => o.objectiveId == "OBJ_04");
            Assert.That(first.result, Is.EqualTo(ObjectiveResult.AchievedIndependently));
            Assert.That(runtime.Submit("ReassessPatient", 2), Is.EqualTo("Accepted"));
            Assert.That(runtime.Submit("ReassessPatient", 3), Is.EqualTo("Accepted"));
            Assert.That(runtime.ObjectiveProgress.Count(o => o.objectiveId == "OBJ_04"), Is.EqualTo(1));
            Assert.That(runtime.ObjectiveProgress.Single(o => o.objectiveId == "OBJ_04").result, Is.EqualTo(first.result));
            Assert.That(runtime.ClinicalEvents.Any(e => e.result == "Duplicate"), Is.False);
            Assert.That(runtime.ClinicalEvents.Any(e => Math.Abs(e.simulationTime - 2) < .001), Is.True);
            Assert.That(runtime.ClinicalEvents.Any(e => Math.Abs(e.simulationTime - 3) < .001), Is.True);
            Assert.That(runtime.Patient.systolic, Is.EqualTo(85), "Learning progress alone does not improve physiology.");
        }

        [TestCase("ProtectedFromFall")]
        [TestCase("MaintainedSafeSupportedPosition")]
        [TestCase("AssistedToSafePosition")]
        public void ConfiguredEquivalentSafetyOutcomesAchieveTheSameObjective(string outcome)
        {
            var runtime = Runtime(out _);
            Assert.That(runtime.ReportOutcome(outcome, 1), Is.True);
            Assert.That(runtime.ObjectiveProgress.Single(o => o.objectiveId == "OBJ_02").result,
                Is.EqualTo(ObjectiveResult.AchievedIndependently));
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo(Initial));
            Assert.That(runtime.Patient.systolic, Is.EqualTo(85));
        }

        [Test]
        public void GuidedEvidenceRemainsDistinguishableAndDoesNotAlterPhysiology()
        {
            var runtime = Runtime(out _);
            Assert.That(runtime.ReportOutcome("ProtectedFromFall", 1, "Interaction", true), Is.True);
            Assert.That(runtime.ObjectiveProgress.Single(o => o.objectiveId == "OBJ_02").result,
                Is.EqualTo(ObjectiveResult.AchievedWithGuidance));
            Assert.That(runtime.Patient.heartRate, Is.EqualTo(88));
            Assert.That(runtime.Patient.systolic, Is.EqualTo(85));
        }

        [Test]
        public void RuntimeResetClearsAttemptKnowledgeAndRejectsCallbacksFromPreviousAttempt()
        {
            var runtime = Runtime(out _);
            string oldAttempt = runtime.AttemptId;
            runtime.Ask(DialogueIntent.MAIN_SYMPTOM, 1);
            runtime.RequestHelp(2);
            runtime.ReportOutcome("ProtectedFromFall", 3);
            runtime.RequestPosition(PhysicalPosition.Supine, 4);
            var previousEvents = runtime.ClinicalEvents;
            runtime.Reset();
            Assert.That(runtime.AttemptId, Is.Not.EqualTo(oldAttempt));
            Assert.That(runtime.Elapsed, Is.Zero);
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo(Initial));
            Assert.That(runtime.Observations.All, Is.Empty);
            Assert.That(runtime.HelpState, Is.EqualTo(HelpRequestState.None));
            Assert.That(runtime.RequestedPosition, Is.EqualTo(PhysicalPosition.Unknown));
            Assert.That(runtime.ObjectiveProgress.All(o => o.result == ObjectiveResult.NotEvaluated), Is.True);
            Assert.That(runtime.ClinicalEvents.All(e => e.attemptId == runtime.AttemptId), Is.True);
            Assert.That(previousEvents.All(e => e.attemptId == oldAttempt), Is.True, "Old exported records must not be relabelled during reset.");
            runtime.RequestPosition(PhysicalPosition.Supine, 1);
            int count = runtime.ClinicalEvents.Count;
            Assert.That(runtime.ConfirmPhysicalPosition(PhysicalPosition.Supine, 5, "Presentation", true, oldAttempt), Is.False);
            Assert.That(runtime.Ask(DialogueIntent.MAIN_SYMPTOM, 5, "Player", false, oldAttempt), Is.Null);
            Assert.That(runtime.RecordMeasurement("BloodPressure", true, "Valid", 5, "AutomaticBloodPressureMonitor", oldAttempt), Is.False);
            Assert.That(runtime.ClinicalEvents.Count, Is.EqualTo(count));
            Assert.That(runtime.Observations.All, Is.Empty);
            Assert.That(runtime.Patient.systolic, Is.EqualTo(85));
        }

        [Test]
        public void InterviewEventsAndObjectiveEvidenceAreDeeplyDetachedFromConsumers()
        {
            var runtime = Runtime(out _);
            runtime.Ask(DialogueIntent.MAIN_SYMPTOM, 12);
            var interview = runtime.Observations.Interviews.Single();
            string responseId = interview.responseId;
            Assert.That(interview.simulationTime, Is.EqualTo(12));
            Assert.That(interview.attemptId, Is.EqualTo(runtime.AttemptId));
            Assert.That(interview.clinicalStateId, Is.EqualTo(Initial));
            interview.responseId = "tampered"; interview.text = "unasked history";
            Assert.That(runtime.Observations.Interviews.Single().responseId, Is.EqualTo(responseId));
            var exportedEvent = runtime.ClinicalEvents.First(e => e.metadata.Length > 0);
            string eventId = exportedEvent.eventId;
            exportedEvent.result = "tampered";
            exportedEvent.metadata[0].value = "tampered";
            Assert.That(runtime.ClinicalEvents.Single(e => e.eventId == eventId).result, Is.Not.EqualTo("tampered"));
            Assert.That(runtime.ClinicalEvents.Single(e => e.eventId == eventId).metadata.Any(m => m.value == "tampered"), Is.False);
            runtime.ReportOutcome("ProtectedFromFall", 13);
            var objective = runtime.ObjectiveProgress.Single(o => o.objectiveId == "OBJ_02");
            objective.result = ObjectiveResult.NotDemonstrated;
            if (objective.evidenceEventIds.Length > 0) objective.evidenceEventIds[0] = "tampered";
            var original = runtime.ObjectiveProgress.Single(o => o.objectiveId == "OBJ_02");
            Assert.That(original.result, Is.EqualTo(ObjectiveResult.AchievedIndependently));
            Assert.That(original.evidenceEventIds, Does.Not.Contain("tampered"));
        }

        [Test]
        public void ConfiguredRepeatedMeasurementsKeepTimestampedHistoryAndNeverFollowLiveValues()
        {
            var runtime = MeasurementRuntime();
            Assert.That(runtime.RecordMeasurement("BloodPressure", true, "Valid", 1.75, "AutomaticBloodPressureMonitor"), Is.True);
            var first = runtime.Observations.Measurements.Single();
            Assert.That(first.systolic, Is.EqualTo(85));
            Assert.That(first.diastolic, Is.EqualTo(55));
            Assert.That(first.simulationTime, Is.EqualTo(1.75));
            Assert.That(first.unit, Is.EqualTo("mmHg"));
            Assert.That(first.source, Is.EqualTo("AutomaticBloodPressureMonitor"));
            runtime.RequestPosition(PhysicalPosition.Supine, 1.8);
            runtime.ConfirmPhysicalPosition(PhysicalPosition.Supine, 2);
            runtime.Tick(20);
            Assert.That(runtime.Patient.systolic, Is.GreaterThan(first.systolic));
            Assert.That(runtime.Observations.Measurements.Single().systolic, Is.EqualTo(85));
            Assert.That(runtime.RecordMeasurement("BloodPressure", true, "Valid", 20, "AutomaticBloodPressureMonitor"), Is.True);
            Assert.That(runtime.Observations.Measurements.Count, Is.EqualTo(2));
            var second = runtime.Observations.LatestMeasurement("BloodPressure");
            Assert.That(second.observationId, Is.Not.EqualTo(first.observationId));
            Assert.That(second.simulationTime, Is.EqualTo(20));
            Assert.That(second.systolic, Is.EqualTo(runtime.Patient.systolic));
            Assert.That(second.clinicalStateId, Is.EqualTo(Supported));
            Assert.That(runtime.ClinicalEvents.Any(e => e.result == "Duplicate"), Is.False);
            var objective = runtime.ObjectiveProgress.Single(o => o.objectiveId == "OBJ_02");
            Assert.That(objective.result, Is.EqualTo(ObjectiveResult.AchievedIndependently));
            Assert.That(runtime.ClinicalEvents.Count(e => e.eventType == "LearningObjectiveAchieved" && e.result == "OBJ_02"), Is.EqualTo(1));
        }

        [Test]
        public void InvalidAcquisitionStoresQualityWithoutCopyingAnyInternalVitalValue()
        {
            var runtime = MeasurementRuntime();
            Assert.That(runtime.RecordMeasurement("BloodPressure", false, "InvalidPlacement", 4, "AutomaticBloodPressureMonitor"), Is.True);
            var invalid = runtime.Observations.Measurements.Single();
            Assert.That(invalid.valid, Is.False);
            Assert.That(invalid.hasValue, Is.False);
            Assert.That(invalid.quality, Is.EqualTo("InvalidPlacement"));
            Assert.That(invalid.systolic, Is.Zero);
            Assert.That(invalid.diastolic, Is.Zero);
            Assert.That(invalid.value, Is.Zero);
            Assert.That(runtime.Patient.systolic, Is.EqualTo(85));
            Assert.That(runtime.ClinicalEvents.Any(e => e.eventType == "MeasurementFailed"), Is.True);
            Assert.That(runtime.RecordMeasurement("SpO2", true, "InvalidPlacement", 5, "PulseOximeter"), Is.False);
            Assert.That(runtime.Observations.Measurements.Count, Is.EqualTo(1), "Invalid quality cannot masquerade as a valid reading.");
        }

        [Test]
        public void MeasurementSnapshotsAndLateAcquisitionCallbacksCannotMutateAnotherAttempt()
        {
            var runtime = MeasurementRuntime();
            runtime.RecordMeasurement("BloodPressure", true, "Valid", 1, "AutomaticBloodPressureMonitor");
            var oldStore = runtime.Observations;
            var value = oldStore.Measurements.Single(); value.systolic = 200;
            var latest = oldStore.LatestMeasurement("BloodPressure"); latest.quality = "tampered";
            Assert.That(runtime.Observations.Measurements.Single().systolic, Is.EqualTo(85));
            Assert.That(runtime.Observations.Measurements.Single().quality, Is.EqualTo("Valid"));
            string oldAttempt = runtime.AttemptId;
            runtime.Reset();
            Assert.That(runtime.RecordMeasurement("BloodPressure", true, "Valid", 1, "AutomaticBloodPressureMonitor", oldAttempt), Is.False);
            Assert.That(runtime.Observations.Measurements, Is.Empty);
            Assert.That(oldStore.Measurements.Single().attemptId, Is.EqualTo(oldAttempt));
            Assert.That(runtime.RecordMeasurement("BloodPressure", true, "Valid", 1, "AutomaticBloodPressureMonitor", runtime.AttemptId), Is.True);
            Assert.That(runtime.Observations.Measurements.Single().attemptId, Is.EqualTo(runtime.AttemptId));
        }

        [Test]
        public void PersistenceRemainsConsciousAndBreathingWithoutInventingArrest()
        {
            var runtime = Runtime(out _);
            Assert.That(runtime.ReportClinicalSignal("NoMeaningfulImprovement", 1), Is.True);
            runtime.Tick(300);
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_03_PERSISTENT_SYMPTOMS"));
            Assert.That(runtime.ClinicalState.Consciousness, Is.EqualTo(ConsciousnessState.Alert));
            Assert.That(runtime.ClinicalState.CanSpeak, Is.True);
            Assert.That(runtime.Patient.respiratoryRate, Is.EqualTo(18));
            Assert.That(runtime.Patient.spo2, Is.EqualTo(97));
        }

        [Test]
        public void RecurrenceRequiresEffectivePostureAndHandoverRequiresConfirmedHelp()
        {
            var runtime = Runtime(out var definition);
            Assert.That(runtime.CompleteHandover(0), Is.False);
            runtime.RequestPosition(PhysicalPosition.Supine, 1);
            runtime.ConfirmPhysicalPosition(PhysicalPosition.Supine, 2);
            double improvedAt = 2 + definition.clinicalV2.clinicalStates.Single(s => s.id == Supported).vitalTrajectories.Max(t => t.durationSeconds);
            runtime.Tick(improvedAt);
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_02_IMPROVING"));
            Assert.That(runtime.ReportClinicalSignal("UnsafeRiseAttempt", improvedAt), Is.False, "A generic signal must not bypass the physical contract.");
            runtime.RequestPosition(PhysicalPosition.Standing, improvedAt + 1);
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_02_IMPROVING"));
            runtime.ConfirmPhysicalPosition(PhysicalPosition.Standing, improvedAt + 2);
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_04_RECURRENT_PRESYNCOPE"));
            Assert.That(runtime.RequestHelp(improvedAt + 3, true), Is.True);
            Assert.That(runtime.HelpState, Is.EqualTo(HelpRequestState.Delegated));
            Assert.That(runtime.CompleteHandover(improvedAt + 4), Is.False);
            Assert.That(runtime.ConfirmHelp(improvedAt + 5), Is.True);
            Assert.That(runtime.SetHandoverAvailable(improvedAt + 6), Is.True);
            Assert.That(runtime.CompleteHandover(improvedAt + 7), Is.True);
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_07_HANDOVER"));
            Assert.That(runtime.HelpState, Is.EqualTo(HelpRequestState.Completed));
            Assert.That(runtime.ClinicalEvents.Select(e => e.eventId).Distinct().Count(), Is.EqualTo(runtime.ClinicalEvents.Count));
            Assert.That(runtime.ClinicalEvents.All(e => e.clinicalStateId != "HYP_05_BRIEF_TLOC" && e.clinicalStateId != "HYP_06_EARLY_RECOVERY_AFTER_TLOC"), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StandingIsRejectedUnlessTheCurrentStateDefinesItsPhysicalTransition(bool supported)
        {
            var runtime = Runtime(out _);
            if (supported)
            {
                runtime.RequestPosition(PhysicalPosition.Supine, 1);
                runtime.ConfirmPhysicalPosition(PhysicalPosition.Supine, 2);
            }
            var before = runtime.ClinicalState;
            Assert.That(runtime.RequestPosition(PhysicalPosition.Standing, supported ? 2 : 0), Is.False);
            Assert.That(runtime.RequestedPosition, Is.EqualTo(PhysicalPosition.Unknown));
            Assert.That(runtime.ClinicalState.ClinicalPosition, Is.EqualTo(before.ClinicalPosition));
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo(before.ClinicalStateId));
        }

        [Test]
        public void HandoverCancelsPendingPositionAndRejectsLateOrNewPhysicalTransitions()
        {
            var runtime = Runtime(out _);
            runtime.RequestPosition(PhysicalPosition.Supine, 1);
            runtime.RequestHelp(2);
            runtime.ConfirmHelp(3);
            var before = runtime.Patient;
            Assert.That(runtime.CompleteHandover(4), Is.True);
            Assert.That(runtime.ClinicalState.ClinicalStateId, Is.EqualTo("HYP_07_HANDOVER"));
            Assert.That(runtime.RequestedPosition, Is.EqualTo(PhysicalPosition.Unknown));
            Assert.That(runtime.ConfirmPhysicalPosition(PhysicalPosition.Supine, 5), Is.False);
            Assert.That(runtime.RequestPosition(PhysicalPosition.Supine, 6), Is.False);
            Assert.That(runtime.RequestPosition(PhysicalPosition.Standing, 7), Is.False);
            Assert.That(runtime.Patient.systolic, Is.EqualTo(before.systolic));
            Assert.That(runtime.Patient.heartRate, Is.EqualTo(before.heartRate));
            Assert.That(runtime.Patient.flags, Is.EquivalentTo(before.flags), "A handover does not cure symptoms.");
            Assert.That(runtime.ClinicalState.ClinicalPosition, Is.EqualTo(ClinicalPosition.SeatedSupported));
        }
    }
}
