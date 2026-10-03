using System;
using System.Linq;
using EmergencyVR.Evaluation;
using EmergencyVR.Medical;
using EmergencyVR.Patient.Presentation;
using UnityEngine;

namespace EmergencyVR.UI
{
    public sealed partial class TrainingExperience
    {
        void DrawVrResults()
        {
            if (!Review.HasResult) { Navigate(ExperiencePage.Welcome); return; }
            bool objectives = Review.Selected.medical?.clinicalV2?.capabilities.usesObjectiveBasedEvaluation == true;
            var result = Review.Manager.MedicalResult;
            var runtime = Review.Manager.MedicalSession;
            VrTitle("Tu práctica, paso a paso");
            Label(content, string.IsNullOrEmpty(notice) ? "Outcome" : "Export notice", !string.IsNullOrEmpty(notice) ? notice : objectives ? LearnerTitle(Review.Selected.medical) :
                Review.Score.ToString("0") + " / 100 · " + (result == null ? "Práctica de controles" : Friendly(result.outcome)),
                36, 210, 928, 77, 29, Accent, true);
            var tabs = objectives ? new[] { "Objetivos", "Cronología", "Información", "Siguiente vez" } :
                new[] { "Resumen", "Acciones", "Cronología", "Métricas", "Fuentes" };
            resultTab = Mathf.Clamp(resultTab, 0, tabs.Length - 1);
            float tabWidth = (928 - (tabs.Length - 1) * 10) / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = i;
                var button = Button(content, "Results tab " + i, tabs[i], 36 + i * (tabWidth + 10), 294, tabWidth, 65,
                    () => { resultTab = tab; redraw = true; }, resultTab == i);
                button.GetComponentInChildren<UnityEngine.UI.Text>().fontSize = 24;
            }
            var body = ScrollArea(36, 384, 928, 337, 337); float y = 0;
            Action<string> line = value =>
            {
                var label = Label(body, "Result detail", value, 0, y, 874, 1500, 29, Soft);
                float height = label.preferredHeight + 22;
                label.rectTransform.sizeDelta = new Vector2(874, height); y += height + 12;
            };
            if (objectives)
            {
                if (resultTab == 0)
                    foreach (var objective in runtime.ObjectiveProgress) line(ClinicalCaseDebrief.ExplainObjective(runtime, objective));
                else if (resultTab == 1)
                    foreach (var entry in runtime.ClinicalEvents.Where(entry => entry.eventType != "ObservationAdded" && !(entry.eventType == "ActionSubmitted" && entry.result == "Accepted")))
                        line(TimeLabel(entry.simulationTime) + " · " + ClinicalCaseDebrief.EventDescription(entry));
                else if (resultTab == 2) line(ClinicalObservationText.Format(runtime.Observations));
                else line(ClinicalCaseDebrief.Reflection(runtime));
            }
            else if (result == null)
            {
                var technical = FindFirstObjectByType<EvaluationManager>().LatestResult;
                line(technical.Errors + " errores · " + technical.OmittedActions.Count + " acciones omitidas");
                foreach (var action in technical.Actions) line(TimeLabel(action.ElapsedSeconds) + " · " + ActionName(action.ActionId) + " · " + Friendly(action.Disposition.ToString()));
            }
            else if (resultTab == 0)
            {
                line(result.correctActions.Length + " acciones correctas · " + result.incorrectActions.Length + " incorrectas · " + result.omittedActions.Length + " omitidas");
                line(result.criticalErrors.Length == 0 ? "Sin errores críticos registrados." : string.Join("\n", result.criticalErrors.Select(DisplayClinicalText)));
                foreach (var recommendation in result.recommendations) line(DisplayClinicalText(recommendation));
                foreach (var section in result.sections) line(section.name + " · " + (section.measured ? section.scorePercent.ToString("0") + "/100" : "No evaluado"));
            }
            else if (resultTab == 1)
            {
                line("Correctas\n" + (result.correctActions.Length == 0 ? "Sin acciones registradas." : string.Join("\n", result.correctActions.Select(ActionName))));
                line("Incorrectas\n" + (result.incorrectActions.Length == 0 ? "Ninguna registrada." : string.Join("\n", result.incorrectActions.Select(ActionName))));
                line("Pendientes\n" + (result.omittedActions.Length == 0 ? "Ninguna registrada." : string.Join("\n", result.omittedActions.Select(ActionName))));
            }
            else if (resultTab == 2)
            {
                if (result.timeline.Length == 0) line("No se registraron eventos.");
                foreach (var entry in result.timeline) line(TimeLabel(entry.elapsedSeconds) + " · " + (entry.section == "Timeline" ? "Evolución del paciente" : ActionName(entry.actionId)) +
                    "\n" + Friendly(entry.disposition) + " · " + DisplayClinicalText(entry.message));
            }
            else if (resultTab == 3)
            {
                line("Duración: " + TimeLabel(result.durationSeconds) + "\nPrimera RCP: " + (result.timeToCpr < 0 ? "No registrada" : TimeLabel(result.timeToCpr)) +
                    "\nPrimer DEA: " + (result.timeToAed < 0 ? "No registrado" : TimeLabel(result.timeToAed)));
                var procedures = result.procedures;
                if (procedures != null)
                {
                    line(procedures.cpr.compressions + " compresiones · ritmo " + procedures.cpr.meanRatePerMinute.ToString("0") + "/min\nRecorrido virtual " + (procedures.cpr.meanDepthMeters * 100).ToString("0.0") + " cm");
                    line(procedures.shocks + " descargas · " + procedures.measurements + " mediciones · " + procedures.unsafeDeviceAttempts + " intentos inseguros");
                    line("El recorrido virtual no mide la profundidad sobre un maniquí instrumentado.");
                }
            }
            else
            {
                foreach (var reference in result.medicalReferences) line(reference.organization + " · " + reference.title + " (" + reference.year + ")\n" + reference.url);
                line(result.clinicallyApproved ? "Contenido revisado." : "Contenido pendiente de revisión clínica.");
            }
            ((RectTransform)body).sizeDelta = new Vector2(910, Mathf.Max(337, y));
            Button(content, "Repeat training", "Repetir entrenamiento", 36, 750, 455, 62, Repeat, true);
            Button(content, ResultNextName, RecommendedNextAvailable ? "Demo: Andrés →" : "Elegir otro caso", 509, 750, 455, 62, NextFromResult);
            Button(content, "Export results", "Guardar informe", 36, 824, 455, 60, () =>
            {
                try { Review.ExportResult(); notice = "Informe guardado."; }
                catch (Exception error) { notice = "No se pudo guardar el informe."; Debug.LogException(error); }
                redraw = true;
            });
            Button(content, "Results home", "Inicio", 509, 824, 455, 60, () => Navigate(ExperiencePage.Welcome));
        }
    }
}
