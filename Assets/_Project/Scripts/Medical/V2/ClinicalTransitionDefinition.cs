using System;
using System.Linq;

namespace EmergencyVR.Medical
{
    [Serializable] public sealed class ClinicalTransitionCondition
    {
        public string type, value;
        public double seconds;
        public ClinicalTransitionCondition Copy() { return (ClinicalTransitionCondition)MemberwiseClone(); }
    }
    [Serializable] public sealed class ClinicalTransitionDefinition
    {
        public string id, from, to;
        public int priority;
        public ClinicalTransitionCondition[] conditions=Array.Empty<ClinicalTransitionCondition>();
        public VitalTrajectoryDefinition[] optionalTrajectory=Array.Empty<VitalTrajectoryDefinition>();
        public ClinicalTransitionDefinition Copy() { var x=(ClinicalTransitionDefinition)MemberwiseClone(); x.conditions=conditions.Select(c=>c.Copy()).ToArray(); x.optionalTrajectory=optionalTrajectory.Select(t=>t.Copy()).ToArray(); return x; }
    }
}
