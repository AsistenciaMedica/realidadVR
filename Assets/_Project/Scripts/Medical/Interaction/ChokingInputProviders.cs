using EmergencyVR.Desktop;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace EmergencyVR.Medical.Interaction
{
    [DefaultExecutionOrder(-45)]
    public sealed class WindowsChokingInput : MonoBehaviour
    {
        ChokingInteractionController controller;
        DesktopDemoController desktop;
        bool dragging;
        float startY, lookupAt;
        Vector3 placement;
        public void Initialize(ChokingInteractionController value) { controller = value; }
        void Update()
        {
            if (desktop == null && Time.unscaledTime >= lookupAt)
            { desktop = FindFirstObjectByType<DesktopDemoController>(); lookupAt = Time.unscaledTime + .5f; }
            // Do not cancel a controller stroke while the XR provider owns contact.
            if (desktop == null) { if (dragging) Cancel(); return; }
            if (Keyboard.current == null || Mouse.current == null) { Cancel(); return; }
            if (desktop.WorldInputBlocked || desktop.PointerOverInterface || desktop.HasHeldTool || !controller.CanAct)
            { Cancel(); return; }
            if (Keyboard.current.hKey.wasPressedThisFrame)
                controller.SetWindowsEngaged(!controller.WindowsEngaged && controller.InReach(desktop.View.transform.position));
            if (!controller.WindowsEngaged || !controller.InReach(desktop.View.transform.position))
            { controller.SetWindowsEngaged(false); Cancel(); return; }
            var mouse = Mouse.current;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                var ray = desktop.View.ScreenPointToRay(mouse.position.ReadValue());
                // The abdomen contact plane can be reached around the patient from behind.
                var plane = new Plane(controller.Anterior, controller.RestPosition);
                if (plane.Raycast(ray, out var distance) && distance < 1.8f)
                {
                    placement = ray.GetPoint(distance) - controller.RestPosition;
                    dragging = placement.magnitude < .08f;
                    placement = Vector3.ProjectOnPlane(placement, controller.GestureDirection);
                    startY = mouse.position.y.ReadValue();
                }
            }
            if (dragging && mouse.leftButton.isPressed)
            {
                float travel = Mathf.Clamp((mouse.position.y.ReadValue() - startY) * .0007f, 0, .09f);
                var hand = controller.RestPosition + placement + controller.GestureDirection * travel;
                var side = Vector3.Cross(controller.GestureDirection, controller.HeadDirection).normalized;
                var support = controller.IsBackBlow ? controller.SupportPosition : hand + side * .035f;
                controller.FeedHands(support, hand, desktop.View.transform.position, true);
            }
            // A release only cancels: the pointer must actually return to complete a stroke.
            if (mouse.leftButton.wasReleasedThisFrame) Cancel();
        }
        void Cancel() { dragging = false; if (controller != null) controller.CancelContact(); }
        void OnDisable() { Cancel(); }
    }

    [DefaultExecutionOrder(-45)]
    public sealed class VRChokingInput : MonoBehaviour
    {
        ChokingInteractionController controller;
        XROrigin origin;
        DesktopDemoController desktop;
        float lookupAt;
        public void Initialize(ChokingInteractionController value) { controller = value; }
        void Update()
        {
            if (Time.unscaledTime >= lookupAt)
            {
                desktop = FindFirstObjectByType<DesktopDemoController>();
                if (desktop == null && origin == null) origin = FindFirstObjectByType<XROrigin>();
                lookupAt = Time.unscaledTime + .5f;
            }
            // The desktop input provider owns the shared contact state when it is active.
            if (desktop != null) return;
            if (origin == null || origin.Camera == null || !controller.CanAct) { controller.CancelContact(); return; }
            bool leftValid = Read(XRNode.LeftHand, out var left, out var leftGrip);
            bool rightValid = Read(XRNode.RightHand, out var right, out var rightGrip);
            controller.FeedHands(left, right, origin.Camera.transform.position, leftValid && rightValid && leftGrip && rightGrip);
        }
        bool Read(XRNode node, out Vector3 position, out bool gripping)
        {
            if (QuestLookSimulation.TryGetControllerPose(node, out position, out _, out var tracked))
            {
                QuestLookSimulation.TryGetGripTrigger(node, out var grip, out _);
                gripping = grip > .5f; return tracked;
            }
            var device = InputDevices.GetDeviceAtXRNode(node);
            bool valid = device.TryGetFeatureValue(CommonUsages.devicePosition, out position) &&
                device.TryGetFeatureValue(CommonUsages.isTracked, out var isTracked) && isTracked;
            gripping = device.TryGetFeatureValue(CommonUsages.gripButton, out var held) && held;
            var frame = origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : origin.transform;
            position = frame.TransformPoint(position); return valid;
        }
        void OnDisable() { if (controller != null) controller.CancelContact(); }
    }
}
