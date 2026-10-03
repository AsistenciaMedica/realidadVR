using System;
using System.Collections;
using System.Linq;
using EmergencyVR.Patient.Presentation;
using EmergencyVR.Scenarios;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EmergencyVR.Tests
{
    public sealed class PatientSceneStagingTests
    {
        [UnityTest]
        public IEnumerator ReleaseSeatedPatientsHavePelvisAndShoeSupportWithoutMovingTheRootOrAnchors()
        {
            yield return SceneManager.LoadSceneAsync("TrainingRoom");
            yield return null;
            var review = Object.FindFirstObjectByType<ReviewCaseSession>();
            var staging = PatientSceneStaging.Attach(review);
            Assert.That(PatientSceneStaging.Attach(review), Is.SameAs(staging), "Attaching twice must not create a second support or subscription.");
            var visual = review.Procedures.Visuals;
            var body = visual.Rig.GetComponent<ArticulatedPatient>();
            var chest = visual.ChestAnchor;
            var finger = visual.FingerAnchor;
            var rightPad = visual.AedRightPadAnchor;
            int seated = 0;
            foreach (var id in review.Scope.ScenarioIds)
            {
                Assert.That(review.Select(Array.FindIndex(review.Catalog.entries, e => e.medical?.id == id)), Is.True);
                Assert.That(staging.SupportVisible, Is.False, id + " must immediately withdraw the previous patient's furniture.");
                var definition = review.Selected.medical;
                bool expected = definition.clinicalV2 == null && PatientVisualState.FromSnapshot(definition.initialState).Posture == PatientPosture.Seated;
                var rootPosition = visual.transform.position;
                var rootRotation = visual.transform.rotation;
                var chestLocal = chest.localPosition;
                var fingerLocal = finger.localPosition;
                var padLocal = rightPad.localPosition;
                if (expected)
                {
                    seated++;
                    yield return WaitForSeat(staging);
                    Assert.That(staging.SupportVisible, Is.True, id + ": " + staging.LastValidationFailure);
                    Assert.That(staging.ValidateSupport(), Is.True, id + ": " + staging.LastValidationFailure);
                    Physics.SyncTransforms();
                    Assert.That(staging.Seat.Raycast(new Ray(body.pelvis.position, Vector3.down), out var hit, .28f), Is.True, id + " pelvis must be above the actual seat collider.");
                    Assert.That(hit.normal.y, Is.GreaterThan(.95f));
                    Assert.That(staging.Seat.isTrigger, Is.False);
                    var position = staging.SupportRoot.position;
                    var rotation = staging.SupportRoot.rotation;
                    int count = staging.LayoutCount;
                    yield return new WaitForSeconds(.2f);
                    Assert.That(staging.LayoutCount, Is.EqualTo(count), "Breathing must not rebuild furniture.");
                    Assert.That(staging.SupportRoot.position, Is.EqualTo(position));
                    Assert.That(staging.SupportRoot.rotation, Is.EqualTo(rotation));
                    Assert.That(visual.transform.position, Is.EqualTo(rootPosition));
                    Assert.That(visual.transform.rotation, Is.EqualTo(rootRotation));
                    Assert.That(chest.localPosition, Is.EqualTo(chestLocal));
                    Assert.That(finger.localPosition, Is.EqualTo(fingerLocal));
                    Assert.That(rightPad.localPosition, Is.EqualTo(padLocal));
                }
                else
                {
                    yield return null;
                    Assert.That(staging.SupportVisible, Is.False, id + " owns no generic seated support.");
                }
            }
            Assert.That(seated, Is.EqualTo(7), "Release posture changes require deliberate support review.");
        }

        [UnityTest]
        public IEnumerator FurnitureWithdrawsForCprAndNonSeatedPosturesAndReusesItsObjects()
        {
            yield return SceneManager.LoadSceneAsync("TrainingRoom");
            yield return null;
            var review = Object.FindFirstObjectByType<ReviewCaseSession>();
            var staging = PatientSceneStaging.Attach(review);
            var visual = review.Procedures.Visuals;
            int index = Array.FindIndex(review.Catalog.entries, e => e.medical?.id == "asthma");
            Assert.That(review.Select(index), Is.True);
            yield return WaitForSeat(staging);
            Assert.That(staging.SupportVisible, Is.True, staging.LastValidationFailure);
            var assembly = staging.SupportRoot;
            var colliders = assembly.GetComponentsInChildren<Collider>(true);
            review.Manager.StartCase();
            review.Manager.SetPaused(true);
            int count = staging.LayoutCount;
            var seatPosition = staging.Seat.transform.position;
            yield return null;
            Assert.That(staging.SupportVisible, Is.True);
            Assert.That(staging.LayoutCount, Is.EqualTo(count));
            Assert.That(staging.Seat.transform.position, Is.EqualTo(seatPosition));
            review.Manager.SetPaused(false);
            review.Manager.FinishCase();

            visual.SetCompressionDepth(.055f);
            yield return null; yield return null;
            Assert.That(staging.SupportVisible, Is.False, "Furniture cannot obstruct a compression interaction.");
            Assert.That(colliders.All(c => !c.gameObject.activeInHierarchy), Is.True);
            visual.SetCompressionDepth(0);
            foreach (var posture in new[] { PatientPosture.Supine, PatientPosture.Recovery, PatientPosture.Standing })
            {
                visual.SetStartingPose(posture);
                yield return null; yield return null;
                Assert.That(staging.SupportVisible, Is.False, posture.ToString());
            }
            visual.ClearStartingPose();
            Assert.That(review.Select(index), Is.True);
            yield return WaitForSeat(staging);
            Assert.That(staging.SupportVisible, Is.True, staging.LastValidationFailure);
            Assert.That(staging.SupportRoot, Is.SameAs(assembly));
            Assert.That(assembly.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(colliders.Length));

            var unresponsive = review.Selected.medical.initialState.Copy();
            unresponsive.consciousness = "Unresponsive";
            visual.SetState(unresponsive);
            yield return null; yield return null;
            Assert.That(staging.SupportVisible, Is.False, "Loss of response must release support even if the snapshot still says seated.");
            Assert.That(review.Select(index), Is.True);
            yield return WaitForSeat(staging);
            Assert.That(staging.SupportVisible, Is.True, staging.LastValidationFailure);
            staging.enabled = false;
            Assert.That(colliders.All(c => !c.gameObject.activeInHierarchy), Is.True, "Disabled staging leaves no invisible collision blocks.");
            staging.enabled = true;
            yield return WaitForSeat(staging);
            Assert.That(staging.SupportVisible, Is.True, staging.LastValidationFailure);
            Assert.That(staging.SupportRoot, Is.SameAs(assembly));

            Assert.That(review.Select(Array.FindIndex(review.Catalog.entries, e => e.medical?.id == "review-hypotension-v2")), Is.True);
            yield return null;
            Assert.That(staging.SupportVisible, Is.False, "Daniel keeps his own validated assistance assembly.");
            Assert.That(review.GetComponent<Case01PatientPresentation>().IsActive, Is.True);
            Object.Destroy(staging);
            yield return null; yield return null;
            Assert.That(assembly == null, Is.True, "Destroying the component releases its furniture.");
            Assert.That(review.Select(index), Is.True, "Destroyed staging must remove its selection subscription.");
            yield return null;
        }

        static IEnumerator WaitForSeat(PatientSceneStaging staging)
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (!staging.SupportVisible && Time.realtimeSinceStartup < deadline && string.IsNullOrEmpty(staging.LastValidationFailure))
                yield return null;
        }
    }
}
