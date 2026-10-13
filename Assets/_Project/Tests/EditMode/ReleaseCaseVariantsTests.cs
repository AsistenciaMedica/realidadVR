using System;
using System.Linq;
using EmergencyVR.Dialogue;
using EmergencyVR.Medical;
using EmergencyVR.Scenarios;
using NUnit.Framework;
using UnityEngine;

namespace EmergencyVR.Tests
{
    public sealed class ReleaseCaseVariantsTests
    {
        [Test]
        public void CompositionRetainsAuthoredProceduresAndCreatesIndependentDefinitions()
        {
            var authored = JsonUtility.FromJson<MedicalLibrary>(Resources.Load<TextAsset>("MedicalScenarios").text);
            ClinicalScenarioV2Catalog.LoadInto(authored);
            var sources = authored.scenarios.ToDictionary(s => s.id);
            var originals = sources.ToDictionary(pair => pair.Key, pair => JsonUtility.ToJson(pair.Value));
            ReleaseCaseVariants.LoadInto(authored);
            foreach (var variant in ReleaseCaseVariants.Load().cases)
            {
                var scenario = authored.scenarios.Single(s => s.id == variant.id);
                var source = sources[variant.sourceId];
                Assert.That(scenario.sourceScenarioId, Is.EqualTo(source.id));
                Assert.That(scenario.actions.Select(a => JsonUtility.ToJson(a)), Is.EqualTo(source.actions.Select(a => JsonUtility.ToJson(a))), scenario.id);
                Assert.That(scenario.timeline.Select(e => JsonUtility.ToJson(e)), Is.EqualTo(source.timeline.Select(e => JsonUtility.ToJson(e))), scenario.id);
                Assert.That(scenario.outcomes.Select(o => JsonUtility.ToJson(o)), Is.EqualTo(source.outcomes.Select(o => JsonUtility.ToJson(o))), scenario.id);
                Assert.That(scenario.recommendedSequence, Is.EqualTo(source.recommendedSequence));
                Assert.That(scenario.references, Is.EqualTo(source.references));
                Assert.That(scenario.actions, Is.Not.SameAs(source.actions));
                Assert.That(scenario.initialState, Is.Not.SameAs(source.initialState));
                if (scenario.clinicalV2 != null)
                {
                    Assert.That(scenario.clinicalV2.metadata.scenarioId, Is.EqualTo(scenario.id));
                    Assert.That(scenario.clinicalV2.metadata.environment, Is.EqualTo(scenario.environment));
                    Assert.That(scenario.clinicalV2.clinicalStates.Select(s => JsonUtility.ToJson(s)),
                        Is.EqualTo(source.clinicalV2.clinicalStates.Select(s => JsonUtility.ToJson(s))));
                    Assert.That(ClinicalScenarioV2Catalog.FindPresentationAssets(scenario.id),
                        Is.SameAs(ClinicalScenarioV2Catalog.FindPresentationAssets(source.id)));
                }
            }
            foreach (var pair in sources) Assert.That(JsonUtility.ToJson(pair.Value), Is.EqualTo(originals[pair.Key]), pair.Key);
            var first = authored.scenarios.Single(s => s.id == "gym-choking");
            var second = authored.scenarios.Single(s => s.id == "street-choking");
            first.actions[0].effect.addFlags = new[] { "only this definition" };
            first.initialState.flags[0] = "only this definition";
            Assert.That(second.initialState.flags, Does.Contain("airway obstruction"));
            Assert.That(second.actions[0].effect.addFlags, Does.Not.Contain("only this definition"));
        }

        [Test]
        public void EverySelectableVariantKeepsItsPatientAndStartingPostureAcrossAttemptSeeds()
        {
            var library = MedicalLibraryLoader.Load();
            var scope = ReleaseScope.Load(library);
            foreach (var variant in ReleaseCaseVariants.Load().cases)
            {
                Assert.That(scope.ScenarioIds, Does.Contain(variant.id));
                var scenario = library.scenarios.Single(s => s.id == variant.id);
                foreach (int seed in new[] { 1, 2026, 7919 })
                {
                    var runtime = new MedicalScenarioRuntime(scenario, library, seed);
                    Assert.That(runtime.Patient.position, Is.EqualTo(variant.position), variant.id);
                    Assert.That(runtime.Patient.patientId, Is.EqualTo(scenario.patientIdentity.id));
                    Assert.That(runtime.Patient.age, Is.EqualTo(scenario.patientIdentity.age));
                    Assert.That(runtime.Patient.sex, Is.EqualTo(scenario.patientIdentity.sex));
                    if (variant.trainingCase == "cardiac-arrest") Assert.That(runtime.Patient.consciousness, Is.EqualTo("Unresponsive"));
                    if (variant.trainingCase == "choking") Assert.That(runtime.Patient.flags, Does.Contain("airway obstruction"));
                }
            }
        }

