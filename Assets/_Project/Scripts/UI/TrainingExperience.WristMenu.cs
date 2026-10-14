using EmergencyVR.Desktop;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace EmergencyVR.UI
{
    /// <summary>
    /// Keeps the Quest training panel out of the way of the patient.
    /// Default (release 0.1.2, not yet validated on a headset): the side panel follows the learner using the
    /// HEAD pose only — after walking more than 1 m, or as soon as it stands between the eyes and the patient,
    /// it glides back to the side away from the patient. It is always visible and clickable.
    /// Opt-in (-vital-wrist-panel): the panel is worn on the left wrist and shows when the learner looks at it.
    /// Enable that mode by default only after checking it on a real Quest.
    /// </summary>
    public sealed partial class TrainingExperience
    {
        const float WristPanelWidth = .34f, WristLookAngle = 38f, WristMaxDistance = .85f;
        CanvasGroup wristGroup;
        XROrigin wristOrigin;
        float wristAlpha = 1, wristLookupAt, wristLastLook = -10;
        bool wristFollowing;
        Vector3 wristPosition;

        static bool WristPanelRequested => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-vital-wrist-panel") >= 0;
        bool sideFollowing, sideGliding;
        Vector3 sideAnchor, sideTarget;
        Quaternion sideTargetRotation;

        /// <summary>True while the Quest training panel is attached to the left wrist.</summary>
        public bool WristPanelActive => wristFollowing;
        /// <summary>True while the wrist panel is actually visible to the learner.</summary>
        public bool WristPanelShown => wristFollowing && wristAlpha > .6f;
        /// <summary>True when the panel is worn on the wrist (opt-in), false for the following side panel.</summary>
        public bool UsesWristPanel => WristPanelRequested;

        void UpdateWristMenu()
        {
            if (IsDesktop || surface == null || viewer == null) return;
            if (wristGroup == null)
            {
                wristGroup = surface.GetComponent<CanvasGroup>();
                if (wristGroup == null) wristGroup = surface.gameObject.AddComponent<CanvasGroup>();
            }
            // The panel stays where it is placed (B / Y re-places it); it no longer follows the learner.
            if (!WristPanelRequested) { SetWristVisibility(1); return; }
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

        void UpdateFollowingSidePanel()
        {
            SetWristVisibility(1);
            if (!UsesSidePanel || QuestLookSimulation.Enabled || Review?.Procedures?.Visuals?.ChestAnchor == null)
            { sideFollowing = false; sideGliding = false; return; }
            var head = viewer.transform.position;
            var flatHead = new Vector3(head.x, 0, head.z);
            if (!sideFollowing) { sideFollowing = true; sideAnchor = flatHead; return; }   // placed by Recenter on entry

            var chest = Review.Procedures.Visuals.ChestAnchor.position;
            var toPatient = chest - head; var toPanel = surface.position - head;
            bool blocking = toPanel.magnitude < toPatient.magnitude && Vector3.Angle(toPatient, toPanel) < 22f;
            bool walkedAway = Vector3.Distance(flatHead, sideAnchor) > 1f;
            if ((blocking || walkedAway) && !sideGliding)
            {
                SidePanelPose(out sideTarget, out sideTargetRotation);
                sideGliding = true; sideAnchor = flatHead;
            }
            if (!sideGliding) return;
            float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 6f);
            surface.position = Vector3.Lerp(surface.position, sideTarget, blend);
            surface.rotation = Quaternion.Slerp(surface.rotation, sideTargetRotation, blend);
            if (Vector3.Distance(surface.position, sideTarget) < .01f) sideGliding = false;
        }

        /// <summary>Same placement as the side panel of PositionVrInterface: beside the learner, away from the patient.</summary>
        void SidePanelPose(out Vector3 position, out Quaternion rotation)
        {
            var forward = Vector3.ProjectOnPlane(viewer.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            var patient = Review.Procedures.Visuals.ChestAnchor.position - viewer.transform.position;
            float side = Vector3.Dot(patient, right) < 0 ? 1 : -1;
            position = viewer.transform.position + forward * 1.15f + right * side * 1.05f - Vector3.up * .22f;
            rotation = Quaternion.LookRotation(position - viewer.transform.position, Vector3.up);
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
