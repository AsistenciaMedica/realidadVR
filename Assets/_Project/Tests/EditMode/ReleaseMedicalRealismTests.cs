using System;
using System.Collections;
using System.IO;
using System.Linq;
using EmergencyVR.Medical;
using EmergencyVR.Scenarios;
using NUnit.Framework;
using UnityEngine;

namespace EmergencyVR.Tests
{
    public sealed class ReleaseMedicalRealismTests
    {
        static MedicalLibrary Load() => JsonUtility.FromJson<MedicalLibrary>(File.ReadAllText("Assets/_Project/Resources/MedicalScenarios.json"));
        static string[] SelectedIds() => JsonUtility.FromJson<ReleaseScope>(File.ReadAllText("Assets/_Project/Resources/ReleaseScope.json"))
            .ScenarioIds.Where(id => id != "review-hypotension-v2").ToArray();
        static IEnumerable ReleaseCases() => SelectedIds().Select(id => new TestCaseData(id));
        static MedicalScenarioRuntime Runtime(string id, out MedicalScenarioDefinition definition, int seed = 42)
        {
            var library = Load();
            definition = library.scenarios.Single(s => s.id == id);
            return new MedicalScenarioRuntime(definition, library, seed);
        }

        [TestCaseSource(nameof(ReleaseCases))]
        public void CompleteReleaseCaseWithRepeatObservationsKeepsSingleCredit(string id)
        {
            var runtime = Runtime(id, out var definition);
            double time = 0;
            foreach (var actionId in definition.recommendedSequence)
            {
                time = Math.Max(time + 1, runtime.EarliestTime(actionId));
                Assert.That(runtime.Submit(actionId, time), Is.EqualTo("Accepted"), id + " " + actionId);
                var rule = definition.actions.Single(a => a.id == actionId);
                if (rule.action != "Monitor" && rule.action != "Reassess") continue;
                Assert.That(rule.repeatPolicy, Is.EqualTo(ActionRepeatPolicy.RepeatableNoAdditionalCredit));
                var before = JsonUtility.ToJson(runtime.Patient);
                Assert.That(runtime.Submit(actionId, ++time), Is.EqualTo("Accepted"));
                Assert.That(runtime.Submit(actionId, ++time), Is.EqualTo("Accepted"));
                Assert.That(JsonUtility.ToJson(runtime.Patient), Is.EqualTo(before), "Observation must not treat the patient.");
                Assert.That(runtime.Completed.Count(x => x == actionId), Is.EqualTo(1));
            }
            var result = runtime.Finish(time + 1);
            Assert.That(result.scorePercent, Is.EqualTo(100));
            Assert.That(result.errors, Is.Zero);
            Assert.That(result.criticalErrors, Is.Empty);
            Assert.That(result.medicalValidationStatus, Is.EqualTo("CLIENT_REVIEW"));
            Assert.That(result.clinicallyApproved, Is.False);
            foreach (var rule in definition.actions.Where(a => a.action == "Monitor" || a.action == "Reassess"))
                Assert.That(result.actions.Count(a => a.actionId == rule.id && a.disposition == "Accepted"), Is.EqualTo(3));
        }

        [TestCaseSource(nameof(ReleaseCases))]
        public void OmittedCriticalActionsRemainAFailureForEveryReleaseCase(string id)
        {
            var runtime = Runtime(id, out _);
            var result = runtime.Finish(0);
            Assert.That(result.outcome, Is.EqualTo("CRITICAL_FAILURE"));
            Assert.That(result.scorePercent, Is.Zero);
            Assert.That(result.criticalErrors, Is.Not.Empty);
        }

        [TestCaseSource(nameof(ReleaseCases))]
        public void PauseAndSeedPreserveEvolutionAcrossTickSizes(string id)
        {
            var a = Runtime(id, out _, 84);
            var b = Runtime(id, out _, 84);
            a.Tick(30);
            var before = JsonUtility.ToJson(a.Patient);
            a.SetPaused(true);
            a.Tick(10000);
            Assert.That(a.Submit("Monitor", 10000), Is.EqualTo("Paused"));
            Assert.That(a.Finish(10000), Is.Null);
            Assert.That(a.Elapsed, Is.EqualTo(30));
            Assert.That(JsonUtility.ToJson(a.Patient), Is.EqualTo(before));
            a.SetPaused(false);
            a.Tick(700);
            for (int time = 0; time <= 700; time++) b.Tick(time);
            Assert.That(JsonUtility.ToJson(a.Patient), Is.EqualTo(JsonUtility.ToJson(b.Patient)));
            Assert.Throws<ArgumentException>(() => a.Submit("Monitor", 699));
            Assert.Throws<ArgumentException>(() => a.Submit("Monitor", double.NaN));
        }

