using System.Collections;
using EmergencyVR.Desktop;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace EmergencyVR.XR
{
    /// <summary>
    /// Lets the doctor walk freely and smoothly on Quest (release 0.1.1 could only teleport between four
    /// hidden pads): left stick walks continuously at a comfortable pace, right stick snap-turns 30 degrees.
    /// Teleporting is disabled.
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
            Applied = false; FallbackMoveActive = false; FallbackTurnActive = false;
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
            rig = origin;
        }

        // The patient's body and the instruments are not walls: walking up to kneel beside the patient must
        // never leave the learner blocked against an invisible interaction collider.
        CharacterController body;
        XROrigin rig;

        // ---------------- Safety net: the thumbsticks always move and turn the learner ----------------
        // Release 0.1.1 could not move on Quest. The XRI wiring above should fix that, but it has not been
        // checked on a headset yet. If the left stick is pushed and the rig does not move at all (and is not
        // simply pressed against a wall), or the right stick is pushed sideways and the view does not turn,
        // this component reads the controllers directly and moves / snap-turns the rig itself from then on.
        public static bool FallbackMoveActive { get; private set; }
        public static bool FallbackTurnActive { get; private set; }
        EmergencyVR.UI.TrainingExperience flow;
        float moveProbe, turnProbe, neutralYaw, flowLookupAt;
        Vector3 probeStart;
        bool turnLatched;

        void LateUpdate()
        {
            if (rig == null || rig.Camera == null || QuestLookSimulation.Enabled) return;
            if (Time.unscaledTime >= flowLookupAt) { flow = FindFirstObjectByType<EmergencyVR.UI.TrainingExperience>(); flowLookupAt = Time.unscaledTime + 1; }
            if (flow != null && flow.BlocksWorldInput) { moveProbe = turnProbe = 0; return; }
            var left = Stick(XRNode.LeftHand); var right = Stick(XRNode.RightHand);
            var t = rig.transform; float dt = Mathf.Min(Time.deltaTime, .05f);

            if (FallbackMoveActive) FallbackMove(left, dt);
            else if (left.magnitude > .45f)
            {
                if (moveProbe <= 0) probeStart = t.position;
                moveProbe += dt;
                if (moveProbe > .6f)
                {
                    var moved = t.position - probeStart; moved.y = 0;
                    bool againstWall = body != null && (body.collisionFlags & CollisionFlags.Sides) != 0;
                    if (moved.magnitude < .03f && !againstWall) { FallbackMoveActive = true; Debug.LogWarning("VITAL VR: stick locomotion fallback enabled."); }
                    moveProbe = 0;
                }
            }
            else moveProbe = 0;

            // Snap turn happens the instant the stick crosses its threshold, so compare with the heading the
            // learner had while the stick was still centred, never with the heading after the push.
            if (Mathf.Abs(right.x) < .3f) neutralYaw = t.eulerAngles.y;
            if (FallbackTurnActive) FallbackTurn(right.x);
            else if (Mathf.Abs(right.x) > .75f && Mathf.Abs(right.y) < .5f)
            {
                turnProbe += dt;
                if (turnProbe > .7f)
                {
                    if (Mathf.Abs(Mathf.DeltaAngle(neutralYaw, t.eulerAngles.y)) < 1f) { FallbackTurnActive = true; Debug.LogWarning("VITAL VR: snap turn fallback enabled."); }
                    turnProbe = -999; // decide once per push
                }
            }
            else turnProbe = 0;
        }

        void FallbackMove(Vector2 stick, float dt)
        {
            if (stick.magnitude < .2f) { if (body != null && body.enabled) body.SimpleMove(Vector3.zero); return; }
            var head = rig.Camera.transform;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(head.right, Vector3.up).normalized;
            var velocity = (forward * stick.y + right * stick.x) * WalkSpeed * Mathf.Clamp01(stick.magnitude);
            if (body != null && body.enabled) body.SimpleMove(velocity);
            else rig.transform.position += velocity * dt;
        }

        void FallbackTurn(float x)
        {
            if (Mathf.Abs(x) < .3f) { turnLatched = false; return; }
            if (turnLatched || Mathf.Abs(x) < .75f) return;
            turnLatched = true;
            rig.transform.RotateAround(rig.Camera.transform.position, Vector3.up, 30f * Mathf.Sign(x));
        }

        static Vector2 Stick(XRNode node)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            return device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out var value) ? value : Vector2.zero;
        }
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
            WalkableGround.Ensure();
            DisableTeleport(origin);
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

        /// <summary>
        /// Movement is continuous only (teleporting was judged jarring): the right stick keeps snap turn, but its
        /// teleport ray can no longer select any destination and is never drawn. The sample's own references stay
        /// intact (no nulls), so its input wiring keeps working.
        /// </summary>
        static void DisableTeleport(XROrigin origin)
        {
            foreach (var behaviour in origin.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().Name != "ControllerInputActionManager") continue;
                var field = behaviour.GetType().GetField("m_TeleportInteractor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (!(field?.GetValue(behaviour) is UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor ray) || ray == null) continue;
                ray.interactionLayers = 0;
                foreach (var visual in ray.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>(true)) visual.enabled = false;
                foreach (var line in ray.GetComponentsInChildren<LineRenderer>(true)) line.enabled = false;
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
