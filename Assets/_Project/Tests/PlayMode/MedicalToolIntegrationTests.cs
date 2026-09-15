using System.Collections;
using System.Linq;
using EmergencyVR.Medical.Interaction;
using EmergencyVR.Scenarios;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EmergencyVR.Tests
{
    public sealed class MedicalToolIntegrationTests
    {
        [UnityTest]
        public IEnumerator GlucoseInstrumentCanBeRemovedRecheckedDroppedAndResetWithoutStaleReadings()
        {
            yield return SceneManager.LoadSceneAsync("TrainingRoom");
            yield return null;
            var review = Object.FindFirstObjectByType<ReviewCaseSession>();
            int index = System.Array.FindIndex(review.Catalog.entries, e => e.medical?.id == "glucose-moderate");
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            Assert.That(review.Select(index), Is.True);
            var rig = review.Procedures;
            Assert.That(rig.NextId("CheckGlucose"), Is.Null, "No attempt means no pending physical action.");
            review.Manager.StartCase();
            review.Submit("CheckSceneSafety");
            review.Submit("CheckResponsiveness");
            review.Submit("CheckBreathing");
            var meter = Object.FindObjectsByType<MedicalPhysicalTool>(FindObjectsSortMode.None)
                .Single(t => t.Kind == MedicalToolKind.Glucose);
            var originalParent = meter.transform.parent;
            var originalLocalPosition = meter.transform.localPosition;
            var body = meter.GetComponent<Rigidbody>();

            PlaceMeter();
            yield return Acquisition();
            Assert.That(review.Manager.MedicalSession.Completed, Does.Contain("CheckGlucose"));
            Assert.That(rig.Measurements, Is.EqualTo(1));
            Assert.That(meter.IsAttached, Is.True);
            Assert.That(meter.CanBeGrabbed, Is.True, "A measurement instrument must remain removable.");
            Assert.That(body.isKinematic, Is.True);

            meter.OnGrabbed();
            Assert.That(meter.IsAttached, Is.False);
            Assert.That(meter.transform.parent, Is.EqualTo(originalParent));
            meter.transform.position = new Vector3(-.4f, 1.4f, 0);
            float dropHeight = meter.transform.position.y;
            Assert.That(meter.OnReleased(), Is.False, "An unprepared tool away from its target must drop.");
            yield return new WaitForSeconds(.15f);
            Assert.That(body.isKinematic, Is.False);
            Assert.That(body.useGravity, Is.True);
            Assert.That(meter.transform.position.y, Is.LessThan(dropHeight));

            review.Submit("GiveGlucose");
            Assert.That(review.Manager.MedicalSession.Completed, Does.Contain("GiveGlucose"));
            PlaceMeter();
            yield return Acquisition();
            Assert.That(review.Manager.MedicalSession.Completed, Does.Not.Contain("RecheckGlucose"),
                "Moving the real instrument cannot bypass the configured 600-second reassessment wait.");
            Assert.That(rig.Measurements, Is.EqualTo(1));

            meter.OnGrabbed();
            review.Manager.AdvanceTrainingTime(600);
            meter.Use();
            AlignAndRelease();
            yield return Acquisition();
            Assert.That(review.Manager.MedicalSession.Completed, Does.Contain("RecheckGlucose"));
            Assert.That(rig.Measurements, Is.EqualTo(2));
            Assert.That(meter.Display.text, Does.Contain("mmol/L"));

            // Begin another acquisition, then switch away before it finishes. It must not outlive its attempt.
            PlaceMeter();
            review.Manager.FinishCase();
            Assert.That(review.Select(0), Is.True);
            Assert.That(rig.NextId("CheckGlucose"), Is.Null);
            Assert.That(meter.IsAttached, Is.False);
            Assert.That(meter.transform.parent, Is.EqualTo(originalParent));
            Assert.That(Vector3.Distance(meter.transform.localPosition, originalLocalPosition), Is.LessThan(.0001f));
            Assert.That(meter.gameObject.activeInHierarchy, Is.False, "The old instrument must leave the patient with its kit.");
            yield return Acquisition();
            Assert.That(meter.Display.text, Is.EqualTo("--"));
            Assert.That(rig.Measurements, Is.Zero);

            void PlaceMeter() { meter.OnGrabbed(); meter.Use(); AlignAndRelease(); }
            void AlignAndRelease()
            {
                var target = rig.Visuals.FingerAnchor;
                meter.transform.SetPositionAndRotation(target.position, target.rotation);
                Assert.That(meter.OnReleased(), Is.True, "Prepared instrument should attach at its anatomical target.");
            }
            IEnumerator Acquisition()
            {
                yield return new WaitForSeconds((float)rig.Settings.readingDelaySeconds + .2f);
            }
        }
    }
}