        [TestCase("asthma")]
        [TestCase("hypoxia")]
        public void RepeatedMonitoringCannotCancelRespiratoryDeterioration(string id)
        {
            var runtime = Runtime(id, out _);
            runtime.Submit("CheckSceneSafety", 1);
            runtime.Submit("CheckResponsiveness", 2);
            runtime.Submit("CheckBreathing", 3);
            Assert.That(runtime.Submit("Monitor", 4), Is.EqualTo("Accepted"));
            Assert.That(runtime.Submit("Monitor", 50), Is.EqualTo("Accepted"));
            runtime.Tick(200);
            Assert.That(runtime.Patient.consciousness, Is.EqualTo("Drowsy"));
            Assert.That(runtime.Patient.canSwallow, Is.False);
            Assert.That(runtime.Submit("Monitor", 201), Is.EqualTo("Accepted"));
            var result = runtime.Finish(202);
            Assert.That(result.timeline.Count(e => e.id == "respiratory-fatigue"), Is.EqualTo(1));
            Assert.That(result.actions.Last(a => a.actionId == "Monitor").patient.consciousness, Is.EqualTo("Drowsy"));
        }

        [Test]
        public void RepeatStillChecksSafetyAndTimeWithoutReplayingEffects()
        {
            var library = Load();
            var definition = library.scenarios.Single(s => s.id == "hypoxia");
            var monitor = definition.actions.Single(a => a.id == "Monitor");
            monitor.guard = "conscious";
            monitor.anchorAction = "CheckBreathing";
            monitor.earliestSeconds = 10;
            monitor.effect.heartRateDelta = 2; // Synthetic rule detects accidental repeat of any effect.
            var runtime = new MedicalScenarioRuntime(definition, library, 42);
            Assert.That(runtime.Submit("Monitor", 0), Is.EqualTo("OutOfOrder"));
            runtime.Submit("CheckSceneSafety", 1);
            runtime.Submit("CheckResponsiveness", 2);
            runtime.Submit("CheckBreathing", 3);
            Assert.That(runtime.Submit("Monitor", 12), Is.EqualTo("OutOfOrder"));
            var baseline = runtime.Patient.heartRate;
            Assert.That(runtime.Submit("Monitor", 13), Is.EqualTo("Accepted"));
            Assert.That(runtime.Patient.heartRate, Is.EqualTo(baseline + 2));
            Assert.That(runtime.Submit("Monitor", 14), Is.EqualTo("Accepted"));
            Assert.That(runtime.Patient.heartRate, Is.EqualTo(baseline + 2));
            Assert.That(runtime.EarliestTime("Monitor"), Is.EqualTo(13));
            runtime.Tick(200);
            Assert.That(runtime.Submit("Monitor", 201), Is.EqualTo("Unsafe"), "The changed patient state must be checked again.");
        }

        [Test]
        public void RepeatingObservationDoesNotResetAnAnchoredEvent()
        {
            var library = Load();
            var definition = library.scenarios.Single(s => s.id == "hypoxia");
            var change = definition.timeline.Single(e => e.id == "respiratory-fatigue");
            change.anchorAction = "Monitor";
            change.afterSeconds = 20;
            definition.variation.timelineJitterSeconds = 0;
            var runtime = new MedicalScenarioRuntime(definition, library, 42);
            runtime.Submit("CheckSceneSafety", 1);
            runtime.Submit("CheckResponsiveness", 2);
            runtime.Submit("CheckBreathing", 3);
            runtime.Submit("Monitor", 4);
            runtime.Submit("Monitor", 20);
            runtime.Tick(24);
            Assert.That(runtime.Patient.consciousness, Is.EqualTo("Drowsy"));
        }

        [Test]
        public void EncouragingEffectiveCoughDoesNotAutomaticallyResolveObstruction()
        {
            var runtime = Runtime("choking-partial", out var definition);
            foreach (var id in definition.recommendedSequence.TakeWhile(id => id != "Reassess"))
                Assert.That(runtime.Submit(id, runtime.Elapsed + 1), Is.EqualTo("Accepted"));
            var before = JsonUtility.ToJson(runtime.Patient);
            runtime.Tick(runtime.Elapsed + 5);
            Assert.That(JsonUtility.ToJson(runtime.Patient), Is.EqualTo(before));
            Assert.That(runtime.Patient.flags, Does.Contain("airway obstruction"));
            runtime.Tick(200);
            Assert.That(runtime.Patient.flags, Does.Contain("airway obstruction"));
            Assert.That(runtime.Patient.canSwallow, Is.False);
        }
    }
}
