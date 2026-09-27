using System;
using System.Collections.Generic;

namespace EmergencyVR.Medical
{
    [Serializable] public sealed class TrainingProfileDefinition
    {
        public TrainingProfileId id;
        public List<string> allowedActions = new List<string>();
        public List<string> allowedEquipment = new List<string>();
        public List<string> allowedMeasurements = new List<string>();
        public List<string> allowedProcedures = new List<string>();
        public TrainingProfileDefinition Copy()
        {
            return new TrainingProfileDefinition { id=id, allowedActions=new List<string>(allowedActions),
                allowedEquipment=new List<string>(allowedEquipment), allowedMeasurements=new List<string>(allowedMeasurements),
                allowedProcedures=new List<string>(allowedProcedures) };
        }
        public string ExportId { get { return Export(id); } }
        public static string Export(TrainingProfileId value)
        {
            switch(value) { case TrainingProfileId.I0_FirstResponder:return "I0_FIRST_RESPONDER";
                case TrainingProfileId.I1_NonInvasiveEquipment:return "I1_NON_INVASIVE_EQUIPMENT";
                case TrainingProfileId.I2_HealthcareProfessional:return "I2_HEALTHCARE_PROFESSIONAL";
                default:throw new ArgumentException("Unknown training profile."); }
        }
    }
}
