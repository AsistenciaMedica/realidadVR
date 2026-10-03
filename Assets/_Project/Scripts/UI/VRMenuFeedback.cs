using System;
using EmergencyVR.Medical.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace EmergencyVR.UI
{
    // Standard uGUI events preserve the authored XRI ray, trigger, focus and accessibility path.
    public sealed class VRMenuFeedback : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        Button target;
        Action<bool> playSound;
        float nextHover;
        public void Initialize(Button button, Action<bool> sound) { target = button; playSound = sound; }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Time.unscaledTime < nextHover || !TryHand(eventData, out var hand)) return;
            nextHover = Time.unscaledTime + .12f;
            MedicalHaptics.Pulse(hand, .045f, .018f, "UIHover");
            playSound?.Invoke(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !TryHand(eventData, out var hand)) return;
            MedicalHaptics.Pulse(hand, .11f, .032f, "UIClick");
            playSound?.Invoke(true);
        }

        bool TryHand(PointerEventData eventData, out XRNode hand)
        {
            hand = XRNode.RightHand;
            if (target == null || !target.IsActive() || !target.IsInteractable() ||
                !(eventData is TrackedDeviceEventData tracked) || !(tracked.interactor is IXRInteractor interactor)) return false;
            if (interactor.handedness == InteractorHandedness.Left) { hand = XRNode.LeftHand; return true; }
            return interactor.handedness == InteractorHandedness.Right;
        }
    }
}
