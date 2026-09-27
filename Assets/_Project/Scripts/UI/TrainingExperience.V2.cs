using System;
using System.Linq;
using EmergencyVR.Dialogue;
using EmergencyVR.Medical;
using EmergencyVR.Patient.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace EmergencyVR.UI
{
    public sealed partial class TrainingExperience
    {
        bool dialogueDrawer, callDrawer, observedDrawer, guidanceDrawer;
        int dialoguePage;
        string case01ViewAttempt, guidanceText = "";
        bool UsesObservedData => Review?.Manager?.MedicalSession?.Capabilities.usesObservedPatientData == true;
        ClinicalHelpController CaseHelp => Review == null ? null : Review.GetComponent<ClinicalHelpController>();
        Case01PatientPresentation CaseBody => Review == null ? null : Review.GetComponent<Case01PatientPresentation>();

        string ObservedMonitorValue(string key)
        {
            string type = key == "bp" ? "BloodPressure" : key == "spo2" ? "SpO2" : null;
            var reading = type == null ? null : Review.Manager.MedicalSession.Observations.LatestMeasurement(type);
            if (reading == null) return "—";
            if (!reading.valid || !reading.hasValue) return "Sin lectura";
            return key == "bp" ? $"{reading.systolic:0}/{reading.diastolic:0}" : reading.value.ToString("0");
        }
        void DrawV2Training()
        {
            var attemptId = Review.Manager.MedicalSession.ClinicalState.AttemptId;
            if (case01ViewAttempt != attemptId)
            {
                case01ViewAttempt = attemptId;
                dialogueDrawer = callDrawer = observedDrawer = guidanceDrawer = actionDrawer = false;
                dialoguePage = 0; guidanceText = "";
            }
            Box(content, "Session bar", 22, 20, 1396, 78, Background, true, true);
            Label(content, "Training brand", "VITAL <color=#ED1939>VR</color>", 43, 44, 196, 38, 28, Ink, true);
            var status = Label(content, "Session status", "", 258, 46, 525, 34, 18, Soft);
            Bind(status, () => "ENTRENAMIENTO · " + (Review.Procedures.TrainingMode ? "PRÁCTICA GUIADA" : "EVALUACIÓN"));
            var clock = Label(content, "Session clock", "", 799, 41, 130, 43, 29, Accent, true);
            Bind(clock, () => TimeLabel(Review.Manager.ElapsedSeconds));
            Button(content, "Technical help", "Controles", 949, 32, 139, 54, () => { ReleaseAssistance(); OpenUtility(ExperiencePage.Help); });
            Button(content, "Pause training", "Pausa", 1103, 32, 139, 54, () => { ReleaseAssistance(); Navigate(ExperiencePage.Pause); });
            Button(content, "Finish training", "Finalizar", 1257, 32, 145, 54, () => { ReleaseAssistance(); Navigate(ExperiencePage.Finish); });
            Button(content, "Open actions", actionDrawer ? "Cerrar acciones" : "Interacciones", 26, 121, 235, 55, () =>
            {
                ReleaseAssistance(); actionDrawer = !actionDrawer; dialogueDrawer = callDrawer = false; redraw = true;
            });
            Button(content, "Open dialogue", dialogueDrawer ? "Cerrar conversación" : "Hablar con el paciente", 276, 121, 257, 55, () =>
            {
                ReleaseAssistance(); dialogueDrawer = !dialogueDrawer; actionDrawer = callDrawer = false; redraw = true;
            });
            Button(content, "Open help conversation", callDrawer ? "Cerrar llamada" : "Llamada y relevo", 548, 121, 265, 55, () =>
            {
                ReleaseAssistance(); callDrawer = !callDrawer; actionDrawer = dialogueDrawer = false; redraw = true;
            });
            Button(content, "Toggle observations", observedDrawer ? "Cerrar información" : "Información obtenida", 828, 121, 300, 55, () =>
            {
                observedDrawer = !observedDrawer; guidanceDrawer = false; redraw = true;
            });
            if (Review.Procedures.TrainingMode)
                Button(content, "Clinical guidance", "Pedir orientación", 1143, 121, 275, 55, AskForGuidance);
            if (actionDrawer) DrawV2Actions();
            if (dialogueDrawer) DrawIntentDialogue();
            if (callDrawer) DrawHelpConversation();
            if (observedDrawer) DrawObservedHistory();
            if (guidanceDrawer) DrawGuidance();
            var preventRise = Button(content, "Prevent rise attempt", "Pedir que espere y mantenga el apoyo", 608, 686, 810, 61,
                () => CaseBody?.PreventRiseAttempt(), true);
            preventRise.gameObject.SetActive(CaseBody != null && CaseBody.CanPreventRise);
            live.Add(() => preventRise.gameObject.SetActive(CaseBody != null && CaseBody.CanPreventRise));
            Box(content, "Feedback surface", 26, 762, 1392, 106, Background, true, true);
            var feedback = Label(content, "Feedback", "", 46, 780, 1347, 78, 21, Soft);
            Bind(feedback, () =>
            {
                if (CaseBody != null && CaseBody.IsAssisting) return CaseBody.Instruction;
                if (callDrawer && CaseHelp != null) return CaseHelp.CurrentLine;
                var response = Review.Manager.Dialogue.LastResponse;
                return response == null ? Review.Manager.Feedback : "Paciente: «" + (response.subtitle ?? response.text) + "»";
            });
        }
        void ReleaseAssistance() { CaseBody?.SetSupportHeld(false); }
        void DrawV2Actions()
        {
            Box(content, "Action drawer", 26, 191, 560, 549, Background, true, true);
            Label(content, "Actions heading", "Valorar y acompañar", 46, 211, 514, 40, 25, Ink, true);
            Button(content, "Action AssessResponsiveness", "Comprobar respuesta", 44, 267, 520, 57, () => Review.Submit("AssessResponsiveness"));
            Button(content, "Action ObserveBreathing", "Observar respiración", 44, 337, 520, 57, () => Review.Submit("ObserveBreathing"));
            Button(content, "Action ReassessPatient", "Reevaluar al paciente", 44, 407, 520, 57, () => Review.Submit("ReassessPatient"));
            var support = Button(content, "Maintain supported position", "Mantener un apoyo seguro", 44, 477, 520, 57, () => { CaseBody?.MaintainSupportedPosition(); });
            live.Add(() => support.interactable = CaseBody != null && CaseBody.IsActive && !CaseBody.IsAssisting);
            var assist = Button(content, "Action AssistPatient", "Mantén pulsado para ayudar a tumbarse", 44, 547, 520, 65, () => { });
            var hold = assist.gameObject.AddComponent<ClinicalAssistanceHold>();
            hold.Configure(() => CaseBody);
            var assistanceLabel = assist.GetComponentInChildren<Text>();
            live.Add(() =>
            {
                assist.interactable = CaseBody != null && (CaseBody.CanAssist || CaseBody.IsAssisting && !CaseBody.IsAttemptingRise);
                assistanceLabel.text = CaseBody != null && CaseBody.IsStanding ? "Mantén pulsado para recuperar el apoyo" : "Mantén pulsado para ayudar a tumbarse";
            });
            var cancel = Button(content, "Cancel assistance", "Cancelar asistencia", 44, 624, 520, 43, () => CaseBody?.CancelAssistance());
            live.Add(() => cancel.interactable = CaseBody != null && CaseBody.IsAssisting);
            var instruction = Label(content, "Assistance instruction", "", 46, 678, 516, 58, 17, Soft);
            Bind(instruction, () => CaseBody == null ? "Acércate al paciente para acompañarlo." :
                CaseBody.Instruction + (CaseBody.IsAssisting ? "  " + Mathf.RoundToInt(CaseBody.Progress * 100) + " %" : ""));
        }
        void DrawIntentDialogue()
        {
            var intents = (DialogueIntent[])Enum.GetValues(typeof(DialogueIntent));
            int pages = (int)Math.Ceiling(intents.Length / 5.0); dialoguePage = Mathf.Clamp(dialoguePage, 0, pages - 1);
            Box(content, "Dialogue drawer", 26, 191, 560, 549, Background, true, true);
            Label(content, "Dialogue heading", "Escucha a la persona", 46, 213, 514, 39, 25, Ink, true);
            for (int i = 0; i < 5; i++)
            {
                int index = dialoguePage * 5 + i; if (index >= intents.Length) break;
                var intent = intents[index];
                Button(content, "Intent " + intent, IntentLabel(intent), 44, 269 + i * 73, 520, 61, () => Review.Manager.Dialogue.Ask(intent));
            }
            Button(content, "Previous dialogue", "←", 44, 659, 89, 51, () => { dialoguePage--; redraw = true; }).interactable = dialoguePage > 0;
            Label(content, "Dialogue page", $"{dialoguePage + 1} / {pages}", 237, 674, 130, 33, 19, Soft);
            Button(content, "Next dialogue", "→", 475, 659, 89, 51, () => { dialoguePage++; redraw = true; }).interactable = dialoguePage < pages - 1;
        }
        void DrawObservedHistory()
        {
            Box(content, "Observed data", 940, 191, 478, 549, Background, true, true);
            Label(content, "Observed heading", "INFORMACIÓN OBTENIDA", 960, 213, 438, 34, 20, Ink, true);
            Label(content, "Observed note", "Lo que has observado o preguntado, con su hora.", 960, 258, 438, 55, 18, Soft);
            var body = ScrollArea(960, 320, 436, 399, 399);
            var observations = Label(body, "Observed patient data", "", 0, 0, 400, 399, 20, Soft);
            Bind(observations, () =>
            {
                string text = ClinicalObservationText.Format(Review.Manager.MedicalSession.Observations);
                observations.text = text;
                float height = Math.Max(399, observations.preferredHeight + 16);
                observations.rectTransform.sizeDelta = new Vector2(400, height);
                ((RectTransform)body).sizeDelta = new Vector2(418, height);
                return text;
            });
        }
        void DrawHelpConversation()
        {
            Box(content, "Help conversation", 26, 191, 560, 549, Background, true, true);
            Label(content, "Help conversation title", "112 · llamada simulada", 46, 213, 514, 40, 25, Ink, true);
            var helper = CaseHelp;
            if (helper == null) { Label(content, "Help unavailable", "La comunicación no está disponible.", 46, 278, 514, 92, 21, Soft); return; }
            var stage = helper.Status;
            live.Add(() => { if (helper.Status != stage) redraw = true; });
            if (stage == ClinicalHelpStage.Idle)
            {
                Label(content, "Help availability", "Puedes pedir ayuda ahora. No necesitas esperar a una medición ni completar la entrevista.", 46, 278, 514, 113, 22, Soft);
                Button(content, "Request simulated call", "Solicitar ayuda al 112", 44, 424, 520, 69, () => { helper.RequestCall(false); redraw = true; }, true);
                Button(content, "Delegate simulated call", "Pedir al compañero que avise", 44, 512, 520, 69, () => { helper.RequestCall(true); redraw = true; });
                Label(content, "Simulated companion note", "La colaboración se representa mediante una conversación fuera de escena.", 46, 616, 514, 85, 18, Soft);
                return;
            }
            var line = Label(content, "Operator line", "", 46, 278, 514, 136, 21, Soft);
            Bind(line, () => helper.CurrentLine);
            if (stage == ClinicalHelpStage.AwaitingSituation || stage == ClinicalHelpStage.Confirmed)
            {
                var scroll = ScrollArea(46, 423, 514, 183, 183);
                var summary = Label(scroll, "Communication summary", "", 0, 0, 479, 183, 19, Soft);
                Bind(summary, () =>
                {
                    string text = helper.SituationSummary; summary.text = text;
                    float height = Mathf.Max(183, summary.preferredHeight + 12);
                    summary.rectTransform.sizeDelta = new Vector2(479, height);
                    ((RectTransform)scroll).sizeDelta = new Vector2(497, height); return text;
                });
            }
            if (stage == ClinicalHelpStage.AwaitingLocation)
                Button(content, "Communicate location", "Comunicar ubicación", 44, 634, 520, 66, () => { helper.CommunicateLocation(); redraw = true; }, true);
            else if (stage == ClinicalHelpStage.AwaitingSituation)
                Button(content, "Communicate observations", "Comunicar lo que he observado", 44, 634, 520, 66, () => { helper.CommunicateSituation(); redraw = true; }, true);
            else if (stage == ClinicalHelpStage.AwaitingConfirmation)
                Button(content, "Receive help confirmation", "Escuchar la confirmación", 44, 634, 520, 66, () => { helper.ConfirmOperator(); redraw = true; }, true);
            else if (stage == ClinicalHelpStage.Confirmed)
                Button(content, "Communicate handover", "Comunicar el relevo", 44, 634, 520, 66, () => { helper.PerformHandover(); redraw = true; }, true);
            else if (stage == ClinicalHelpStage.HandoverCompleted)
                Button(content, "Review completed practice", "Finalizar y revisar la práctica", 44, 634, 520, 66, () => Navigate(ExperiencePage.Finish), true);
        }
        void AskForGuidance()
        {
            if (!Review.Procedures.TrainingMode) return;
            var objective = Review.Manager.MedicalSession.ObjectiveProgress.FirstOrDefault(x => x.result != ObjectiveResult.AchievedIndependently && x.result != ObjectiveResult.AchievedWithGuidance);
            if (objective != null)
            {
                Review.Manager.PerformClinical((runtime, time) => runtime.RecordGuidance(objective.objectiveId, time));
                guidanceText = ClinicalCaseDebrief.ObjectiveTitle(objective.objectiveId) + "\n\n" + ClinicalCaseDebrief.GuidanceFor(objective.objectiveId);
            }
            else guidanceText = "Ya has registrado los objetivos. Puedes revisar la información y completar el entrenamiento.";
            guidanceDrawer = true; observedDrawer = false; redraw = true;
        }
        void DrawGuidance()
        {
            Box(content, "Guidance panel", 940, 191, 478, 394, Background, true, true);
            Label(content, "Guidance title", "ORIENTACIÓN SOLICITADA", 960, 213, 438, 39, 20, Accent, true);
            Label(content, "Guidance text", guidanceText, 960, 277, 432, 208, 21, Soft);
            Button(content, "Close guidance", "Continuar la práctica", 960, 510, 438, 52, () => { guidanceDrawer = false; redraw = true; });
        }
        static string IntentLabel(DialogueIntent intent)
        {
            switch (intent)
            {
                case DialogueIntent.GREETING: return "Hola, estoy aquí para ayudarte.";
                case DialogueIntent.MAIN_SYMPTOM: return "¿Qué te pasa?";
                case DialogueIntent.SYMPTOM_DESCRIPTION: return "¿Cómo es el mareo?";
                case DialogueIntent.ONSET: return "¿Cuándo empezó?";
                case DialogueIntent.LOSS_OF_CONSCIOUSNESS: return "¿Has perdido el conocimiento?";
                case DialogueIntent.CHEST_PAIN: return "¿Te duele el pecho?";
                case DialogueIntent.BREATHING_DIFFICULTY: return "¿Te cuesta respirar?";
                case DialogueIntent.PALPITATIONS: return "¿Notas palpitaciones?";
                case DialogueIntent.CARDIAC_HISTORY: return "¿Tienes antecedentes del corazón?";
                case DialogueIntent.MEDICATION: return "¿Tomas alguna medicación?";
                case DialogueIntent.FOOD_DRINK: return "¿Has comido y bebido?";
                case DialogueIntent.CONSENT_HELP: return "¿Puedo ayudarte?";
                default: return "¿Cómo te encuentras ahora?";
            }
        }
        void DrawV2Results()
        {
            if (!Review.HasResult) { Navigate(ExperiencePage.Welcome); return; }
            var runtime = Review.Manager.MedicalSession;
            PageTitle("Revisión del entrenamiento", "Tu práctica, paso a paso", "Objetivos formativos · " + TimeLabel(runtime.Elapsed));
            var tabs = new[] { "Objetivos", "Cronología", "Información obtenida", "Próximo intento" };
            resultTab = Mathf.Clamp(resultTab, 0, tabs.Length - 1);
            for (int i = 0; i < tabs.Length; i++) { int tab = i; Button(content, "Results tab " + i, tabs[i], 48 + i * 340, 307, 324, 54, () => { resultTab = tab; redraw = true; }, resultTab == i); }
            var body = ScrollArea(48, 390, 1344, 351, 351); float y = 0;
            Action<string> line = text =>
            {
                var label = Label(body, "Formative result", text, 10, y, 1289, 1000, 23, Soft);
                float h = label.preferredHeight + 24; label.rectTransform.sizeDelta = new Vector2(1289, h); y += h;
            };
            if (resultTab == 0)
                foreach (var objective in runtime.ObjectiveProgress) line(ClinicalCaseDebrief.ExplainObjective(runtime, objective));
            else if (resultTab == 1)
                foreach (var entry in runtime.ClinicalEvents.Where(x => x.eventType != "ObservationAdded" && !(x.eventType == "ActionSubmitted" && x.result == "Accepted")))
                    line(TimeLabel(entry.simulationTime) + " · " + ClinicalCaseDebrief.EventDescription(entry));
            else if (resultTab == 2) line(ClinicalObservationText.Format(runtime.Observations));
            else line(ClinicalCaseDebrief.Reflection(runtime));
            ((RectTransform)body).sizeDelta = new Vector2(1326, Math.Max(351, y));
            Button(content, "Repeat training", "Repetir entrenamiento", 48, 764, 350, 56, Repeat, true);
            Button(content, "Return catalog", "Elegir otro entrenamiento", 418, 764, 424, 56, () => Browse(SelectedEnvironment));
            Button(content, "Export results", "Guardar informe", 862, 764, 255, 56, () => { try { Review.ExportResult(); notice = "Informe guardado."; } catch (Exception e) { notice = "No se pudo guardar el informe."; Debug.LogException(e); } redraw = true; });
            Button(content, "Results home", "Inicio", 1137, 764, 255, 56, () => Navigate(ExperiencePage.Welcome));
            Label(content, "Export notice", notice, 50, 735, 1280, 27, 16, Accent);
        }
        static string ObjectiveLabel(string id) => ClinicalCaseDebrief.ObjectiveTitle(id);
        static string ObjectiveStatus(ObjectiveResult result) => ClinicalCaseDebrief.ResultLabel(result);
    }
}
