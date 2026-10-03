using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EmergencyVR.Desktop;
using EmergencyVR.Environment;
using EmergencyVR.Medical.Interaction;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace EmergencyVR.UI
{
    public sealed partial class TrainingExperience
    {
        readonly Dictionary<Renderer, bool> introRendering = new Dictionary<Renderer, bool>();
        readonly Dictionary<Collider, bool> introColliders = new Dictionary<Collider, bool>();
        XRIntroTutorial tutorial;
        AudioSource introVoice;
        CanvasGroup fade;
        Coroutine transition;
        Action pendingTransition;
        bool welcomeWorldActive;
        int recommendedStep = -1;
        public XRIntroTutorial Tutorial => tutorial;
        public bool IsTransitioning => transition != null;
        bool WelcomeWorldVisible => !IsDesktop && (Page == ExperiencePage.Welcome || Page == ExperiencePage.Tutorial);
        bool RecommendedNextAvailable => recommendedStep == 0 && Review.Selected.medical?.id == "review-hypotension-v2";
        string ResultNextName => RecommendedNextAvailable ? "Next recommended demo" : "Return catalog";
        string ResultNextText => RecommendedNextAvailable ? "Siguiente demo: Andrés →" : "Elegir otro entrenamiento";

        void InitializeIntro()
        {
            var sound = new GameObject("VITAL welcome synthetic voice", typeof(AudioSource)); sound.transform.SetParent(transform, false);
            introVoice = sound.GetComponent<AudioSource>(); introVoice.playOnAwake = false; introVoice.spatialBlend = 0; introVoice.volume = .7f;
            var cover = new GameObject("VITAL transition fade", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            cover.layer = 5; cover.transform.SetParent(viewer.transform, false);
            // A camera-local world canvas covers both XR eyes. It is disabled outside brief transitions.
            cover.transform.localPosition = Vector3.forward * (viewer.nearClipPlane + .015f);
            var coverCanvas = cover.GetComponent<Canvas>(); coverCanvas.renderMode = RenderMode.WorldSpace; coverCanvas.worldCamera = viewer; coverCanvas.sortingOrder = short.MaxValue;
            ((RectTransform)cover.transform).sizeDelta = Vector2.one * 2;
            var black = new GameObject("Black", typeof(RectTransform), typeof(Image)); black.layer = 5; black.transform.SetParent(cover.transform, false);
            var rect = (RectTransform)black.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = black.GetComponent<Image>(); image.color = Color.black; image.raycastTarget = false;
            fade = cover.GetComponent<CanvasGroup>(); fade.alpha = 0; cover.SetActive(false);
            if (!IsDesktop) { ApplyIntroWorld(); SpeakIntro("WELCOME"); StartCoroutine(RevealWelcome()); }
        }

        IEnumerator RevealWelcome()
        {
            SetFade(1);
            yield return FadeTo(0, .35f);
        }

        void ApplyIntroWorld()
        {
            if (!WelcomeWorldVisible) { RestoreIntroWorld(); return; }
            if (welcomeWorldActive) return;
            // Reuse the real gym module. Its baked asset is supplied by the environment build pipeline;
            // this view does not manufacture a lightmap or claim runtime geometry has been baked.
            ScenarioEnvironmentPresenter.Apply("gym", Review.Procedures.Visuals.GetComponent<EmergencyVR.Patient.PatientController>());
            var roots = new[] { Review.Procedures.Visuals.transform, Review.Procedures.transform };
            foreach (var root in roots)
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.GetComponentInParent<ArticulatedHand>() != null || introRendering.ContainsKey(renderer)) continue;
                    introRendering[renderer] = renderer.forceRenderingOff; renderer.forceRenderingOff = true;
                }
                foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                {
                    if (introColliders.ContainsKey(collider)) continue;
                    introColliders[collider] = collider.enabled; collider.enabled = false;
                }
            }
            welcomeWorldActive = true;
        }

        void RestoreIntroWorld()
        {
            foreach (var pair in introRendering) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            foreach (var pair in introColliders) if (pair.Key != null) pair.Key.enabled = pair.Value;
            introRendering.Clear(); introColliders.Clear(); welcomeWorldActive = false;
        }

        public void StartRecommendedDemo()
        {
            if (Review.Manager.IsRunning) return;
            recommendedStep = 0; SelectedEnvironment = "gym";
            Prepare(Array.FindIndex(Review.Catalog.entries, e => e.medical?.id == "review-hypotension-v2"));
        }

        void NextFromResult()
        {
            if (!RecommendedNextAvailable) { Browse(SelectedEnvironment); return; }
            recommendedStep = 1; SelectedEnvironment = "football";
            Prepare(Array.FindIndex(Review.Catalog.entries, e => e.medical?.id == "arrest-witnessed"));
        }

        public void StartIntroTutorial()
        {
            if (IsDesktop) { OpenUtility(ExperiencePage.Help); return; }
            if (Review.Manager.IsRunning) return;
            if (tutorial != null) Destroy(tutorial);
            Navigate(ExperiencePage.Tutorial);
            tutorial = gameObject.AddComponent<XRIntroTutorial>();
            tutorial.Changed += TutorialChanged;
            tutorial.Finished += TutorialFinished;
            tutorial.Initialize(FindFirstObjectByType<XROrigin>(), viewer);
            SpeakIntro("POINT");
        }

        void TutorialChanged()
        {
            redraw = true;
            if (tutorial == null) return;
            if (tutorial.Step == XRIntroStep.Grab) SpeakIntro("GRAB");
            else if (tutorial.Step == XRIntroStep.Teleport) SpeakIntro("TELEPORT");
            else if (tutorial.Step == XRIntroStep.Recenter) SpeakIntro("RECENTER");
        }

        void TutorialFinished()
        {
            if (tutorial == null) return;
            notice = tutorial.Skipped ? "Puedes volver al tutorial cuando quieras." : tutorial.TimedOut ? "La orientación de 30 segundos ha terminado. Puedes repetirla cuando quieras." : "Controles practicados. Elige tu entrenamiento.";
            // Keep the tutorial's observed flags for inspection until the next tutorial or scene unload.
            RunInterfaceAction("Finish tutorial", () => Navigate(ExperiencePage.Welcome));
        }

        void DrawIntroTutorial()
        {
            Box(content, "Tutorial card", 426, 76, 588, 332, Background, true, true);
            Label(content, "Tutorial title", "TUS PRIMEROS 30 SEGUNDOS", 450, 98, 540, 35, 24, Ink, true);
            var remaining = Label(content, "Tutorial clock", "", 450, 143, 540, 29, 19, Accent);
            Bind(remaining, () => "Orientación de controles · " + Mathf.CeilToInt(tutorial?.RemainingSeconds ?? 0) + " s");
            var step = tutorial?.Step ?? XRIntroStep.Point;
            string instruction = step == XRIntroStep.Point ? "Apunta con un mando al botón y pulsa el gatillo." :
                step == XRIntroStep.Grab ? "Coge el cubo verde con Grip, el botón lateral del mando. Después suéltalo." :
                step == XRIntroStep.Teleport ? "Empuja el stick hacia delante, apunta al cuadrado verde del suelo y suelta el stick para viajar." :
                "Pulsa B o Y para traer el menú delante de ti. Puedes usar cualquiera de los dos mandos.";
            Label(content, "Tutorial instruction", instruction, 450, 189, 540, 128, 27, Ink);
            if (step == XRIntroStep.Point)
                Button(content, "Tutorial target", "Pulsa aquí con el gatillo", 450, 327, 540, 58, () => tutorial?.ConfirmPointing(), true);
            else Label(content, "Tutorial step", "Paso " + ((int)step + 1) + " de 4", 450, 335, 540, 36, 21, Accent);
            Button(content, "Skip tutorial", "Saltar tutorial", 525, 447, 390, 58, () => tutorial?.Skip());
            Label(content, "Tutorial synthetic voice", "Voz sintética · orientación opcional", 426, 527, 588, 28, 18, Soft);
        }

        void SpeakIntro(string id)
        {
            if (introVoice == null) return;
            var clip = Resources.Load<AudioClip>("Audio/Intro/" + id);
            introVoice.Stop();
            if (clip != null) { introVoice.clip = clip; introVoice.Play(); }
        }

        static bool IsNavigationControl(string name) => name.StartsWith("Environment ") || name.StartsWith("Case ") ||
            name == "Start learning" || name == "Learn controls" || name == "Recommended demo" || name == "Next recommended demo" ||
            name == "Begin training" || name == "Pause training" || name == "Resume training" || name == "Finish training" ||
            name == "Request finish" || name == "Request restart" || name == "Cancel confirmation" || name == "Confirm finish" ||
            name == "Repeat training" || name == "Return catalog" || name == "Results home" || name == "Choose environment" ||
            name == "All scenarios" || name.StartsWith("Back ") || name == "Help" || name == "Settings" || name == "Finish tutorial";

        void RunInterfaceAction(string name, Action action)
        {
            if (!IsNavigationControl(name) || fade == null) { action(); return; }
            if (pendingTransition != null || transition != null) return;
            pendingTransition = action;
            transition = StartCoroutine(TransitionPage());
        }

        IEnumerator TransitionPage()
        {
            if (introVoice != null) introVoice.Stop();
            foreach (var ray in canvas.GetComponents<BaseRaycaster>()) ray.enabled = false;
            yield return FadeTo(1, .08f);
            var action = pendingTransition; pendingTransition = null; action?.Invoke();
            // Render the new page while the camera is covered, then reveal it.
            if (redraw) Render();
            yield return null;
            yield return FadeTo(0, .12f);
            foreach (var ray in canvas.GetComponents<BaseRaycaster>()) ray.enabled = true;
            transition = null;
            ApplyVisibility();
        }

        IEnumerator FadeTo(float target, float duration)
        {
            float from = fade == null ? 0 : fade.alpha, elapsed = 0;
            while (elapsed < duration && fade != null)
            {
                elapsed += Time.unscaledDeltaTime; SetFade(Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            SetFade(target);
        }

        void SetFade(float alpha)
        {
            if (fade == null) return;
            fade.alpha = alpha; fade.gameObject.SetActive(alpha > .001f);
        }

        // Legacy screenshot tours call UI callbacks semantically. Settle their presentation before a snapshot;
        // the separate XR walkthrough never calls this helper and waits for the real animated transition.
        void SettleSnapshotTransition()
        {
            if (transition != null) StopCoroutine(transition);
            transition = null;
            var action = pendingTransition; pendingTransition = null; action?.Invoke();
            SetFade(0);
            foreach (var ray in canvas.GetComponents<BaseRaycaster>()) ray.enabled = true;
            ApplyVisibility();
        }

        void DisposeIntro()
        {
            RestoreIntroWorld();
            if (fade != null) Destroy(fade.gameObject);
            if (tutorial != null) { tutorial.Changed -= TutorialChanged; tutorial.Finished -= TutorialFinished; }
        }
    }
}
