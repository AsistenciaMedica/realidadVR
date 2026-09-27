using System;
using System.Collections.Generic;
using System.Linq;

namespace EmergencyVR.Medical
{
    // Only MedicalScenarioRuntime writes this per-attempt store; consumers receive detached records.
    public sealed class ObservedPatientDataStore
    {
        readonly List<ObservationRecord> records=new List<ObservationRecord>();
        public string AttemptId { get; private set; }
        internal ObservedPatientDataStore(string attemptId) { AttemptId=attemptId; }
        public int Count { get { return records.Count; } }
        public IReadOnlyList<ObservationRecord> All { get { return Array.AsReadOnly(records.Select(x=>x.Copy()).ToArray()); } }
        public IReadOnlyList<MeasurementObservation> Measurements { get { return Array.AsReadOnly(records.OfType<MeasurementObservation>().Select(x=>(MeasurementObservation)x.Copy()).ToArray()); } }
        public IReadOnlyList<InterviewObservation> Interviews { get { return Array.AsReadOnly(records.OfType<InterviewObservation>().Select(x=>(InterviewObservation)x.Copy()).ToArray()); } }
        public IReadOnlyList<PhysicalObservation> Physical { get { return Array.AsReadOnly(records.OfType<PhysicalObservation>().Select(x=>(PhysicalObservation)x.Copy()).ToArray()); } }
        public MeasurementObservation LatestMeasurement(string type) { var value=records.OfType<MeasurementObservation>().LastOrDefault(x=>x.type==type);return value==null?null:(MeasurementObservation)value.Copy(); }
        internal void Add(ObservationRecord observation)
        {
            if(observation.attemptId!=AttemptId) throw new ArgumentException("Observation belongs to another attempt.");
            records.Add(observation.Copy());
        }
    }
}
