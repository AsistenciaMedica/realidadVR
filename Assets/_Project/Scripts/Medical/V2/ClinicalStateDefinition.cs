using System;
using System.Linq;

namespace EmergencyVR.Medical
{
    [Serializable] public sealed class ClinicalStateDefinition
    {
        public string id;
        public ConsciousnessState consciousness;
        public OrientationState orientation;
        public ClinicalPosition position;
        public bool canSpeak=true, canCooperate=true;
        public bool preserveSymptomsOnEntry;
        public string[] symptoms=Array.Empty<string>();
        public VitalTrajectoryDefinition[] vitalTrajectories=Array.Empty<VitalTrajectoryDefinition>();
        public ClinicalStateDefinition Copy() { var x=(ClinicalStateDefinition)MemberwiseClone(); x.symptoms=(string[])symptoms.Clone(); x.vitalTrajectories=vitalTrajectories.Select(t=>t.Copy()).ToArray(); return x; }
        public void Validate()
        {
            if(string.IsNullOrWhiteSpace(id)||symptoms==null||vitalTrajectories==null||!Enum.IsDefined(typeof(ConsciousnessState),consciousness)||!Enum.IsDefined(typeof(OrientationState),orientation)||!Enum.IsDefined(typeof(ClinicalPosition),position)) throw new ArgumentException("Invalid clinical state.");
            if(vitalTrajectories.Select(x=>x.vital).Distinct().Count()!=vitalTrajectories.Length) throw new ArgumentException("Duplicate vital trajectory.");
            foreach(var trajectory in vitalTrajectories) trajectory.Validate();
        }
    }
}
