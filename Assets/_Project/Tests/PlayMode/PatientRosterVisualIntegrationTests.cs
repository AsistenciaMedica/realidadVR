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
    public sealed class PatientRosterVisualIntegrationTests
    {
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
