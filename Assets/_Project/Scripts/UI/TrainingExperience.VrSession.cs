using System.Linq;
using EmergencyVR.Dialogue;
using EmergencyVR.Medical;
using EmergencyVR.Medical.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace EmergencyVR.UI
{
    public sealed partial class TrainingExperience
    {
        Canvas vrSubtitleCanvas;
        Text vrSubtitle;
        bool suppressVrSubtitles;

        void DrawVrTraining()
        {
            Box(content, "Side consultation panel", 0, 0, 600, 900, Background, true, true);
            Label(content, "Session status", Review.Selected.medical?.patientIdentity?.displayName ?? "Entrenamiento", 26, 22, 548, 43, 34, Ink, true);
            var clock = Label(content, "Session clock", "", 26, 73, 548, 34, 26, Accent);
            Bind(clock, () => "Tiempo " + TimeLabel(Review.Manager.ElapsedSeconds) + " · panel de consulta");
            if (UsesObservedData)
            {
                VrSessionTab("Open actions", "Valorar", 26, 142, () => { ReleaseAssistance(); actionDrawer = !actionDrawer; dialogueDrawer = callDrawer = observedDrawer = false; redraw = true; });
                VrSessionTab("Open dialogue", "Hablar", 175, 128, () => { ReleaseAssistance(); dialogueDrawer = !dialogueDrawer; actionDrawer = callDrawer = observedDrawer = false; redraw = true; });
                VrSessionTab("Open help conversation", "Teléfono", 310, 151, () => { ReleaseAssistance(); callDrawer = !callDrawer; actionDrawer = dialogueDrawer = observedDrawer = false; redraw = true; });
                VrSessionTab("Toggle observations", "Ficha", 468, 106, () => { observedDrawer = !observedDrawer; actionDrawer = dialogueDrawer = callDrawer = false; redraw = true; });
                if (actionDrawer) DrawVrV2Actions();
                else if (dialogueDrawer) DrawVrIntentDialogue();
                else if (callDrawer) DrawVrPhone();
                else if (observedDrawer) DrawVrObservedHistory();
                else
                {
                    var title = Label(content, "Guide title", "", 26, 222, 548, 102, 34, Ink, true);
                    var detail = Label(content, "Consultation guidance", "", 26, 348, 548, 244, 29, Soft);
                    Bind(title, () => Review.Procedures.TrainingMode && guide?.Current != null ? guide.Current.Title : "Observa y acompaña");
                    Bind(detail, () => Review.Procedures.TrainingMode && guide?.Current != null ? guide.Current.Detail :
                        "Las preguntas, valoraciones y llamadas están en las pestañas de arriba.");
                    if (Review.Procedures.TrainingMode)
                        Button(content, "Clinical guidance", "Pedir orientación", 26, 620, 548, 67, AskForGuidance);
                }
                if (guidanceDrawer) DrawVrGuidance();
                if (CaseBody != null)
                {
                    var prevent = Button(content, "Prevent rise attempt", "Pedir que espere con apoyo", 26, 714, 548, 59, () => CaseBody.PreventRiseAttempt(), true);
                    live.Add(() => prevent.gameObject.SetActive(CaseBody.CanPreventRise));
                    prevent.gameObject.SetActive(CaseBody.CanPreventRise);
                }
            }
            else
            {
                Button(content, "Open actions", "Acciones", 26, 119, 265, 61, () => { actionDrawer = !actionDrawer; patientDrawer = false; redraw = true; });
                Button(content, "Open patient", "Conversar", 307, 119, 267, 61, () => { patientDrawer = !patientDrawer; actionDrawer = false; redraw = true; });
                if (actionDrawer) DrawVrActions();
                else if (patientDrawer) DrawVrPatient();
                else
                {
                    Label(content, "Consultation guidance", "Observa al paciente y utiliza el equipo.\n\nLas constantes se consultan en la pantalla de cada aparato después de medir.\n\nEste panel queda a tu lado para mantener la vista libre.", 26, 222, 548, 438, 30, Soft);
                    var acquired = Label(content, "Acquired reading notice", "", 26, 683, 548, 80, 25, Accent);
                    Bind(acquired, () => readings.Count == 0 ? "Aún no has medido constantes." : "Lecturas disponibles en los instrumentos.");
                }
            }
            Button(content, "Pause training", "Pausa", 26, 810, 265, 65, () => Navigate(ExperiencePage.Pause));
            Button(content, "Finish training", "Finalizar", 307, 810, 267, 65, () => Navigate(ExperiencePage.Finish));
        }

        void VrSessionTab(string name, string title, float x, float width, System.Action action)
        {
            var button = Button(content, name, title, x, 119, width, 61, action);
            var label = button.GetComponentInChildren<Text>().rectTransform;
            label.anchoredPosition = new Vector2(10, 0);
            label.sizeDelta = new Vector2(width - 20, 61);
        }

        void DrawVrObservedHistory()
        {
            Label(content, "Observed heading", "INFORMACIÓN OBTENIDA", 26, 211, 548, 46, 28, Ink, true);
            var body = ScrollArea(26, 282, 548, 489, 489);
            var observations = Label(body, "Observed patient data", "", 0, 0, 494, 489, 28, Soft);
            Bind(observations, () =>
            {
                string text = ClinicalObservationText.Format(Review.Manager.MedicalSession.Observations);
                observations.text = text;
                float height = Mathf.Max(489, observations.preferredHeight + 20);
                observations.rectTransform.sizeDelta = new Vector2(494, height);
                ((RectTransform)body).sizeDelta = new Vector2(530, height);
                return text;
            });
        }

        void DrawVrGuidance()
        {
            Box(content, "Guidance panel", 20, 193, 560, 596, Background, true, true);
            Label(content, "Guidance title", "ORIENTACIÓN SOLICITADA", 36, 217, 524, 75, 29, Accent, true);
            var body = ScrollArea(36, 302, 524, 366, 366);
            var label = Label(body, "Guidance text", guidanceText, 0, 0, 470, 1000, 29, Soft);
            float height = Mathf.Max(366, label.preferredHeight + 16);
            label.rectTransform.sizeDelta = new Vector2(470, height); ((RectTransform)body).sizeDelta = new Vector2(506, height);
            Button(content, "Close guidance", "Continuar la práctica", 36, 700, 524, 64, () => { guidanceDrawer = false; redraw = true; });
        }

        void UpdateVrSubtitles()
        {
            bool visible = !suppressVrSubtitles && !IsDesktop && Page == ExperiencePage.Training && Review.Manager.IsRunning;
            if (!visible) { if (vrSubtitleCanvas != null) vrSubtitleCanvas.gameObject.SetActive(false); return; }
            if (vrSubtitleCanvas == null)
            {
                var root = new GameObject("Patient subtitles", typeof(RectTransform), typeof(Canvas)); root.layer = 5; root.transform.SetParent(transform, false);
                vrSubtitleCanvas = root.GetComponent<Canvas>(); vrSubtitleCanvas.renderMode = RenderMode.WorldSpace; vrSubtitleCanvas.worldCamera = viewer;
                var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(720, 155); rect.localScale = Vector3.one * .0012f;
                Box(root.transform, "Subtitle background", 0, 0, 720, 155, new Color(Background.r, Background.g, Background.b, .88f));
                vrSubtitle = Label(root.transform, "Patient subtitle", "", 18, 12, 684, 133, 28, Ink);
            }
            string text = DisplayClinicalText(Review.Manager.Feedback);
            if (Phone != null && Phone.InCall) text = Phone.Subtitle;
            else if (UsesObservedData && Review.Manager.Dialogue?.LastResponse != null)
                text = Review.Manager.Dialogue.LastResponse.subtitle ?? Review.Manager.Dialogue.LastResponse.text;
            else if (PatientConversation?.Lines.Length > 0) text = PatientConversation.Lines.Last().ToString();
            text = (text ?? "").Replace('\n', ' ').Replace('\r', ' ');
            if (text.Length > 180) text = text.Substring(0, 177).TrimEnd() + "…";
            vrSubtitle.text = text;
            var head = Review.Procedures.Visuals.Rig.head;
            var position = head.position + Vector3.up * .38f;
            vrSubtitleCanvas.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - viewer.transform.position, Vector3.up));
            vrSubtitleCanvas.gameObject.SetActive(true);
        }

        void DrawVrTutorial()
        {
            Box(content, "Tutorial card", 36, 90, 928, 570, Background, true, true);
            Label(content, "Tutorial title", "TUS PRIMEROS 30 SEGUNDOS", 68, 124, 864, 59, 37, Ink, true);
            var remaining = Label(content, "Tutorial clock", "", 68, 204, 864, 49, 29, Accent);
            Bind(remaining, () => "Orientación de controles · " + Mathf.CeilToInt(tutorial?.RemainingSeconds ?? 0) + " s");
            var step = tutorial?.Step ?? XRIntroStep.Point;
            string instruction = step == XRIntroStep.Point ? "Apunta con un mando al botón y pulsa el gatillo." :
                step == XRIntroStep.Grab ? "Coge el cubo verde con Grip, el botón lateral. Después suéltalo." :
                step == XRIntroStep.Teleport ? "Empuja el stick hacia delante, apunta al cuadrado verde del suelo y suelta para viajar." :
                "Pulsa B o Y para traer el menú delante de ti. Puedes usar cualquiera de los mandos.";
            Label(content, "Tutorial instruction", instruction, 68, 289, 864, 193, 36, Ink);
            if (step == XRIntroStep.Point) Button(content, "Tutorial target", "Pulsa aquí con el gatillo", 68, 521, 864, 95, () => tutorial?.ConfirmPointing(), true);
            else Label(content, "Tutorial step", "Paso " + ((int)step + 1) + " de 4", 68, 545, 864, 50, 30, Accent);
            Button(content, "Skip tutorial", "Saltar tutorial", 36, 718, 928, 83, () => tutorial?.Skip());
            Label(content, "Tutorial synthetic voice", "Voz sintética · orientación opcional", 36, 837, 928, 38, 25, Soft);
        }
    }
}
