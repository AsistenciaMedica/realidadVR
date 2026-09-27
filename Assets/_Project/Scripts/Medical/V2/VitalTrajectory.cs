using System;

namespace EmergencyVR.Medical
{
    [Serializable] public sealed class VitalTrajectoryDefinition
    {
        public string vital, ruleId;
        public double startValue, targetValue, durationSeconds;
        public CurveType interpolation;
        public bool clinicalReviewRequired=true;
        public VitalTrajectoryDefinition Copy() { return (VitalTrajectoryDefinition)MemberwiseClone(); }
        public void Validate()
        {
            double max;
            switch(vital) { case "HeartRate":max=250;break;case "Systolic":max=260;break;case "Diastolic":max=180;break;case "RespiratoryRate":max=65;break;case "SpO2":max=100;break;default:throw new ArgumentException("Unsupported vital trajectory."); }
            PatientSnapshot.Bounds(startValue,0,max); PatientSnapshot.Bounds(targetValue,0,max); PatientSnapshot.Bounds(durationSeconds,0,86400);
            if(!Enum.IsDefined(typeof(CurveType),interpolation)) throw new ArgumentException("Unsupported trajectory interpolation.");
        }
    }
    // Runtime-owned curve. The start is the patient's actual value at entry, preventing jumps.
    internal sealed class VitalTrajectory
    {
        readonly double start, target, duration;
        readonly CurveType curve;
        internal VitalTrajectory(double actualStart,VitalTrajectoryDefinition definition) { start=actualStart;target=definition.targetValue;duration=definition.durationSeconds;curve=definition.interpolation; }
        internal double Duration { get { return duration; } }
        internal double Value(double seconds)
        {
            var t=duration<=0?1:Math.Max(0,Math.Min(1,seconds/duration));
            if(curve==CurveType.SmoothStep) t=t*t*(3-2*t);
            return start+(target-start)*t;
        }
        // Exact integral ensures respiratory phase is independent of frame/tick subdivision.
        internal double Integral(double from,double to) { return Primitive(to)-Primitive(from); }
        double Primitive(double seconds)
        {
            seconds=Math.Max(0,seconds);
            if(duration<=0) return target*seconds;
            var t=Math.Min(1,seconds/duration);
            var integral=curve==CurveType.SmoothStep?t*t*t-.5*t*t*t*t:.5*t*t;
            return duration*(start*t+(target-start)*integral)+Math.Max(0,seconds-duration)*target;
        }
    }
}
