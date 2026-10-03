using System.Linq;
using EmergencyVR.Dialogue;
using EmergencyVR.Medical;
using NUnit.Framework;
using UnityEngine;

namespace EmergencyVR.Tests
{
    public sealed class PatientConversationTests
    {
        MedicalLibrary library;
        MedicalScenarioDefinition definition;
        MedicalScenarioRuntime runtime;
        PatientConversationController conversation;
        bool acceptsInput;

        [SetUp] public void SetUp()
        {
            library = MedicalLibraryLoader.Load();
            definition = library.scenarios.Single(s => s.id == "dehydration").Copy();
            definition.initialState.consciousness = "Conscious";
            definition.initialState.respiration = "normal";
            definition.initialState.flags = new string[0];
            acceptsInput = true;
            StartAttempt();
            conversation = new PatientConversationController(() => runtime, () => definition, () => acceptsInput);
        }
        void StartAttempt() => runtime = new MedicalScenarioRuntime(definition, library, 2026);

        [Test] public void AuthoredConversationAndWitnessNeverChangeClinicalEvidenceOrScore()
        {
            var before = JsonUtility.ToJson(runtime.Patient);
            var name = conversation.Ask(PatientQuestion.NameAndAge);
            Assert.That(name.Text, Does.Contain(definition.patientIdentity.displayName));
            Assert.That(name.Text, Does.Contain(runtime.Patient.age.ToString()));
            Assert.That(name.SpokenByPatient, Is.True);
            Assert.That(conversation.IdentityObtained, Is.True);
            Assert.That(conversation.Ask(PatientQuestion.WhatHappened).Text, Is.EqualTo(definition.patientIdentity.patientOpeningLine));
            Assert.That(conversation.Ask(PatientQuestion.History).Text, Is.EqualTo(definition.patientIdentity.historyReply));
            foreach (var account in definition.patientIdentity.witnessLines)
            {
                var witness = conversation.Ask(PatientQuestion.Witness);
                Assert.That(witness.Text, Is.EqualTo(account));
                Assert.That(witness.SpokenByPatient, Is.False);
            }
            Assert.That(runtime.Elapsed, Is.Zero);
            Assert.That(runtime.Completed, Is.Empty);
            Assert.That(runtime.ClinicalEvents, Is.Empty);
            Assert.That(JsonUtility.ToJson(runtime.Patient), Is.EqualTo(before));
            var untouched = new MedicalScenarioRuntime(definition, library, 2026).Finish(0);
            var result = runtime.Finish(0);
            Assert.That(result.scorePercent, Is.EqualTo(untouched.scorePercent));
            Assert.That(result.omittedActions, Is.EqualTo(untouched.omittedActions));
            Assert.That(result.actions, Is.Empty);
            Assert.That(conversation.CanAsk, Is.False);
        }

        [TestCase("Unresponsive", "normal", "normal")]
        [TestCase("Conscious", "agonal", "normal")]
        [TestCase("Conscious", "absent", "normal")]
        [TestCase("Conscious", "normal", "pulseless")]
        [TestCase("Drowsy", "normal", "normal")]
        public void UnableToAnswerNeverSpeaksAuthoredLines(string consciousness, string respiration, string circulation)
        {
            definition.initialState.consciousness = consciousness;
            definition.initialState.respiration = respiration;
            definition.initialState.circulation = circulation;
            definition.initialState.canSwallow = consciousness != "Unresponsive";
            StartAttempt();
            foreach (var question in new[] { PatientQuestion.NameAndAge, PatientQuestion.WhatHappened, PatientQuestion.History })
            {
                var line = conversation.Ask(question);
                Assert.That(line.SpokenByPatient, Is.False);
                Assert.That(line.Speaker, Is.EqualTo("Observación"));
            }
            Assert.That(conversation.IdentityObtained, Is.False);
            Assert.That(conversation.Ask(PatientQuestion.Witness).Text, Is.EqualTo(definition.patientIdentity.witnessLines[0]));
        }

