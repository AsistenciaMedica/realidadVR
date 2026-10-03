using System.Collections;
using System.Linq;
using EmergencyVR.Dialogue;
using EmergencyVR.Patient.Presentation;
using EmergencyVR.Scenarios;
using EmergencyVR.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EmergencyVR.Tests
{
    public sealed class SupportingCastTests
    {
        [UnityTest]
        public IEnumerator WitnessesStandOnTheGroundAndSpeakWithoutChangingClinicalEvidence()
        {
            yield return SceneManager.LoadSceneAsync("TrainingRoom"); yield return null;
            var review = Object.FindFirstObjectByType<ReviewCaseSession>();
            var flow = Object.FindFirstObjectByType<TrainingExperience>();
            foreach (string id in new[] { "asthma", "confusion", "arrest-witnessed" })
            {
                Assert.That(review.Select(System.Array.FindIndex(review.Catalog.entries, e => e.medical?.id == id)), Is.True);
                review.Manager.StartCase();
                yield return new WaitForSeconds(.35f);
                var cast = review.GetComponent<ScenarioSupportingCast>();
                Assert.That(cast.Count, Is.EqualTo(2));
                Assert.That(cast.Witness, Is.Not.Null);
                var actors = review.GetComponentsInChildren<SupportingActor>();
                Assert.That(actors.Select(a => a.AppearanceId).Distinct().Count(), Is.EqualTo(2));
                foreach (var actor in actors)
                {
                    Assert.That(actor.AppearanceId, Is.Not.EqualTo(review.Selected.medical.patientIdentity.id));
                    Assert.That(actor.Motion.Playing, Does.Contain("idle"));
                    var mesh = new Mesh();
                    ((SkinnedMeshRenderer)actor.Skin).BakeMesh(mesh);
                    var points = mesh.vertices.Select(p => actor.Skin.transform.TransformPoint(p)).ToArray();
                    Object.Destroy(mesh);
                    Assert.That(points.Min(p => p.y), Is.InRange(-.06f, .06f), "Witness feet must touch the floor: " + actor.AppearanceId);
                    Assert.That(points.Max(p => p.y), Is.InRange(1.4f, 2.1f), "The independent rig must be upright.");
                    Assert.That(actor.GetComponent<CharacterContactShadow>().Visible, Is.True);
                }
                var attempt = review.Manager.MedicalSession;
                var before = JsonUtility.ToJson(attempt.Patient);
                var line = flow.PatientConversation.Ask(PatientQuestion.Witness);
                var audio = review.GetComponent<RosterConversationAudio>();
                Assert.That(audio.LastSpokenText, Is.EqualTo(line.Text));
                Assert.That(audio.ActiveVoice, Is.SameAs(cast.Witness.Voice));
                Assert.That(audio.ActiveVoice.clip, Is.Not.Null);
                Assert.That(attempt.Completed, Is.Empty);
                Assert.That(JsonUtility.ToJson(attempt.Patient), Is.EqualTo(before));
                if (id == "arrest-witnessed")
                {
                    flow.PatientConversation.Ask(PatientQuestion.NameAndAge);
                    Assert.That(audio.ActiveVoice, Is.SameAs(cast.Witness.Voice), "An observation must never play the unconscious patient's voice.");
                }
                review.Manager.FinishCase();
                yield return null; yield return null;
                Assert.That(cast.Count, Is.Zero);
                Assert.That(audio.ActiveVoice, Is.Null);
            }
        }
    }
}
