#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using EmergencyVR.Desktop;
using EmergencyVR.Medical.Interaction;
using EmergencyVR.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR.Haptics;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR;

namespace EmergencyVR.Tests
{
    [PrebuildSetup(SimulatedXRTestHooks.Setup), PostBuildCleanup(SimulatedXRTestHooks.Setup)]
    public sealed class QuestLookSimulationTests
    {
        QuestLookSimulation simulation;
        TrainingExperience flow;

        [UnitySetUp]
        public IEnumerator Load()
        {
            Time.timeScale = 1;
            SceneManager.sceneLoaded += Install;
            yield return SceneManager.LoadSceneAsync("TrainingRoom");
            SceneManager.sceneLoaded -= Install;
            yield return null; yield return null;
            flow = Object.FindFirstObjectByType<TrainingExperience>();
        }

        void Install(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "TrainingRoom") return;
            simulation = QuestLookSimulation.CreateForValidation();
            simulation.ManualControlsEnabled = false;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            SceneManager.sceneLoaded -= Install;
            if (flow != null && flow.Review.Manager != null) flow.Review.Manager.SetPaused(false);
            if (simulation != null) Object.Destroy(simulation.gameObject);
            Time.timeScale = 1;
            yield return null;
        }

        [UnityTest]
        public IEnumerator QuestProfileUsesAuthoredRigAndWorldSpaceUIWithoutDesktopEffects()
        {
            Assert.That(Object.FindFirstObjectByType<DesktopDemoController>(), Is.Null);
            Assert.That(flow.IsDesktop, Is.False);
            Assert.That(flow.InterfaceCanvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(simulation.Origin.gameObject.activeInHierarchy, Is.True);
            Assert.That(simulation.View, Is.SameAs(simulation.Origin.Camera));
            Assert.That(simulation.DeviceCount, Is.EqualTo(3));
            Assert.That(QualitySettings.names[QualitySettings.GetQualityLevel()], Is.EqualTo(QuestLookSimulation.QualityName));
            Assert.That(QualitySettings.renderPipeline.name, Is.EqualTo("QuestURP"));
            Assert.That(simulation.View.fieldOfView, Is.EqualTo(100));
            Assert.That(simulation.View.aspect, Is.EqualTo(2064f / 2208).Within(.0001f));
            Assert.That(simulation.View.allowHDR, Is.False);
            Assert.That(simulation.View.GetUniversalAdditionalCameraData().renderPostProcessing, Is.False);
            DesktopAtmosphere.Apply(simulation.View);
            Assert.That(GameObject.Find("VITAL desktop grade"), Is.Null);
            Assert.That(QualitySettings.renderPipeline.name, Is.EqualTo("QuestURP"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SimulatedPoseMovesTrackedCameraAndTrackingLossReleasesButtons()
        {
            var position = new Vector3(.3f, 1.2f, -1.4f);
            var rotation = Quaternion.Euler(8, 25, 0);
            simulation.SetHeadPose(position, rotation);
            yield return null; yield return null;
            Assert.That(Vector3.Distance(simulation.View.transform.position, position), Is.LessThan(.015f));
            Assert.That(Quaternion.Angle(simulation.View.transform.rotation, rotation), Is.LessThan(.1f));
            simulation.SetGrip(XRNode.LeftHand, true);
            simulation.SetTrigger(XRNode.LeftHand, true);
            yield return null;
            Assert.That(QuestLookSimulation.TryGetGripTrigger(XRNode.LeftHand, out var grip, out var trigger), Is.True);
            Assert.That(grip, Is.EqualTo(1)); Assert.That(trigger, Is.EqualTo(1));
            simulation.SetControllerPose(XRNode.LeftHand, position, rotation, false);
            yield return null;
            Assert.That(QuestLookSimulation.TryGetControllerPose(XRNode.LeftHand, out _, out _, out var tracked), Is.True);
            Assert.That(tracked, Is.False);
            QuestLookSimulation.TryGetGripTrigger(XRNode.LeftHand, out grip, out trigger);
            Assert.That(grip, Is.Zero); Assert.That(trigger, Is.Zero);
            QuestLookSimulation.TryGetControllerPose(XRNode.RightHand, out _, out _, out var rightTracked);
            Assert.That(rightTracked, Is.True, "Losing one controller must preserve the other device.");
        }

        [UnityTest]
        public IEnumerator AuthoredXRRayAndTriggerSelectMenuWithOnlyOneTrackedController()
        {
            flow.Navigate(ExperiencePage.Environments);
            yield return null;
            var button = flow.GetComponentsInChildren<Button>().Single(b => b.name == "Environment gym");
            var center = button.transform.TransformPoint(((RectTransform)button.transform).rect.center);
            var forward = flow.InterfaceCanvas.transform.forward;
            simulation.SetControllerPose(XRNode.LeftHand, Vector3.zero, Quaternion.identity, false);
            simulation.SetControllerPose(XRNode.RightHand, center - forward * .8f, Quaternion.LookRotation(forward, Vector3.up));
            yield return null; yield return null; yield return null;
            simulation.SetTrigger(XRNode.RightHand, true);
            yield return null; yield return null;
            simulation.SetTrigger(XRNode.RightHand, false);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(flow.Page, Is.EqualTo(ExperiencePage.Catalog), "Selection must come from the authored XR ray and Input System trigger, with no Button.onClick invocation.");
            Assert.That(flow.SelectedEnvironment, Is.EqualTo("gym"));
        }

        [UnityTest]
        public IEnumerator SimulatedHapticBackendAdvertisesAndAcceptsImpulseWithoutNativeRuntime()
        {
            foreach (var device in InputSystem.devices.Where(d => d.layout == "XRSimulatedController"))
            {
                var capabilities = GetHapticCapabilitiesCommand.Create();
                Assert.That(device.ExecuteCommand(ref capabilities), Is.GreaterThanOrEqualTo(0));
                Assert.That(capabilities.numChannels, Is.EqualTo(1));
                Assert.That(capabilities.supportsImpulse, Is.True);
                Assert.That(capabilities.supportsBuffer, Is.False);
                int previous = simulation.SimulatedHapticImpulseCount;
                var impulse = SendHapticImpulseCommand.Create(0, .15f, .02f);
                Assert.That(device.ExecuteCommand(ref impulse), Is.GreaterThanOrEqualTo(0));
                Assert.That(simulation.SimulatedHapticImpulseCount, Is.EqualTo(previous + 1));
                Assert.That(simulation.LastSimulatedHapticHand, Is.EqualTo(device.usages.Contains(UnityEngine.InputSystem.CommonUsages.LeftHand) ? XRNode.LeftHand : XRNode.RightHand));
            }
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SimulationDisposalRemovesOnlyOwnedDevices()
        {
            int count = InputSystem.devices.Count;
            Object.Destroy(simulation.gameObject);
            yield return null;
            Assert.That(QuestLookSimulation.Instance, Is.Null);
            Assert.That(InputSystem.devices.Count, Is.EqualTo(count - 3));
        }
    }
}
#endif
