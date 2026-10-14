using System.Collections.Generic;
using EmergencyVR.Desktop;
using EmergencyVR.Medical.Interaction;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace EmergencyVR.UI
{
    /// <summary>
    /// "Primeros pasos": the first time a training starts in each run, a short checklist teaches how to move,
    /// look, kneel and act on the patient. Each item ticks itself when the learner actually does it, so nobody
    /// is left standing still without knowing the controls. Windows: card on the right, F1 shows/hides it.
    /// Quest: a small card floating low and to the left of the view, never in front of the patient.
    /// It records nothing in the evaluation.
    /// </summary>
    public sealed class FirstStepsCoach : MonoBehaviour
    {
        sealed class Step { public string Text; public System.Func<bool> Done; public bool Ticked; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
        }

        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "TrainingRoom" || FindFirstObjectByType<FirstStepsCoach>() != null) return;
            // Automated validation and capture runs must not show the coach.
            foreach (var arg in System.Environment.GetCommandLineArgs())
                if (arg.StartsWith("-vital-") || arg.StartsWith("-demo-")) return;
            if (Application.isBatchMode) return;
            new GameObject("VITAL VR first steps").AddComponent<FirstStepsCoach>();
        }

        static bool completedThisRun;

        TrainingExperience experience;
        DesktopDemoController desktop;
        XROrigin origin;
        HandsOnInteraction handsOn;
        readonly List<Step> steps = new List<Step>();
        bool started, visible, desktopMode;
        float trainingSince, finishedAt = -1, lookupAt, turned, lastYaw;
        Vector3 startPosition;
        bool kneeled;
        TextMesh vrText;
        GUIStyle cardStyle;

        /// <summary>Shown or hidden by F1 on Windows.</summary>
        public bool Visible => visible;

        void Update()
        {
            if (Time.unscaledTime >= lookupAt)
            {
                lookupAt = Time.unscaledTime + .5f;
                if (experience == null) experience = FindFirstObjectByType<TrainingExperience>();
                if (desktop == null) desktop = FindFirstObjectByType<DesktopDemoController>();
                if (desktop == null && origin == null) origin = FindFirstObjectByType<XROrigin>();
                if (handsOn == null) handsOn = FindFirstObjectByType<HandsOnInteraction>();
            }
            if (experience == null) return;
            bool training = experience.Page == ExperiencePage.Training && !experience.BlocksWorldInput;
            if (training && !started && !completedThisRun) Begin();
            if (desktop != null && Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame && started) visible = !visible;
            if (!started) return;

            var mover = Mover();
            if (mover != null)
            {
                float yaw = mover.eulerAngles.y;
                turned += Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw)); lastYaw = yaw;
            }
            if (desktop != null && desktop.Kneeling) kneeled = true;
            if (training)
                foreach (var step in steps)
                    if (!step.Ticked && step.Done()) step.Ticked = true;
            if (finishedAt < 0 && steps.TrueForAll(s => s.Ticked)) { finishedAt = Time.unscaledTime; completedThisRun = true; }
            if (finishedAt >= 0 && Time.unscaledTime - finishedAt > 4f) visible = false;
            UpdateVrCard(training && visible);
        }

        Transform Mover() => desktop != null ? desktop.transform : origin != null ? origin.transform : null;
        Camera Eye => desktop != null ? desktop.View : origin != null ? origin.Camera : Camera.main;

        float DistanceToPatient()
        {
            var chest = experience.Review?.Procedures?.Visuals?.ChestAnchor;
            var eye = Eye;
            return chest == null || eye == null ? 99 : Vector3.Distance(eye.transform.position, chest.position);
        }

        bool Moved() { var m = Mover(); return m != null && Vector3.Distance(m.position, startPosition) > .8f; }

        void Begin()
        {
            started = true; visible = true; trainingSince = Time.unscaledTime;
            desktopMode = desktop != null;
            var mover = Mover();
            startPosition = mover == null ? Vector3.zero : mover.position;
            lastYaw = mover == null ? 0 : mover.eulerAngles.y;
            steps.Clear();
            if (desktopMode)
            {
                Add("Camina: W A S D o las flechas", Moved);
                Add("Mira alrededor: mantén el clic derecho y mueve el ratón (Tab: mirada libre)", () => turned > 40);
                Add("Acércate al paciente (R te lleva junto a él)", () => DistanceToPatient() < 1.6f);
                Add("Arrodíllate a su lado: Ctrl", () => kneeled);
                Add("Actúa sobre él: apunta a sus hombros o al pecho y mantén el clic", () => handsOn != null && handsOn.PerformedCount > 0);
            }
            else
            {
                Add("Camina: stick izquierdo", Moved);
                Add("Gira: stick derecho hacia los lados", () => turned > 25);
                if (experience.UsesWristPanel)
                    Add("Mírate la mano izquierda: ahí está tu panel", () => Time.unscaledTime - trainingSince > 4 && experience.WristPanelShown);
                Add("Agáchate y toca sus hombros: mano en la zona y gatillo", () => handsOn != null && handsOn.PerformedCount > 0);
            }
        }

        void Add(string text, System.Func<bool> done) => steps.Add(new Step { Text = text, Done = done });

        string Body()
        {
            var lines = new List<string> { finishedAt >= 0 ? "¡Listo! Ya sabes moverte y actuar." : "PRIMEROS PASOS" + (desktopMode ? "  ·  F1 ocultar" : "") };
            foreach (var step in steps) lines.Add((step.Ticked ? "✓  " : "•  ") + step.Text);
            lines.Add(desktopMode ? "Esc: pausa  ·  I: ocultar paneles" : "Tu panel te acompaña a un lado  ·  B / Y: pausa");
            return string.Join("\n", lines);
        }

        void OnGUI()
        {
            if (!desktopMode || !visible || experience == null || experience.Page != ExperiencePage.Training) return;
            if (cardStyle == null)
                cardStyle = new GUIStyle(GUI.skin.box) { fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height / 52f, 12, 22)), wordWrap = true,
                    alignment = TextAnchor.UpperLeft, padding = new RectOffset(14, 14, 10, 10), normal = { textColor = Color.white } };
            string text = Body();
            float width = Mathf.Clamp(Screen.width * .27f, 300, 520);
            float height = cardStyle.CalcHeight(new GUIContent(text), width);
            var previous = GUI.color; GUI.color = new Color(1, 1, 1, .94f);
            GUI.Box(new Rect(Screen.width - width - 18, Screen.height * .22f, width, height), text, cardStyle);
            GUI.color = previous;
        }

        void UpdateVrCard(bool show)
        {
            if (desktopMode) return;
            var eye = Eye;
            if (vrText == null)
            {
                if (!show || eye == null) return;
                var go = new GameObject("First steps card", typeof(TextMesh));
                go.transform.SetParent(transform, false);
                vrText = go.GetComponent<TextMesh>();
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                vrText.font = font; vrText.fontSize = 48; vrText.characterSize = .01f; vrText.anchor = TextAnchor.MiddleLeft;
                vrText.color = new Color(.85f, 1f, .97f); go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            vrText.gameObject.SetActive(show && eye != null);
            if (!show || eye == null) return;
            vrText.text = Body();
            // Low and to the left of the view, lazily following the head: never in front of the patient.
            var head = eye.transform;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            var target = head.position + forward * 1.1f - right * .42f - Vector3.up * .38f;
            var t = vrText.transform;
            t.position = Vector3.Lerp(t.position == Vector3.zero ? target : t.position, target, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 3f));
            t.rotation = Quaternion.LookRotation((t.position - head.position).normalized, Vector3.up);
            var bounds = vrText.GetComponent<MeshRenderer>().localBounds.size;
            int lines = vrText.text.Split('\n').Length;
            if (bounds.y > 1e-5f) t.localScale = Vector3.one * (.024f * lines / bounds.y);
        }
    }
}
