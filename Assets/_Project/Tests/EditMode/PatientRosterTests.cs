using System;
using System.Linq;
using EmergencyVR.Medical;
using EmergencyVR.Scenarios;
using NUnit.Framework;
using UnityEngine;

namespace EmergencyVR.Tests
{
    public sealed class PatientRosterTests
    {
        [Test]
        public void EveryReleaseCaseHasADistinctIdentityAndAppearanceWithoutChangingTheArchive()
        {
            var library = MedicalLibraryLoader.Load();
            var scope = ReleaseScope.Load(library);
            var release = scope.ScenarioIds.Select(id => library.scenarios.Single(s => s.id == id)).ToArray();
            Assert.That(release.Length, Is.EqualTo(15));
            Assert.That(release.All(s => s.patientIdentity != null), Is.True);
            Assert.That(release.Select(s => s.patientIdentity.id).Distinct().Count(), Is.EqualTo(15));
            Assert.That(release.Select(s => s.patientIdentity.appearanceResource).Distinct().Count(), Is.EqualTo(15));
            Assert.That(release.Select(s => s.patientIdentity.sex).Distinct().Count(), Is.EqualTo(2));
            Assert.That(library.scenarios.Where(s => !scope.ScenarioIds.Contains(s.id)).All(s => s.patientIdentity == null), Is.True);
            foreach (var scenario in release)
            {
                Assert.That(scenario.initialState.patientId, Is.EqualTo(scenario.patientIdentity.id));
                Assert.That(scenario.initialState.patientName, Is.EqualTo(scenario.patientIdentity.displayName));
                Assert.That(scenario.variation.positions, Is.EqualTo(new[] { scenario.initialState.position }));
                if (scenario.initialState.consciousness == "Unresponsive")
                    Assert.That(scenario.initialDialogue, Is.Empty);
            }
        }

        [TestCase(1)]
        [TestCase(2026)]
        [TestCase(9182)]
        public void SeedVariationNeverChangesThePersonAndReportsPreserveTheirIdentity(int seed)
        {
            var library = MedicalLibraryLoader.Load();
            foreach (var scenario in library.scenarios.Where(s => s.patientIdentity != null))
            {
                var runtime = new MedicalScenarioRuntime(scenario, library, seed);
                Assert.That(runtime.Patient.patientId, Is.EqualTo(scenario.patientIdentity.id), scenario.id);
                Assert.That(runtime.Patient.patientName, Is.EqualTo(scenario.patientIdentity.displayName), scenario.id);
                Assert.That(runtime.Patient.age, Is.EqualTo(scenario.patientIdentity.age), scenario.id);
                Assert.That(runtime.Patient.sex, Is.EqualTo(scenario.patientIdentity.sex), scenario.id);
                var report = runtime.Finish(0);
                Assert.That(report.initialPatient.patientId, Is.EqualTo(scenario.patientIdentity.id), scenario.id);
                Assert.That(report.finalPatient.patientName, Is.EqualTo(scenario.patientIdentity.displayName), scenario.id);
                if (report.objectiveBasedEvaluation)
                {
                    var learnerReport = ClinicalV2Report.From(report);
                    Assert.That(learnerReport.patientId, Is.EqualTo(scenario.patientIdentity.id));
                    Assert.That(learnerReport.patientName, Is.EqualTo(scenario.patientIdentity.displayName));
                    Assert.That(JsonUtility.ToJson(learnerReport), Does.Not.Contain("initialPatient"));
                }
            }
        }

        [Test]
        public void DefinitionCopiesDoNotShareMutableInterviewData()
        {
            var scenario = MedicalLibraryLoader.Load().scenarios.Single(s => s.id == "gym-faint");
            var copy = scenario.Copy();
            copy.patientIdentity.witnessLines[0] = "Changed only in this copy";
            copy.initialState.patientName = "Different";
            Assert.That(scenario.patientIdentity.witnessLines[0], Is.Not.EqualTo(copy.patientIdentity.witnessLines[0]));
            Assert.That(scenario.initialState.patientName, Is.EqualTo("Lucía"));
        }

        [TestCase("duplicate-person")]
        [TestCase("missing-case")]
        [TestCase("wrong-appearance")]
        [TestCase("v2-age")]
        [TestCase("unresponsive-speech")]
        public void InvalidRosterCannotPartiallyChangeTheLibrary(string invalid)
        {
            var library = MedicalLibraryLoader.Load();
            var before = JsonUtility.ToJson(library);
            var roster = PatientRoster.Load();
            switch (invalid)
            {
                case "duplicate-person": roster.profiles[1].id = roster.profiles[0].id; break;
                case "missing-case": roster.profiles[1].scenarioId = "not-in-release"; break;
                case "wrong-appearance": roster.profiles[1].appearanceResource = "Visual/Patient"; break;
                case "v2-age": roster.profiles.Single(p => p.id == "Daniel").age++; break;
                case "unresponsive-speech": roster.profiles.Single(p => p.id == "Rosa").patientOpeningLine = "I can talk"; break;
            }
            Assert.Throws<ArgumentException>(() => roster.Apply(library, ReleaseScope.Load(library)));
            Assert.That(JsonUtility.ToJson(library), Is.EqualTo(before));
        }
    }
}
