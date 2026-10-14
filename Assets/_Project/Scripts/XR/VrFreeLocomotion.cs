using System.Collections;
using EmergencyVR.Desktop;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace EmergencyVR.XR
{
    /// <summary>
    /// Lets the doctor move freely on Quest instead of only jumping between four fixed pads that were
    /// authored for the old technical room (and are invisible in the training environments):
    /// left stick walks continuously at a comfortable pace, right stick keeps snap turn and teleport,
    /// and the whole floor of every environment is a valid teleport destination.
    /// Runs only when the authored XR rig is active, so the Windows build is untouched.
    /// </summary>
    public sealed class VrFreeLocomotion : MonoBehaviour
    {
        /// <summary>Comfortable walking pace for seated and standing users (m/s).</summary>
        public const float WalkSpeed = 1.1f;
        const int StarterAssetsTeleportLayer = unchecked((int)0x80000000);

        public static bool Applied { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            Applied = false;
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
        }

        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "TrainingRoom" || FindFirstObjectByType<VrFreeLocomotion>() != null) return;
            new GameObject("VITAL VR free locomotion").AddComponent<VrFreeLocomotion>();
        }

        IEnumerator Start()
        {
            // The Windows controller deactivates every XR origin in its Awake; wait one frame so this
            // only ever configures a rig that is really in use.
            yield return null;
            var origin = FindFirstObjectByType<XROrigin>();
            if (origin == null || FindFirstObjectByType<DesktopDemoController>() != null) { Destroy(gameObject); yield break; }
            Apply(origin);
            body = origin.GetComponentInChildren<CharacterController>(true);
        }

        // The patient's body and the instruments are not walls: walking up to kneel beside the patient must
        // never leave the learner blocked against an invisible interaction collider.
        CharacterController body;
        readonly System.Collections.Generic.HashSet<Collider> passThrough = new System.Collections.Generic.HashSet<Collider>();
        float passThroughAt;
        void Update()
        {
            if (body == null || Time.unscaledTime < passThroughAt) return;
            passThroughAt = Time.unscaledTime + 1;
            foreach (var patient in FindObjectsByType<EmergencyVR.Patient.PatientController>(FindObjectsSortMode.None))
                foreach (var c in patient.GetComponentsInChildren<Collider>(true)) Ignore(c);
            foreach (var tool in FindObjectsByType<EmergencyVR.Medical.Interaction.MedicalPhysicalTool>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var c in tool.GetComponentsInChildren<Collider>(true)) Ignore(c);
        }
        void Ignore(Collider c) { if (c != null && c != body && passThrough.Add(c)) Physics.IgnoreCollision(body, c, true); }

        public static void Apply(XROrigin origin)
        {
            EnableLeftStickWalking(origin);
            foreach (var move in origin.GetComponentsInChildren<ContinuousMoveProvider>(true))
            {
                move.moveSpeed = WalkSpeed;
                move.enableStrafe = true;
            }
            var ground = WalkableGround.Ensure();
            var teleport = origin.GetComponentInChildren<TeleportationProvider>(true);
            if (teleport != null && !ground.TryGetComponent<TeleportationArea>(out _))
            {
                int layers = StarterAssetsTeleportLayer;
                foreach (var pad in FindObjectsByType<TeleportationArea>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                { layers = pad.interactionLayers.value; break; }
                var area = ground.gameObject.AddComponent<TeleportationArea>();
                area.interactionLayers = layers;
                area.teleportationProvider = teleport;
                area.matchOrientation = MatchOrientation.WorldSpaceUp;
                area.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
            }
            Applied = true;
        }

        /// <summary>
        /// The Starter Assets ControllerInputActionManager lives in the sample assembly, which this runtime
        /// assembly cannot reference; its public smoothMotionEnabled property is set by reflection.
        /// Left hand walks, right hand keeps teleport + snap turn (the official XRI default layout).
        /// </summary>
        static void EnableLeftStickWalking(XROrigin origin)
        {
            foreach (var behaviour in origin.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().Name != "ControllerInputActionManager") continue;
                var property = behaviour.GetType().GetProperty("smoothMotionEnabled");
                if (property == null || !property.CanWrite) continue;
                bool left = IsLeftHand(behaviour.transform);
                property.SetValue(behaviour, left);
            }
        }

        static bool IsLeftHand(Transform node)
        {
            for (var current = node; current != null; current = current.parent)
                if (current.name.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
    }
}
