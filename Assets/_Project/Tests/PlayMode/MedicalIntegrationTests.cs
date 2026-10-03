using System.Collections;
using System.IO;
using System.Linq;
using EmergencyVR.Scenarios;
using EmergencyVR.Environment;
using EmergencyVR.Medical;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EmergencyVR.Tests
{
    public sealed class MedicalIntegrationTests
    {
        [UnityTest] public IEnumerator PhysicalCprAndAedReachSharedEngineAndExportMetrics()
        {
            yield return SceneManager.LoadSceneAsync("TrainingRoom");yield return null;
            var review=Object.FindFirstObjectByType<ReviewCaseSession>();
            yield return EmergencyVR.Desktop.ProcedureValidation.Run(review);
            Assert.That(review.Manager.MedicalResult.procedures.shocks,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator SharedSelectorPreservesTechnicalDemoAndSwitchesThreeReleaseEnvironments()
        {
            yield return SceneManager.LoadSceneAsync("TrainingRoom");yield return null;
            var review=Object.FindFirstObjectByType<ReviewCaseSession>();
            Assert.That(review.Catalog.entries.Length,Is.EqualTo(16));
            Assert.That(review.Catalog.entries.Count(e=>e.medical==null),Is.EqualTo(1),"The original technical demo remains available.");
            Assert.That(review.Selected.definition,Is.SameAs(review.Manager.Scenario.defaultCase));
            Assert.That(review.Selected.definition.isTechnicalDemo,Is.True);
            Assert.That(review.Catalog.entries.Where(e=>e.medical!=null).Select(e=>e.medical.id),Is.EqualTo(review.Scope.ScenarioIds));
            Assert.That(review.Catalog.entries.Any(e=>e.medical?.environment=="dental"),Is.False);
            Assert.That(review.Catalog.entries.Count(e=>e.medical?.id=="review-hypotension-v2"),Is.EqualTo(1));
            foreach(var env in review.Scope.environments.Select(e=>e.id))
            {
                Assert.That(review.Catalog.entries.Count(e=>e.medical?.environment==env),Is.EqualTo(5));
                int i=System.Array.FindIndex(review.Catalog.entries,e=>e.medical?.environment==env&&e.medical.clinicalV2==null);
                Assert.That(review.Select(i),Is.True);review.Manager.StartCase();Assert.That(review.Select(0),Is.False);
                review.Submit("CheckSceneSafety");Assert.That(review.Manager.MedicalSession.Completed,Does.Contain("CheckSceneSafety"));
                Assert.That(Object.FindFirstObjectByType<ScenarioEnvironmentPresenter>().CurrentEnvironment,Is.EqualTo(env));
                review.Manager.FinishCase();var path=review.ExportResult();
                Assert.That(File.Exists(path),Is.True);var report=JsonUtility.FromJson<MedicalDebrief>(File.ReadAllText(path));Assert.That(report.schemaVersion,Is.EqualTo(3));File.Delete(path);
            }
            Assert.That(review.Select(0),Is.True);Assert.That(review.Manager.MedicalDefinition,Is.Null);
            Assert.That(Object.FindFirstObjectByType<GeneratedEnvironment>(),Is.Not.Null);
        }
    }
}
