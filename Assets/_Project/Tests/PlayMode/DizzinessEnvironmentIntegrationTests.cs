using System;
using System.Collections;
using System.Linq;
using EmergencyVR.Dialogue;
using EmergencyVR.Medical;
using EmergencyVR.Patient.Presentation;
using EmergencyVR.UI;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EmergencyVR.Tests
{
    public sealed class DizzinessEnvironmentIntegrationTests
    {
        TrainingExperience flow;

        [UnityTearDown]
        public IEnumerator Restore()
        {
            if (flow != null && flow.Review.Manager.IsRunning) flow.Review.Manager.FinishCase();
            Time.timeScale = 1;
            AudioListener.pause = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator DanielKeepsSupportAndCommunicatesTheSelectedSettingAcrossAllThreeEnvironments()
        {
            Time.timeScale = 1;
            AudioListener.pause = false;
            yield return SceneManager.LoadSceneAsync("TrainingRoom");
            yield return null;
            yield return null;
            flow = UnityEngine.Object.FindFirstObjectByType<TrainingExperience>();
            Assert.That(flow, Is.Not.Null);
            var ids = new[] { "review-hypotension-v2", "football-dizziness", "street-dizziness" };
            var environments = new[] { "gym", "football", "street" };
            var places = new[] { "gimnasio", "cancha", "calle" };
            var onsets = new[] { "cinta", "entrenamiento", "caminar" };
            for (int i = 0; i < ids.Length; i++)
            {
                int index = Array.FindIndex(flow.Review.Catalog.entries, entry => entry.medical?.id == ids[i]);
                Assert.That(index, Is.GreaterThanOrEqualTo(0), ids[i]);
                flow.Prepare(index);
                flow.BeginTraining();
                yield return null;
                yield return null;
                var manager = flow.Review.Manager;
                var body = flow.Review.GetComponent<Case01PatientPresentation>();
                Assert.That(manager.MedicalDefinition.environment, Is.EqualTo(environments[i]));
                Assert.That(body.IsActive, Is.True, ids[i]);
                Assert.That(body.ValidateCurrentPose(), Is.True, body.LastValidationFailure);
                Assert.That(body.PhoneAnchor, Is.Not.Null);

                // The answer recorded for the eventual handover must match the answer seen by the learner.
                var response = manager.Dialogue.Ask(DialogueIntent.ONSET);
                Assert.That(response, Is.Not.Null);
                Assert.That(response.text, Does.Contain(onsets[i]));
                Assert.That(manager.MedicalSession.Observations.Interviews.Last().text, Is.EqualTo(response.text));
                if (i > 0) Assert.That(manager.Dialogue.HasVoiceForLastResponse, Is.False,
                    "An exterior answer must not play the old treadmill recording.");

                if (manager.Dialogue.Voice != null) manager.Dialogue.Voice.Stop();
                manager.Dialogue.AskSpoken(new SpokenRequest { SmallTalk = "place" });
                yield return new WaitForSeconds(.6f);
                Assert.That(manager.Dialogue.LastResponse.text, Does.Contain(places[i]));
                if (i > 0) Assert.That(manager.Dialogue.LastResponse.text, Does.Not.Contain("gimnasio"));

                var phone = flow.Review.GetComponent<PhoneCallController>();
                foreach (char digit in PhoneCallController.EmergencyNumber) phone.Press(digit);
                phone.Dial();
                float timeout = Time.realtimeSinceStartup + 8;
                while (phone.Stage == PhoneCallStage.Ringing && Time.realtimeSinceStartup < timeout) yield return null;
                Assert.That(phone.Stage, Is.EqualTo(PhoneCallStage.Talking));
                Assert.That(phone.Current.Id, Is.EqualTo("greeting"));
                Assert.That(phone.Current.Options[0].Text, Does.Contain(i == 2 ? "acera" : places[i]));
                phone.Choose(0);
                Assert.That(phone.Current.Id, Is.EqualTo("location"));
                var help = flow.Review.GetComponent<ClinicalHelpController>();
                Assert.That(phone.Current.Options[0].Text, Is.EqualTo(help.LocationSummary));
                if (i > 0)
                {
                    Assert.That(help.LocationSummary, Does.Not.Contain("Gimnasio"));
                    phone.Choose(1);
                    Assert.That(phone.Subtitle, Does.Not.Contain("gimnasio"));
                }
                phone.Choose(0);
                Assert.That(help.Status, Is.EqualTo(ClinicalHelpStage.AwaitingSituation));

                // Each environment must offer a physically clear, supported seated-to-supine route.
                var camera = Camera.main;
                var origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
                var desktop = UnityEngine.Object.FindFirstObjectByType<EmergencyVR.Desktop.DesktopDemoController>();
                var target = body.AssistanceAnchor.position;
                if (desktop != null)
                {
                    bool wasEnabled = desktop.Body.enabled;
                    desktop.Body.enabled = false;
                    desktop.transform.position += target - camera.transform.position;
                    desktop.Body.enabled = wasEnabled;
                }
                else if (origin != null && camera.transform.IsChildOf(origin.transform)) origin.transform.position += target - camera.transform.position;
                else camera.transform.position = target;
                Physics.SyncTransforms();
                body.assistanceSeconds = 2;
                Assert.That(body.BeginAssistance(), Is.True, ids[i] + ": " + body.LastValidationFailure);
                body.SetSupportHeld(true);
                timeout = Time.realtimeSinceStartup + 6;
                while (!body.FinalPositionValidated && Time.realtimeSinceStartup < timeout) yield return null;
                Assert.That(body.FinalPositionValidated, Is.True, ids[i] + ": " + body.Instruction);
                Assert.That(manager.MedicalSession.ClinicalState.ClinicalPosition, Is.EqualTo(ClinicalPosition.Supine));
                manager.FinishCase();
                yield return null;
            }
        }
    }
}
