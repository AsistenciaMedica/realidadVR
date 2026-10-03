using System;
using System.Collections.Generic;
using System.Linq;
using EmergencyVR.Desktop;
using EmergencyVR.Medical;
using EmergencyVR.Medical.Interaction;
using EmergencyVR.Scenarios;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.UI;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace EmergencyVR.UI
{
    public enum ExperiencePage { Welcome, Environments, Catalog, Briefing, Training, Pause, Results, Help, Settings, Finish, Restart, Exit }

    // Product navigation owns presentation only. ReviewCaseSession remains the case gateway.
    public sealed partial class TrainingExperience : MonoBehaviour
    {
        public ExperiencePage Page { get; private set; } = ExperiencePage.Welcome;
        public ReviewCaseSession Review { get; private set; }
        public bool BlocksWorldInput => Page != ExperiencePage.Training;
        public bool IsDesktop => desktop != null;
        public bool WorldVisible => Page == ExperiencePage.Training || (Review.Manager.IsRunning && Page != ExperiencePage.Results);
        public string SelectedEnvironment { get; private set; } = "";
        public int PendingCaseIndex { get; private set; } = -1;
        public Canvas InterfaceCanvas => canvas;
        DesktopDemoController desktop;
        Camera viewer;
        Canvas canvas;
        RectTransform surface;
        GameObject pageRoot;
        int originalMask;
        Color originalBackground;
        CameraClearFlags originalClear;
        readonly Dictionary<Behaviour, bool> locomotion = new Dictionary<Behaviour, bool>();
        readonly Dictionary<GameObject, int> rayLayers = new Dictionary<GameObject, int>();
        readonly Dictionary<MedicalToolKind, Reading> readings = new Dictionary<MedicalToolKind, Reading>();
        MedicalScenarioRuntime observedSession;
        XRInteractionManager xrManager;
        XRSelectFilterDelegate selectionFilter;
        ExperiencePage returnPage = ExperiencePage.Welcome;
        bool ready, redraw = true, actionDrawer, patientDrawer, secondaryHeld;
        int catalogPage, actionPage, resultTab;
        string category = "Todas", difficulty = "Todas", search = "", notice = "";
        float nextRefresh;
        readonly List<Action> live = new List<Action>();
        sealed class Reading { public PatientSnapshot Patient; public double Time; }

        public static TrainingExperience Attach(ReviewCaseSession session)
        {
            var go = new GameObject("VITAL VR Experience");
            var flow = go.AddComponent<TrainingExperience>();
            flow.Review = session;
            return flow;
        }

        void Start()
        {
            desktop = FindFirstObjectByType<DesktopDemoController>();
            viewer = desktop != null ? desktop.View : Camera.main;
            if (viewer == null) { Debug.LogError("VITAL VR needs an active viewer."); enabled = false; return; }
            originalMask = viewer.cullingMask; originalBackground = viewer.backgroundColor; originalClear = viewer.clearFlags;
            foreach (var panel in FindObjectsByType<TrainingPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var oldCanvas = panel.GetComponent<Canvas>(); if (oldCanvas != null) oldCanvas.enabled = false;
                foreach (var ray in panel.GetComponents<BaseRaycaster>()) ray.enabled = false;
            }
            foreach (var provider in FindObjectsByType<LocomotionProvider>(FindObjectsSortMode.None)) locomotion[provider] = provider.enabled;
            xrManager = FindFirstObjectByType<XRInteractionManager>();
            if (xrManager != null)
            {
                selectionFilter = new XRSelectFilterDelegate((interactor, interactable) => !BlocksWorldInput || interactable.interactorsSelecting.Contains(interactor));
                xrManager.selectFilters.Add(selectionFilter);
            }
            foreach (var line in FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>(FindObjectsSortMode.None))
            { rayLayers[line.gameObject] = line.gameObject.layer; line.gameObject.layer = 5; }
            CreateCanvas();
            CreateGuide();
            PatientConversation = new EmergencyVR.Dialogue.PatientConversationController(
                () => Review.Manager.MedicalSession, () => Review.Manager.MedicalDefinition, () => Review.Manager.AcceptsInput);
            Review.Manager.Changed += Changed;
            Review.SelectionChanged += Selected;
            Review.Procedures.MeasurementRecorded += Measured;
            ready = true;
            Render();
            if (System.Environment.GetCommandLineArgs().Contains("-vital-patient-roster-smoke")) StartCoroutine(PatientRosterSmoke());
            else if (System.Environment.GetCommandLineArgs().Contains("-vital-ux-smoke")) StartCoroutine(ExperienceSmoke());
        }

        void CreateCanvas()
        {
            var root = new GameObject("VITAL VR Interface", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.layer = 5; root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>(); canvas.sortingOrder = 100;
            surface = root.GetComponent<RectTransform>();
            var scaler = root.GetComponent<CanvasScaler>();
            if (IsDesktop)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = viewer; canvas.planeDistance = viewer.nearClipPlane + .01f;
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1440, 900); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                root.AddComponent<GraphicRaycaster>();
                var events = EventSystem.current;
                if (events == null) events = new GameObject("VITAL VR Events", typeof(EventSystem)).GetComponent<EventSystem>();
                foreach (var module in events.GetComponents<BaseInputModule>()) module.enabled = false;
                var input = events.GetComponent<InputSystemUIInputModule>() ?? events.gameObject.AddComponent<InputSystemUIInputModule>();
                input.enabled = true; input.AssignDefaultActions();
            }
            else
            {
                canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = viewer;
                surface.sizeDelta = new Vector2(1440, 900); surface.localScale = Vector3.one * .00165f;
                root.AddComponent<TrackedDeviceGraphicRaycaster>();
                // Retain the authored XR UI input module and all existing controller bindings.
                if (EventSystem.current == null) new GameObject("VITAL VR XR Events", typeof(EventSystem), typeof(XRUIInputModule));
            }
            Recenter();
        }

        public void Recenter()
        {
            if (viewer == null || surface == null || IsDesktop) return;
            var forward = Vector3.ProjectOnPlane(viewer.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            surface.position = viewer.transform.position + forward * 2.2f;
            surface.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        public bool PointerOverInterface
        {
            get
            {
                if (BlocksWorldInput) return true;
                if (EventSystem.current == null || Mouse.current == null) return false;
                var data = new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() };
                pointerHits.Clear(); EventSystem.current.RaycastAll(data, pointerHits);
                return pointerHits.Any(h => h.gameObject.transform.IsChildOf(transform));
            }
        }
        readonly List<RaycastResult> pointerHits = new List<RaycastResult>();

        void Changed() { nextRefresh = 0; }
        void Selected() { readings.Clear(); observedSession = null; actionPage = 0; dialoguePage = 0; dialogueDrawer = false; }
        void Measured(MedicalToolKind kind, PatientSnapshot patient, double at)
        {
            if (observedSession != Review.Manager.MedicalSession) { readings.Clear(); observedSession = Review.Manager.MedicalSession; }
            readings[kind] = new Reading { Patient = patient, Time = at }; nextRefresh = 0;
        }

        void Update()
        {
            if (!ready) return;
            var manager = Review.Manager;
            if (manager.MedicalSession != observedSession) { readings.Clear(); observedSession = manager.MedicalSession; }
            if (manager.IsRunning && Page == ExperiencePage.Welcome) Navigate(ExperiencePage.Training);
            if (manager.IsPaused && Page == ExperiencePage.Training) Navigate(ExperiencePage.Pause);
            if (!manager.IsRunning && Review.HasResult && (Page == ExperiencePage.Training || Page == ExperiencePage.Pause || Page == ExperiencePage.Finish))
            { resultTab = 0; Navigate(ExperiencePage.Results); }
            if (!IsDesktop)
            {
                bool pressed = false;
                foreach (var hand in new[] { XRNode.LeftHand, XRNode.RightHand })
                    pressed |= InputDevices.GetDeviceAtXRNode(hand).TryGetFeatureValue(CommonUsages.secondaryButton, out var value) && value;
                if (pressed && !secondaryHeld) { Recenter(); if (Page == ExperiencePage.Training || Page == ExperiencePage.Pause) TogglePause(); }
                secondaryHeld = pressed;
            }
            if (redraw) Render();
            if (Time.unscaledTime >= nextRefresh)
            { nextRefresh = Time.unscaledTime + .2f; foreach (var update in live) update(); }
            UpdateGuideHighlight();
            UpdateAssistKey();
            UpdateWorldKeys();
        }

        public void Navigate(ExperiencePage page)
        {
            if (Review.Manager.IsRunning && (page == ExperiencePage.Welcome || page == ExperiencePage.Environments || page == ExperiencePage.Catalog || page == ExperiencePage.Briefing)) return;
            Page = page;
            if (page != ExperiencePage.Training && Review.Manager.IsRunning) Review.Manager.SetPaused(true);
            else if (page == ExperiencePage.Training) Review.Manager.SetPaused(false);
            redraw = true; Recenter();
        }
        public void Browse(string environment)
        {
            if (Review.Manager.IsRunning) return;
            if (environment != "" && !Review.Scope.environments.Any(e => e.id == environment)) return;
            SelectedEnvironment = environment; catalogPage = 0; category = difficulty = "Todas"; search = "";
            Navigate(ExperiencePage.Catalog);
        }
        public void Prepare(int index)
        {
            if (Review.Manager.IsRunning || index < 0 || index >= Review.Catalog.entries.Length) return;
            var definition = Review.Catalog.entries[index].medical;
            if (definition != null && definition.availability != "AVAILABLE") return;
            PendingCaseIndex = index; Navigate(ExperiencePage.Briefing);
        }
        public void BeginTraining()
        {
            if (PendingCaseIndex < 0 || Review.Manager.IsRunning) return;
            if (!Review.Select(PendingCaseIndex)) return;
            Review.Manager.StartCase();
            if (!Review.Manager.IsRunning) { notice = Review.Manager.Feedback; redraw = true; return; }
            notice = ""; actionDrawer = patientDrawer = false;
            desktop?.ResetPosition();
            if (desktop != null && Review.Selected.medical != null) desktop.FocusPatient(Review.Procedures.Visuals.ChestAnchor.position);
            Navigate(ExperiencePage.Training);
        }
        public void TogglePause()
        {
            if (Page == ExperiencePage.Training) Navigate(ExperiencePage.Pause);
            else if (Page == ExperiencePage.Pause) Navigate(ExperiencePage.Training);
            else if (Page == ExperiencePage.Help || Page == ExperiencePage.Settings) Navigate(returnPage);
        }
        public void FinishTraining()
        {
            if (!Review.Manager.IsRunning) return;
            Review.Manager.FinishCase(); resultTab = 0; Navigate(ExperiencePage.Results);
        }
        void Repeat()
        {
            PendingCaseIndex = Review.SelectedIndex;
            if (Review.Manager.IsRunning) Review.Manager.FinishCase();
            Navigate(ExperiencePage.Briefing);
        }
        void OpenUtility(ExperiencePage page)
        {
            if (Page == page) return;
            if (Page != ExperiencePage.Help && Page != ExperiencePage.Settings) returnPage = Page;
            Navigate(page);
        }
        void ApplyVisibility()
        {
            viewer.cullingMask = WorldVisible ? originalMask : 1 << 5;
            viewer.backgroundColor = WorldVisible ? originalBackground : Background;
            viewer.clearFlags = WorldVisible ? originalClear : CameraClearFlags.SolidColor;
            foreach (var pair in locomotion) if (pair.Key != null) pair.Key.enabled = !BlocksWorldInput && pair.Value;
        }
        void OnDestroy()
        {
            if (rounded != null) Destroy(rounded);
            if (roundedTexture != null) Destroy(roundedTexture);
            if (xrManager != null && selectionFilter != null) xrManager.selectFilters.Remove(selectionFilter);
            if (!ready) return;
            if (Review != null)
            {
                Review.SelectionChanged -= Selected;
                if (Review.Procedures != null) Review.Procedures.MeasurementRecorded -= Measured;
                if (Review.Manager != null) { Review.Manager.Changed -= Changed; Review.Manager.SetPaused(false); }
            }
            if (viewer != null) { viewer.cullingMask = originalMask; viewer.backgroundColor = originalBackground; viewer.clearFlags = originalClear; }
            foreach (var pair in locomotion) if (pair.Key != null) pair.Key.enabled = pair.Value;
            foreach (var pair in rayLayers) if (pair.Key != null) pair.Key.layer = pair.Value;
        }
    }
}