        [Test]
        public void OutdoorDizzinessAnswersAreStoredForHandoverWithoutChangingTheGymSource()
        {
            var library = MedicalLibraryLoader.Load();
            var ids = new[] { "review-hypotension-v2", "football-dizziness", "street-dizziness" };
            var contexts = new[] { "cinta", "entrenamiento", "caminar" };
            for (int i = 0; i < ids.Length; i++)
            {
                var scenario = library.scenarios.Single(s => s.id == ids[i]);
                var runtime = new MedicalScenarioRuntime(scenario, library, 2026);
                var response = runtime.Ask(DialogueIntent.ONSET, 1);
                Assert.That(response, Is.Not.Null);
                Assert.That(response.text, Does.Contain(contexts[i]));
                Assert.That(runtime.Observations.Interviews.Single().text, Is.EqualTo(response.text));
                Assert.That(ClinicalHelpController.SummarizeObserved(runtime.Observations), Does.Contain(response.text));
                if (i > 0)
                {
                    Assert.That(response.id, Is.Not.EqualTo("DANIEL_ONSET_01"));
                    Assert.That(response.audioReference, Is.Not.EqualTo("DANIEL_ONSET_01"));
                }
                var foodAndDrink = runtime.Ask(DialogueIntent.FOOD_DRINK, 2);
                Assert.That(foodAndDrink, Is.Not.Null);
                Assert.That(foodAndDrink.subtitle, Is.EqualTo(foodAndDrink.text));
                Assert.That(runtime.Observations.Interviews.Single(interview => interview.intent == DialogueIntent.FOOD_DRINK).text,
                    Is.EqualTo(foodAndDrink.text));
                if (scenario.environment == "street")
                {
                    Assert.That(foodAndDrink.text, Does.Contain("caminaba").And.Not.Contain("entrenamiento"));
                    Assert.That(foodAndDrink.id, Is.EqualTo("DANIEL_FOOD_DRINK_01_STREET"));
                    Assert.That(foodAndDrink.audioReference, Is.EqualTo(foodAndDrink.id), "The street subtitle must not play the gym recording.");
                }
                else
                {
                    Assert.That(foodAndDrink.text, Is.EqualTo("Comí antes de venir… y he bebido durante el entrenamiento."));
                    Assert.That(foodAndDrink.id, Is.EqualTo("DANIEL_FOOD_DRINK_01"));
                    Assert.That(foodAndDrink.audioReference, Is.EqualTo(foodAndDrink.id));
                }
            }
        }

        [TestCase("football-choking")]
        [TestCase("gym-choking")]
        [TestCase("street-choking")]
        public void SevereObstructionPreventsSpeechAndIdentityUntilTheRuntimeRecordsRelief(string id)
        {
            var library = MedicalLibraryLoader.Load();
            var definition = library.scenarios.Single(s => s.id == id);
            var runtime = new MedicalScenarioRuntime(definition, library, 2026);
            var conversation = new PatientConversationController(() => runtime, () => definition, () => true);
            var before = JsonUtility.ToJson(runtime.Patient);
            Assert.That(conversation.CanAskPatient, Is.False);
            Assert.That(conversation.HasWitness, Is.True);
            foreach (PatientQuestion question in new[] { PatientQuestion.NameAndAge, PatientQuestion.WhatHappened, PatientQuestion.History })
            {
                var reply = conversation.Ask(question);
                Assert.That(reply.SpokenByPatient, Is.False);
                Assert.That(reply.Speaker, Is.EqualTo("Observación"));
                Assert.That(reply.Text, Does.Contain("no puede hablar"));
            }
            Assert.That(conversation.IdentityObtained, Is.False);
            Assert.That(conversation.Lines.Length, Is.EqualTo(1));
            Assert.That(conversation.Ask(PatientQuestion.Witness).Text, Is.EqualTo(definition.patientIdentity.witnessLines[0]));
            Assert.That(runtime.Completed, Is.Empty);
            Assert.That(JsonUtility.ToJson(runtime.Patient), Is.EqualTo(before));

            foreach (string action in definition.recommendedSequence.TakeWhile(a => a != "Reassess"))
                Assert.That(runtime.Submit(action, runtime.Elapsed + 1), Is.EqualTo("Accepted"), action);
            runtime.Tick(runtime.Elapsed + 6);
            Assert.That(runtime.Patient.flags, Does.Not.Contain("airway obstruction"));
            Assert.That(conversation.CanAskPatient, Is.True);
            Assert.That(conversation.Ask(PatientQuestion.NameAndAge).SpokenByPatient, Is.True);
            Assert.That(conversation.IdentityObtained, Is.True);
        }

        [Test]
        public void PartialObstructionWithEffectiveCoughDoesNotSilenceThePatient()
        {
            var library = MedicalLibraryLoader.Load();
            var definition = library.scenarios.Single(s => s.id == "choking-partial").Copy();
            definition.patientIdentity = library.scenarios.Single(s => s.id == "gym-choking").patientIdentity.Copy();
            definition.patientIdentity.scenarioId = definition.id;
            definition.initialState.patientId = definition.patientIdentity.id;
            definition.initialState.patientName = definition.patientIdentity.displayName;
            definition.initialState.age = definition.patientIdentity.age;
            definition.initialState.sex = definition.patientIdentity.sex;
            definition.variation.minAge = definition.variation.maxAge = definition.patientIdentity.age;
            definition.variation.sexes = new[] { definition.patientIdentity.sex };
            var runtime = new MedicalScenarioRuntime(definition, library, 2026);
            var conversation = new PatientConversationController(() => runtime, () => definition, () => true);
            Assert.That(runtime.Patient.flags, Does.Contain("airway obstruction"));
            Assert.That(conversation.CanAskPatient, Is.True);
            Assert.That(conversation.Ask(PatientQuestion.NameAndAge).SpokenByPatient, Is.True);
        }
    }
}
