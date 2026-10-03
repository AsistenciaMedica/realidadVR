using System;
using System.Linq;
using EmergencyVR.Evaluation;
using EmergencyVR.Medical;
using EmergencyVR.Medical.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace EmergencyVR.UI
{
    public sealed partial class TrainingExperience
    {
        public string MonitorValue(string key)
        {
            if(UsesObservedData) return ObservedMonitorValue(key);
            var p = Review.Manager.MedicalSession?.Patient;
            if (p == null) return "—";
            var kind = key == "bp" ? MedicalToolKind.BloodPressure : key == "glucose" ? MedicalToolKind.Glucose : MedicalToolKind.Oximeter;
            if (key != "bp" && key != "glucose" && key != "spo2") return "—";
            return readings.TryGetValue(kind, out var measurement) ? Value(measurement.Patient, key) : "—";
        }
        static string Value(PatientSnapshot p, string key)
        {
            bool noPerfusion = p.circulation == "pulseless";
            switch (key)
            {
                case "hr": return noPerfusion ? "Sin pulso" : p.heartRate.ToString("0");
                case "spo2": return noPerfusion ? "Sin lectura" : p.spo2.ToString("0");
                case "bp": return noPerfusion ? "Sin lectura" : $"{p.systolic:0}/{p.diastolic:0}";
                case "rr": return p.respiratoryRate.ToString("0");
                case "temp": return p.temperature.ToString("0.0");
                case "glucose": return p.glucose.ToString("0.0");
                default: return "—";
            }
        }
        string ReadingTime(string key)
        {
            var kind = key == "bp" ? MedicalToolKind.BloodPressure : key == "glucose" ? MedicalToolKind.Glucose : MedicalToolKind.Oximeter;
            if (key != "bp" && key != "glucose" && key != "spo2") return "No monitorizado";
            return readings.TryGetValue(kind, out var r) ? "Medido a " + TimeLabel(r.Time) : "Sin medir";
        }
        void Vital(string key, string title, string unit, float x, float y, float width, Color tone)
        {
            Box(content, "Vital " + key, x, y, width, 126, CardColor, true, true);
            Label(content, "Vital title", title, x + 13, y + 10, width - 26, 27, 17, Soft, true);
            var value = Label(content, "Value " + key, "", x + 13, y + 42, width - 26, 47, 38, tone, true);
            Bind(value, () => { string v = MonitorValue(key); value.fontSize = v.Length > 7 ? 21 : v.Length > 5 ? 29 : 38; return v; });
            Label(content, "Unit", unit, x + 13, y + 87, width - 26, 21, 15, Soft);
        }
        void DrawTraining()
        {
            if(UsesObservedData) { DrawV2Training(); return; }
            Box(content, "Session bar", 22, 20, 1396, 78, Background, true, true);
            Label(content, "Training brand", "VITAL <color=#ED1939>VR</color>", 43, 45, 188, 36, 28, Ink, true);
            Box(content, "Bar divider", 242, 39, 1, 40, Border, false);
            Label(content, "Current case", CleanCopy(Review.Selected.medical == null ? Review.Selected.definition.displayName : LearnerTitle(Review.Selected.medical)), 264, 34, 640, 57, 22, Ink, true);
            var clock = Label(content, "Session clock", "", 920, 44, 147, 39, 29, Accent, true);
            Bind(clock, () => TimeLabel(Review.Manager.ElapsedSeconds));
            Button(content, "Pause training", "Pausa", 1103, 32, 148, 54, () => Navigate(ExperiencePage.Pause));
            Button(content, "Finish training", "Finalizar", 1264, 32, 139, 54, () => Navigate(ExperiencePage.Finish));

            Button(content, "Open actions", actionDrawer ? "Cerrar acciones" : "Acciones", 26, 121, 244, 55, () => { actionDrawer = !actionDrawer; patientDrawer = false; redraw = true; });
            Button(content, "Open patient", patientDrawer ? "Cerrar conversación" : "Hablar y escuchar", 284, 121, 251, 55, () => { patientDrawer = !patientDrawer; actionDrawer = false; redraw = true; });
            if (actionDrawer) DrawActions();
            if (patientDrawer) DrawPatient();

            Box(content, "Clinical monitor", 1054, 118, 364, 714, Background, true, true);
            Label(content, "Monitor heading", "MONITOR DEL PACIENTE", 1072, 140, 327, 30, 20, Ink, true);
            Label(content, "Monitor mode", Review.Procedures.TrainingMode ? "PRÁCTICA GUIADA · SIMULACIÓN" : "EVALUACIÓN · ÚLTIMAS LECTURAS", 1072, 180, 330, 25, 15, Accent, true);
            var measuredKeys = new[] { "spo2", "bp", "glucose" }.Where(key => MonitorValue(key) != "—").ToArray();
            if (measuredKeys.Length == 0)
                Label(content, "No measurements yet", "Aún no has medido constantes.\n\nUtiliza los instrumentos para obtener una lectura.", 1073, 242, 324, 188, 23, Soft);
            for (int i = 0; i < measuredKeys.Length; i++)
            {
                string key = measuredKeys[i];
                Vital(key, key == "spo2" ? "SpO₂" : key == "bp" ? "PRESIÓN ARTERIAL" : "GLUCEMIA",
                    key == "spo2" ? "%" : key == "bp" ? "mmHg" : "mmol/L", 1070, 222 + i * 138, 330,
                    key == "glucose" ? Amber : key == "spo2" ? new Color32(126, 197, 239, 255) : Ink);
            }
            if (measuredKeys.Length > 0)
            {
                var source = Label(content, "Measurement provenance", "", 1073, 643, 324, 96, 17, Soft);
                Bind(source, () => string.Join("\n", measuredKeys.Select(key => (key == "spo2" ? "SpO₂" : key == "bp" ? "TA" : "Glucemia") + "  ·  " + ReadingTime(key))));
                Label(content, "Monitor note", "Una lectura no equivale a monitorización continua. Repite la medición cuando corresponda.", 1073, 750, 324, 66, 17, Soft);
            }

            Box(content, "Feedback surface", 26, 752, 998, 116, Background, true, true);
            Label(content, "Feedback title", "REGISTRO DE LA SESIÓN", 44, 766, 952, 26, 16, Accent, true);
            var feedback = Label(content, "Feedback", "", 44, 800, 957, 54, 22);
            Bind(feedback, () => DisplayClinicalText(Review.Manager.Feedback));
            if (showHints)
            {
                Box(content, "Interaction hint surface", 560, 623, 464, 111, Background);
                var hint = Label(content, "Interaction hint", "", 576, 638, 432, 85, 18, Soft);
                Bind(hint, () => Review.Procedures.TrainingMode ? (IsDesktop ? Review.Procedures.Hint : "Grip: coger / soltar · Gatillo: seleccionar\nB / Y: centrar interfaz y pausar") : (IsDesktop ? "E: coger · Q: usar · C: RCP\nEsc: pausa · Acciones: registrar decisiones" : "Grip: equipo · Gatillo: seleccionar\nB / Y: centrar interfaz y pausar"));
            }
        }
        void DrawActions()
        {
            Box(content, "Action drawer", 26, 191, 509, 543, Background, true, true);
            Label(content, "Actions heading", "Decisiones y procedimientos", 46, 212, 470, 44, 25, Ink, true);
            var ids = Review.ActionIds; int pages = Mathf.Max(1, Mathf.CeilToInt(ids.Length / 5f)); actionPage = Mathf.Clamp(actionPage, 0, pages - 1);
            for (int i = 0; i < 5; i++)
            {
                int index = actionPage * 5 + i; if (index >= ids.Length) break;
                string id = ids[index]; bool physical = Review.Procedures.IsPhysicalAction(id);
                var b = Button(content, "Action " + id, Review.ActionLabel(id), 44, 267 + i * 75, 473, 62, () => Review.Submit(id));
                b.interactable = !physical;
                var label = b.GetComponentInChildren<Text>(); label.fontSize = 19;
                Bind(label, () => Review.ActionLabel(id) + (physical ? "\nUtiliza el equipo" : Review.Manager.MedicalSession?.Completed.Contains(id) == true ? "  ✓" : ""));
            }
            Button(content, "Previous actions", "←", 44, 666, 87, 49, () => { actionPage--; redraw = true; }).interactable = actionPage > 0;
            Label(content, "Action page", $"Acciones  {actionPage + 1} / {pages}", 159, 680, 233, 28, 19, Soft);
            Button(content, "Next actions", "→", 430, 666, 87, 49, () => { actionPage++; redraw = true; }).interactable = actionPage < pages - 1;
        }
        void DrawPatient()
        {
            Box(content, "Patient drawer", 26, 191, 509, 543, Background, true, true);
            Label(content, "Patient heading", "Ficha del paciente", 46, 212, 470, 43, 27, Ink, true);
            if (Review.Selected.medical?.patientIdentity != null)
            {
                var identity = Label(content, "Patient details", "", 47, 271, 467, 63, 22, Soft);
                Bind(identity, PatientDetails);
                DrawLegacyConversation();
                return;
            }
            var details = Label(content, "Patient details", "", 47, 277, 467, 425, 22, Soft);
            Bind(details, () =>
            {
                var p = Review.Manager.MedicalSession?.Patient;
                if (p == null) return "Práctica de familiarización.\n\nSelecciona el paciente y completa las acciones para conocer el flujo del simulador.";
                string text = $"{p.age} años · {Friendly(p.sex)}\n\n";
                if (Review.Procedures.TrainingMode) text += $"Conciencia: {Friendly(p.consciousness)}\nRespiración: {Friendly(p.respiration)}\nTemperatura: {p.temperature:0.0} °C\nDolor: {p.pain:0}/10\n\n";
                return text + p.dialogue + "\n\n" + (Review.Procedures.TrainingMode ? "Datos de ayuda del caso simulado." : "Observa al paciente y utiliza los instrumentos para obtener las lecturas.");
            });
        }
        string ActionName(string id) => Review.ActionIds.Contains(id) ? Review.ActionLabel(id) : "Observación de la sesión";

        // IDs remain unchanged in the medical DTO/export. Only learner-facing text is formatted here.
        public string DisplayClinicalText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text ?? "";
            int separator = text.IndexOf(':');
            string id = separator < 0 ? "" : text.Substring(0, separator);
            if (Review.ActionIds.Contains(id) && text.IndexOf("omisión crítica", StringComparison.OrdinalIgnoreCase) >= 0)
                return ActionName(id) + ": no realizada";
            foreach (var action in Review.ActionIds.OrderByDescending(value => value.Length))
                text = System.Text.RegularExpressions.Regex.Replace(text,
                    @"(?<![A-Za-z0-9_])" + System.Text.RegularExpressions.Regex.Escape(action) + @"(?![A-Za-z0-9_])", ActionName(action));
            return text.Replace("normalBreathing", "respiración normal").Replace("noTrauma", "ausencia de traumatismo")
                .Replace("noShock", "descarga no indicada").Replace("seizureStopped", "convulsión finalizada")
                .Replace("CRITICAL_FAILURE", "resultado crítico");
        }

        void DrawResults()
        {
            if(Review.Selected.medical?.clinicalV2?.capabilities.usesObjectiveBasedEvaluation==true) { DrawV2Results(); return; }
            var result = Review.Manager.MedicalResult;
            var technical = FindFirstObjectByType<EvaluationManager>().LatestResult;
            if (!Review.HasResult) { Navigate(ExperiencePage.Welcome); return; }
            double duration = result != null ? result.durationSeconds : technical.DurationSeconds;
            PageTitle("04 / Revisión", "Cada práctica cuenta.", CleanCopy(Review.Selected.definition.displayName) + "  ·  " + TimeLabel(duration));
            Box(content, "Results score card", 48, 307, 304, 428, CardColor);
            Label(content, "Score title", "RESULTADO DEL INTENTO", 72, 334, 263, 50, 17, Soft, true);
            Label(content, "Score", Review.Score.ToString("0"), 69, 395, 252, 117, 86, Accent, true);
            Label(content, "Score denominator", "sobre 100", 78, 505, 242, 35, 23, Soft);
            Label(content, "Outcome", result == null ? "Práctica de controles" : Friendly(result.outcome), 74, 567, 254, 91, 25, Ink, true);
            Label(content, "Learning note", "Revisa tus decisiones y prepara la siguiente práctica.", 74, 666, 254, 58, 18, Soft);
            var tabs = new[] { "Resumen", "Acciones", "Cronología", "Métricas", "Referencias" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = i; Button(content, "Results tab " + i, tabs[i], 376 + i * 206, 307, 195, 51, () => { resultTab = tab; redraw = true; }, resultTab == i);
            }
            var body = ScrollArea(378, 380, 1014, 351, 351); float y = 0;
            Action<string, bool> line = (value, title) =>
            {
                var text = Label(body, title ? "Section title" : "Result detail", value, 10, y, 968, 1000, title ? 26 : 22, title ? Ink : Soft, title);
                float height = text.preferredHeight + 14; text.rectTransform.sizeDelta = new Vector2(968, height); y += height + (title ? 7 : 13);
            };
            if (result == null)
            {
                line("Tu primera práctica", true);
                line($"{technical.Errors} errores · {technical.OmittedActions.Count} acciones omitidas · {technical.LateActions} fuera de tiempo", false);
                foreach (var action in technical.Actions) line(TimeLabel(action.ElapsedSeconds) + "  ·  " + ActionName(action.ActionId) + "  ·  " + Friendly(action.Disposition.ToString()), false);
                foreach (var id in technical.OmittedActions) line("Pendiente: " + ActionName(id), false);
            }
            else if (resultTab == 0)
            {
                if (Review.Selected.medical?.patientIdentity != null)
                {
                    line("Paciente de este intento", true);
                    line(LearnerTitle(Review.Selected.medical) + " · " + result.initialPatient.age + " años", false);
                }
                line("Qué ocurrió", true);
                line($"{result.correctActions.Length} acciones correctas · {result.incorrectActions.Length} incorrectas · {result.omittedActions.Length} omitidas", false);
                line("Errores críticos", true);
                line(result.criticalErrors.Length == 0 ? "No se registraron errores críticos." : string.Join("\n", result.criticalErrors.Select(DisplayClinicalText)), false);
                line("Para tu siguiente práctica", true);
                foreach (var recommendation in result.recommendations) line(recommendation, false);
                foreach (var section in result.sections) line(section.name + "  ·  " + (section.measured ? section.scorePercent.ToString("0") + "/100" : "No evaluado"), false);
            }
            else if (resultTab == 1)
            {
                line("Acciones correctas", true); line(result.correctActions.Length == 0 ? "Sin acciones registradas." : string.Join("\n", result.correctActions.Select(ActionName)), false);
                line("Acciones incorrectas", true); line(result.incorrectActions.Length == 0 ? "Ninguna registrada." : string.Join("\n", result.incorrectActions.Select(ActionName)), false);
                line("Acciones omitidas", true); line(result.omittedActions.Length == 0 ? "Ninguna registrada." : string.Join("\n", result.omittedActions.Select(ActionName)), false);
            }
            else if (resultTab == 2)
            {
                line("La evolución de tu sesión", true);
                if (result.timeline.Length == 0) line("No se registraron eventos.", false);
                foreach (var entry in result.timeline) line(TimeLabel(entry.elapsedSeconds) + "  ·  " +
                    (entry.section == "Timeline" ? "Evolución del paciente" : ActionName(entry.actionId)) + "\n" +
                    Friendly(entry.disposition) + "  ·  " + DisplayClinicalText(entry.message), false);
            }
            else if (resultTab == 3)
            {
                line("Procedimientos y tiempos", true);
                line("Duración activa: " + TimeLabel(result.durationSeconds) + "\nPrimera RCP: " + (result.timeToCpr < 0 ? "No registrada" : TimeLabel(result.timeToCpr)) + "\nPrimer DEA: " + (result.timeToAed < 0 ? "No registrado" : TimeLabel(result.timeToAed)), false);
                var p = result.procedures;
                if (p != null)
                {
                    line("RCP virtual", true);
                    line($"{p.cpr.compressions} compresiones · ritmo medio {p.cpr.meanRatePerMinute:0}/min\nRecorrido virtual medio {p.cpr.meanDepthMeters * 100:0.0} cm\nMayor pausa entre compresiones {p.cpr.longestPauseSeconds:0.0} s", false);
                    line($"Recorrido en rango: {p.cpr.depthInRange}\nRitmo en rango: {p.cpr.rateInRange}\nColocación correcta: {p.cpr.correctPlacement}\nLiberaciones completas: {p.cpr.fullReleases}", false);
                    line("Equipo y mediciones", true);
                    line($"{p.shocks} descargas · {p.measurements} adquisiciones · {p.unsafeDeviceAttempts} intentos inseguros\nParches colocados: derecho {(p.rightPadPlaced ? "sí" : "no")} / izquierdo {(p.leftPadPlaced ? "sí" : "no")}", false);
                    line("El recorrido virtual no es una medición de profundidad sobre un maniquí instrumentado.", false);
                }
            }
            else
            {
                line("Fuentes del entrenamiento", true);
                foreach (var reference in result.medicalReferences) line(reference.organization + " · " + reference.title + " (" + reference.year + ")\n" + reference.url, false);
                line(result.clinicallyApproved ? "Estado del contenido: revisado." : "Estado del contenido: pendiente de revisión clínica.", false);
            }
            ((RectTransform)body).sizeDelta = new Vector2(996, Mathf.Max(351, y));
            Button(content, "Repeat training", "Repetir entrenamiento", 48, 764, 304, 56, Repeat, true);
            Button(content, ResultNextName, ResultNextText, 377, 764, 422, 56, NextFromResult);
            Button(content, "Export results", "Guardar informe", 821, 764, 276, 56, () =>
            {
                try { Review.ExportResult(); notice = "Informe JSON guardado en ReviewResults."; }
                catch (Exception error) { notice = "No se pudo guardar el informe. Inténtalo de nuevo."; Debug.LogException(error); }
                redraw = true;
            });
            Button(content, "Results home", "Inicio", 1118, 764, 274, 56, () => Navigate(ExperiencePage.Welcome));
            Label(content, "Export notice", notice, 378, 733, 1010, 27, 16, Accent);
        }
    }
}
