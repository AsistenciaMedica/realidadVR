using System;
using System.Linq;
using EmergencyVR.Medical;

namespace EmergencyVR.UI
{
    /// <summary>Formative text from recorded evidence. Never derives observations from hidden patient values.</summary>
    public static class ClinicalCaseDebrief
    {
        public static string ObjectiveTitle(string id)
        {
            switch(id)
            {
                case "OBJ_01":return "Reconocer el riesgo y valorar al paciente";
                case "OBJ_02":return "Proteger al paciente de una caída";
                case "OBJ_03":return "Obtener información relevante";
                case "OBJ_04":return "Reevaluar la evolución";
                case "OBJ_05":return "Solicitar ayuda y comunicar la situación";
                default:return "Objetivo formativo";
            }
        }
        public static string ResultLabel(ObjectiveResult result)
        {
            switch(result)
            {
                case ObjectiveResult.AchievedIndependently:return "Logrado autónomamente";
                case ObjectiveResult.AchievedWithGuidance:return "Logrado con ayuda";
                case ObjectiveResult.NotDemonstrated:return "No demostrado";
                default:return "No evaluado";
            }
        }
        public static string GuidanceFor(string id)
        {
            switch(id)
            {
                case "OBJ_01":return "Acércate, habla con el paciente y comprueba cómo responde y respira. Registra lo que observas.";
                case "OBJ_02":return "Explica la ayuda. Mantén un apoyo seguro o acompaña el cambio de posición por una zona libre. Mantén el control de asistencia mientras ayudas.";
                case "OBJ_03":return "Pregunta qué le pasa y cuándo empezó. Escucha cada respuesta; puedes explorar otros síntomas por separado.";
                case "OBJ_04":return "Vuelve a comprobar cómo responde y respira. Pregunta cómo se encuentra ahora y compara con lo que observaste antes.";
                case "OBJ_05":return "Puedes pedir ayuda desde el inicio. Comunica dónde estáis y lo que has observado, recibe la confirmación y transmite esa información en el relevo.";
                default:return "Revisa la información obtenida y finaliza cuando hayas completado tu intervención.";
            }
        }
        public static string ExplainObjective(MedicalScenarioRuntime runtime,LearningObjectiveProgress objective)
        {
            var evidence=runtime.ClinicalEvents.Where(x=>objective.evidenceEventIds.Contains(x.eventId)).ToArray();
            string explanation=ObjectiveTitle(objective.objectiveId)+"\n"+ResultLabel(objective.result);
            if(evidence.Length>0)
                explanation+="\nEvidencia: "+string.Join(" · ",evidence.Select(x=>Clock(x.simulationTime)+" "+EventDescription(x)));
            var guidance=runtime.ClinicalEvents.Where(x=>x.eventType=="GuidanceUsed"&&Metadata(x,"objectiveId")==objective.objectiveId).ToArray();
            if(guidance.Length>0) explanation+="\nOrientación solicitada: "+string.Join(", ",guidance.Select(x=>Clock(x.simulationTime)))+".";
            if(objective.result!=ObjectiveResult.AchievedIndependently&&objective.result!=ObjectiveResult.AchievedWithGuidance)
                explanation+="\nPara otro intento: "+MissingEvidence(runtime,objective.objectiveId);
            return explanation;
        }
        static string MissingEvidence(MedicalScenarioRuntime runtime,string id)
        {
            switch(id)
            {
                case "OBJ_01":
                    bool response=runtime.Observations.Physical.Any(x=>x.type=="PatientResponsive");
                    bool breathing=runtime.Observations.Physical.Any(x=>x.type=="BreathingNormal");
                    return !response&&!breathing?"No se registró la valoración de respuesta y respiración.":!response?"Faltó registrar la comprobación de respuesta.":"Faltó registrar la observación de la respiración.";
                case "OBJ_02":return "No quedó registrada una protección efectiva mediante apoyo seguro o asistencia completada. Solicitar el traslado no equivale a terminarlo.";
                case "OBJ_03":return "Faltó obtener la descripción principal del problema o su momento de inicio. Cada pregunta aporta información distinta.";
                case "OBJ_04":return "No quedó registrada una reevaluación. Revisa de nuevo al paciente y compara con tus observaciones iniciales.";
                case "OBJ_05":
                    if(!runtime.ClinicalEvents.Any(x=>x.eventType=="HelpRequested"||x.eventType=="HelpDelegated")) return "No se registró una petición de ayuda. Puedes solicitarla sin esperar mediciones.";
                    if(!runtime.ClinicalEvents.Any(x=>x.eventType=="HelpConfirmed")) return "La petición quedó pendiente: faltó completar la comunicación y recibir la confirmación.";
                    return "La ayuda estaba confirmada, pero faltó comunicar y completar el relevo.";
                default:return GuidanceFor(id);
            }
        }
        public static string EventDescription(ClinicalEvent entry)
        {
            switch(entry.eventType)
            {
                case "ScenarioStarted":return "Inicio del entrenamiento";
                case "ScenarioFinished":return "Fin del entrenamiento";
                case "ClinicalStateChanged":return "Evolución del paciente registrada";
                case "PatientContactStarted":return "Contacto con el paciente";
                case "AskedSymptom":return "Pregunta: "+IntentDescription(entry.result);
                case "ResponseObserved":return "Respuesta del paciente escuchada y registrada";
                case "ResponsivenessAssessed":return "Respuesta del paciente comprobada";
                case "BreathingObserved":return "Respiración observada";
                case "PatientReassessed":return "Paciente reevaluado";
                case "PhysicalObservationRecorded":return "Observación física registrada";
                case "PositionAssistanceStarted":case "PositionTransitionRequested":return "Asistencia postural solicitada";
                case "PositionAssistanceCancelled":return "Asistencia cancelada";
                case "PhysicalPositionConfirmed":return entry.result=="Standing"?"Incorporación física registrada":entry.result=="Supine"?"Posición supina confirmada":"Posición con apoyo confirmada";
                case "OutcomeObserved":return "Apoyo seguro comprobado";
                case "HelpRequested":return "Ayuda solicitada";
                case "HelpDelegated":return "Petición de ayuda delegada";
                case "HelpConfirmed":return "Confirmación de ayuda recibida";
                case "HandoverAvailable":return "Relevo disponible";
                case "HandoverStarted":return "Comunicación del relevo iniciada";
                case "HandoverCompleted":return "Relevo comunicado y recibido";
                case "CommunicationRecorded":return Audience(Metadata(entry,"audience"))+": "+Metadata(entry,"message");
                case "GuidanceUsed":return "Orientación solicitada: "+ObjectiveTitle(Metadata(entry,"objectiveId"));
                case "LearningObjectiveAchieved":return "Objetivo demostrado: "+ObjectiveTitle(entry.result);
                case "MeasurementStarted":return "Medición iniciada";
                case "MeasurementFailed":return "Medición sin lectura válida: "+ClinicalObservationText.Quality(entry.quality);
                case "MeasurementCompleted":return "Medición obtenida y fechada";
                case "ObservationAdded":return "Información añadida al historial";
                case "ActionSubmitted":return entry.result=="Accepted"?"Interacción registrada":"La interacción no se completó en ese contexto";
                default:return "Interacción registrada";
            }
        }
        public static string Reflection(MedicalScenarioRuntime runtime)
        {
            int guided=runtime.ClinicalEvents.Count(x=>x.eventType=="GuidanceUsed");
            int cancelled=runtime.ClinicalEvents.Count(x=>x.eventType=="PositionAssistanceCancelled");
            int invalid=runtime.Observations.Measurements.Count(x=>!x.valid);
            return "¿Qué información tenías cuando decidiste actuar? ¿Qué volverías a comprobar?\n\n"+
                $"Orientaciones solicitadas: {guided}. Asistencias canceladas: {cancelled}. Adquisiciones sin lectura válida: {invalid}.\n\n"+
                "Cancelar una maniobra ante un problema o repetir una observación no es por sí mismo un error. Revisa el contexto y el resultado registrados.\n\n"+
                "Esta revisión describe lo demostrado en la simulación. No acredita competencia sanitaria ni convierte la mejoría del paciente en una nota.";
        }
        static string IntentDescription(string intent)
        {
            switch(intent)
            {
                case "MAIN_SYMPTOM":return "qué le pasa";case "SYMPTOM_DESCRIPTION":return "cómo es el malestar";case "ONSET":return "cuándo empezó";
                case "LOSS_OF_CONSCIOUSNESS":return "pérdida de conocimiento";case "CHEST_PAIN":return "dolor de pecho";case "BREATHING_DIFFICULTY":return "dificultad respiratoria";
                case "PALPITATIONS":return "palpitaciones";case "CARDIAC_HISTORY":return "antecedentes";case "MEDICATION":return "medicación";
                case "FOOD_DRINK":return "comida y bebida";case "CONSENT_HELP":return "consentimiento para ayudar";case "CURRENT_STATUS":return "cómo se encuentra ahora";default:return "saludo y contacto";
            }
        }
        static string Audience(string value) { return value=="TrainingConfiguration"||value=="TrainingPresentation"?"Contexto del entrenamiento":value=="Operator"?"Mensaje al operador":value=="Companion"?"Mensaje al compañero":value=="HandoverTeam"?"Información del relevo":"Respuesta recibida"; }
        static string Metadata(ClinicalEvent entry,string key) { return entry.metadata?.FirstOrDefault(x=>x.key==key)?.value??""; }
        public static string Clock(double time) { return TimeSpan.FromSeconds(time).ToString(@"mm\:ss"); }
    }
}
