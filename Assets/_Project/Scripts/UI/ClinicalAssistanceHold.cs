using System;
using EmergencyVR.Patient.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EmergencyVR.UI
{
    /// <summary>The same press-and-hold contract for mouse, XR UI pointer and focused keyboard control.</summary>
    public sealed class ClinicalAssistanceHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        Func<Case01PatientPresentation> findBody;
        bool keyboardHeld;
        public void Configure(Func<Case01PatientPresentation> source) { findBody = source; }
        void Hold(bool held)
        {
            var body = findBody?.Invoke(); if (body == null) return;
            if (held)
            {
                var button = GetComponent<Button>(); if (button == null || !button.IsInteractable()) return;
                if (!body.IsAssisting && !body.BeginAssistance()) return;
            }
            body.SetSupportHeld(held);
        }
        public void OnPointerDown(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) Hold(true); }
        public void OnPointerUp(PointerEventData data) { Hold(false); }
        public void OnPointerExit(PointerEventData data) { Hold(false); }
        void Update()
        {
            bool held = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject && Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            if (held == keyboardHeld) return;
            keyboardHeld = held; Hold(held);
        }
        void OnDisable() { keyboardHeld = false; Hold(false); }
    }
}
