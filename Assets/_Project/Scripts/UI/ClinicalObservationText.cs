using System.Linq;
using EmergencyVR.Medical;

namespace EmergencyVR.UI
{
    // Formatting only. All values and responses must already exist in the observation history.
    public static class ClinicalObservationText
    {
        public static string Format(ObservedPatientDataStore store)
        {
            if(store==null || store.Count==0) return "Todavía no has registrado observaciones.";
            return string.Join("\n\n",store.All.Select(Format));
        }
        public static string Format(ObservationRecord record)
        {
            string time=System.TimeSpan.FromSeconds(record.simulationTime).ToString(@"mm\:ss");
            var measurement=record as MeasurementObservation;
            if(measurement!=null)
            {
                string name=measurement.type=="BloodPressure"?"Presión arterial":measurement.type=="SpO2"?"SpO₂":"Medición";
                string value=measurement.valid && measurement.hasValue ? (measurement.type=="BloodPressure"?$"{measurement.systolic:0}/{measurement.diastolic:0}":$"{measurement.value:0}")+" "+measurement.unit : "Sin lectura válida";
                return time+" · "+name+"\n"+value+" · "+Quality(measurement.quality);
            }
            var interview=record as InterviewObservation;
            if(interview!=null) return time+" · Respuesta del paciente\n"+(string.IsNullOrEmpty(interview.subtitle)?interview.text:interview.subtitle);
            switch(record.type)
            {
                case "PatientResponsive":return time+" · El paciente responde.";
                case "BreathingNormal":return time+" · Respiración espontánea observada.";
                case "PatientPale":return time+" · Palidez observada.";
                case "PatientUnstableWhenStanding":return time+" · Inestabilidad al incorporarse observada.";
                case "Reassessment":case "PatientReassessed":return time+" · Respuesta y respiración reevaluadas.";
                default:return time+" · Observación registrada.";
            }
        }
        public static string Quality(string quality)
        {
            switch(quality)
            {
                case "Valid":return "Calidad válida";
                case "InvalidPlacement":return "Colocación no válida";
                case "Motion":return "Movimiento durante la adquisición";
                case "InsufficientSignal":return "Señal insuficiente";
                default:return "Calidad no confirmada";
            }
        }
    }
}
