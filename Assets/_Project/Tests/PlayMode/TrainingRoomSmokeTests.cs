using System.Collections;
using System.Linq;
using EmergencyVR.Core;
using EmergencyVR.Environment;
using EmergencyVR.Evaluation;
using EmergencyVR.Patient;
using EmergencyVR.Scenarios;
using EmergencyVR.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using Unity.XR.CoreUtils;

namespace EmergencyVR.Tests
{
    public sealed class TrainingRoomSmokeTests
    {
        [UnityTest]
        public IEnumerator BootstrapLoadsRoomAndExistingUiPatientSequenceScores100()
        {
            yield return SceneManager.LoadSceneAsync("Assets/_Project/Scenes/Bootstrap/Bootstrap.unity");
            float deadline=Time.realtimeSinceStartup+30;
            while(SceneManager.GetActiveScene().name!="TrainingRoom" && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("TrainingRoom"));
            yield return null; // Start wires UI listeners.
            Assert.That(Object.FindObjectsByType<GeneratedEnvironment>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            var origin=Object.FindFirstObjectByType<XROrigin>();
            Assert.That(origin,Is.Not.Null);
            Assert.That(origin.Camera.isActiveAndEnabled,Is.True);
            Assert.That(Object.FindObjectsByType<TeleportationArea>(FindObjectsSortMode.None).Length,Is.EqualTo(4));
            var manager=Object.FindFirstObjectByType<ScenarioManager>();
            var patient=Object.FindFirstObjectByType<PatientController>();
            var panel=Object.FindFirstObjectByType<TrainingPanel>();
            var buttons=panel.GetComponentsInChildren<Button>();
            buttons.Single(b=>b.name=="Iniciar caso demo").onClick.Invoke();
            Assert.That(manager.IsRunning,Is.True);
            // Exercise the existing PatientInteraction listener; this does not simulate a controller ray.
            patient.GetComponent<XRSimpleInteractable>().selectEntered.Invoke(new SelectEnterEventArgs());
            Assert.That(patient.State,Is.EqualTo(PatientState.Recovering));
            buttons.Single(b=>b.name=="Transición demo").onClick.Invoke();
            Assert.That(patient.State,Is.EqualTo(PatientState.Recovered));
            buttons.Single(b=>b.name=="Finalizar").onClick.Invoke();
            Assert.That(Object.FindFirstObjectByType<EvaluationManager>().LatestResult.ScorePercent,Is.EqualTo(100));
            Assert.That(manager.IsRunning,Is.False);
            Assert.That(panel.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("100 / 100")),Is.True);
        }
    }
}
