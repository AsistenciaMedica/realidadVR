using EmergencyVR.Dialogue;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EmergencyVR.UI
{
    /// <summary>
    /// Desktop world interaction for clinical v2 cases: look at the patient and act with keys instead of menus.
    /// Every key reuses the same clinical entry points as the buttons, so evaluation records are identical.
    /// </summary>
    public sealed partial class TrainingExperience
    {
        const float FocusDistance = 2.3f;
        DesktopVoiceInput voice;

        bool FocusingPatient
        {
            get
            {
                var body = CaseBody;
                if (body == null || !body.IsActive || viewer == null) return false;
                var eye = viewer.transform.position;
                foreach (var point in new[] { body.HeadPosition, body.PelvisPosition, Vector3.Lerp(body.PelvisPosition, body.HeadPosition, .5f) })
                {
                    var to = point - eye;
                    if (to.magnitude <= FocusDistance && Vector3.Angle(viewer.transform.forward, to) < 28) return true;
                }
                return false;
            }
        }

        void UpdateWorldKeys()
        {
            if (!IsDesktop || Page != ExperiencePage.Training || !UsesObservedData || !Review.Manager.AcceptsInput) return;
            var keys = Keyboard.current; if (keys == null) return;
            if (voice == null) voice = gameObject.AddComponent<DesktopVoiceInput>();
            voice.Configure(Review.Manager, Page == ExperiencePage.Training && keys.mKey.isPressed);
            if (keys.tKey.wasPressedThisFrame) { ReleaseAssistance(); callDrawer = !callDrawer; actionDrawer = dialogueDrawer = false; redraw = true; }
            if (!FocusingPatient) return;
            if (keys.eKey.wasPressedThisFrame) { ReleaseAssistance(); dialogueDrawer = !dialogueDrawer; actionDrawer = callDrawer = false; redraw = true; }
            if (keys.gKey.wasPressedThisFrame) Review.Submit("AssessResponsiveness");
            if (keys.bKey.wasPressedThisFrame) Review.Submit("ObserveBreathing");
            if (keys.hKey.wasPressedThisFrame) Review.Submit("ReassessPatient");
        }

        /// <summary>Crosshair and the contextual actions available while looking at the patient.</summary>
        void DrawWorldPrompt()
        {
            if (!IsDesktop) return;
            var dot = Box(content, "Crosshair", 716, 446, 8, 8, new Color(1, 1, 1, .75f), true, false);
            var prompt = Label(content, "World prompt", "", 360, 700, 720, 50, 15, Ink, true);
            prompt.alignment = TextAnchor.UpperCenter;
            var shadow = prompt.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .8f); shadow.effectDistance = new Vector2(1.5f, -1.5f);
            var listening = Label(content, "Voice status", "", 360, 664, 720, 30, 17, Accent, true);
            listening.alignment = TextAnchor.UpperCenter;
            live.Add(() =>
            {
                bool drawer = actionDrawer || dialogueDrawer || callDrawer;
                dot.gameObject.SetActive(!drawer);
                var body = CaseBody;
                prompt.text = drawer || body == null || body.IsAssisting ? "" : FocusingPatient ?
                    "[E] Hablar   [G] Comprobar respuesta   [B] Observar respiración\n[F] Mantener: ayudar a tumbarse   [H] Reevaluar   [T] Teléfono   [M] Hablar con tu voz" :
                    "[T] Teléfono · Mira a Daniel para ver qué puedes hacer";
                listening.text = voice == null ? "" : voice.Status;
            });
        }
    }
}
