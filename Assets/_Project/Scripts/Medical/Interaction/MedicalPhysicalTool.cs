using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace EmergencyVR.Medical.Interaction
{
    public enum MedicalToolKind { AED, RightPad, LeftPad, BloodPressure, Oximeter, Glucose, Phone, AutoInjector, Bandage }

    public sealed class MedicalPhysicalTool : MonoBehaviour
    {
        MedicalProcedureRig rig;
        Rigidbody body;
        XRGrabInteractable grab;
        Transform originalParent;
        Vector3 originalPosition;
        Quaternion originalRotation;
        bool attached, prepared, reading, resetting;
        XRNode? feedbackHand;
        public MedicalToolKind Kind { get; private set; }
        public TextMesh Display;
        public bool IsAttached => attached;
        public bool CanBeGrabbed => !resetting && rig!=null && rig.AllowsEquipment(Kind) && (!attached || !IsPad);
        bool IsPad => Kind == MedicalToolKind.RightPad || Kind == MedicalToolKind.LeftPad;

        public void Initialize(MedicalProcedureRig rig, MedicalToolKind kind)
        {
            this.rig = rig; Kind = kind;
            body = GetComponent<Rigidbody>(); grab = GetComponent<XRGrabInteractable>();
            originalParent = transform.parent; originalPosition = transform.localPosition; originalRotation = transform.localRotation;
            grab.selectEntered.AddListener(args =>
            {
                feedbackHand = args.interactorObject.handedness == InteractorHandedness.Left ? XRNode.LeftHand :
                    args.interactorObject.handedness == InteractorHandedness.Right ? XRNode.RightHand : (XRNode?)null;
                OnGrabbed();
            });
            grab.selectExited.AddListener(_ => OnReleased());
            grab.activated.AddListener(_ => Use());
            body.isKinematic = true; body.useGravity = false;
        }

        public void OnGrabbed()
        {
            if (!CanBeGrabbed) return;
            // Measurements can be removed and acquired again; attached AED pads remain connected for the attempt.
            StopAllCoroutines(); reading = false;
            if (attached)
            {
                attached = false; prepared = false;
                transform.SetParent(originalParent, true);
                if (Display != null) Display.text = "--";
            }
            body.isKinematic = true; body.useGravity = false;
            FeedbackPulse(.1f, .03f, "Grab");
            if (Kind == MedicalToolKind.AED) rig.AED.Grabbed();
        }

        public void Use()
        {
            if (resetting || rig.Manager == null || !rig.Manager.AcceptsInput || rig.Manager.MedicalSession == null || !rig.AllowsEquipment(Kind)) return;
            if (Kind == MedicalToolKind.AED) { rig.AED.Use(); return; }
            if (IsPad)
            {
                if (attached) return;
                prepared = rig.AED.Peel(Kind == MedicalToolKind.RightPad);
                var backing = transform.Find("Peel backing");
                if (backing != null && prepared) backing.gameObject.SetActive(false);
                return;
            }
            if (Kind == MedicalToolKind.Phone)
            {
                if(rig.Manager.MedicalSession.Capabilities.usesObservedPatientData)
                {
                    var help=rig.Review.GetComponent<EmergencyVR.Dialogue.ClinicalHelpController>();
                    if(help!=null&&help.RequestCall(false))
                    {
                        if(Display!=null) Display.text="112\nSIMULACIÓN";
                        rig.Hint="Llamada simulada. Comunica la ubicación y lo observado desde el panel de ayuda.";
                    }
                    return;
                }
                if (rig.SubmitNext("CallEmergencyServices"))
                {
                    if (Display != null) Display.text = "AYUDA\nSOLICITADA";
                    rig.Audio.Pulse(.08f, 600);
                    rig.Hint = "Llamada simulada confirmada. Testigo disponible para dar información.";
                }
                return;
            }
            if (attached || reading) return;
            if (Kind == MedicalToolKind.Glucose)
            {
                prepared = true; if (Display != null) Display.text = "TIRA\nPREPARADA";
                rig.Hint = "Acercar tira de entrenamiento al dedo; soltar para completar adquisición.";
                return;
            }
            if (Kind == MedicalToolKind.Oximeter)
            {
                prepared = true;
                var screen = transform.Find("Screen");
                if (screen != null) screen.localRotation = Quaternion.Euler(0, 0, 15);
                rig.Hint = "Clip abierto: colocar en el dedo.";
                return;
            }
            prepared = true; rig.Hint = "Instrumento preparado: acercar a la zona y soltar.";
        }

        public bool OnReleased()
        {
            if (resetting) return false;
            FeedbackPulse(.07f, .02f, "Release");
            if(!rig.AllowsEquipment(Kind)) { Drop();return false; }
            if (attached) return true;
            if (IsPad)
            {
                if (rig.AED.Place(this, Kind == MedicalToolKind.RightPad)) return true;
                Drop(); return false;
            }
            if (rig.Manager == null || !rig.Manager.AcceptsInput || rig.Manager.MedicalSession == null ||
                Kind == MedicalToolKind.Phone || Kind == MedicalToolKind.AED)
            { Drop(); return false; }
            var target = Kind == MedicalToolKind.BloodPressure ? rig.Visuals.UpperArmAnchor :
                Kind == MedicalToolKind.AutoInjector ? rig.Visuals.ThighAnchor :
                Kind == MedicalToolKind.Bandage ? rig.Visuals.WoundAnchor : rig.Visuals.FingerAnchor;
            if (!prepared || target == null || Vector3.Distance(transform.position, target.position) > .14f)
            { Drop(); return false; }
            AttachTo(target);
            if (!reading) StartCoroutine(Read());
            return true;
        }

        void Drop()
        {
            transform.SetParent(originalParent, true);
            body.isKinematic = false; body.useGravity = true;
            // XR restores its pre-grab Rigidbody settings after selectExited; apply the resting physics afterward.
            StartCoroutine(FinishDrop());
        }
        IEnumerator FinishDrop()
        {
            yield return new WaitForEndOfFrame();
            if (body != null && !resetting && !attached && grab != null && !grab.isSelected)
            { body.isKinematic = false; body.useGravity = true; }
        }

        public void AttachTo(Transform target)
        {
            if (target == null) return;
            attached = true; body.isKinematic = true; body.useGravity = false;
            if (IsPad) grab.enabled = false;
            var screen = transform.Find("Screen");
            if (screen != null && Kind == MedicalToolKind.Oximeter) screen.localRotation = Quaternion.identity;
            StartCoroutine(Snap(target));
        }
        IEnumerator Snap(Transform target)
        {
            var start = transform.position; var rotation = transform.rotation; float t = 0;
            while (t < 1 && target != null && attached)
            {
                t += Time.deltaTime * 5;
                transform.SetPositionAndRotation(Vector3.Lerp(start, target.position, Mathf.SmoothStep(0, 1, t)),
                    Quaternion.Slerp(rotation, target.rotation, Mathf.SmoothStep(0, 1, t)));
                yield return null;
            }
            if (target != null && attached)
            {
                transform.SetParent(target, true);
                transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity;
            }
        }
        IEnumerator Read()
        {
            var attempt = rig.Manager.MedicalSession;
            reading = true; if (Display != null) Display.text = "...";
            yield return new WaitForSeconds((float)rig.Settings.readingDelaySeconds);
            while (rig.Manager.IsPaused && attached && rig.Manager.MedicalSession == attempt) yield return null;
            // A pending acquisition must never submit to a different attempt or continue after its result is frozen.
            if (!attached || !rig.Manager.IsRunning || attempt == null || rig.Manager.MedicalSession != attempt)
            { reading = false; yield break; }
            string action = Kind == MedicalToolKind.BloodPressure ? "CheckBloodPressure" :
                Kind == MedicalToolKind.Oximeter ? "CheckSpO2" :
                Kind == MedicalToolKind.Glucose ? "CheckGlucose" :
                Kind == MedicalToolKind.Bandage ? "ApplyBandage" : "UseEpinephrineAutoInjector";
            bool accepted = rig.SubmitNext(action);
            var p = attempt.Patient;
            if (Display != null)
                Display.text = !accepted ? "REVISAR" :
                    Kind == MedicalToolKind.BloodPressure ? (p.circulation == "pulseless" ? "SIN LECTURA" : $"{p.systolic:0}/{p.diastolic:0}") :
                    Kind == MedicalToolKind.Oximeter ? (p.circulation == "pulseless" ? "SIN LECTURA" : $"SpO2 {p.spo2:0}%") :
                    Kind == MedicalToolKind.Glucose ? $"{p.glucose:0.0}\nmmol/L" : "OK";
            if (accepted)
            {
                rig.RecordMeasurement(Kind, p);
                rig.Measurements++; rig.Audio.Pulse(.04f, 800); FeedbackPulse(.12f, .03f, "Measurement");
                rig.Hint = "Adquisición registrada. El instrumento puede retirarse y recolocarse para una nueva medición prevista.";
            }
            else rig.Hint = "Adquisición no registrada: revisar la secuencia del caso y repetir cuando corresponda.";
            reading = false;
        }

        public void ResetTool()
        {
            resetting = true;
            StopAllCoroutines();
            if (grab != null && grab.isSelected && grab.interactionManager != null)
                for (int i = grab.interactorsSelecting.Count - 1; i >= 0; i--)
                    grab.interactionManager.SelectExit(grab.interactorsSelecting[i], grab);
            transform.SetParent(originalParent, false);
            transform.localPosition = originalPosition; transform.localRotation = originalRotation;
            attached = false; prepared = false; reading = false;
            if (grab != null) grab.enabled = true;
            if (body != null)
            {
                body.isKinematic = false;
                body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
                body.isKinematic = true; body.useGravity = false;
            }
            var backing = transform.Find("Peel backing"); if (backing != null) backing.gameObject.SetActive(true);
            var screen = transform.Find("Screen"); if (screen != null) screen.localRotation = Quaternion.identity;
            if (Display != null) Display.text = Kind == MedicalToolKind.Phone ? "SOS" : "--";
            resetting = false;
            feedbackHand = null;
        }

        public void FeedbackPulse(float amplitude,float seconds,string reason)
        {
            if(feedbackHand.HasValue) MedicalHaptics.Pulse(feedbackHand.Value,amplitude,seconds,reason);
            else MedicalHaptics.Pulse(amplitude,seconds,reason);
        }

        // Set only when selecting an environment, after the previous attempt has returned its tools.
        // The physical object and its patient attachment targets remain the same across all storage layouts.
        public void SetStoragePose(Vector3 localPosition, Quaternion localRotation)
        {
            originalPosition = localPosition;
            originalRotation = localRotation;
            ResetTool();
        }
    }
}

