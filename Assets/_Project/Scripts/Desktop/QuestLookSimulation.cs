using System;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
#if ENABLE_VR || UNITY_GAMECORE
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
#endif

namespace EmergencyVR.Desktop
{
    /// <summary>
    /// Explicit Windows visual verification mode. The real XR rig, Input Actions and world-space UI
    /// consume simulated devices. This is a single-eye rendering approximation, not Quest hardware validation.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class QuestLookSimulation : MonoBehaviour
    {
        public const string Argument = "-vital-quest-look";
        public const string QualityName = "Medium"; // ProjectSettings: Android default quality, using QuestURP.
        public const int CaptureWidth = 2064, CaptureHeight = 2208, Width = CaptureWidth, Height = CaptureHeight;
        public const float FieldOfView = 100;
        public static QuestLookSimulation Instance { get; private set; }
        public static bool Requested
        {
            get
            {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
                return System.Environment.GetCommandLineArgs().Contains(Argument);
#else
                return false;
#endif
            }
        }
        public static bool Enabled => Requested || Instance != null;
        public XROrigin Origin { get; private set; }
        public Camera View { get; private set; }
        public bool ManualControlsEnabled { get; set; }
        public int DeviceCount { get; private set; }
        public bool IsSimulation => true;
        Transform TrackingSpace => Origin.CameraFloorOffsetObject != null ? Origin.CameraFloorOffsetObject.transform : Origin.transform;
        int previousQuality;
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        bool initialized;
#if ENABLE_VR || UNITY_GAMECORE
        XRSimulatedHMD hmd;
        XRSimulatedController left, right;
        XRSimulatedHMDState headState;
        XRSimulatedControllerState leftState, rightState;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= Loaded;
            if (Requested) SceneManager.sceneLoaded += Loaded;
        }

        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "TrainingRoom") Create();
        }

        public static QuestLookSimulation Create()
        {
            if (!Requested) throw new InvalidOperationException("Quest simulation requires " + Argument + ".");
            return Install();
        }

#if UNITY_EDITOR
        // PlayMode tests opt in explicitly. There is no equivalent enable switch in the Android player.
        public static QuestLookSimulation CreateForValidation() => Install();
#endif

        static QuestLookSimulation Install()
        {
            if (Instance != null) return Instance;
            if (FindFirstObjectByType<DesktopDemoController>() != null)
                throw new InvalidOperationException("Start Quest simulation before installing a desktop controller.");
            var go = new GameObject("VITAL Quest look - simulated XR devices");
            var simulation = go.AddComponent<QuestLookSimulation>();
            try { simulation.Initialize(); }
            catch { Destroy(go); throw; }
            return simulation;
        }

        void Initialize()
        {
            Origin = FindFirstObjectByType<XROrigin>();
            if (Origin == null || Origin.Camera == null) throw new InvalidOperationException("Quest look requires the authored active XROrigin and its camera.");
            Instance = this;
            View = Origin.Camera;
            previousQuality = QualitySettings.GetQualityLevel();
            int quality = Array.IndexOf(QualitySettings.names, QualityName);
            if (quality < 0) throw new InvalidOperationException("Android quality level '" + QualityName + "' is missing.");
            QualitySettings.SetQualityLevel(quality, true);
            var pipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null || pipeline.name != "QuestURP") throw new InvalidOperationException("Android quality must reference QuestURP.");
            var cameraData = View.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None; // MSAA is owned by the actual QuestURP asset.
            View.allowHDR = false;
            View.allowDynamicResolution = false;
            View.fieldOfView = FieldOfView;
            View.aspect = (float)CaptureWidth / CaptureHeight;
            if (!Application.isEditor) Screen.SetResolution(CaptureWidth, CaptureHeight, FullScreenMode.Windowed);
            spawnPosition = Origin.transform.position;
            spawnRotation = Origin.transform.rotation;
            var args = System.Environment.GetCommandLineArgs();
            ManualControlsEnabled = !Application.isBatchMode && !args.Contains("-vital-patient-roster-smoke") && !args.Contains("-vital-xr-walkthrough");
#if ENABLE_VR || UNITY_GAMECORE
            SimulatedInputLayoutLoader.Initialize();
            hmd = InputSystem.AddDevice<XRSimulatedHMD>();
            left = InputSystem.AddDevice<XRSimulatedController>();
            right = InputSystem.AddDevice<XRSimulatedController>();
            InputSystem.SetDeviceUsage(left, UnityEngine.InputSystem.CommonUsages.LeftHand);
            InputSystem.SetDeviceUsage(right, UnityEngine.InputSystem.CommonUsages.RightHand);
            DeviceCount = 3;
            headState.Reset(); leftState.Reset(); rightState.Reset();
            initialized = true;
            ResetPose();
            Debug.Log("QUEST_LOOK_SIMULATION: QuestURP, " + CaptureWidth + "x" + CaptureHeight + ", FOV " + FieldOfView + "; simulated Input System HMD/controllers; no headset/performance claim.");
