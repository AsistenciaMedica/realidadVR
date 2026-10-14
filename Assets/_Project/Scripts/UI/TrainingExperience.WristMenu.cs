using EmergencyVR.Desktop;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace EmergencyVR.UI
{
    /// <summary>
    /// Quest training panel worn on the left wrist, like a tablet: it appears when the learner looks at the left
    /// controller and fades away otherwise, so it never stands between the learner and the patient however much
    /// they move. Actions on the patient itself are done by touching the body (HandsOnInteraction).
    /// Falls back to the original fixed side panel when the left controller is not tracked and in the desktop
    /// Quest-look simulation (whose validation flows click the panel at its authored place).
    /// </summary>
    public sealed partial class TrainingExperience
    {
        const float WristPanelWidth = .34f, WristLookAngle = 38f, WristMaxDistance = .85f;
        CanvasGroup wristGroup;
        XROrigin wristOrigin;
        float wristAlpha = 1, wristLookupAt, wristLastLook = -10;
        bool wristFollowing;
        Vector3 wristPosition;

        /// <summary>True while the Quest training panel is attached to the left wrist.</summary>
        public bool WristPanelActive => wristFollowing;

        void UpdateWristMenu()
        {
            if (IsDesktop || surface == null || viewer == null) return;
            if (wristGroup == null)
            {
                wristGroup = surface.GetComponent<CanvasGroup>();
                if (wristGroup == null) wristGroup = surface.gameObject.AddComponent<CanvasGroup>();
            }
            bool wanted = UsesSidePanel && !QuestLookSimulation.Enabled && TryLeftHand(out var hand, out _);
            if (!wanted)
            {
                if (wristFollowing) { wristFollowing = false; Recenter(); }
                SetWristVisibility(1);
                return;
            }

            // Entering training: show the panel for a few seconds so the learner finds it on the wrist.
            if (!wristFollowing) wristLastLook = Time.unscaledTime + 3f;
            var head = viewer.transform.position;
            var toHand = hand - head;
            bool looking = toHand.magnitude < WristMaxDistance && Vector3.Angle(viewer.transform.forward, toHand) < WristLookAngle;
            // Short grace period so a glance at the patient while choosing an option does not make it vanish.
            if (looking) wristLastLook = Time.unscaledTime;
            bool show = Time.unscaledTime - wristLastLook < 1.2f;
            wristAlpha = Mathf.MoveTowards(wristAlpha, show ? 1 : 0, Time.unscaledDeltaTime * 5f);
            SetWristVisibility(wristAlpha);

            // Panel floats just above the back of the left hand and faces the eyes.
            surface.sizeDelta = VrPageSize;
            surface.localScale = Vector3.one * (WristPanelWidth / surface.sizeDelta.x);
            float height = surface.sizeDelta.y * surface.localScale.y;
            var up = Vector3.up;
            var target = hand + up * (height * .5f + .06f) + (head - hand).normalized * .03f;
            wristPosition = wristFollowing ? Vector3.Lerp(wristPosition, target, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 14f)) : target;
            wristFollowing = true;
            surface.position = wristPosition;
            var away = wristPosition - head;
            if (away.sqrMagnitude > 1e-4f) surface.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        }

        void SetWristVisibility(float alpha)
        {
            if (wristGroup == null) return;
            wristGroup.alpha = alpha;
            bool usable = alpha > .6f;
            wristGroup.interactable = usable;
            wristGroup.blocksRaycasts = usable;
        }

        bool TryLeftHand(out Vector3 position, out Quaternion rotation)
        {
            position = default; rotation = Quaternion.identity;
            if (Time.unscaledTime >= wristLookupAt && wristOrigin == null)
            {
                wristOrigin = FindFirstObjectByType<XROrigin>();
                wristLookupAt = Time.unscaledTime + 1;
            }
            if (wristOrigin == null) return false;
            var device = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (!device.isValid || !device.TryGetFeatureValue(CommonUsages.devicePosition, out var local) ||
                !device.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) || !tracked) return false;
            device.TryGetFeatureValue(CommonUsages.deviceRotation, out var localRotation);
            var frame = wristOrigin.CameraFloorOffsetObject != null ? wristOrigin.CameraFloorOffsetObject.transform : wristOrigin.transform;
            position = frame.TransformPoint(local);
            rotation = frame.rotation * localRotation;
            return true;
        }
    }
}
