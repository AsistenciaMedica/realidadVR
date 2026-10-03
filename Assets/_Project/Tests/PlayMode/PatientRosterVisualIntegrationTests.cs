using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EmergencyVR.Patient.Presentation;
using EmergencyVR.Scenarios;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EmergencyVR.Tests
{
    [DefaultExecutionOrder(1000)]
    public sealed class PatientAttentionPoseProbe : MonoBehaviour
    {
        public Transform Head;
        public Vector3 LocalFaceForward;
        public float DirectionError { get; private set; }
        public Quaternion LocalRotation { get; private set; }
        public int CapturedFrames { get; private set; }

        void LateUpdate()
        {
            if (Head == null) return;
            DirectionError = Vector3.Angle(Head.TransformDirection(LocalFaceForward), transform.position - Head.position);
            LocalRotation = Head.localRotation;
            CapturedFrames++;
        }
    }

    public sealed class PatientRosterVisualIntegrationTests
    {
        [UnityTest]
        public IEnumerator UnsupportedSeatedPatientKeepsAnUprightTrunkAndBothHandsAboveThighs()
        {
            yield return SceneManager.LoadSceneAsync("TrainingRoom"); yield return null;
            var review = Object.FindFirstObjectByType<ReviewCaseSession>();
            Assert.That(review.Select(System.Array.FindIndex(review.Catalog.entries, e => e.medical?.id == "football-glucose")), Is.True);
            var visual = review.Procedures.Visuals;
            var body = visual.Rig.GetComponent<ArticulatedPatient>();
            var staging = review.GetComponent<PatientSceneStaging>();
            float deadline = Time.realtimeSinceStartup + 5;
            while (!staging.SupportVisible && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(staging.ValidateSupport(), Is.True, staging.LastValidationFailure);
            Assert.That(Vector3.Angle(body.neck.position - body.pelvis.position, Vector3.up), Is.LessThan(15),
                "A backless bench must not support a visibly reclined trunk.");
            for (int side = 0; side < 2; side++)
            {
                var thigh = body.thighs[side].position;
                var towardKnee = body.calves[side].position - thigh;
                float along = Vector3.Dot(body.hands[side].position - thigh, towardKnee) / towardKnee.sqrMagnitude;
                var separation = body.hands[side].position - (thigh + towardKnee * along);
                Assert.That(along, Is.InRange(.30f, .90f), "Wrist must rest over the thigh rather than hang beside the seat.");
                Assert.That(separation.magnitude, Is.LessThan(.17f));
                Assert.That(separation.y, Is.GreaterThan(.025f), "The hand must not penetrate the leg.");
                Assert.That(body.forearms[side].position.y, Is.LessThan(body.upperArms[side].position.y), "Elbows remain relaxed.");
            }
        }

        [UnityTest]
        public IEnumerator ConsciousSeatedAndSupinePatientsFollowTheLearnerWithoutTrackingWhenUnresponsive()
        {
            yield return SceneManager.LoadSceneAsync("TrainingRoom"); yield return null;
            var review = Object.FindFirstObjectByType<ReviewCaseSession>();
            var visual = review.Procedures.Visuals;
            var body = visual.Rig.GetComponent<ArticulatedPatient>();
            var target = new GameObject("Attention test learner");
            var pose = target.AddComponent<PatientAttentionPoseProbe>();
            visual.SetLookTarget(target.transform);
            try
            {
                foreach (var id in new[] { "football-glucose", "gym-faint" })
                {
                    target.transform.position = Vector3.one * 100;
                    Assert.That(review.Select(System.Array.FindIndex(review.Catalog.entries, e => e.medical?.id == id)), Is.True);
                    yield return new WaitForSeconds(1.3f);
                    var head = visual.Rig.head;
                    var forward = body.skeleton.TransformDirection(Vector3.forward);
                    var right = body.skeleton.TransformDirection(Vector3.right);
                    var localFaceForward = head.InverseTransformDirection(forward);
                    pose.Head = head;
                    pose.LocalFaceForward = localFaceForward;
                    var authoredPosture = visual.EffectivePosture;
                    foreach (float side in new[] { -.55f, .55f })
                    {
                        target.transform.position = head.position + forward * 1.1f + right * side;
                        yield return new WaitForSeconds(.9f);
                        // Update removes the previous additive head rotation. The probe
                        // observes the completed LateUpdate pose, also in batchmode.
                        Assert.That(pose.CapturedFrames, Is.GreaterThan(0));
                        Assert.That(pose.DirectionError,
                            Is.LessThan(8), id + " must turn toward the learner using anatomical, not imported bone, axes.");
                        Assert.That(visual.EffectivePosture, Is.EqualTo(authoredPosture));
                    }
                    var unresponsive = review.Selected.medical.initialState.Copy();
                    unresponsive.consciousness = "Unresponsive";
                    visual.SetState(unresponsive);
                    yield return new WaitForSeconds(1.2f);
                    var restingHead = pose.LocalRotation;
                    target.transform.position = head.position + forward + right * -1;
                    yield return new WaitForSeconds(.8f);
                    Assert.That(Quaternion.Angle(pose.LocalRotation, restingHead), Is.LessThan(1), "An unresponsive patient must not track a moving learner.");
                }
            }
            finally
            {
                visual.SetLookTarget(null);
                Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator SymptomaticSeatedPatientsHoldTheirTorsoAndClearTheirHandsForCompressions()
        {
            yield return SceneManager.LoadSceneAsync("TrainingRoom"); yield return null;
            var review = Object.FindFirstObjectByType<ReviewCaseSession>();
            var visual = review.Procedures.Visuals;
            var body = visual.Rig.GetComponent<ArticulatedPatient>();
            foreach (var id in new[] { "chest-pain", "asthma", "choking-partial", "hypoxia" })
            {
                Assert.That(review.Select(System.Array.FindIndex(review.Catalog.entries, e => e.medical?.id == id)), Is.True);
                yield return new WaitForSeconds(.35f);
                var chest = visual.ChestAnchor.position;
                var front = body.skeleton.TransformDirection(Vector3.forward).normalized;
                var left = body.skeleton.TransformDirection(Vector3.left).normalized;
                Assert.That(Vector3.Distance(body.hands[0].position, chest), Is.LessThan(.24f), id + " wrist must remain close to the torso.");
                Assert.That(Vector3.Dot(body.hands[0].position - chest, front), Is.InRange(.035f, .12f), id + " wrist must stay in front of the chest surface.");
                Assert.That(Vector3.Dot(body.forearms[0].position - chest, left), Is.GreaterThan(.15f), id + " elbow must remain outside the ribcage.");
                Assert.That(body.forearms[0].position.y, Is.LessThan(body.upperArms[0].position.y), id + " elbow must not form a raised greeting pose.");
                visual.SetCompressionDepth(.055f); yield return null; yield return null;
                Assert.That(Vector3.Distance(body.hands[0].position, visual.ChestAnchor.position), Is.GreaterThan(.3f), id + " compression access must remain clear.");
                visual.SetCompressionDepth(0);
                visual.SetStartingPose(PatientPosture.Supine); yield return null; yield return null;
                Assert.That(Vector3.Distance(body.hands[0].position, visual.ChestAnchor.position), Is.GreaterThan(.3f), id + " the seated symptom gesture must not persist supine.");
                visual.ClearStartingPose();
            }
        }

        [UnityTest]
        public IEnumerator AllFifteenSkinsReuseTheRigAndDanielRestoresCleanly()
        {
            yield return SceneManager.LoadSceneAsync("TrainingRoom"); yield return null;
            var review = Object.FindFirstObjectByType<ReviewCaseSession>();
            var appearance = review.GetComponent<PatientAppearanceController>();
            var rig = review.Procedures.Visuals.Rig;
            var defaultMesh = rig.face.sharedMesh;
            var chest = rig.chestAnchor; var finger = rig.fingerAnchor; var rightPad = rig.aedRightPadAnchor;
            foreach (var id in review.Scope.ScenarioIds)
            {
                int index = System.Array.FindIndex(review.Catalog.entries, e => e.medical?.id == id);
                Assert.That(review.Select(index), Is.True); yield return null;
                var identity = review.Selected.medical.patientIdentity;
                Assert.That(appearance.ActivePatientId, Is.EqualTo(identity.id));
                Assert.That(rig.face.sharedMesh, Is.SameAs(Resources.Load<PatientAppearance>(identity.appearanceResource).mesh));
                Assert.That(review.Procedures.Visuals.Rig, Is.SameAs(rig));
                Assert.That(rig.chestAnchor, Is.SameAs(chest)); Assert.That(rig.fingerAnchor, Is.SameAs(finger));
                Assert.That(rig.aedRightPadAnchor, Is.SameAs(rightPad));
                Assert.That(review.Procedures.Visuals.IsPatientContact(rig.head.position), Is.True);
                var baked = new Mesh(); rig.face.BakeMesh(baked);
                var points = baked.vertices.Select(v => rig.face.transform.TransformPoint(v)).ToArray();
                Assert.That(points.Min(v => Vector3.Distance(v, rig.head.position)), Is.LessThan(.25f), identity.id + " head contact must follow the visible mesh.");
                Assert.That(points.Min(v => Vector3.Distance(v, chest.position)), Is.LessThan(.25f), identity.id + " sternum must remain near the torso surface.");
                Object.Destroy(baked);
                int changes = appearance.AppearanceChanges;
                yield return null; yield return null;
                Assert.That(appearance.AppearanceChanges, Is.EqualTo(changes), "Rendering frames must not reload or swap the patient.");
                var warnings = new List<string>();
                Application.LogCallback collect = (message, stack, type) => {
                    if (type == LogType.Warning || type == LogType.Error || type == LogType.Exception) warnings.Add(message);
                };
                Application.logMessageReceived += collect;
                try
                {
                    var visuals = review.Procedures.Visuals;
                    visuals.enabled = false;
                    for (int channel = 0; channel < rig.face.sharedMesh.blendShapeCount; channel++)
                        Assert.That(rig.face.GetBlendShapeWeight(channel), Is.Zero, identity.id + " must restore its own facial baseline.");
                    var block = new MaterialPropertyBlock(); rig.face.GetPropertyBlock(block);
                    Assert.That(block.isEmpty, Is.True, identity.id + " must remove the previous physiology tint when disabled.");
                    visuals.enabled = true;
                    // Each appearance must also survive teardown while installed on a fresh rig.
                    var teardown = Object.Instantiate(Resources.Load<GameObject>("Visual/Patient"));
                    var teardownRig = teardown.GetComponent<PatientRigAdapter>();
                    var teardownVisuals = teardown.AddComponent<PatientVisualController>();
                    teardownVisuals.enabled = false; teardownVisuals.Bind(teardownRig);
                    teardownRig.face.sharedMesh = rig.face.sharedMesh;
                    teardownRig.face.sharedMaterials = rig.face.sharedMaterials;
                    for (int channel = 0; channel < teardownRig.face.sharedMesh.blendShapeCount; channel++) teardownRig.face.SetBlendShapeWeight(channel, 0);
                    teardownVisuals.RefreshAppearance();
                    teardownVisuals.enabled = true; teardownVisuals.enabled = false;
                    Object.Destroy(teardown);
                    yield return null;
                    Assert.That(warnings, Is.Empty, identity.id + " swap/disable/destroy must not log warnings or errors.");
                }
                finally { Application.logMessageReceived -= collect; }
            }
            foreach (var id in new[] { "review-hypotension-v2", "gym-faint", "review-hypotension-v2" })
            {
                Assert.That(review.Select(System.Array.FindIndex(review.Catalog.entries, e => e.medical?.id == id)), Is.True);
                yield return null;
                Assert.That(rig.face.sharedMesh, Is.SameAs(Resources.Load<PatientAppearance>(review.Selected.medical.patientIdentity.appearanceResource).mesh));
            }
            Assert.That(review.Select(0), Is.True); yield return null;
            Assert.That(appearance.ActivePatientId, Is.Empty);
            Assert.That(rig.face.sharedMesh, Is.SameAs(defaultMesh));
        }
    }
}
