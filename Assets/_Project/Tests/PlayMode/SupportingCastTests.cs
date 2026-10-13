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
    [DefaultExecutionOrder(1000)]
    public sealed class WitnessPoseProbe : MonoBehaviour
    {
        public Transform[] Feet;
        public SupportingActor[] Actors;
        Vector3[] anchors;
        public float MaximumDrift { get; private set; }
        public bool SawGesture { get; private set; }
        public int Frames { get; private set; }
        void LateUpdate()
        {
            if (Feet == null) return;
            if (anchors == null) anchors = Feet.Select(t => t.position).ToArray();
            for (int i = 0; i < Feet.Length; i++)
                MaximumDrift = Mathf.Max(MaximumDrift, Vector3.Distance(anchors[i], Feet[i].position));
            SawGesture |= Actors.Any(a => a.Motion.Playing.Contains("gestic"));
            Frames++;
        }
    }

    public sealed class SupportingCastTests
    {
        [UnityTest]
        public IEnumerator WitnessesStandOnTheGroundAndSpeakWithoutChangingClinicalEvidence()
        {
            yield return SceneManager.LoadSceneAsync("TrainingRoom"); yield return null;
            var review = Object.FindFirstObjectByType<ReviewCaseSession>();
            var flow = Object.FindFirstObjectByType<TrainingExperience>();
            var manifestSource = Resources.Load<TextAsset>("Audio/RosterVoices");
            Assert.That(manifestSource, Is.Not.Null);
            var manifest = JsonUtility.FromJson<RosterConversationAudio.Manifest>(manifestSource.text);
            foreach (string id in new[] { "gym-choking", "street-choking", "arrest-witnessed" })
            {
                Assert.That(review.Select(System.Array.FindIndex(review.Catalog.entries, e => e.medical?.id == id)), Is.True);
                review.Manager.StartCase();
                yield return new WaitForSeconds(.35f);
                var cast = review.GetComponent<ScenarioSupportingCast>();
                Assert.That(cast.Count, Is.EqualTo(2));
                Assert.That(cast.Witness, Is.Not.Null);
                Assert.That(cast.Witness.Voice, Is.Not.Null);
                Assert.That(cast.Witness.Voice.transform.IsChildOf(cast.Witness.transform), Is.True);
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
                // Exercise a complete idle/gesture transition, not just the spawn pose.
                var feet = actors.SelectMany(a => a.GetComponentsInChildren<Transform>())
                    .Where(t => t.name == "Bip01 L Foot" || t.name == "Bip01 R Foot").ToArray();
                Assert.That(feet.Length, Is.EqualTo(actors.Length * 2));
                // Observe the rendered pose after LateUpdate, not the intermediate mocap
                // evaluated before the planted-foot correction (also works in batchmode).
                var probe = actors[0].gameObject.AddComponent<WitnessPoseProbe>();
                probe.Feet = feet; probe.Actors = actors;
                yield return new WaitForSeconds(14);
                Assert.That(probe.Frames, Is.GreaterThan(10));
                Assert.That(probe.SawGesture, Is.True);
                Assert.That(probe.MaximumDrift, Is.LessThan(.008f), "A witness must not slide during attention or gestures.");
                review.Manager.SetPaused(true);
                yield return null;
                var heads = actors.SelectMany(a => a.GetComponentsInChildren<Transform>()).Where(t => t.name == "Bip01 Head").ToArray();
                var rotations = heads.Select(t => t.rotation).ToArray();
                yield return new WaitForSecondsRealtime(.2f);
                for (int h = 0; h < heads.Length; h++)
                    Assert.That(Quaternion.Angle(rotations[h], heads[h].rotation), Is.LessThan(.1f), "Pausing also stops witness attention.");
                review.Manager.SetPaused(false);
                var before = JsonUtility.ToJson(attempt.Patient);
                var eventsBefore = attempt.ClinicalEvents.Count;
                var line = flow.PatientConversation.Ask(PatientQuestion.Witness);
                var audio = review.GetComponent<RosterConversationAudio>();
                Assert.That(line.SpokenByPatient, Is.False);
                Assert.That(line.Speaker, Is.EqualTo("Testigo"));
                Assert.That(flow.PatientConversation.Transcript, Does.Contain(line.Text));
                var recording = manifest.lines.SingleOrDefault(entry => entry.scenarioId == id &&
                    entry.speaker == line.Speaker && entry.text == line.Text);
                if (recording != null)
                {
                    Assert.That(audio.LastSpokenText, Is.EqualTo(line.Text));
                    Assert.That(audio.ActiveVoice, Is.SameAs(cast.Witness.Voice));
                    Assert.That(audio.ActiveVoice.clip, Is.SameAs(Resources.Load<AudioClip>(recording.resource)));
                    Assert.That(audio.ActiveVoice.clip, Is.Not.Null);
                    Assert.That(audio.SubtitleOnly, Is.False);
                }
                else
                {
                    Assert.That(audio.SubtitleOnly, Is.True, "A new setting must keep its authored subtitle until an exact recording exists.");
                    Assert.That(audio.LastSpokenText, Is.Empty);
                    Assert.That(audio.ActiveVoice, Is.Null, "Do not substitute an archived witness recording for the current text.");
                    Assert.That(cast.Witness.Voice.clip, Is.Null);
                }
                Assert.That(attempt.Completed, Is.Empty);
                Assert.That(attempt.ClinicalEvents.Count, Is.EqualTo(eventsBefore));
                Assert.That(JsonUtility.ToJson(attempt.Patient), Is.EqualTo(before));
                if (id == "arrest-witnessed")
                {
                    var observation = flow.PatientConversation.Ask(PatientQuestion.NameAndAge);
                    Assert.That(observation.SpokenByPatient, Is.False);
                    Assert.That(observation.Speaker, Is.EqualTo("Observación"));
                    Assert.That(audio.ActiveVoice, Is.Null, "An observation stops older speech and never plays the unconscious patient's voice.");
                    Assert.That(audio.SubtitleOnly, Is.True);
                }
                review.Manager.FinishCase();
                Assert.That(review.Manager.MedicalResult.actions, Is.Empty);
                Assert.That(review.Manager.MedicalResult.scorePercent, Is.Zero, "Witness presentation must not earn clinical credit.");
                yield return null; yield return null;
                Assert.That(cast.Count, Is.Zero);
                Assert.That(audio.ActiveVoice, Is.Null);
            }
        }
    }
}
