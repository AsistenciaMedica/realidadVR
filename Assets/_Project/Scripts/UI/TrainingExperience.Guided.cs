using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EmergencyVR.UI
{
    public sealed partial class TrainingExperience
    {
        GuidedPracticeDirector guide;
        Outline guideOutline;
        bool assistKeyHeld;

        /// <summary>Desktop: hold F beside Daniel to lower him (or recover support); releasing pauses the movement.</summary>
        void UpdateAssistKey()
        {
            var body = CaseBody;
            bool held = IsDesktop && Page == ExperiencePage.Training && Keyboard.current != null && Keyboard.current.fKey.isPressed &&
                body != null && body.IsActive && !body.IsAttemptingRise;
            if (held == assistKeyHeld) return;
            assistKeyHeld = held;
            if (body == null) return;
            if (held && !body.IsAssisting && !body.BeginAssistance()) { nextRefresh = 0; return; }
            body.SetSupportHeld(held);
        }

        /// <summary>Large progress bar while an assisted transition is running.</summary>
        void DrawAssistProgress()
        {
            var panel = Box(content, "Assist progress panel", 470, 560, 500, 110, Background, true, false);
            var title = Label(content, "Assist progress title", "", 494, 574, 452, 30, 20, Ink, true);
            var track = Box(content, "Assist progress track", 494, 612, 452, 18, Raised, true, false);
            var fill = Box(content, "Assist progress fill", 494, 612, 452, 18, Accent, true, false);
            var hint = Label(content, "Assist progress hint", "", 494, 636, 452, 26, 16, Soft);
            live.Add(() =>
            {
                var body = CaseBody;
                bool visible = body != null && body.IsAssisting;
                foreach (var part in new Graphic[] { panel, title, track, fill, hint }) part.gameObject.SetActive(visible);
                if (!visible) return;
                float progress = Mathf.Clamp01(body.Progress);
                fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(18, 452 * progress), 18);
                title.text = (body.IsStanding ? "Recuperando el apoyo" : body.IsReturning ? "Volviendo al banco" : "Ayudando a Daniel a tumbarse") +
                    " · " + Mathf.RoundToInt(progress * 100) + " %";
                hint.text = IsDesktop ? "Mantén pulsada F. Si la sueltas, el movimiento se detiene." : "Mantén pulsado el botón de asistencia.";
            });
        }

        void CreateGuide()
        {
            guide = gameObject.AddComponent<GuidedPracticeDirector>();
            guide.Initialize(Review, viewer);
        }

        /// <summary>Step card for guided practice; evaluation never draws it.</summary>
        void DrawGuideCard()
        {
            if (guide == null || !Review.Procedures.TrainingMode) return;
            var card = Box(content, "Guide card", 940, 191, 478, 190, Background, true, false);
            var stripe = Box(content, "Guide stripe", 940, 191, 8, 190, Accent, false);
            var step = Label(content, "Guide step", "", 966, 205, 430, 26, 17, Accent, true);
            var title = Label(content, "Guide title", "", 966, 233, 430, 38, 25, Ink, true);
            var detail = Label(content, "Guide detail", "", 966, 276, 430, 100, 18, Soft);
            live.Add(() =>
            {
                var current = guide.Current;
                bool visible = current != null && !observedDrawer && !guidanceDrawer;
                card.gameObject.SetActive(visible); stripe.gameObject.SetActive(visible);
                step.gameObject.SetActive(visible); title.gameObject.SetActive(visible); detail.gameObject.SetActive(visible);
                if (!visible) return;
                bool alert = current.Id == "prevent-rise";
                stripe.color = alert ? Amber : Accent;
                step.color = alert ? Amber : Accent;
                step.text = alert ? "ATENCIÓN · SEGURIDAD" : current.Id == "finish" ? "PRÁCTICA GUIADA" : $"PASO {guide.StepNumber} DE {guide.StepCount}";
                title.text = current.Title;
                detail.text = current.Detail;
            });
        }

        /// <summary>Pulses an outline on the one control the current step needs.</summary>
        void UpdateGuideHighlight()
        {
            Button target = null;
            var current = guide == null || Page != ExperiencePage.Training ? null : guide.Current;
            if (current?.Controls != null && pageRoot != null)
                foreach (var name in current.Controls)
                {
                    var found = Find(pageRoot.transform, name);
                    if (found == null || !found.gameObject.activeInHierarchy) continue;
                    var button = found.GetComponent<Button>();
                    if (button == null || !button.interactable) continue;
                    target = button; break;
                }
            if (guideOutline != null && (target == null || guideOutline.gameObject != target.gameObject))
            { Destroy(guideOutline); guideOutline = null; }
            if (target == null) return;
            if (guideOutline == null)
            {
                guideOutline = target.gameObject.AddComponent<Outline>();
                guideOutline.useGraphicAlpha = false;
            }
            float pulse = .5f + Mathf.Sin(Time.unscaledTime * 5) * .5f;
            guideOutline.effectColor = Color.Lerp(new Color(Accent.r, Accent.g, Accent.b, .35f), new Color(1, 1, 1, 1), pulse * .6f);
            guideOutline.effectDistance = Vector2.one * Mathf.Lerp(3, 6, pulse);
        }

        static Transform Find(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var nested = Find(child, name);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}
