using EmergencyVR.Desktop;
using UnityEngine;
using UnityEngine.XR;

namespace EmergencyVR.UI
{
    public sealed partial class TrainingExperience
    {
        bool applicationInterrupted, applicationPauseReported, applicationFocusLost;
        int recenterAfterTrackingFrames;
        bool controllersUnavailable;

        void OnApplicationPause(bool paused)
        {
            if (!ready || IsDesktop || !Review.Manager.IsRunning && !applicationInterrupted) return;
            applicationPauseReported = paused;
            if (paused) SuspendForApplication();
            else if (!applicationFocusLost) ReturnFromApplication();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!ready || IsDesktop || !Review.Manager.IsRunning && !applicationInterrupted) return;
            applicationFocusLost = !focused;
            if (!focused) SuspendForApplication();
            else if (!applicationPauseReported) ReturnFromApplication();
        }

        void SuspendForApplication()
        {
            if (!ready || IsDesktop || !Review.Manager.IsRunning) return;
            applicationInterrupted = true;
            ReleaseAssistance();
            Navigate(ExperiencePage.Pause); // ScenarioManager pauses the clock, patient, procedures and audio together.
            secondaryHeld = true;
        }

        void ReturnFromApplication()
        {
            if (!ready || IsDesktop || !applicationInterrupted) return;
            applicationInterrupted = false;
            if (Review.Manager.IsRunning) Navigate(ExperiencePage.Pause);
            // Tracking can deliver the returned head pose after the lifecycle callback.
            // Keep the case paused and center again once the new pose has arrived.
            recenterAfterTrackingFrames = 2;
            secondaryHeld = true;
        }

        void UpdateHeadsetLifecycle()
        {
            if (IsDesktop) return;
            if (recenterAfterTrackingFrames > 0 && --recenterAfterTrackingFrames == 0) Recenter();
            bool previous = controllersUnavailable;
            controllersUnavailable = (QuestLookSimulation.Instance != null || XRSettings.isDeviceActive) &&
                !ControllerTracked(XRNode.LeftHand) && !ControllerTracked(XRNode.RightHand);
            if (previous != controllersUnavailable) nextRefresh = 0;
            if (controllersUnavailable && Review.Manager.IsRunning && Page == ExperiencePage.Training)
            {
                ReleaseAssistance();
                Navigate(ExperiencePage.Pause);
            }
        }

        static bool ControllerTracked(XRNode hand)
        {
            if (QuestLookSimulation.TryGetControllerPose(hand, out _, out _, out var simulatedTracked)) return simulatedTracked;
            var device = InputDevices.GetDeviceAtXRNode(hand);
            // Optical hands may occupy the same XRNode after putting down the controllers.
            // This release uses controller input; keep the attempt paused until one returns.
            return device.isValid && (device.characteristics & InputDeviceCharacteristics.Controller) != 0 &&
                device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out var tracked) && tracked;
        }

        // Raw controller feedback is visible before any button has to work. It deliberately
        // says only what the device reports, not that UI selection or grabbing has succeeded.
        static string ControllerCheckSummary() => ControllerCheck(XRNode.LeftHand, "Izquierdo") + "\n" +
            ControllerCheck(XRNode.RightHand, "Derecho");

        static string ControllerCheck(XRNode hand, string label)
        {
            if (!ControllerTracked(hand)) return label + ": sin seguimiento · activa el mando";
            bool trigger, grip;
            if (QuestLookSimulation.TryGetGripTrigger(hand, out var simulatedGrip, out var simulatedTrigger))
            { trigger = simulatedTrigger > .5f; grip = simulatedGrip > .5f; }
            else
            {
                var device = InputDevices.GetDeviceAtXRNode(hand);
                trigger = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out var pressedTrigger) && pressedTrigger;
                grip = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out var pressedGrip) && pressedGrip;
            }
            return label + ": detectado · gatillo " + (trigger ? "PULSADO" : "—") +
                " · agarre " + (grip ? "PULSADO" : "—");
        }
    }
}
