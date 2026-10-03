using System;
using System.Collections.Generic;
using EmergencyVR.Medical;
using EmergencyVR.Scenarios;
using UnityEngine;

namespace EmergencyVR.Patient.Presentation
{
    /// <summary>CASE 01 body choreography and physical evidence. Never owns physiology.</summary>
    [DefaultExecutionOrder(110)]
    public sealed partial class Case01PatientPresentation : MonoBehaviour
    {
        [Min(2)] public float assistanceSeconds = 8;
        [Min(.1f)] public float maximumAssistanceDistance = 2.2f;
        ReviewCaseSession review;
        PatientVisualController visual;
        PatientRigAdapter rig;
        ArticulatedPatient body;
        MedicalScenarioRuntime attempt;
        PatientController patientProjection;
        PatientClinicalState Clinical => patientProjection == null ? null : patientProjection.ClinicalState;
        EmergencyVR.Dialogue.ClinicalDialogueController dialogue;
        string selectedId, attemptId, ticket;
        bool held, returning, bodyWasEnabled, busy, completed, evidenceRecorded;
        float progress, attention, discomfort = 12;
        float attentionUntil;
        double lastSupportTime;
        GameObject assembly, shirt;
        Mesh defaultMesh;
        Material[] defaultMaterials;
        Transform patient, model;
        readonly Dictionary<Transform, Pose> original = new Dictionary<Transform, Pose>();
        readonly Dictionary<Transform, Pose> neutral = new Dictionary<Transform, Pose>();
        readonly Dictionary<GameObject, bool> hidden = new Dictionary<GameObject, bool>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly Collider[] overlaps = new Collider[64];
        readonly RaycastHit[] supportHits = new RaycastHit[32];
        readonly Vector3[] previousPoints = new Vector3[8];
        readonly Vector3[] points = new Vector3[8];
        readonly Quaternion[] restingFeet = new Quaternion[2];
        Transform oldBody, oldHead;
        BoxCollider seat;
        Collider floor;
        Material benchMaterial, metalMaterial, matMaterial;
        Quaternion attentionRotation = Quaternion.identity;
        static readonly Vector3 Origin = new Vector3(.60f, 0, .48f);
        static readonly Vector3 Forward = Vector3.back;
        static readonly Vector3 Right = Vector3.left;
        public bool IsActive { get; private set; }
        public bool IsAssisting => IsActive && busy;
        public bool IsReturning => IsAssisting && returning;
        public float Progress => risePending || standing || regaining ? riseAmount : progress;
        public string Instruction { get; private set; } = "";
        public bool CanAssist => IsActive && review != null && review.Manager.AcceptsInput && !busy && (!completed || standing) && NearPatient();
        public Transform AssistanceAnchor { get; private set; }
        public Transform PhoneAnchor { get; private set; }
        public Vector3 PelvisPosition => body != null ? body.pelvis.position : Origin;
        public Vector3 HeadPosition => rig != null ? rig.head.position : Origin;
        public Vector3 LeftFootPosition => body != null ? body.ankles[0].position : Origin;
        public Vector3 RightFootPosition => body != null ? body.ankles[1].position : Origin;
        public Vector3 LeftHandPosition => body != null ? body.hands[0].position : Origin;
        public Vector3 RightHandPosition => body != null ? body.hands[1].position : Origin;
        public Vector3 TransitionAreaCenter => Origin + Forward * .85f + Vector3.up * .24f;
        public bool FinalPositionValidated { get; private set; }
        public string LastValidationFailure { get; private set; } = "";

        public void Initialize(ReviewCaseSession owner)
        {
            if (review != null) review.SelectionChanged -= SelectionChanged;
            review = owner;
            if (review != null) review.SelectionChanged += SelectionChanged;
            SelectionChanged();
        }

