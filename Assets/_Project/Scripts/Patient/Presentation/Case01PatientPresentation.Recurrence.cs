using EmergencyVR.Medical;
using UnityEngine;

namespace EmergencyVR.Patient.Presentation
{
    public sealed partial class Case01PatientPresentation
    {
        bool risePending, riseReturning, standing, regaining;
        float riseAmount, riseWarning;
        [Min(2)] public float riseWarningSeconds = 4;
        public bool IsRiseWarning => risePending && riseWarning > 0;
        public bool IsAttemptingRise => risePending;
        public bool IsStanding => standing;
        public bool CanPreventRise => risePending && review.Manager.AcceptsInput && NearPatient();

        void ResetRise()
        {
            risePending = riseReturning = standing = regaining = false;
            riseAmount = riseWarning = 0;
        }

        public bool BeginRiseAttempt()
        {
            if (!IsActive || busy || !completed || !review.Manager.AcceptsInput ||
                Clinical?.ClinicalStateId != "HYP_02_IMPROVING") return false;
            if (!ValidateRiseRoute()) { Instruction = "Daniel permanece apoyado: no hay espacio para incorporarse."; return false; }
            var transition = review.Manager.PositionTransitions;
            if (!transition.Request(PhysicalPosition.Standing)) return false;
            ticket = transition.TransitionId;
            if (!transition.BeginPreparing(ticket)) return false;
            risePending = busy = true; riseReturning = returning = false;
            riseAmount = 0; riseWarning = Mathf.Max(2, riseWarningSeconds);
            Instruction = "Daniel: «Creo que puedo incorporarme…». Puedes pedirle que espere y mantenga el apoyo.";
            CapturePoints(previousPoints);
            return true;
        }

        public bool PreventRiseAttempt()
        {
            if (!CanPreventRise) return false;
            riseReturning = true;
            Instruction = "Daniel mantiene el apoyo. Volviendo gradualmente a la posición de reposo.";
            if (riseAmount <= 0) FinishPreventedRise();
            return true;
        }

        void FinishPreventedRise()
        {
            if (!HasFinalSupport() || !review.Manager.PositionTransitions.Cancel("RisePreventedAndSupportValidated")) return;
            risePending = busy = riseReturning = false; riseWarning = 0; completed = true; FinalPositionValidated = true;
            review.Manager.PerformClinical((runtime, time) => runtime.ReportOutcome("ProtectedFromFall", time, "ValidatedPhysicalSupport", false, attemptId));
            Instruction = "Daniel: «De acuerdo, voy a esperar». Continúa la observación sin reincorporarlo.";
        }

        bool BeginRegainSupport()
        {
            var transition = review.Manager.PositionTransitions;
            if (!transition.Request(PhysicalPosition.Supine)) return false;
            ticket = transition.TransitionId;
            if (!transition.BeginPreparing(ticket) || !transition.BeginTransition(ticket)) return false;
            regaining = busy = true; returning = held = false;
            CapturePoints(previousPoints);
            Instruction = "Daniel: «Me vuelve el mareo». Mantén la asistencia para recuperar el apoyo, sin caída.";
            return true;
        }

