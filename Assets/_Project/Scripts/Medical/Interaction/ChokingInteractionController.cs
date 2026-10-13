using System.Linq;
using EmergencyVR.Patient.Presentation;
using UnityEngine;

namespace EmergencyVR.Medical.Interaction
{
    [DefaultExecutionOrder(120)]
    public sealed class ChokingInteractionController : MonoBehaviour
    {
        public const string BackBlows = "ChokingBackBlows", AbdominalThrusts = "ChokingAbdominalThrusts";
        const int ScriptedBlockGestures = 5;
        readonly ChokingGestureEvaluator evaluator = new ChokingGestureEvaluator();
        MedicalProcedureRig rig;
        PatientVisualController visuals;
        Transform targetGuide, supportGuide, expelledObject;
        TextMesh label;
        Renderer targetRenderer;
        MaterialPropertyBlock contactColor;
        float contactTravel;
        string gestureAction;
        bool sawObstruction;
        double expelledAt;
        Vector3 expelledFrom, expelledDirection;
        public bool WindowsEngaged { get; private set; }
        public bool HasContact { get; private set; }
        public int GesturesInBlock { get; private set; }
        public int BackBlowGestures { get; private set; }
        public int AbdominalGestures { get; private set; }
        public bool ObjectExpelled { get; private set; }
        public Vector3 LeftHandPosition { get; private set; }
        public Vector3 RightHandPosition { get; private set; }
        public bool IsChokingCase => rig?.Manager?.MedicalDefinition?.actions.Any(a => a.action == BackBlows) == true;
        public bool Obstructed => rig?.Manager?.MedicalSession?.Patient.flags.Contains("airway obstruction") == true;
        public string PendingAction => rig.NextId(BackBlows) != null ? BackBlows : rig.NextId(AbdominalThrusts) != null ? AbdominalThrusts : null;
        public bool IsBackBlow => PendingAction == BackBlows;
        public Vector3 Anterior => visuals.ChestAnchor.up;
        public Vector3 HeadDirection
        {
            get
            {
                var direction = Vector3.ProjectOnPlane(visuals.ChinAnchor.position - visuals.ChestRestPosition, Anterior);
                return direction.sqrMagnitude > .0001f ? direction.normalized : visuals.ChestAnchor.forward;
            }
        }
        public Vector3 TargetPosition => IsBackBlow
            ? visuals.ChestRestPosition - Anterior * .26f + HeadDirection * .065f
            : visuals.ChestRestPosition - HeadDirection * .20f;
        public Vector3 GestureDirection => IsBackBlow ? Anterior : (-Anterior + HeadDirection * .75f).normalized;
        public Vector3 RestPosition => TargetPosition - GestureDirection * .04f;
        public Vector3 SupportPosition => visuals.ChestRestPosition + Anterior * .025f;
        public string Instructions => IsBackBlow
            ? "Detrás del paciente: apoya una mano delante y mueve la otra hacia la zona dorsal y de vuelta."
            : "Detrás del paciente: reúne ambas manos en la zona abdominal y mueve hacia dentro y arriba; vuelve al inicio.";
        public bool CanAct
        {
            get
            {
                var manager = rig?.Manager;
                if (manager == null || !manager.AcceptsInput || !IsChokingCase || !Obstructed ||
                    manager.MedicalSession.Patient.consciousness != "Conscious") return false;
                string next = PendingAction;
                var rule = manager.MedicalDefinition.actions.FirstOrDefault(a => a.action == next);
                return rule != null && rule.prerequisites.All(manager.MedicalSession.Completed.Contains) &&
                    manager.MedicalSession.Elapsed >= manager.MedicalSession.EarliestTime(rule.id);
            }
        }