#else
            throw new InvalidOperationException("Quest look requires Unity XR support (ENABLE_VR).");
#endif
        }

        public void ResetPose()
        {
            Origin.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            SetHeadPose(spawnPosition + Vector3.up * 1.62f, spawnRotation);
        }

        public void FocusPatient(Vector3 chest)
        {
            var position = new Vector3(chest.x + .9f, Mathf.Max(chest.y + .85f, 1.62f), chest.z - 1.15f);
            SetHeadPose(position, Quaternion.LookRotation(chest - position, Vector3.up));
        }

        public void SetHeadPose(Vector3 worldPosition, Quaternion worldRotation, bool tracked = true)
        {
#if ENABLE_VR || UNITY_GAMECORE
            headState.devicePosition = headState.centerEyePosition = TrackingSpace.InverseTransformPoint(worldPosition);
            headState.deviceRotation = headState.centerEyeRotation = Quaternion.Inverse(TrackingSpace.rotation) * worldRotation;
            headState.leftEyePosition = headState.centerEyePosition + headState.centerEyeRotation * Vector3.left * .032f;
            headState.rightEyePosition = headState.centerEyePosition + headState.centerEyeRotation * Vector3.right * .032f;
            headState.leftEyeRotation = headState.rightEyeRotation = headState.centerEyeRotation;
            headState.isTracked = tracked;
            headState.trackingState = tracked ? (int)(InputTrackingState.Position | InputTrackingState.Rotation) : 0;
            InputSystem.QueueStateEvent(hmd, headState);
            var upright = Quaternion.Euler(0, worldRotation.eulerAngles.y, 0);
            SetControllerPose(XRNode.LeftHand, worldPosition + upright * new Vector3(-.24f, -.38f, .35f), worldRotation);
            SetControllerPose(XRNode.RightHand, worldPosition + upright * new Vector3(.24f, -.38f, .35f), worldRotation);
#endif
        }

        public void SetControllerPose(XRNode node, Vector3 worldPosition, Quaternion worldRotation, bool tracked = true)
        {
#if ENABLE_VR || UNITY_GAMECORE
            var state = ControllerState(node);
            state.devicePosition = TrackingSpace.InverseTransformPoint(worldPosition);
            state.deviceRotation = Quaternion.Inverse(TrackingSpace.rotation) * worldRotation;
            state.isTracked = tracked;
            state.trackingState = tracked ? (int)(InputTrackingState.Position | InputTrackingState.Rotation) : 0;
            if (!tracked) { state.buttons = 0; state.grip = state.trigger = 0; state.primary2DAxis = Vector2.zero; }
            StoreControllerState(node, state);
#endif
        }

        public void SetTrigger(XRNode node, bool pressed)
        {
#if ENABLE_VR || UNITY_GAMECORE
            var state = ControllerState(node); state.trigger = pressed && state.isTracked ? 1 : 0;
            StoreControllerState(node, state.WithButton(ControllerButton.TriggerButton, state.trigger > .5f));
#endif
        }

        public void SetGrip(XRNode node, bool pressed)
        {
#if ENABLE_VR || UNITY_GAMECORE
            var state = ControllerState(node); state.grip = pressed && state.isTracked ? 1 : 0;
            StoreControllerState(node, state.WithButton(ControllerButton.GripButton, state.grip > .5f));
#endif
        }

        public void SetSecondaryButton(XRNode node, bool pressed)
        {
#if ENABLE_VR || UNITY_GAMECORE
            var state = ControllerState(node);
            StoreControllerState(node, state.WithButton(ControllerButton.SecondaryButton, pressed && state.isTracked));
#endif
        }

        public void SetPrimaryAxis(XRNode node, Vector2 value)
        {
#if ENABLE_VR || UNITY_GAMECORE
            var state = ControllerState(node); state.primary2DAxis = state.isTracked ? Vector2.ClampMagnitude(value, 1) : Vector2.zero;
            StoreControllerState(node, state);
#endif
        }

