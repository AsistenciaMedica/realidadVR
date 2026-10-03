using System;
using System.Linq;
using EmergencyVR.Dialogue;
using EmergencyVR.Medical;
using UnityEngine;
using UnityEngine.UI;

namespace EmergencyVR.UI
{
    public sealed partial class TrainingExperience
    {
        // Consultation content keeps its reading size. Long copy expands inside a scroll region
        // instead of borrowing the fixed, smaller rectangles from the desktop drawers.
        Transform VrDrawer(string name, string heading, float viewportHeight = 446)
        {
            Box(content, name, 20, 197, 560, 595, Background, true, true);
            Label(content, "Drawer heading", heading, 26, 210, 548, 44, 30, Ink, true);
            return ScrollArea(26, 268, 548, viewportHeight, viewportHeight);
        }

        float VrTextHeight(Text label, float minimum = 0)
        {
            return Mathf.Max(minimum, Mathf.Ceil(label.preferredHeight) + 16);
        }

        Button VrStackButton(Transform parent, string name, string title, ref float y, Action action, bool primary = false)
        {
            var button = Button(parent, name, title, 0, y, 512, 80, action, primary);
            var label = button.GetComponentInChildren<Text>(); label.fontSize = 28;
            float height = VrTextHeight(label, 72);
            ((RectTransform)button.transform).sizeDelta = new Vector2(512, height);
            label.rectTransform.sizeDelta = new Vector2(484, height);
            y += height + 12;
            return button;
        }

        void VrDrawerPaging(string name, int page, int pages, Action previous, Action next)
        {
            Button(content, "Previous " + name, "←", 26, 729, 100, 58, previous).interactable = page > 0;
            var number = Label(content, "Drawer page", (page + 1) + " / " + pages, 143, 740, 314, 37, 28, Soft);
            number.alignment = TextAnchor.MiddleCenter;
            Button(content, "Next " + name, "→", 474, 729, 100, 58, next).interactable = page < pages - 1;
        }

        void DrawVrActions()
        {
            var body = VrDrawer("Action drawer", "Decisiones y procedimientos");
            var ids = Review.ActionIds; int pages = Mathf.Max(1, Mathf.CeilToInt(ids.Length / 5f));
            actionPage = Mathf.Clamp(actionPage, 0, pages - 1); float y = 0;
            for (int i = 0; i < 5; i++)
            {
                int index = actionPage * 5 + i; if (index >= ids.Length) break;
                string id = ids[index]; bool physical = Review.Procedures.IsPhysicalAction(id);
                string title = Review.ActionLabel(id) + (physical ? "\nUtiliza el equipo" : "  ✓");
                var button = VrStackButton(body, "Action " + id, title, ref y, () => Review.Submit(id));
                button.interactable = !physical;
                Bind(button.GetComponentInChildren<Text>(), () => Review.ActionLabel(id) +
                    (physical ? "\nUtiliza el equipo" : Review.Manager.MedicalSession?.Completed.Contains(id) == true ? "  ✓" : ""));
            }
            ((RectTransform)body).sizeDelta = new Vector2(530, Mathf.Max(446, y));
            VrDrawerPaging("actions", actionPage, pages, () => { actionPage--; redraw = true; }, () => { actionPage++; redraw = true; });
        }