        [Test] public void ConfusionDoesNotInventReliableIdentityOrHistory()
        {
            definition.initialState.consciousness = "Confused";
            StartAttempt();
            Assert.That(conversation.Ask(PatientQuestion.NameAndAge).SpokenByPatient, Is.False);
            Assert.That(conversation.Ask(PatientQuestion.History).SpokenByPatient, Is.False);
            Assert.That(conversation.IdentityObtained, Is.False);
            Assert.That(conversation.Ask(PatientQuestion.WhatHappened).Text, Does.EndWith("…"));
        }

        [Test] public void BreathlessAnswersStayShortButConfirmedAgeMatchesWhatWasSaid()
        {
            definition.initialState.respiration = "fast";
            StartAttempt();
            var name = conversation.Ask(PatientQuestion.NameAndAge);
            Assert.That(name.Text, Does.Contain(runtime.Patient.age.ToString()));
            Assert.That(name.Text, Does.EndWith("…"));
            var history = conversation.Ask(PatientQuestion.History);
            Assert.That(history.Text.Split(' ').Length, Is.LessThanOrEqualTo(9));
            Assert.That(history.Text, Does.EndWith("…"));
        }

        [Test] public void PausingReselectingAndResettingDoNotMixAttempts()
        {
            conversation.Ask(PatientQuestion.NameAndAge);
            conversation.Ask(PatientQuestion.Witness);
            string transcript = conversation.Transcript;
            runtime.SetPaused(true);
            Assert.That(conversation.Ask(PatientQuestion.History), Is.Null);
            Assert.That(conversation.Transcript, Is.EqualTo(transcript));
            runtime.SetPaused(false);
            acceptsInput = false;
            Assert.That(conversation.Ask(PatientQuestion.History), Is.Null);
            acceptsInput = true;
            runtime.Reset();
            Assert.That(conversation.Lines, Is.Empty, "A reset on the same runtime is a new attempt.");
            Assert.That(conversation.IdentityObtained, Is.False);
            Assert.That(conversation.Ask(PatientQuestion.Witness).Text, Is.EqualTo(definition.patientIdentity.witnessLines[0]));
            definition = library.scenarios.Single(s => s.id == "chest-pain");
            StartAttempt();
            Assert.That(conversation.Transcript, Is.Empty);
            Assert.That(conversation.Ask(PatientQuestion.Witness).Text, Is.EqualTo(definition.patientIdentity.witnessLines[0]));
        }

        [Test] public void LegacyConversationDoesNotReplaceDanielsClinicalDialogue()
        {
            definition = library.scenarios.Single(s => s.id == "review-hypotension-v2");
            StartAttempt();
            Assert.That(conversation.CanAsk, Is.False);
            Assert.That(conversation.Ask(PatientQuestion.NameAndAge), Is.Null);
            Assert.That(conversation.Lines, Is.Empty);
        }

        [Test] public void RecoveryReplacesTheOpeningComplaintWithCurrentObservation()
        {
            definition = library.scenarios.Single(s => s.id == "glucose-moderate");
            StartAttempt();
            foreach (var id in definition.recommendedSequence.TakeWhile(id => id != "RecheckGlucose"))
                Assert.That(runtime.Submit(id, runtime.Elapsed + 1), Is.EqualTo("Accepted"));
            runtime.Tick(runtime.Elapsed + 610);
            Assert.That(runtime.Patient.dialogue, Is.Not.EqualTo(definition.initialDialogue));
            var line = conversation.Ask(PatientQuestion.WhatHappened);
            Assert.That(line.SpokenByPatient, Is.False);
            Assert.That(line.Speaker, Is.EqualTo("Observación"));
            Assert.That(line.Text, Is.EqualTo(runtime.Patient.dialogue));
        }
    }
}
