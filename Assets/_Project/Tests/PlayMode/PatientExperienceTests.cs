using System;
using System.Collections;
using System.Linq;
using EmergencyVR.Dialogue;
using EmergencyVR.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace EmergencyVR.Tests
{
    public sealed class PatientExperienceTests
    {
        TrainingExperience flow;
        [UnitySetUp] public IEnumerator Load()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("Assets/_Project/Scenes/Training/TrainingRoom.unity");
            yield return null; yield return null;
            flow = UnityEngine.Object.FindFirstObjectByType<TrainingExperience>();
            Assert.That(flow, Is.Not.Null);
            flow.Review.Procedures.TrainingMode = false;
        }
        [UnityTearDown] public IEnumerator Restore()
        {
            if (flow != null && flow.Review.Manager != null)
            {
                flow.Review.Manager.SetPaused(false);
                if (flow.Review.Manager.IsRunning) flow.Review.Manager.FinishCase();
            }
            Time.timeScale = 1; yield return null;
        }
        string TextOf(string name) => flow.GetComponentsInChildren<Text>().Single(t => t.name == name).text;
        void Click(string name)
        {
            var button = flow.GetComponentsInChildren<Button>().Single(b => b.name == name);
            Assert.That(button.interactable, Is.True, name);
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        }

        [UnityTest] public IEnumerator FifteenPatientsKeepNeutralTitlesUntilTheDebrief()
        {
            foreach (var id in flow.Review.Scope.ScenarioIds)
            {
                int index = Array.FindIndex(flow.Review.Catalog.entries, e => e.medical?.id == id);
                var definition = flow.Review.Catalog.entries[index].medical;
                var identity = definition.patientIdentity;
                flow.Prepare(index); yield return null;
                Assert.That(TextOf("Page title"), Is.EqualTo(TrainingExperience.LearnerTitle(definition)), id);
                Assert.That(TextOf("Page title"), Does.Not.Contain(definition.name), id);
                Assert.That(TextOf("Briefing context"), Does.Contain(identity.context), id);
                Assert.That(TextOf("Briefing context"), Does.Contain(identity.presentingComplaint), id);
                Assert.That(flow.Review.Manager.IsRunning, Is.False);
                flow.BeginTraining(); yield return null;
                Assert.That(TextOf(definition.clinicalV2 == null ? "Current case" : "Session status"), Does.Contain(identity.displayName), id);
                Assert.That(flow.MonitorValue("spo2"), Is.EqualTo("—"), id);
                Assert.That(flow.MonitorValue("bp"), Is.EqualTo("—"), id);
                flow.TogglePause(); yield return null;
                Assert.That(TextOf("Pause case"), Is.EqualTo(TrainingExperience.LearnerTitle(definition)), id);
                Assert.That(flow.PatientConversation.CanAsk, Is.False);
                flow.TogglePause(); flow.FinishTraining(); yield return null;
                Assert.That(TextOf("Subtitle"), Does.Contain(definition.name.Replace(" · piloto migrado", "")), id);
            }
        }

        [UnityTest] public IEnumerator ConversationButtonsShowAuthoredRepliesAndResetOnRepeat()
        {
            int index = Array.FindIndex(flow.Review.Catalog.entries, e => e.medical?.id == "dehydration");
            flow.Prepare(index); flow.BeginTraining(); yield return null;
            Click("Open patient"); yield return null;
            Assert.That(TextOf("Patient details"), Does.Not.Contain(" años"));
            Click("Patient name"); yield return null;
            Assert.That(TextOf("Patient details"), Does.Contain(flow.Review.Manager.MedicalSession.Patient.age + " años"));
            Assert.That(TextOf("Patient conversation"), Does.Contain(flow.Review.Selected.medical.patientIdentity.displayName));
            Click("Witness account"); yield return null;
            string transcript = TextOf("Patient conversation");
            Assert.That(transcript, Does.Contain(flow.Review.Selected.medical.patientIdentity.witnessLines[0]));
            Assert.That(flow.Review.Manager.MedicalSession.Completed, Is.Empty);
            Assert.That(flow.MonitorValue("spo2"), Is.EqualTo("—"));
            flow.TogglePause(); yield return null;
            Assert.That(flow.PatientConversation.Ask(PatientQuestion.History), Is.Null);
            Assert.That(flow.PatientConversation.Transcript, Is.EqualTo(transcript));
            flow.TogglePause(); flow.FinishTraining(); yield return null;
            flow.Prepare(index); flow.BeginTraining(); yield return null;
            Click("Open patient"); yield return null;
            Assert.That(flow.PatientConversation.Lines, Is.Empty);
            Assert.That(TextOf("Patient details"), Does.Not.Contain(" años"));
            Assert.That(TextOf("Patient conversation"), Does.Not.Contain(transcript));
        }
    }
}