        public void Initialize(MedicalProcedureRig owner, PatientVisualController patient)
        {
            rig = owner; visuals = patient;
            targetGuide = Guide("Choking gesture target", .06f);
            targetRenderer = targetGuide.GetComponent<Renderer>();
            contactColor = new MaterialPropertyBlock();
            supportGuide = Guide("Choking supporting hand", .045f);
            expelledObject = Guide("Expelled training obstruction", .022f);
            var objectColor = new MaterialPropertyBlock(); objectColor.SetColor("_BaseColor", new Color(.65f, .27f, .07f));
            expelledObject.GetComponent<Renderer>().SetPropertyBlock(objectColor);
            label = rig.Display(gameObject, "Choking gesture instructions", Vector3.zero, .014f);
            label.alignment = TextAlignment.Center;
            gameObject.AddComponent<WindowsChokingInput>().Initialize(this);
            gameObject.AddComponent<VRChokingInput>().Initialize(this);
            ResetAttempt();
        }
        Transform Guide(string name, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name; go.transform.SetParent(transform, false); go.transform.localScale = Vector3.one * size;
            var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            go.GetComponent<Renderer>().sharedMaterial = rig.GloveMaterial;
            return go.transform;
        }
        public bool InReach(Vector3 learner) => Vector3.Distance(learner, visuals.ChestRestPosition) < 1.6f &&
            Vector3.Dot(learner - visuals.ChestRestPosition, Anterior) < -.04f;
        public void SetWindowsEngaged(bool value)
        {
            WindowsEngaged = value && CanAct;
            CancelContact();
            if (WindowsEngaged) rig.Hint = "H: salir · Arrastra desde la zona marcada hacia arriba y vuelve; suelta entre gestos. " + Instructions;
        }
        public void FeedHands(Vector3 left, Vector3 right, Vector3 learner, bool engaged)
        {
            if (!CanAct || !engaged || !InReach(learner)) { CancelContact(); return; }
            if (gestureAction != PendingAction) { CancelContact(); gestureAction = PendingAction; GesturesInBlock = 0; }
            Vector3 moving = right;
            bool valid;
            if (IsBackBlow)
            {
                bool leftMoving = (left - RestPosition).sqrMagnitude < (right - RestPosition).sqrMagnitude;
                moving = leftMoving ? left : right;
                var support = leftMoving ? right : left;
                valid = Vector3.Distance(support, SupportPosition) < .23f;
            }
            else
            {
                moving = (left + right) * .5f;
                valid = Vector3.Distance(left, right) < .17f;
            }
            var offset = moving - RestPosition;
            float travel = Vector3.Dot(offset, GestureDirection);
            valid &= Vector3.ProjectOnPlane(offset, GestureDirection).magnitude < .085f;
            if (!valid || travel < -.025f || travel > .09f) { CancelContact(); return; }
            HasContact = true; contactTravel = travel; LeftHandPosition = left; RightHandPosition = right;
            if (!evaluator.Sample(rig.Manager.SimulationClock, travel, true)) return;
            bool back = gestureAction == BackBlows;
            if (back) BackBlowGestures++; else AbdominalGestures++;
            GesturesInBlock++;
            rig.Audio.Pulse(.035f, 160); MedicalHaptics.Pulse(.15f, .035f, "ChokingGesture");
            rig.Hint = $"{(back ? "Gesto dorsal" : "Gesto abdominal")} simulado {GesturesInBlock}/{ScriptedBlockGestures}.";
            // This authored branch persists through the dorsal block and resolves on its existing timeline.
            // Stop immediately if the clinical state changes; this count does not prescribe a real treatment.
            if (GesturesInBlock >= ScriptedBlockGestures && rig.SubmitNext(gestureAction))
            {
                CancelContact(); GesturesInBlock = 0; gestureAction = null; WindowsEngaged = false;
                rig.Hint = PendingAction != null ? "Bloque registrado. " + Instructions + " H: maniobra con ratón."
                    : "Maniobra registrada. Observa al paciente y reevalúa su respiración.";
            }
        }
        public void CancelContact() { HasContact = false; evaluator.Cancel(); }
        public bool TryHandPose(int side, out Vector3 position, out Quaternion rotation)
        {
            position = side == 0 ? LeftHandPosition : RightHandPosition;
            rotation = Quaternion.LookRotation(HeadDirection, Anterior);
            return HasContact && WindowsEngaged;
        }
        public void ResetAttempt()
        {
            CancelContact(); WindowsEngaged = false; gestureAction = null; GesturesInBlock = 0;
            BackBlowGestures = AbdominalGestures = 0; ObjectExpelled = sawObstruction = false;
            if (expelledObject != null) expelledObject.gameObject.SetActive(false);
            if (targetGuide != null) targetGuide.gameObject.SetActive(false);
            if (supportGuide != null) supportGuide.gameObject.SetActive(false);
            if (label != null) label.gameObject.SetActive(false);
        }
        void LateUpdate()
        {
            if (rig == null) return;
            if (!CanAct) { CancelContact(); WindowsEngaged = false; }
            bool show = CanAct && rig.TrainingMode;
            targetGuide.gameObject.SetActive(show); supportGuide.gameObject.SetActive(show); label.gameObject.SetActive(show);
            if (show)
            {
                targetGuide.position = RestPosition;
                supportGuide.position = IsBackBlow ? SupportPosition : RestPosition + visuals.ChestAnchor.right * .05f;
                label.transform.position = visuals.ChestRestPosition + HeadDirection * .40f - Anterior * .24f;
                if (Camera.main != null) label.transform.rotation = Camera.main.transform.rotation;
                contactColor.SetColor("_BaseColor", HasContact ? new Color(.16f, .8f, .55f) : new Color(1f, .68f, .18f));
                targetRenderer.SetPropertyBlock(contactColor);
                string control = HasContact
                    ? (contactTravel > .025f ? "Contacto activo · vuelve al inicio" : "Contacto activo · mueve y vuelve")
                    : rig.IsWindows
                        ? (WindowsEngaged ? "Pulsa sobre la marca y mantén para arrastrar" : "H: activar · arrastra arriba y vuelve")
                        : "Coloca ambas manos · mantén ambos grips";
                label.text = (IsBackBlow ? "ZONA DORSAL" : "ZONA ABDOMINAL") + $" · {GesturesInBlock}/{ScriptedBlockGestures}\n" +
                    control;
            }
            if (!rig.Manager.AcceptsInput || !IsChokingCase) return;
            if (Obstructed) sawObstruction = true;
            if (sawObstruction && !Obstructed && !ObjectExpelled && rig.Manager.MedicalSession.Completed.Any(id =>
                rig.Manager.MedicalDefinition.actions.Any(a => a.id == id && a.action == AbdominalThrusts)))
            {
                ObjectExpelled = true; expelledAt = rig.Manager.SimulationClock;
                expelledFrom = visuals.ChinAnchor.position; expelledDirection = Anterior;
                expelledObject.gameObject.SetActive(true);
                visuals.Rig.GetComponent<ArticulatedPatient>()?.React("EncourageCough");
                rig.Hint = "Objeto expulsado en la simulación. Reevalúa respuesta y respiración; solicita revisión.";
            }
            if (ObjectExpelled)
            {
                float t = (float)(rig.Manager.SimulationClock - expelledAt);
                expelledObject.position = expelledFrom + expelledDirection * (.08f + t * .55f) + Vector3.down * t * t * .8f;
                if (t > 1.2f) expelledObject.gameObject.SetActive(false);
            }
        }
        void OnDisable() { CancelContact(); WindowsEngaged = false; }
    }
}
