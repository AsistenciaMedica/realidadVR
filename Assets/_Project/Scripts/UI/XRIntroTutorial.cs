using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using EmergencyVR.Medical.Interaction;

namespace EmergencyVR.UI
{
    public enum XRIntroStep { Point, Grab, Teleport, Recenter, Complete }

    /// <summary>Control familiarization only. Evidence comes from actual UI, grab and locomotion events.</summary>
    public sealed class XRIntroTutorial : MonoBehaviour
    {
        public const float DurationSeconds = 30;
        public XRIntroStep Step { get; private set; }
        public float RemainingSeconds => Mathf.Max(0, DurationSeconds - (Time.unscaledTime - started));
        public bool Pointed { get; private set; }
        public bool Grabbed { get; private set; }
        public bool Teleported { get; private set; }
        public bool Recentered { get; private set; }
        public bool Skipped { get; private set; }
        public bool TimedOut { get; private set; }
        public XRGrabInteractable PracticeObject { get; private set; }
        public Transform TeleportTarget { get; private set; }
        public event Action Changed;
        public event Action Finished;
        XROrigin origin;
        TeleportationProvider teleport;
        GameObject props;
        Material material, standMaterial;
        Vector3 teleportStart;
        float started;
        bool ready, held;

        public void Initialize(XROrigin rig, Camera view)
        {
            if (rig == null || view == null) throw new ArgumentNullException(nameof(rig));
            origin = rig;
            teleport = rig.GetComponentInChildren<TeleportationProvider>(true);
            if (teleport == null) throw new InvalidOperationException("The tutorial requires the authored teleportation provider.");
            started = Time.unscaledTime;
            var forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(.1f, .75f, .65f));
            standMaterial = new Material(material); standMaterial.SetColor("_BaseColor", new Color(.05f, .12f, .16f));
            props = new GameObject("VITAL control tutorial objects"); props.transform.SetParent(transform, false);
            var surface = view.transform.position + forward * .55f - right * .43f - Vector3.up * .45f;
            var stand = Cube("Tutorial practice stand", surface - Vector3.up * .05f, new Vector3(.4f, .055f, .32f), standMaterial);
            var cube = Cube("Tutorial grip object", surface + Vector3.up * .06f, Vector3.one * .11f, material);
            var body = cube.AddComponent<Rigidbody>(); body.mass = .15f; body.isKinematic = true; body.useGravity = false;
            PracticeObject = cube.AddComponent<XRGrabInteractable>();
            PracticeObject.throwOnDetach = false; PracticeObject.movementType = XRBaseInteractable.MovementType.Kinematic;
            PracticeObject.selectEntered.AddListener(OnGrab);
            PracticeObject.selectExited.AddListener(OnRelease);
            var floor = origin.transform.position.y;
            var destination = new Vector3(view.transform.position.x, floor + .025f, view.transform.position.z) + forward * 1.45f + right * .7f;
            var pad = Cube("Tutorial teleport destination", destination, new Vector3(.8f, .035f, .8f), material);
            var area = pad.AddComponent<TeleportationArea>();
            area.interactionLayers = unchecked((int)0x80000000); // Authored Starter Assets teleport interaction layer.
            area.teleportationProvider = teleport;
            area.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
            TeleportTarget = pad.transform;
            teleport.locomotionStarted += TeleportStarted;
            teleport.locomotionEnded += TeleportEnded;
            ready = true; Changed?.Invoke();
        }

        GameObject Cube(string name, Vector3 position, Vector3 scale, Material surface)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(props.transform, true); go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = surface;
            return go;
        }

        public void ConfirmPointing()
        {
            if (!ready || Step != XRIntroStep.Point) return;
            Pointed = true; Step = XRIntroStep.Grab; MedicalHaptics.Pulse(.1f, .025f); Changed?.Invoke();
        }

        void OnGrab(SelectEnterEventArgs args)
        {
            if (!ready || Step != XRIntroStep.Grab) return;
            held = true; MedicalHaptics.Pulse(.1f, .035f);
        }

        void OnRelease(SelectExitEventArgs args)
        {
            if (!ready || Step != XRIntroStep.Grab || !held || args.isCanceled) return;
            Grabbed = true; held = false; Step = XRIntroStep.Teleport; MedicalHaptics.Pulse(.08f, .025f); Changed?.Invoke();
        }

        void TeleportStarted(LocomotionProvider source) { teleportStart = origin.transform.position; }
        void TeleportEnded(LocomotionProvider source)
        {
            if (!ready || Step != XRIntroStep.Teleport || Vector3.Distance(teleportStart, origin.transform.position) < .25f) return;
            Teleported = true; Step = XRIntroStep.Recenter; Changed?.Invoke();
        }

        public void ConfirmRecenter()
        {
            if (!ready || Step != XRIntroStep.Recenter) return;
            Recentered = true; Finish(false, false);
        }

        public void Skip() => Finish(true, false);
        void Update() { if (ready && RemainingSeconds <= 0) Finish(false, true); }

        void Finish(bool skipped, bool timeout)
        {
            if (!ready) return;
            Skipped = skipped; TimedOut = timeout; Step = XRIntroStep.Complete; ready = false;
            // Timeout/skip never invent completion of controls the learner did not demonstrate.
            if (props != null) props.SetActive(false);
            Changed?.Invoke(); Finished?.Invoke();
        }

        void OnDestroy()
        {
            if (teleport != null) { teleport.locomotionStarted -= TeleportStarted; teleport.locomotionEnded -= TeleportEnded; }
            if (props != null) Destroy(props);
            if (material != null) Destroy(material);
            if (standMaterial != null) Destroy(standMaterial);
        }
    }
}
