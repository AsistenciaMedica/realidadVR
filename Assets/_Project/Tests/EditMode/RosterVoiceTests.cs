using System.Linq;
using System.Reflection;
using EmergencyVR.Dialogue;
using EmergencyVR.Medical;
using EmergencyVR.Scenarios;
using NUnit.Framework;
using UnityEngine;

namespace EmergencyVR.Tests
{
    public sealed class RosterVoiceTests
    {
        [Test]
        public void EveryActuallySpokenLegacyReplyHasAnExactSpanishRecording()
        {
            var library = ArchivedPatientFixtures.LoadLibrary();
            var source = Resources.Load<TextAsset>("Audio/RosterVoices");
            Assert.That(source, Is.Not.Null);
            var manifest = JsonUtility.FromJson<RosterConversationAudio.Manifest>(source.text);
            int cases = 0;
            foreach (var definition in library.scenarios.Where(s => s.patientIdentity != null && s.clinicalV2 == null))
            {
                cases++;
                var attempt = new MedicalScenarioRuntime(definition, library, 2026);
                var conversation = new PatientConversationController(() => attempt, () => definition, () => true);
                foreach (var question in new[] { PatientQuestion.NameAndAge, PatientQuestion.WhatHappened, PatientQuestion.History, PatientQuestion.Witness, PatientQuestion.Witness })
                {
                    var response = conversation.Ask(question);
                    if (!response.SpokenByPatient && response.Speaker != "Testigo") continue;
                    var entry = manifest.FindExact(definition.id, response);
                    Assert.That(entry, Is.Not.Null, definition.id + ": " + response.Text);
                    var clip = Resources.Load<AudioClip>(entry.resource);
                    Assert.That(clip, Is.Not.Null, entry.resource);
                    Assert.That(clip.length, Is.GreaterThan(.2f));
                }
            }
            Assert.That(cases, Is.EqualTo(14));
        }

        [TestCase("different-scenario")]
        [TestCase("different-text")]
        [TestCase("different-speaker")]
        [TestCase("observation")]
        public void ExistingRecordingIsNeverUsedForAnotherSettingOrResponse(string mismatch)
        {
            var recording = new RosterConversationAudio.Line { scenarioId = "arrest-witnessed", speaker = "Testigo",
                text = "Se ha desplomado mientras jugábamos.", resource = "Audio/Roster/existing" };
            var manifest = new RosterConversationAudio.Manifest { lines = new[] { recording } };
            Assert.That(manifest.FindExact(recording.scenarioId, new PatientConversationLine(recording.speaker, recording.text)), Is.SameAs(recording));
            string id = mismatch == "different-scenario" ? "gym-arrest" : recording.scenarioId;
            string speaker = mismatch == "different-speaker" ? "Camila" : mismatch == "observation" ? "Observación" : recording.speaker;
            string text = mismatch == "different-text" ? "Se ha desplomado después de entrenar en el gimnasio." : recording.text;
            Assert.That(manifest.FindExact(id, new PatientConversationLine(speaker, text, mismatch == "different-speaker")), Is.Null);
        }

        [Test]
        public void ReplacingSpeechWithAnObservationStopsThePreviousClipAndLeavesAnExplicitSubtitle()
        {
            var item = new GameObject("Roster voice regression", typeof(AudioSource), typeof(RosterConversationAudio));
            var source = item.GetComponent<AudioSource>();
            var presenter = item.GetComponent<RosterConversationAudio>();
            var clip = AudioClip.Create("Previous response", 22050, 1, 22050, false);
            try
            {
                source.clip = clip;
                typeof(RosterConversationAudio).GetField("activeVoice", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(presenter, source);
                typeof(RosterConversationAudio).GetMethod("Presented", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(presenter, new object[] { new PatientConversationLine("Observación", "El paciente no puede hablar por el atragantamiento.") });
                Assert.That(presenter.ActiveVoice, Is.Null);
                Assert.That(source.clip, Is.Null, "An older response must not continue under a different subtitle.");
                Assert.That(presenter.LastSpokenText, Is.Empty);
                Assert.That(presenter.SubtitleOnly, Is.True);
            }
            finally { Object.DestroyImmediate(item); Object.DestroyImmediate(clip); }
        }

        [Test]
        public void CurrentReleaseDoesNotSubstituteArchivedWitnessRecordingsForNewContext()
        {
            var library = MedicalLibraryLoader.Load();
            var manifest = JsonUtility.FromJson<RosterConversationAudio.Manifest>(Resources.Load<TextAsset>("Audio/RosterVoices").text);
            var selected = ReleaseScope.Load(library).ScenarioIds;
            int subtitles = 0;
            foreach (var definition in library.scenarios.Where(s => selected.Contains(s.id) && s.clinicalV2 == null))
            {
                var attempt = new MedicalScenarioRuntime(definition, library, 2026);
                var conversation = new PatientConversationController(() => attempt, () => definition, () => true);
                foreach (var witness in definition.patientIdentity.witnessLines)
                {
                    var response = conversation.Ask(PatientQuestion.Witness);
                    Assert.That(response.Text, Is.EqualTo(witness));
                    var exact = manifest.FindExact(definition.id, response);
                    if (exact != null)
                    {
                        Assert.That(exact.scenarioId, Is.EqualTo(definition.id));
                        Assert.That(exact.text, Is.EqualTo(response.Text));
                        Assert.That(Resources.Load<AudioClip>(exact.resource), Is.Not.Null);
                    }
                    else subtitles++;
                }
            }
            Assert.That(subtitles, Is.GreaterThan(0), "New context currently has authored subtitles awaiting exact recordings.");
        }
    }
}
