using System.Linq;
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
            var library = MedicalLibraryLoader.Load();
            var source = Resources.Load<TextAsset>("Audio/RosterVoices");
            Assert.That(source, Is.Not.Null);
            var manifest = JsonUtility.FromJson<RosterConversationAudio.Manifest>(source.text);
            var scope = ReleaseScope.Load(library);
            int cases = 0;
            foreach (var definition in library.scenarios.Where(s => scope.ScenarioIds.Contains(s.id) && s.clinicalV2 == null))
            {
                cases++;
                var attempt = new MedicalScenarioRuntime(definition, library, 2026);
                var conversation = new PatientConversationController(() => attempt, () => definition, () => true);
                foreach (var question in new[] { PatientQuestion.NameAndAge, PatientQuestion.WhatHappened, PatientQuestion.History, PatientQuestion.Witness, PatientQuestion.Witness })
                {
                    var response = conversation.Ask(question);
                    if (!response.SpokenByPatient && response.Speaker != "Testigo") continue;
                    var entry = manifest.lines.SingleOrDefault(l => l.scenarioId == definition.id && l.speaker == response.Speaker && l.text == response.Text);
                    Assert.That(entry, Is.Not.Null, definition.id + ": " + response.Text);
                    var clip = Resources.Load<AudioClip>(entry.resource);
                    Assert.That(clip, Is.Not.Null, entry.resource);
                    Assert.That(clip.length, Is.GreaterThan(.2f));
                }
            }
            Assert.That(cases, Is.EqualTo(14));
        }
    }
}
