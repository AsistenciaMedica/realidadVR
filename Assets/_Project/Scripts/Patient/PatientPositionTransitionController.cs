using System;
using EmergencyVR.Medical;
using EmergencyVR.Scenarios;
using UnityEngine;

namespace EmergencyVR.Patient
{
    public enum PositionTransitionPhase { Idle, Requested, Preparing, Transitioning, Completed, Cancelled, Failed }

    // Physical orchestration only. A future animation/space validator must explicitly confirm
    // the ticket; neither a button nor elapsed animation time is proof of a safe position.
    public sealed class PatientPositionTransitionController : MonoBehaviour
    {
        ScenarioManager manager;
        MedicalScenarioRuntime attempt;
        string attemptId;
        PositionTransitionPhase state;
        public PositionTransitionPhase State { get { SynchronizeAttempt(); return state; } private set { state=value; } }
        public string TransitionId { get; private set; }
        public PhysicalPosition RequestedPosition => attempt?.RequestedPosition ?? PhysicalPosition.Unknown;
        public PhysicalPosition EffectivePhysicalPosition => attempt?.ClinicalState?.EffectivePhysicalPosition ?? PhysicalPosition.Unknown;
        public ClinicalPosition ClinicalPosition => attempt?.ClinicalState?.ClinicalPosition ?? ClinicalPosition.Unknown;

        public void Initialize(ScenarioManager owner) { manager=owner; }
        public void ResetForAttempt()
        {
            attempt=manager?.MedicalSession;
            attemptId=attempt?.ClinicalState?.AttemptId;
            TransitionId=null; State=PositionTransitionPhase.Idle;
        }
        bool Ready()
        {
            if(manager==null) return false;
            SynchronizeAttempt();
            return manager.AcceptsInput && attempt!=null && attempt.Capabilities.usesPhysicalPositionConfirmation;
        }
        void SynchronizeAttempt()
        {
            if(manager!=null && (attempt!=manager.MedicalSession || attemptId!=manager.MedicalSession?.ClinicalState?.AttemptId)) ResetForAttempt();
            else if(attempt!=null && attempt.RequestedPosition==PhysicalPosition.Unknown &&
                (state==PositionTransitionPhase.Requested || state==PositionTransitionPhase.Preparing || state==PositionTransitionPhase.Transitioning))
            {
                // The runtime can cancel an outstanding request when a terminal state is entered.
                state=PositionTransitionPhase.Cancelled;
                TransitionId=null;
            }
        }
        public bool Request(PhysicalPosition position)
        {
            if(!Ready() || State==PositionTransitionPhase.Requested || State==PositionTransitionPhase.Preparing || State==PositionTransitionPhase.Transitioning) return false;
            bool accepted=false;
            manager.PerformClinical((runtime,time)=>accepted=runtime.RequestPosition(position,time,"Player",attemptId));
            if(!accepted) return false;
            TransitionId=Guid.NewGuid().ToString("N"); State=PositionTransitionPhase.Requested;
            return true;
        }
        bool TicketIsCurrent(string ticket) => Ready() && !string.IsNullOrEmpty(ticket) && ticket==TransitionId;
        public bool BeginPreparing(string ticket)
        {
            if(!TicketIsCurrent(ticket) || State!=PositionTransitionPhase.Requested) return false;
            State=PositionTransitionPhase.Preparing; return true;
        }
        public bool BeginTransition(string ticket)
        {
            if(!TicketIsCurrent(ticket) || State!=PositionTransitionPhase.Preparing) return false;
            State=PositionTransitionPhase.Transitioning; return true;
        }
        public bool Confirm(string ticket,bool placementValid)
        {
            if(!TicketIsCurrent(ticket) || State!=PositionTransitionPhase.Transitioning) return false;
            if(!placementValid)
            {
                if(!Cancel("PhysicalValidationFailed")) return false;
                State=PositionTransitionPhase.Failed; return false;
            }
            bool accepted=false;
            manager.PerformClinical((runtime,time)=>accepted=runtime.ConfirmPhysicalPosition(RequestedPosition,time,"PhysicalValidation",true,attemptId));
            if(accepted) State=PositionTransitionPhase.Completed;
            return accepted;
        }
        public bool Cancel(string reason)
        {
            if(!Ready() || (State!=PositionTransitionPhase.Requested && State!=PositionTransitionPhase.Preparing && State!=PositionTransitionPhase.Transitioning)) return false;
            bool accepted=false;
            manager.PerformClinical((runtime,time)=>accepted=runtime.CancelPosition(time,reason,"PhysicalValidation",attemptId));
            if(accepted) State=PositionTransitionPhase.Cancelled;
            return accepted;
        }
    }
}