        void AdvanceRise()
        {
            if (risePending && riseWarning > 0 && !riseReturning)
            {
                riseWarning -= Time.deltaTime;
                if (riseWarning > 0) return;
                if (!review.Manager.PositionTransitions.BeginTransition(ticket)) { busy = risePending = false; return; }
                Instruction = "Daniel comienza a incorporarse despacio. Puedes detener el intento.";
            }
            float remaining = Mathf.Min(Time.deltaTime, .1f) / Mathf.Max(2, assistanceSeconds);
            while (remaining > 0 && busy)
            {
                float step = Mathf.Min(remaining, .004f); remaining -= step;
                float direction = regaining ? (returning ? 1 : -1) : riseReturning ? -1 : 1;
                float next = Mathf.Clamp01(riseAmount + direction * step);
                ApplyRisePose(next);
                if (!ValidatePose(1, true))
                {
                    ApplyRisePose(riseAmount);
                    Instruction = "Incorporación detenida: " + LastValidationFailure + ". Despeja la zona o conserva el apoyo.";
                    return;
                }
                riseAmount = next; CapturePoints(previousPoints);
                if (risePending && riseReturning && riseAmount <= 0) { FinishPreventedRise(); return; }
                if (risePending && !riseReturning && riseAmount >= 1)
                {
                    if (!StandingSupportValid() || !review.Manager.PositionTransitions.Confirm(ticket, true)) return;
                    standing = true; risePending = busy = completed = false; FinalPositionValidated = false;
                    Instruction = "Daniel: «Uf… me vuelve el mareo». Ayúdale a recuperar una posición apoyada.";
                    return;
                }
                if (regaining && returning && riseAmount >= 1)
                {
                    if (StandingSupportValid() && review.Manager.PositionTransitions.Cancel("ReturnToPreviousSupportedPosition"))
                    { regaining = busy = returning = false; Instruction = "Asistencia detenida. Daniel necesita recuperar una posición apoyada."; }
                    return;
                }
                if (regaining && !returning && riseAmount <= 0)
                {
                    if (HasFinalSupport() && review.Manager.PositionTransitions.Confirm(ticket, true))
                    {
                        regaining = standing = busy = held = false; completed = true; FinalPositionValidated = true;
                        Instruction = "Daniel está nuevamente apoyado. Revalora sus síntomas y comunica lo ocurrido.";
                    }
                    return;
                }
            }
        }

        bool StandingSupportValid() => FootSupported(body.ankles[0]) && FootSupported(body.ankles[1]) &&
            body.pelvis.position.y > .65f && Vector3.Dot((rig.head.position - body.pelvis.position).normalized, Vector3.up) > .8f;

        bool ValidateRiseRoute()
        {
            bool valid = true;
            ApplyRisePose(0); CapturePoints(previousPoints);
            for (int i = 1; i <= 64; i++)
            {
                ApplyRisePose(i / 64f);
                if (!ValidatePose(1, true)) { valid = false; break; }
                CapturePoints(previousPoints);
            }
            ApplyPose(1); CapturePoints(previousPoints);
            return valid;
        }

        void ApplyRisePose(float amount)
        {
            if (amount <= 0) { ApplyPose(1); return; }
            ResetNeutral();
            float sit = Smooth(amount / .55f), lift = Smooth((amount - .42f) / .58f);
            var rotation = Quaternion.LookRotation(Forward, Vector3.up) * Quaternion.Euler(Mathf.Lerp(-90, 0, sit), 0, 0);
            body.skeleton.rotation = rotation;
            var pelvis = Origin + Forward * Mathf.Lerp(1.15f, 1.43f, lift) + Vector3.up * Mathf.Lerp(.185f, .87f, lift);
            body.skeleton.position += pelvis - body.pelvis.position;
            var up = rotation * Vector3.up; var front = rotation * Vector3.forward;
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1 : 1;
                var lateral = Right * sign;
                var foot = Origin + lateral * .14f + Forward * Mathf.Lerp(1.89f, 1.43f, sit) + Vector3.up * .085f;
                SolveLimb(body.thighs[side], body.calves[side], body.ankles[side], foot, body.thighs[side].position + Forward * .8f + Vector3.up * .35f);
                body.ankles[side].rotation = Quaternion.AngleAxis(-80 * (1 - sit), Right) * restingFeet[side];
                var hand = pelvis + lateral * .29f + up * Mathf.Lerp(-.16f, .16f, sit) + front * .04f;
                SolveLimb(body.upperArms[side], body.forearms[side], body.hands[side], hand, body.upperArms[side].position + lateral * .8f - up * .35f);
            }
            float phase = Clinical?.RespiratoryPhase ?? 0;
            float breath = (1 - Mathf.Cos(phase * 2 * Mathf.PI)) * .5f * (.006f / .011f) * 100;
            foreach (var renderer in body.deformingMeshes) SetShape(renderer, "VitalBreath", breath);
            if (shirt != null) SetShape(shirt.GetComponent<SkinnedMeshRenderer>(), "VitalBreath", breath);
        }
    }
}
