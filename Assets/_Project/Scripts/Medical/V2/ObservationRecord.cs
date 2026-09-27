using System;

namespace EmergencyVR.Medical
{
    [Serializable] public abstract class ObservationRecord
    {
        public string observationId, attemptId, type, clinicalStateId, source, quality;
        public double simulationTime;
        public bool valid;
        public abstract ObservationRecord Copy();
    }
    [Serializable] public sealed class MeasurementObservation : ObservationRecord
    {
        // hasValue is explicit: invalid acquisition has no clinical numeric value.
        public bool hasValue;
        public double value, systolic, diastolic;
        public string unit;
        public override ObservationRecord Copy() { return (MeasurementObservation)MemberwiseClone(); }
    }
    [Serializable] public sealed class InterviewObservation : ObservationRecord
    {
        public DialogueIntent intent;
        public string responseId, text, subtitle;
        public override ObservationRecord Copy() { return (InterviewObservation)MemberwiseClone(); }
    }
    [Serializable] public sealed class PhysicalObservation : ObservationRecord
    {
        public string description;
        public override ObservationRecord Copy() { return (PhysicalObservation)MemberwiseClone(); }
    }
}
