using System;
using System.Linq;
using EmergencyVR.Medical;
using EmergencyVR.Scenarios;
using NUnit.Framework;
using UnityEngine;

namespace EmergencyVR.Tests
{
    public sealed class ReleaseScopeTests
    {
        [Test]
        public void ReleaseHasThreeEnvironmentsAndFifteenCasesWhileTheFullLibraryIsPreserved()
        {
            var library = MedicalLibraryLoader.Load();
            var before = JsonUtility.ToJson(library);
            var scope = ReleaseScope.Load(library);
            Assert.That(scope.environments.Select(e => e.id), Is.EqualTo(new[] { "gym", "mall", "football" }));
            Assert.That(scope.environments.All(e => e.scenarioIds.Length == 5), Is.True);
            Assert.That(scope.ScenarioIds.Length, Is.EqualTo(15));
            Assert.That(scope.ScenarioIds.Distinct().Count(), Is.EqualTo(15));
            Assert.That(scope.ScenarioIds, Does.Contain("review-hypotension-v2"));
            Assert.That(scope.ScenarioIds, Does.Not.Contain("review-hypotension-v1"));
            Assert.That(library.scenarios.Any(s => s.environment == "dental"), Is.True);
            Assert.That(library.scenarios.Any(s => s.id == "review-hypotension-v1"), Is.True);
            Assert.That(JsonUtility.ToJson(library), Is.EqualTo(before), "Selection must not mutate or truncate authored cases.");
        }

        [TestCase("schema")]
        [TestCase("release-id")]
        [TestCase("environment-count")]
        [TestCase("null-environment")]
        [TestCase("duplicate-environment")]
        [TestCase("environment-name")]
        [TestCase("archived-environment")]
        [TestCase("case-count")]
        [TestCase("duplicate-case")]
        [TestCase("missing-case")]
        [TestCase("wrong-environment")]
        [TestCase("unavailable-case")]
        public void InvalidReleaseScopeIsRejectedBeforeBuildingTheSelectableCatalog(string invalid)
        {
            var library = MedicalLibraryLoader.Load();
            var scope = ReleaseScope.Load(library);
            switch (invalid)
            {
                case "schema": scope.schemaVersion = 2; break;
                case "release-id": scope.releaseId = " "; break;
                case "environment-count": scope.environments = scope.environments.Take(2).ToArray(); break;
                case "null-environment": scope.environments[0] = null; break;
                case "duplicate-environment": scope.environments[1].id = scope.environments[0].id; break;
                case "environment-name": scope.environments[0].name = ""; break;
                case "archived-environment":
                    scope.environments[2].id = "dental";
                    scope.environments[2].scenarioIds = library.scenarios.Where(s => s.environment == "dental").Take(5).Select(s => s.id).ToArray();
                    break;
                case "case-count": scope.environments[0].scenarioIds = scope.environments[0].scenarioIds.Take(4).ToArray(); break;
                case "duplicate-case": scope.environments[0].scenarioIds[1] = scope.environments[0].scenarioIds[0]; break;
                case "missing-case": scope.environments[0].scenarioIds[0] = "missing"; break;
                case "wrong-environment": scope.environments[0].scenarioIds[0] = "review-fainting-v1"; break;
                case "unavailable-case": library.scenarios.Single(s => s.id == scope.ScenarioIds[0]).availability = "DISABLED"; break;
            }
            Assert.Throws<ArgumentException>(() => scope.Validate(library));
        }
    }
}
