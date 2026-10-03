using System.Collections;
using System.IO;
using System.Linq;
using EmergencyVR.Scenarios;
using EmergencyVR.Environment;
using EmergencyVR.Medical;
using EmergencyVR.Medical.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EmergencyVR.Tests
{
    public sealed class MedicalIntegrationTests
    {
        [UnityTest]
        public IEnumerator PublicEnvironmentsStoreTheSameFunctionalToolsInCabinetsOrPortableCases()
        {
            yield return SceneManager.LoadSceneAsync("TrainingRoom"); yield return null;
            var review = Object.FindFirstObjectByType<ReviewCaseSession>();
            var flow = Object.FindFirstObjectByType<EmergencyVR.UI.TrainingExperience>();
            var aed = review.Procedures.GetComponentsInChildren<MedicalPhysicalTool>(true).Single(t => t.Kind == MedicalToolKind.AED);
            var chest = review.Procedures.Visuals.ChestAnchor;
            foreach (var environment in new[] { "gym", "mall", "football", "gym" })
            {
                int index = System.Array.FindIndex(review.Catalog.entries, e => e.medical?.environment == environment && e.medical.clinicalV2 == null);
                flow.Prepare(index);
                flow.BeginTraining();
                yield return null;
                yield return null;
                Assert.That(review.Manager.IsRunning, Is.True);
                var presenter = Object.FindFirstObjectByType<ScenarioEnvironmentPresenter>();
                var module = presenter.transform.Find("Environment_" + environment);
                Assert.That(module, Is.Not.Null);
                Assert.That(module.GetComponentsInChildren<Transform>().Any(t => t.name == "MedicalCart" || t.name == "DefibrillatorPlaceholder"), Is.False);
                Assert.That(review.Procedures.GetComponentsInChildren<Transform>().Any(t => t.name == "Bedside tablet case"), Is.False);
                Assert.That(aed.CanBeGrabbed, Is.True);
                Assert.That(review.Procedures.Visuals.ChestAnchor, Is.SameAs(chest));
                var shelf = module.Find(environment == "football" ? "First aid station shelf collider" : "AED cabinet shelf collider").GetComponent<Collider>();
                var shell = aed.transform.Find("AED shell").GetComponent<Renderer>().bounds;
                var sole = new Vector3(shell.center.x, shell.min.y, shell.center.z);
                Physics.SyncTransforms();
                Assert.That(shelf.Raycast(new Ray(sole + Vector3.up * .1f, Vector3.down), out var contact, .2f), Is.True,
                    environment + ": AED sole=" + sole + ", shelf=" + shelf.bounds);
                Assert.That(Mathf.Abs(contact.point.y - sole.y), Is.LessThan(.025f), "The real grab object must rest on visible support.");
                var home = aed.transform.position;
                aed.transform.position += Vector3.up;
                aed.ResetTool();
                Assert.That(aed.transform.position, Is.EqualTo(home), "Reset must use the selected environment's storage position.");
                flow.FinishTraining();
                yield return null;
            }
        }

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
