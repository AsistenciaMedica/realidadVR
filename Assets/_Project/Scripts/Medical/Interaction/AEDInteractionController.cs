using UnityEngine;

namespace EmergencyVR.Medical.Interaction
{
    public sealed class AEDInteractionController : MonoBehaviour
    {
        MedicalProcedureRig rig;
        MedicalPhysicalTool rightPad, leftPad;
        LineRenderer rightCable, leftCable;
        public AEDProcedureState State { get; private set; } = new AEDProcedureState();
        public Transform Device, Lid;
        public TextMesh Display;
        float analysisStarted;
        bool contactWarning;
        bool Running => rig != null && rig.Manager != null && rig.Manager.IsRunning && rig.Manager.MedicalSession != null;
        bool Touching => rig.CPR != null && rig.CPR.Touching;

        public void Initialize(MedicalProcedureRig rig) { this.rig = rig; }
        public void RegisterPad(MedicalPhysicalTool pad, bool right)
        {
            if (right) { rightPad = pad; rightCable = Cable(); }
            else { leftPad = pad; leftCable = Cable(); }
        }
        LineRenderer Cable()
        {
            var go = new GameObject("AED electrode cable");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true; line.positionCount = 3;
            line.startWidth = line.endWidth = .0025f; line.sharedMaterial = rig.GloveMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }
        public void ResetAttempt()
        {
            State = new AEDProcedureState(); analysisStarted = 0; contactWarning = false;
            SetDisplay("DEA\nABRIR / ENCENDER");
            if (Lid != null) Lid.localRotation = Quaternion.identity;
        }
        public void Grabbed()
        {
            if (!Running) return;
            rig.SubmitNext("BringAED");
            rig.Hint = "DEA: Q para abrir y encender. En VR, activar con el objeto cogido.";
        }
        public void Use()
        {
            if (!Running) return;
            // Retrieval may have occurred before its configured prerequisite; carrying the actual device remains required.
            rig.SubmitNext("BringAED");
            if (State.Phase == AEDPhase.Closed)
            {
                State.Open(); rig.Audio.Pulse(.04f, 650); SetDisplay("ABIERTO\nENCENDER"); return;
            }
            if (State.Phase == AEDPhase.Open)
            {
                State.PowerOn(); rig.Audio.Pulse(.12f, 850); SetDisplay("RETIRAR RESPALDO\nCOLOCAR PARCHES"); return;
            }
            if (State.Phase == AEDPhase.Powered)
            {
                rig.Hint = "Coge cada parche, retira el respaldo con Q y colócalo en su zona."; return;
            }
            if (State.Phase == AEDPhase.PadsReady || State.Phase == AEDPhase.ContinueCPR)
            {
                if (Touching) { RejectContact("NO TOCAR\nAL PACIENTE"); return; }
                // Physical placement persists when performed early; register it once its clinical prerequisites are met.
                if (State.RightPad && State.LeftPad) rig.SubmitNext("AttachAEDPads");
                if (!rig.SubmitNext("AnalyzeRhythm"))
                {
                    rig.Hint = "Revisar los pasos y la espera del caso antes del siguiente análisis."; return;
                }
                if (State.BeginAnalysis(Touching))
                {
                    analysisStarted = Time.time; SetDisplay("ANALIZANDO\nNO TOCAR");
                    rig.Audio.Pulse(.08f, 650); contactWarning = false;
                }
                return;
            }
            if (State.Phase == AEDPhase.ShockAdvised)
            {
                if (Touching) { RejectContact("ALEJARSE\nANTES DE DESCARGAR"); return; }
                // Capture the pre-action rhythm: a correctly accepted shock may change the clinical state immediately.
                bool shockable = rig.Manager.MedicalSession.Patient.shockAdvised;
                if (!shockable) { SetDisplay("DESCARGA BLOQUEADA\nREVISAR ESTADO"); return; }
                if (rig.SubmitNext("DeliverAEDShock") && State.Shock(Touching, shockable))
                {
                    rig.Visuals.TriggerShockReaction(); rig.Audio.Pulse(.12f, 450); MedicalHaptics.Pulse(.25f, .06f);
                    SetDisplay("DESCARGA SIMULADA\nREANUDAR RCP");
                }
            }
        }
        public bool Peel(bool right)
        {
            if (!Running) return false;
            bool ok = State.Peel(right);
            if (ok) { rig.Audio.Pulse(.02f, 1000); rig.Hint = "Parche preparado. Colocar siguiendo el dibujo del DEA."; }
            else rig.Hint = "Abrir y encender el DEA antes de sacar parches.";
            return ok;
        }
        public bool Place(MedicalPhysicalTool pad, bool right)
        {
            if (!Running || pad == null || rig.Visuals == null) return false;
            var target = right ? rig.Visuals.AedRightPadAnchor : rig.Visuals.AedLeftPadAnchor;
            if (target == null) return false;
            bool close = Vector3.Distance(pad.transform.position, target.position) <= rig.Settings.padSnapDistance;
            bool aligned = Vector3.Angle(pad.transform.up, target.up) <= rig.Settings.padSnapAngle;
            if (!State.PlacePad(right, close, aligned))
            {
                if (close) rig.Hint = "Comprueba respaldo retirado y orientación del parche.";
                return false;
            }
            pad.AttachTo(target); MedicalHaptics.Pulse(.13f, .04f); rig.Audio.Pulse(.04f, 700);
            if (State.RightPad && State.LeftPad)
            {
                rig.SubmitNext("AttachAEDPads"); SetDisplay("PARCHES CONECTADOS\nINICIAR ANÁLISIS");
            }
            return true;
        }
        void Update()
        {
            if (Device == null) return;
            bool visible = Device.gameObject.activeInHierarchy;
            if (rightCable != null) rightCable.enabled = visible;
            if (leftCable != null) leftCable.enabled = visible;
            if (!visible) return;
            if (Lid != null)
                Lid.localRotation = Quaternion.Slerp(Lid.localRotation, Quaternion.Euler(State.Phase == AEDPhase.Closed ? 0 : -95, 0, 0), Time.deltaTime * 5);
            Draw(rightCable, rightPad); Draw(leftCable, leftPad);
            if (!Running || State.Phase != AEDPhase.Analysing) return;
            if (Touching)
            {
                analysisStarted = Time.time;
                if (!contactWarning) { rig.UnsafeAttempts++; contactWarning = true; }
                SetDisplay("CONTACTO DETECTADO\nRETIRAR MANOS");
                return;
            }
            SetDisplay("ANALIZANDO\nNO TOCAR");
            if (Time.time - analysisStarted < rig.Settings.readingDelaySeconds ||
                !State.FinishAnalysis(rig.Manager.MedicalSession.Patient.shockAdvised, Touching)) return;
            bool shock = State.Phase == AEDPhase.ShockAdvised;
            SetDisplay(shock ? "DESCARGA INDICADA\nNADIE TOCA: PULSAR" : "NO DESCARGAR\nREANUDAR RCP");
            if (!shock) rig.SubmitNext("FollowNoShock");
            rig.Audio.Pulse(.12f, shock ? 1000 : 650);
        }
        void RejectContact(string message)
        {
            rig.UnsafeAttempts++; SetDisplay(message); rig.Hint = "Contacto detectado: retirar las manos antes de utilizar el DEA.";
        }
        void SetDisplay(string text) { if (Display != null) Display.text = text; }
        void Draw(LineRenderer line, MedicalPhysicalTool pad)
        {
            if (line == null || pad == null) return;
            var start = Device.position + Device.up * .08f; var end = pad.transform.position;
            line.SetPosition(0, start); line.SetPosition(1, (start + end) * .5f + Vector3.up * .025f); line.SetPosition(2, end);
        }
    }
}