        void DrawVrPatient()
        {
            var body = VrDrawer("Patient drawer", "Hablar y escuchar", 518);
            var details = Label(body, "Patient details", PatientDetails(), 0, 0, 512, 150, 28, Soft);
            // Reserve one extra line for the age once it is obtained during an assessment attempt.
            float detailsHeight = VrTextHeight(details) + 34; details.rectTransform.sizeDelta = new Vector2(512, detailsHeight);
            Bind(details, PatientDetails);
            var questions = new[] { PatientQuestion.NameAndAge, PatientQuestion.WhatHappened, PatientQuestion.History, PatientQuestion.Witness };
            var names = new[] { "Patient name", "Patient situation", "Patient history", "Witness account" };
            var titles = new[] { "Nombre y edad", "¿Qué ha ocurrido?", "Antecedentes", "Escuchar al testigo" };
            for (int i = 0; i < questions.Length; i++)
            {
                var question = questions[i];
                var button = Button(body, names[i], titles[i], i % 2 * 262, detailsHeight + 12 + i / 2 * 96, 250, 84,
                    () => { PatientConversation.Ask(question); nextRefresh = 0; }, question == PatientQuestion.Witness && !PatientConversation.CanAskPatient);
                button.GetComponentInChildren<Text>().fontSize = 28;
                Action update = () =>
                {
                    button.interactable = question == PatientQuestion.Witness ? PatientConversation.HasWitness : PatientConversation.CanAskPatient;
                    if (question != PatientQuestion.Witness) return;
                    bool emphasize = PatientConversation.HasWitness && !PatientConversation.CanAskPatient;
                    var colors = button.colors; colors.normalColor = emphasize ? Accent : Raised;
                    colors.highlightedColor = emphasize ? new Color32(129, 240, 214, 255) : new Color32(49, 80, 96, 255);
                    colors.selectedColor = colors.highlightedColor; button.colors = colors;
                    button.GetComponentInChildren<Text>().color = emphasize ? Background : Ink;
                };
                live.Add(update); update();
            }
            float transcriptY = detailsHeight + 217;
            var transcript = Label(body, "Patient conversation", "", 0, transcriptY, 512, 200, 28, Soft);
            Action refresh = () =>
            {
                transcript.text = PatientConversation.Transcript;
                if (string.IsNullOrWhiteSpace(transcript.text)) transcript.text = PatientConversation.CanAskPatient
                    ? "Pregunta y escucha. Aquí se conservarán las respuestas.\n\nLas mediciones se consultan en los instrumentos."
                    : "El paciente no puede responder. Escucha al testigo y observa al paciente.";
                float height = VrTextHeight(transcript, 160);
                transcript.rectTransform.sizeDelta = new Vector2(512, height);
                ((RectTransform)body).sizeDelta = new Vector2(530, Mathf.Max(518, transcriptY + height));
            };
            live.Add(refresh); refresh();
        }

        void DrawVrV2Actions()
        {
            var body = VrDrawer("Action drawer", "Valorar y acompañar", 518); float y = 0;
            VrStackButton(body, "Action AssessResponsiveness", "Comprobar respuesta", ref y, () => Review.Submit("AssessResponsiveness"));
            VrStackButton(body, "Action ObserveBreathing", "Observar respiración", ref y, () => Review.Submit("ObserveBreathing"));
            VrStackButton(body, "Action ReassessPatient", "Reevaluar al paciente", ref y, () => Review.Submit("ReassessPatient"));
            var support = VrStackButton(body, "Maintain supported position", "Mantener un apoyo seguro", ref y, () => CaseBody?.MaintainSupportedPosition());
            live.Add(() => support.interactable = CaseBody != null && CaseBody.IsActive && !CaseBody.IsAssisting);
            var assist = VrStackButton(body, "Action AssistPatient", "Ayudar a tumbarse\nMantén pulsado", ref y, () => { });
            assist.gameObject.AddComponent<ClinicalAssistanceHold>().Configure(() => CaseBody);
            var assistanceLabel = assist.GetComponentInChildren<Text>();
            live.Add(() =>
            {
                assist.interactable = CaseBody != null && (CaseBody.CanAssist || CaseBody.IsAssisting && !CaseBody.IsAttemptingRise);
                assistanceLabel.text = (CaseBody != null && CaseBody.IsStanding ? "Recuperar el apoyo" : "Ayudar a tumbarse") + "\nMantén pulsado";
            });
            var cancel = VrStackButton(body, "Cancel assistance", "Cancelar asistencia", ref y, () => CaseBody?.CancelAssistance());
            live.Add(() => cancel.interactable = CaseBody != null && CaseBody.IsAssisting);
            var instruction = Label(body, "Assistance instruction", "", 0, y, 512, 200, 28, Soft);
            Action refresh = () =>
            {
                instruction.text = CaseBody == null ? "Acércate al paciente para acompañarlo." : CaseBody.Instruction +
                    (CaseBody.IsAssisting ? "  " + Mathf.RoundToInt(CaseBody.Progress * 100) + " %" : "");
                float height = VrTextHeight(instruction);
                instruction.rectTransform.sizeDelta = new Vector2(512, height);
                ((RectTransform)body).sizeDelta = new Vector2(530, Mathf.Max(518, y + height));
            };
            live.Add(refresh); refresh();
        }