        void SelectionChanged()
        {
            var id = review?.Selected?.medical?.id;
            bool supported = review?.Selected?.medical?.clinicalV2?.capabilities.usesPhysicalPositionConfirmation == true &&
                ClinicalScenarioV2Catalog.FindPresentationAssets(id) != null;
            if (IsActive && (!supported || selectedId != id)) Restore();
            if (supported && !IsActive) Activate(id);
        }

        void Activate(string id)
        {
            visual = review.Procedures?.Visuals;
            rig = visual?.Rig;
            body = rig == null ? null : rig.GetComponent<ArticulatedPatient>();
            if (body == null || body.pelvis == null || rig.head == null) { Instruction = "Falta el rig articulado del paciente."; return; }
            selectedId = id; patient = visual.transform; model = body.skeleton.GetChild(0);
            patientProjection = patient.GetComponent<PatientController>();
            original.Clear(); neutral.Clear(); hidden.Clear();
            var prefab = Resources.Load<GameObject>("Visual/Patient");
            var authored = new Dictionary<string, Transform>();
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true)) authored[t.name] = t;
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
            {
                original[t] = new Pose(t.localPosition, t.localRotation);
                if (authored.TryGetValue(t.name, out var source)) neutral[t] = new Pose(source.localPosition, source.localRotation);
            }
            oldBody = patient.Find("Body"); oldHead = patient.Find("Head");
            if (oldBody != null) original[oldBody] = new Pose(oldBody.localPosition, oldBody.localRotation);
            if (oldHead != null) original[oldHead] = new Pose(oldHead.localPosition, oldHead.localRotation);
            bodyWasEnabled = body.enabled; body.enabled = false;
            visual.ExternalBodyPresentation = true;
            dialogue = review.Manager.Dialogue;
            if (dialogue != null) dialogue.ResponsePresented += ResponsePresented;
            BuildAssembly();
            ResetNeutral();
            body.skeleton.rotation = Quaternion.LookRotation(Forward, Vector3.up);
            for (int i = 0; i < 2; i++) restingFeet[i] = body.ankles[i].rotation;
            // Daniel wears real gym clothes (Rocketbox Sports_Male_04 skin on the shared skeleton); the procedural
            // shirt remains only as a fallback when that appearance has not been built.
            var appearance = Resources.Load<PatientAppearance>("Visual/Appearances/Daniel");
            if (appearance != null && appearance.mesh != null && rig.face != null)
            {
                defaultMesh = rig.face.sharedMesh; defaultMaterials = rig.face.sharedMaterials;
                rig.face.sharedMesh = appearance.mesh; rig.face.sharedMaterials = appearance.materials;
            }
            else shirt = Case01SportsShirt.Create(rig.face, model, owned);
            IsActive = true;
            ResetAttempt();
            Physics.SyncTransforms();
        }

        void ResetAttempt()
        {
            attempt = review.Manager.MedicalSession; attemptId = attempt?.ClinicalState?.AttemptId;
            busy = returning = held = completed = evidenceRecorded = false;
            progress = 0; ticket = null; FinalPositionValidated = false; lastSupportTime = -1;
            ResetRise();
            attentionUntil = 0;
            Instruction = "Daniel está sentado. Acércate, habla con él y comprueba el espacio antes de ayudarle.";
            ApplyPose(0); CapturePoints(previousPoints);
        }

        void LateUpdate()
        {
            if (!IsActive || review == null) return;
            if (review.Manager.MedicalSession != attempt) ResetAttempt();
            if (busy && review.Manager.AcceptsInput)
            {
                if (review.Manager.PositionTransitions.TransitionId != ticket || attempt?.RequestedPosition == PhysicalPosition.Unknown)
                {
                    busy = held = false; Instruction = "La transición ya no está activa en este intento.";
                }
                else if (risePending || returning || held && NearPatient()) Advance();
                else Instruction = "Movimiento detenido. Mantén la asistencia para continuar o cancela para volver al banco.";
            }
            if (risePending || standing || regaining) ApplyRisePose(riseAmount);
            else ApplyPose(progress);
            PresentAttention();
            PresentPerformance();
            SyncInteractionColliders();
        }

        public bool BeginAssistance()
        {
            if (!CanAssist) { if (IsActive && !NearPatient()) Instruction = "Acércate al costado de Daniel para asistirle."; return false; }
            if (standing) return BeginRegainSupport();
            if (!ValidateRoute()) { Instruction = "No hay espacio suficiente: " + LastValidationFailure; return false; }
            var transition = review.Manager.PositionTransitions;
            if (transition.State != PositionTransitionPhase.Requested && !transition.Request(PhysicalPosition.Supine)) return false;
            ticket = transition.TransitionId;
            if (!transition.BeginPreparing(ticket) || !transition.BeginTransition(ticket)) return false;
            busy = true; returning = false; held = false;
            Instruction = "Mantén la asistencia. Daniel se desplazará gradualmente hacia la zona despejada.";
            CapturePoints(previousPoints);
            return true;
        }

        public void SetSupportHeld(bool value)
        {
            held = value && IsActive && review != null && review.Manager.AcceptsInput;
            if (held) lastSupportTime = review.Manager.ElapsedSeconds;
        }

        public void CancelAssistance()
        {
            if (!IsAssisting) return;
            if (risePending) { PreventRiseAttempt(); return; }
            returning = true; held = false;
            Instruction = "Volviendo al último asiento seguro. Mantén despejado el recorrido.";
        }

        void Advance()
        {
            if (risePending || regaining) { AdvanceRise(); return; }
            // Bounded geometric steps also cover slow frames; no frame can jump over the path.
            float remaining = Mathf.Min(Time.deltaTime, .1f) / Mathf.Max(2, assistanceSeconds);
            while (remaining > 0 && busy)
            {
                float step = Mathf.Min(remaining, .004f); remaining -= step;
                float next = Mathf.Clamp01(progress + (returning ? -step : step));
                ApplyPose(next);
                if (!ValidatePose(next, true))
                {
                    ApplyPose(progress);
                    Instruction = "Movimiento detenido: " + LastValidationFailure + ". Retira el obstáculo para continuar.";
                    return;
                }
                progress = next; CapturePoints(previousPoints);
                if (returning && progress <= 0)
                {
                    if (review.Manager.PositionTransitions.Cancel("ReturnedToValidatedSeat")) { busy = returning = false; ticket = null; }
                    Instruction = "Daniel vuelve a estar apoyado en el banco.";
                }
                else if (!returning && progress >= 1)
                {
                    if (HasFinalSupport() && review.Manager.PositionTransitions.Confirm(ticket, true))
                    {
                        busy = false; held = false; completed = true; FinalPositionValidated = true;
                        Instruction = "Posición apoyada confirmada. Continúa observando y comunícate con Daniel.";
                    }
                    else Instruction = "No se puede confirmar la posición: comprueba los apoyos.";
                }
            }
        }

        public bool MaintainSupportedPosition()
        {
            if (!IsActive || !review.Manager.AcceptsInput || busy || !NearPatient()) return false;
            ApplyPose(progress);
            if (!ValidatePose(progress, false) || !(completed ? HasFinalSupport() : HasSeatSupport())) return false;
            if (evidenceRecorded) return true;
            bool accepted = false;
            review.Manager.PerformClinical((runtime, time) => accepted = runtime.ReportOutcome("MaintainedSafeSupportedPosition", time,
                "ValidatedPhysicalSupport", false, attemptId));
            evidenceRecorded = accepted;
            if (accepted) Instruction = "Apoyo comprobado. Daniel permanece acompañado y en observación.";
            return accepted;
        }

        bool NearPatient()
        {
            var camera = Camera.main;
            return camera != null && Vector3.Distance(camera.transform.position, PelvisPosition + Vector3.up * .35f) <= maximumAssistanceDistance;
        }

        void ResetNeutral()
        {
            foreach (var pair in neutral) if (pair.Key != null) pair.Key.SetLocalPositionAndRotation(pair.Value.position, pair.Value.rotation);
        }

        void ApplyPose(float value)
        {
            if (body == null) return;
            ResetNeutral();
            // Clear the front edge while supported before lowering the pelvis; simultaneous
            // descent/translation would drive the thighs through the seat cushion.
            float slide = Smooth(value / .25f), lower = Smooth((value - .20f) / .28f), recline = Smooth((value - .40f) / .60f);
            float displacement = .50f * slide + .65f * recline;
            float height = Mathf.Lerp(.67f, .30f, lower) - .115f * recline;
            // Presyncope: seated trunk leans forward over the knees (spec F), then reclines as he is lowered.
            float tilt = Mathf.Lerp(16, -90, recline);
            var rotation = Quaternion.LookRotation(Forward, Vector3.up) * Quaternion.Euler(tilt, 0, 0);
            body.skeleton.rotation = rotation;
            body.skeleton.position += Origin + Forward * displacement + Vector3.up * height - body.pelvis.position;
            var up = rotation * Vector3.up;
            var front = rotation * Vector3.forward;
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1 : 1;
                var lateral = Right * sign;
                var foot = Origin + lateral * .13f + Forward * Mathf.Lerp(.43f, 1.89f, Smooth(value / .78f)) + Vector3.up * .085f;
                SolveLimb(body.thighs[side], body.calves[side], body.ankles[side], foot,
                    body.thighs[side].position + Forward * .9f + Vector3.up * .35f);
                body.ankles[side].rotation = Quaternion.AngleAxis(-80 * recline, Right) * restingFeet[side];
                var benchHand = Origin + lateral * .34f + Forward * .075f + Vector3.up * .53f;
                var assistedHand = body.pelvis.position + lateral * .29f + up * .30f + front * .12f;
                var restingHand = body.pelvis.position + lateral * .29f - up * .16f + front * .04f;
                var target = Vector3.Lerp(benchHand, assistedHand, Smooth(value / .24f));
                target = Vector3.Lerp(target, restingHand, recline);
                if (side == 1 && value <= 0) target = body.pelvis.position + up * .23f + front * .18f + lateral * .13f;
                SolveLimb(body.upperArms[side], body.forearms[side], body.hands[side], target,
                    body.upperArms[side].position + lateral * .8f - up * .35f);
            }
            float phase = Clinical?.RespiratoryPhase ?? 0;
            float rate = Clinical?.RespiratoryRate ?? visual.VisualState.RespiratoryRate;
            float breath = rate <= 0 ? 0 : (1 - Mathf.Cos(phase * 2 * Mathf.PI)) * .5f * (.006f / .011f) * 100;
            foreach (var renderer in body.deformingMeshes) SetShape(renderer, "VitalBreath", breath);
            if (shirt != null) SetShape(shirt.GetComponent<SkinnedMeshRenderer>(), "VitalBreath", breath);
        }

        static float Smooth(float x) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(x));
        static void SolveLimb(Transform upper, Transform middle, Transform end, Vector3 target, Vector3 pole)
        {
            float a = Vector3.Distance(upper.position, middle.position), b = Vector3.Distance(middle.position, end.position);
            var delta = target - upper.position; float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
            if (delta.sqrMagnitude < .00001f || a < .001f || b < .001f) return;
            var direction = delta.normalized; var bend = Vector3.ProjectOnPlane(pole - upper.position, direction).normalized;
            if (bend.sqrMagnitude < .001f) bend = Vector3.Cross(direction, Vector3.right).normalized;
            float along = (a * a - b * b + distance * distance) / (2 * distance);
            var joint = upper.position + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            upper.rotation = Quaternion.FromToRotation(middle.position - upper.position, joint - upper.position) * upper.rotation;
            middle.rotation = Quaternion.FromToRotation(end.position - middle.position, upper.position + direction * distance - middle.position) * middle.rotation;
        }

        static void SetShape(SkinnedMeshRenderer renderer, string name, float weight)
        {
            if (renderer == null) return;
            int shape = renderer.sharedMesh.GetBlendShapeIndex(name);
            if (shape >= 0) renderer.SetBlendShapeWeight(shape, weight);
        }

        void PresentAttention()
        {
            float time = Clinical?.SimulationTime ?? 0;
            bool listens = time < attentionUntil || dialogue != null && dialogue.IsSpeaking || time % 7.7f < 3.2f;
            attention = Mathf.MoveTowards(attention, listens ? 1 : 0, Time.deltaTime * .7f);
            var camera = Camera.main;
            if (camera != null && !review.Manager.IsPaused)
            {
                var delta = camera.transform.position - rig.head.position;
                var frame = Quaternion.LookRotation(Forward, Vector3.up);
                var local = Quaternion.Inverse(frame) * delta;
                float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -20, 20) * attention;
                float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, Mathf.Max(.1f, new Vector2(local.x, local.z).magnitude)) * Mathf.Rad2Deg, -10, 12) * attention;
                attentionRotation = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, Right);
                if (rig.leftEye != null) rig.leftEye.rotation = Quaternion.AngleAxis(yaw * .20f, Vector3.up) * rig.leftEye.rotation;
                if (rig.rightEye != null) rig.rightEye.rotation = Quaternion.AngleAxis(yaw * .20f, Vector3.up) * rig.rightEye.rotation;
            }
            // While symptomatic and not attending to someone, the head droops; attention lifts it toward the speaker.
            string state = Clinical?.ClinicalStateId;
            bool symptomatic = state == "HYP_00_INITIAL_PRESYNCOPE" || state == "HYP_03_PERSISTENT_SYMPTOMS" || state == "HYP_04_RECURRENT_PRESYNCOPE";
            float droop = symptomatic && progress <= 0 ? (1 - attention) * 16 : 0;
            rig.head.rotation = attentionRotation * Quaternion.AngleAxis(droop, Right) * rig.head.rotation;
            // Existing real facial channel, never substitute a false mouth/face overlay.
            string clinical = Clinical?.ClinicalStateId;
            float expression = clinical == "HYP_02_IMPROVING" ? 4 : clinical == "HYP_03_PERSISTENT_SYMPTOMS" ? 15 :
                clinical == "HYP_04_RECURRENT_PRESYNCOPE" ? 18 : 12;
            discomfort = Mathf.MoveTowards(discomfort, expression, Time.deltaTime * 4);
            rig.SetBlendShape(rig.pain, discomfort);
        }

        void ResponsePresented(DialogueResponseDefinition response)
        {
            if (!IsActive || response == null) return;
            attentionUntil = (Clinical?.SimulationTime ?? 0) + Mathf.Clamp((response.text ?? "").Length * .05f, 2, 7);
        }

        void CapturePoints(Vector3[] destination)
        {
            destination[0] = body.pelvis.position;
            destination[1] = body.spine.position;
            destination[2] = rig.head.position;
            destination[3] = body.calves[0].position;
            destination[4] = body.calves[1].position;
            destination[5] = body.ankles[0].position;
            destination[6] = body.ankles[1].position;
            destination[7] = (body.forearms[0].position + body.forearms[1].position) * .5f;
        }

        bool ValidateRoute()
        {
            float originalProgress = progress;
            ApplyPose(originalProgress); CapturePoints(previousPoints);
            bool valid = true;
            for (int i = 0; i <= 64; i++)
            {
                float p = Mathf.Lerp(originalProgress, 1, i / 64f);
                ApplyPose(p);
                if (!ValidatePose(p, true)) { valid = false; break; }
                CapturePoints(previousPoints);
            }
            ApplyPose(originalProgress); CapturePoints(previousPoints);
            return valid;
        }

        bool ValidatePose(float value, bool sweep)
        {
            LastValidationFailure = "";
            CapturePoints(points);
            if (!ClearCapsule(points[0], points[1], .105f, value, "tronco") ||
                !ClearCapsule(points[1], points[2], .10f, value, "cabeza y hombros")) return false;
            for (int side = 0; side < 2; side++)
            {
                if (!ClearCapsule(body.thighs[side].position, body.calves[side].position, .07f, value, "piernas") ||
                    !ClearCapsule(body.calves[side].position, body.ankles[side].position, .055f, value, "pies") ||
                    !ClearCapsule(body.upperArms[side].position, body.forearms[side].position, .045f, value, "brazos") ||
                    !ClearCapsule(body.forearms[side].position, body.hands[side].position, .035f, value, "manos")) return false;
            }
            if (sweep)
                for (int i = 0; i < points.Length; i++)
                    if (!ClearCapsule(previousPoints[i], points[i], i < 3 ? .085f : .04f, value, "recorrido")) return false;
            return true;
        }

        bool ClearCapsule(Vector3 from, Vector3 to, float radius, float p, string region)
        {
            int count = Physics.OverlapCapsuleNonAlloc(from, to, radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) { LastValidationFailure = "demasiados obstáculos en la zona"; return false; }
            for (int i = 0; i < count; i++)
            {
                var c = overlaps[i]; if (c == null || !c.enabled || c.transform.IsChildOf(patient)) continue;
                // Floor is an authored support, but penetration below its surface is never accepted.
                if (c == floor && Mathf.Min(from.y, to.y) - radius >= -.008f) continue;
                if (c == seat && p < .12f && region == "manos" && Mathf.Min(from.y, to.y) >= .48f) continue;
                // Seated thighs rest on the seat until the pelvis has left it; that contact is support, not a collision.
                if (c == seat && p < .35f && region == "piernas") continue;
                // Some scene floors overlap at y=0; support only flat surfaces below the tested shape.
                if (c.bounds.max.y <= .031f && Mathf.Min(from.y, to.y) - radius >= -.008f) continue;
                LastValidationFailure = region + " cerca de " + c.gameObject.name;
                return false;
            }
            return true;
        }

        public bool ValidateCurrentPose() => IsActive && ValidatePose(progress, false) && (completed ? HasFinalSupport() : progress > 0 || HasSeatSupport());
        bool HasSeatSupport() => seat != null && seat.enabled && seat.gameObject.activeInHierarchy && Mathf.Abs(body.pelvis.position.y - .67f) < .04f &&
            Mathf.Abs(body.pelvis.position.x - Origin.x) < .1f && Mathf.Abs(body.pelvis.position.z - Origin.z) < .10f &&
            SeatUnderPelvis() && FootSupported(body.ankles[0]) && FootSupported(body.ankles[1]);
        bool SeatUnderPelvis()
        {
            int count = Physics.RaycastNonAlloc(body.pelvis.position, Vector3.down, supportHits, .20f, ~0, QueryTriggerInteraction.Ignore);
            if (count == supportHits.Length) return false;
            for (int i = 0; i < count; i++) if (supportHits[i].collider == seat && supportHits[i].normal.y > .9f) return true;
            return false;
        }
        bool FootSupported(Transform foot) => SupportBelow(foot.position + Vector3.up * .02f, .15f);
        bool SupportBelow(Vector3 from, float distance)
        {
            int count = Physics.RaycastNonAlloc(from, Vector3.down, supportHits, distance, ~0, QueryTriggerInteraction.Ignore);
            if (count == supportHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (!supportHits[i].collider.transform.IsChildOf(patient) && supportHits[i].normal.y > .85f) return true;
            return false;
        }
        bool HasFinalSupport()
        {
            if (progress < .999f || !ValidatePose(1, false)) return false;
            foreach (var point in new[] { body.pelvis.position, body.spine.position, rig.head.position })
                if (!SupportBelow(point + Vector3.up * .02f, .31f)) return false;
            return true;
        }

        void SyncInteractionColliders()
        {
            if (oldBody != null) oldBody.SetPositionAndRotation(Vector3.Lerp(body.pelvis.position, body.spine.position, .55f),
                Quaternion.FromToRotation(Vector3.up, rig.head.position - body.pelvis.position));
            if (oldHead != null) oldHead.position = rig.head.position;
        }

        void BuildAssembly()
        {
            assembly = new GameObject("CASE 01 cardio assistance area");
            benchMaterial = Surface("CASE 01 upholstered navy", new Color(.04f, .15f, .20f));
            metalMaterial = Surface("CASE 01 bench frame", new Color(.24f, .28f, .30f));
            matMaterial = Surface("CASE 01 clean exercise mat", new Color(.045f, .29f, .32f));
            seat = Box("Stable cardio bench seat", Origin + Vector3.up * .465f, new Vector3(1.15f, .08f, .43f), benchMaterial).GetComponent<BoxCollider>();
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Box("Bench leg", Origin + new Vector3(x * .47f, .22f, z * .14f), new Vector3(.055f, .44f, .055f), metalMaterial);
            Box("Bench back support", Origin + new Vector3(0, .77f, .245f), new Vector3(1.15f, .44f, .055f), benchMaterial);
            floor = Box("Clear assistance mat", Origin + Forward * 1.04f + Vector3.up * .012f, new Vector3(1.34f, .024f, 2.17f), matMaterial).GetComponent<Collider>();
            AssistanceAnchor = new GameObject("AssistanceAnchor").transform;
            AssistanceAnchor.SetParent(assembly.transform, false); AssistanceAnchor.position = Origin + Vector3.right * .95f + Vector3.up * 1.4f + Forward * .60f;
            var reception = Box("Cardio reception shelf", new Vector3(-1.8f, .86f, 2.8f), new Vector3(.64f, .07f, .43f), benchMaterial);
            Box("Reception support", new Vector3(-1.8f, .43f, 2.8f), new Vector3(.11f, .86f, .11f), metalMaterial);
            PhoneAnchor = new GameObject("PhoneAnchor").transform;
            PhoneAnchor.SetParent(reception.transform, true); PhoneAnchor.position = new Vector3(-1.8f, .91f, 2.8f);
            var environment = GameObject.Find("Environment_gym");
            if (environment != null)
            {
                foreach (var t in environment.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Reused medical equipment") { hidden[t.gameObject] = t.gameObject.activeSelf; t.gameObject.SetActive(false); }
            }
        }

        Material Surface(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .22f); owned.Add(material); return material;
        }
        GameObject Box(string name, Vector3 position, Vector3 size, Material material)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube); item.name = name; item.transform.SetParent(assembly.transform, false);
            item.transform.position = position; item.transform.localScale = size; item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        void Restore()
        {
            IsActive = busy = held = returning = false;
            if (dialogue != null) dialogue.ResponsePresented -= ResponsePresented;
            dialogue = null;
            if (visual != null) visual.ExternalBodyPresentation = false;
            foreach (var pair in original) if (pair.Key != null) pair.Key.SetLocalPositionAndRotation(pair.Value.position, pair.Value.rotation);
            if (body != null) body.enabled = bodyWasEnabled;
            foreach (var pair in hidden) if (pair.Key != null) pair.Key.SetActive(pair.Value);
            if (assembly != null) Destroy(assembly); if (shirt != null) Destroy(shirt);
            if (defaultMesh != null && rig != null && rig.face != null) { rig.face.sharedMesh = defaultMesh; rig.face.sharedMaterials = defaultMaterials; }
            defaultMesh = null; defaultMaterials = null;
            foreach (var asset in owned) if (asset != null) Destroy(asset); owned.Clear();
            original.Clear(); neutral.Clear(); hidden.Clear(); attempt = null; attemptId = null;
            DestroyPerformance();
        }
        void OnDisable() { if (IsActive) Restore(); }
        void OnDestroy() { if (review != null) review.SelectionChanged -= SelectionChanged; if (IsActive) Restore(); }
    }
}