#if ENABLE_VR || UNITY_GAMECORE
        XRSimulatedControllerState ControllerState(XRNode node)
        {
            if (node != XRNode.LeftHand && node != XRNode.RightHand) throw new ArgumentOutOfRangeException(nameof(node));
            return node == XRNode.LeftHand ? leftState : rightState;
        }
        void StoreControllerState(XRNode node, XRSimulatedControllerState state)
        {
            if (node == XRNode.LeftHand) leftState = state; else rightState = state;
            InputSystem.QueueStateEvent(node == XRNode.LeftHand ? left : right, state);
        }
#endif

        // Legacy UnityEngine.XR polling cannot see Input System simulated devices. These readers expose the
        // committed device state only while this explicit mode is active; real headsets retain their native path.
        public static bool TryGetControllerPose(XRNode node, out Vector3 position, out Quaternion rotation, out bool tracked)
        {
            position = default; rotation = Quaternion.identity; tracked = false;
#if ENABLE_VR || UNITY_GAMECORE
            if (Instance == null || !Instance.initialized || (node != XRNode.LeftHand && node != XRNode.RightHand)) return false;
            var device = node == XRNode.LeftHand ? Instance.left : Instance.right;
            tracked = device.isTracked.isPressed;
            position = Instance.TrackingSpace.TransformPoint(device.devicePosition.ReadValue());
            rotation = Instance.TrackingSpace.rotation * device.deviceRotation.ReadValue();
            return true;
#else
            return false;
#endif
        }

        public static bool TryGetSecondaryButton(XRNode node, out bool pressed)
        {
            pressed = false;
#if ENABLE_VR || UNITY_GAMECORE
            if (Instance == null || !Instance.initialized || (node != XRNode.LeftHand && node != XRNode.RightHand)) return false;
            var device = node == XRNode.LeftHand ? Instance.left : Instance.right;
            pressed = device.isTracked.isPressed && device.secondaryButton.isPressed;
            return true;
#else
            return false;
#endif
        }

        public static bool TryGetGripTrigger(XRNode node, out float grip, out float trigger)
        {
            grip = trigger = 0;
#if ENABLE_VR || UNITY_GAMECORE
            if (Instance == null || !Instance.initialized || (node != XRNode.LeftHand && node != XRNode.RightHand)) return false;
            var device = node == XRNode.LeftHand ? Instance.left : Instance.right;
            if (device.isTracked.isPressed) { grip = device.grip.ReadValue(); trigger = device.trigger.ReadValue(); }
            return true;
#else
            return false;
#endif
        }

        void Update()
        {
            if (!initialized || !ManualControlsEnabled || Keyboard.current == null || Mouse.current == null) return;
            var keys = Keyboard.current; var mouse = Mouse.current;
            var selected = keys.leftShiftKey.isPressed ? XRNode.LeftHand : XRNode.RightHand;
            if (mouse.rightButton.isPressed)
            {
                var angles = View.transform.eulerAngles;
                float pitch = Mathf.DeltaAngle(0, angles.x) - mouse.delta.ReadValue().y * .09f;
                SetHeadPose(View.transform.position, Quaternion.Euler(Mathf.Clamp(pitch, -85, 85), angles.y + mouse.delta.ReadValue().x * .09f, 0));
            }
            else
            {
                var ray = View.ScreenPointToRay(mouse.position.ReadValue());
                var position = View.transform.position + View.transform.rotation * new Vector3(selected == XRNode.LeftHand ? -.2f : .2f, -.25f, .25f);
                SetControllerPose(selected, position, Quaternion.LookRotation(ray.GetPoint(2) - position, Vector3.up));
            }
            SetTrigger(selected, mouse.leftButton.isPressed);
            SetGrip(selected, keys.gKey.isPressed);
            SetSecondaryButton(XRNode.RightHand, keys.bKey.isPressed);
            SetSecondaryButton(XRNode.LeftHand, keys.yKey.isPressed);
            SetPrimaryAxis(XRNode.LeftHand, new Vector2((keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0), (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0)));
            SetPrimaryAxis(XRNode.RightHand, new Vector2((keys.eKey.isPressed ? 1 : 0) - (keys.qKey.isPressed ? 1 : 0), 0));
        }

        void OnDestroy()
        {
#if ENABLE_VR || UNITY_GAMECORE
            if (hmd != null && hmd.added) InputSystem.RemoveDevice(hmd);
            if (left != null && left.added) InputSystem.RemoveDevice(left);
            if (right != null && right.added) InputSystem.RemoveDevice(right);
#endif
            DeviceCount = 0;
            if (Instance == this)
            {
                Instance = null;
                if (initialized) QualitySettings.SetQualityLevel(previousQuality, true);
            }
        }
    }
}
