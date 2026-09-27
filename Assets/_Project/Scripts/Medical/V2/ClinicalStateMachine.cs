using System;
using System.Collections.Generic;
using System.Linq;

namespace EmergencyVR.Medical
{
    // An internal runtime component. It has no independent clock, patient, Unity behaviour or public mutation API.
    internal sealed class ClinicalStateMachine
    {
        readonly ClinicalScenarioV2Definition definition;
        readonly Dictionary<string,VitalTrajectory> trajectories=new Dictionary<string,VitalTrajectory>();
        internal ClinicalStateDefinition State { get; private set; }
        internal double EnteredAt { get; private set; }
        internal ClinicalStateMachine(ClinicalScenarioV2Definition definition,PatientSnapshot patient)
        {
            this.definition=definition;
            State=definition.clinicalStates.Single(x=>x.id==definition.metadata.initialClinicalStateId);
            foreach(var curve in State.vitalTrajectories) Set(patient,curve.vital,curve.startValue);
            Enter(State,patient,0,Array.Empty<VitalTrajectoryDefinition>());
        }
        internal void Enter(ClinicalStateDefinition state,PatientSnapshot patient,double time,VitalTrajectoryDefinition[] overrides)
        {
            State=state;EnteredAt=time;trajectories.Clear();
            foreach(var curve in state.vitalTrajectories) trajectories[curve.vital]=new VitalTrajectory(Get(patient,curve.vital),curve);
            foreach(var curve in overrides) trajectories[curve.vital]=new VitalTrajectory(Get(patient,curve.vital),curve);
            patient.consciousness=state.consciousness==ConsciousnessState.Alert?"Conscious":state.consciousness.ToString();
            patient.canSwallow=state.consciousness!=ConsciousnessState.Unresponsive;
            if(!state.preserveSymptomsOnEntry) patient.flags=(string[])state.symptoms.Clone();
        }
        internal void Advance(PatientSnapshot patient,double from,double to,ref double respiratoryPhase)
        {
            double breaths=trajectories.TryGetValue("RespiratoryRate",out var respiratory)?respiratory.Integral(from-EnteredAt,to-EnteredAt)/60:patient.respiratoryRate*(to-from)/60;
            respiratoryPhase=(respiratoryPhase+breaths)%1;
            if(respiratoryPhase>1-1e-12||respiratoryPhase<1e-12) respiratoryPhase=0;
            foreach(var curve in trajectories) Set(patient,curve.Key,curve.Value.Value(to-EnteredAt));
            patient.Validate();
        }
        internal double NextTimedBoundary(double now,double target)
        {
            var next=target;
            foreach(var transition in definition.transitions.Where(x=>x.from==State.id))
                foreach(var condition in transition.conditions)
                {
                    var boundary=condition.type=="StateElapsed"?EnteredAt+condition.seconds:condition.type=="TrajectoryCompleted"?EnteredAt+Duration:double.PositiveInfinity;
                    if(boundary>now+1e-9&&boundary<next) next=boundary;
                }
            return next;
        }
        internal double Duration { get { return trajectories.Count==0?0:trajectories.Values.Max(x=>x.Duration); } }
        internal ClinicalTransitionDefinition Eligible(double time,PhysicalPosition position,ISet<string> signals)
        {
            return definition.transitions.Where(x=>x.from==State.id).OrderByDescending(x=>x.priority).ThenBy(x=>x.id,StringComparer.Ordinal)
                .FirstOrDefault(x=>x.conditions.All(c=>Condition(c,time,position,signals)));
        }
        bool Condition(ClinicalTransitionCondition condition,double time,PhysicalPosition position,ISet<string> signals)
        {
            switch(condition.type)
            {
                case "Signal":return signals.Contains(condition.value);
                case "StateElapsed":return time-EnteredAt+1e-9>=condition.seconds;
                case "TrajectoryCompleted":return time-EnteredAt+1e-9>=Duration;
                case "PhysicalPosition":return position.ToString()==condition.value;
                case "ClinicalState":return State.id==condition.value;
                default:return false;
            }
        }
        internal ClinicalStateDefinition Find(string id) { return definition.clinicalStates.Single(x=>x.id==id); }
        static double Get(PatientSnapshot patient,string vital)
        {
            switch(vital) { case "HeartRate":return patient.heartRate;case "Systolic":return patient.systolic;case "Diastolic":return patient.diastolic;case "RespiratoryRate":return patient.respiratoryRate;case "SpO2":return patient.spo2;default:throw new ArgumentException("Unsupported vital."); }
        }
        static void Set(PatientSnapshot patient,string vital,double value)
        {
            switch(vital) { case "HeartRate":patient.heartRate=value;break;case "Systolic":patient.systolic=value;break;case "Diastolic":patient.diastolic=value;break;case "RespiratoryRate":patient.respiratoryRate=value;break;case "SpO2":patient.spo2=value;break;default:throw new ArgumentException("Unsupported vital."); }
        }
    }
}
