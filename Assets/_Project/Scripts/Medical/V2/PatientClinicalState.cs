using System;
using System.Collections.Generic;

namespace EmergencyVR.Medical
{
    public sealed class BloodPressureValue
    {
        public float Systolic { get; }
        public float Diastolic { get; }
        internal BloodPressureValue(double systolic,double diastolic) { Systolic=(float)systolic;Diastolic=(float)diastolic; }
    }
    // A detached immutable projection, never an alternate mutable patient model.
    public sealed class PatientClinicalState
    {
        public string AttemptId { get; }
        public string ScenarioId { get; }
        public string ClinicalStateId { get; }
        public float SimulationTime { get; }
        public ConsciousnessState Consciousness { get; }
        public OrientationState Orientation { get; }
        public ClinicalPosition ClinicalPosition { get; }
        public PhysicalPosition EffectivePhysicalPosition { get; }
        public PhysicalPosition RequestedPosition { get; }
        public float HeartRate { get; }
        public float RespiratoryRate { get; }
        public BloodPressureValue BloodPressure { get; }
        public float SpO2 { get; }
        public float RespiratoryPhase { get; }
        public bool CanSpeak { get; }
        public bool CanCooperate { get; }
        public IReadOnlyCollection<string> Symptoms { get; }
        internal PatientClinicalState(string attempt,string scenario,ClinicalStateDefinition state,PatientSnapshot patient,double time,
            ClinicalPosition clinicalPosition,PhysicalPosition effective,PhysicalPosition requested,double phase)
        {
            AttemptId=attempt;ScenarioId=scenario;ClinicalStateId=state==null?"":state.id;SimulationTime=(float)time;
            Consciousness=state==null?(patient.consciousness=="Conscious"?ConsciousnessState.Alert:(ConsciousnessState)Enum.Parse(typeof(ConsciousnessState),patient.consciousness)):state.consciousness;Orientation=state==null?OrientationState.Oriented:state.orientation;
            ClinicalPosition=clinicalPosition;EffectivePhysicalPosition=effective;RequestedPosition=requested;
            HeartRate=(float)patient.heartRate;RespiratoryRate=(float)patient.respiratoryRate;BloodPressure=new BloodPressureValue(patient.systolic,patient.diastolic);SpO2=(float)patient.spo2;
            RespiratoryPhase=(float)phase;CanSpeak=state==null?patient.consciousness!="Unresponsive":state.canSpeak;CanCooperate=state==null?patient.consciousness=="Conscious":state.canCooperate;
            Symptoms=Array.AsReadOnly((string[])patient.flags.Clone());
        }
    }
}