        void DrawVrIntentDialogue()
        {
            var intents = (DialogueIntent[])Enum.GetValues(typeof(DialogueIntent));
            int pages = Mathf.CeilToInt(intents.Length / 5f); dialoguePage = Mathf.Clamp(dialoguePage, 0, pages - 1);
            var body = VrDrawer("Dialogue drawer", "Escucha a la persona"); float y = 0;
            for (int i = 0; i < 5; i++)
            {
                int index = dialoguePage * 5 + i; if (index >= intents.Length) break;
                var intent = intents[index];
                VrStackButton(body, "Intent " + intent, IntentLabel(intent), ref y, () => Review.Manager.Dialogue.Ask(intent));
            }
            ((RectTransform)body).sizeDelta = new Vector2(530, Mathf.Max(446, y));
            VrDrawerPaging("dialogue", dialoguePage, pages, () => { dialoguePage--; redraw = true; }, () => { dialoguePage++; redraw = true; });
        }

        void DrawVrPhone()
        {
            var phone = Phone;
            if (phone == null) { Label(content, "Phone unavailable", "La comunicación no está disponible.", 26, 220, 548, 150, 30, Soft); return; }
            phoneStage = phone.Stage; phoneQuestion = phone.Current;
            string subtitleAtRender = phone.Subtitle; int optionCount = phone.Current?.Options.Count ?? 0;
            live.Add(() => { if (phone.Stage != phoneStage || phone.Current != phoneQuestion || phone.Subtitle != subtitleAtRender || (phone.Current?.Options.Count ?? 0) != optionCount) redraw = true; });
            Box(content, "Phone body", 20, 197, 560, 595, Background, true, true);
            Label(content, "Phone title", phone.InCall ? "112 · LLAMADA SIMULADA" : "TELÉFONO · SIMULACIÓN", 26, 210, 548, 40, 28, Accent, true);
            if (phone.Stage == PhoneCallStage.Idle)
            {
                var display = Label(content, "Phone display", "", 26, 257, 548, 46, 38, Ink, true); display.alignment = TextAnchor.MiddleCenter;
                Bind(display, () => phone.Dialed.Length == 0 ? " " : phone.Dialed);
                var noticeText = Label(content, "Phone notice", "", 26, 309, 548, 84, 24, Amber);
                Bind(noticeText, () => phone.Notice);
                const string keys = "123456789";
                for (int i = 0; i < 9; i++)
                {
                    char key = keys[i];
                    Button(content, "Phone key " + key, key.ToString(), 26 + i % 3 * 186, 401 + i / 3 * 71, 176, 61, () => phone.Press(key));
                }
                Button(content, "Phone erase", "Borrar", 26, 614, 176, 61, phone.Erase);
                Button(content, "Phone key 0", "0", 212, 614, 176, 61, () => phone.Press('0'));
                Button(content, "Phone call", "Llamar", 398, 614, 176, 61, phone.Dial, true);
                Button(content, "Delegate simulated call", "Pedir a un compañero que llame", 26, 696, 548, 87, phone.Delegate);
                return;
            }
            float viewport = phone.InCall ? 443 : 518;
            var body = ScrollArea(26, 266, 548, viewport, viewport);
            var subtitle = Label(body, "Phone subtitle", phone.Subtitle, 0, 0, 512, 200, 28, Ink);
            float y = VrTextHeight(subtitle); subtitle.rectTransform.sizeDelta = new Vector2(512, y); y += 12;
            if (phone.Current != null)
            {
                for (int i = 0; i < phone.Current.Options.Count; i++)
                {
                    int index = i;
                    VrStackButton(body, "Call option " + i, phone.Current.Options[i].Text, ref y, () => phone.Choose(index));
                }
            }
            else if (phone.Stage == PhoneCallStage.EnRoute)
            {
                var waiting = Label(body, "Phone waiting", "La ambulancia está en camino. Quédate con Daniel, vuelve a valorarle y avisa si empeora.", 0, y, 512, 200, 28, Soft);
                float height = VrTextHeight(waiting); waiting.rectTransform.sizeDelta = new Vector2(512, height); y += height;
            }
            else if (phone.Stage == PhoneCallStage.Done)
                VrStackButton(body, "Review completed practice", "Finalizar y revisar la práctica", ref y, () => Navigate(ExperiencePage.Finish), true);
            ((RectTransform)body).sizeDelta = new Vector2(530, Mathf.Max(viewport, y));
            if (phone.InCall) Button(content, "Phone hang up", "Colgar", 26, 729, 548, 58, phone.HangUp);
        }
    }
}
