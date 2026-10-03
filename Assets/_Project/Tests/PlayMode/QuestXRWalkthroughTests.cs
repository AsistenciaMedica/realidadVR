#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EmergencyVR.Desktop;
using EmergencyVR.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using Object = UnityEngine.Object;

namespace EmergencyVR.Tests
{
    /// <summary>
    /// Fifteen independent navigation results. Every learner action is an authored XR ray + Input System
    /// trigger event. This validates the product flow in simulation, not clinical mastery or headset performance.
    /// </summary>
    [PrebuildSetup(SimulatedXRTestHooks.Setup), PostBuildCleanup(SimulatedXRTestHooks.Setup)]
    public sealed class QuestXRWalkthroughTests
    {
        QuestLookSimulation simulation;
        TrainingExperience flow;
        readonly List<string> errors = new List<string>();
        readonly List<string> visited = new List<string>();
        int clicks;
        XRNode activeHand;

        [UnitySetUp]
        public IEnumerator Load()
        {
            Time.timeScale = 1; AudioListener.pause = false;
            errors.Clear(); visited.Clear(); clicks = 0;
            Application.logMessageReceived += RecordError;
            SceneManager.sceneLoaded += Install;
            yield return SceneManager.LoadSceneAsync("TrainingRoom");
            SceneManager.sceneLoaded -= Install;
            yield return null; yield return null;
            flow = Object.FindFirstObjectByType<TrainingExperience>();
            Assert.That(flow, Is.Not.Null);
            Assert.That(flow.IsDesktop, Is.False);
            Assert.That(flow.InterfaceCanvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
        }

        void Install(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "TrainingRoom") return;
            simulation = QuestLookSimulation.CreateForValidation();
            simulation.ManualControlsEnabled = false;
        }

        void RecordError(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                errors.Add(message + "\n" + trace);
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            SceneManager.sceneLoaded -= Install;
            Application.logMessageReceived -= RecordError;
            if (flow != null && flow.Review.Manager != null) flow.Review.Manager.SetPaused(false);
            if (simulation != null) Object.Destroy(simulation.gameObject);
            Time.timeScale = 1; AudioListener.pause = false;
            yield return null;
            Assert.That(errors, Is.Empty, "The simulated XR route must not emit runtime errors or exceptions.");
        }

        [UnityTest] public IEnumerator Daniel_Hypotension() => Walkthrough("review-hypotension-v2");
        [UnityTest] public IEnumerator Gym_Faint() => Walkthrough("gym-faint");
        [UnityTest] public IEnumerator Gym_Glucose() => Walkthrough("glucose-moderate");
        [UnityTest] public IEnumerator Gym_ChestPain() => Walkthrough("chest-pain");
        [UnityTest] public IEnumerator Gym_Asthma() => Walkthrough("asthma");
        [UnityTest] public IEnumerator Mall_UnconsciousBreathing() => Walkthrough("review-unconscious-breathing-v1");
        [UnityTest] public IEnumerator Mall_AbnormalBreathing() => Walkthrough("review-abnormal-breathing-v1");
        [UnityTest] public IEnumerator Mall_Choking() => Walkthrough("choking-partial");
        [UnityTest] public IEnumerator Mall_Confusion() => Walkthrough("confusion");
        [UnityTest] public IEnumerator Mall_Dehydration() => Walkthrough("dehydration");
        [UnityTest] public IEnumerator Andres_Arrest() => Walkthrough("arrest-witnessed");
        [UnityTest] public IEnumerator Football_Faint() => Walkthrough("football-faint");
        [UnityTest] public IEnumerator Football_Glucose() => Walkthrough("football-glucose");
        [UnityTest] public IEnumerator Football_HeatExhaustion() => Walkthrough("heat-exhaustion");
        [UnityTest] public IEnumerator Football_Hypoxia() => Walkthrough("hypoxia");

        IEnumerator Walkthrough(string scenarioId)
        {
            var definition = flow.Review.Catalog.entries.Single(e => e.medical?.id == scenarioId).medical;
            Assert.That(flow.Review.Scope.ScenarioIds, Has.Length.EqualTo(15));
            activeHand = Array.IndexOf(flow.Review.Scope.ScenarioIds, scenarioId) % 2 == 0 ? XRNode.RightHand : XRNode.LeftHand;
            // Alternate the only tracked controller across cases. The route never needs the second hand.
            var unavailableHand = activeHand == XRNode.LeftHand ? XRNode.RightHand : XRNode.LeftHand;
            simulation.SetControllerPose(unavailableHand, Vector3.zero, Quaternion.identity, false);
            yield return null;
            CheckPage(ExperiencePage.Welcome, "Start learning", "Learn controls");

            yield return Select("Start learning");
            CheckPage(ExperiencePage.Environments, "Back home", "Environment " + definition.environment);
            yield return Select("Environment " + definition.environment);
            CheckPage(ExperiencePage.Catalog, "Back environments", "Case " + scenarioId);
            yield return Select("Case " + scenarioId);
            CheckPage(ExperiencePage.Briefing, "Back catalog", "Begin training");
            Assert.That(flow.Review.Manager.IsRunning, Is.False);
            Assert.That(flow.Review.Procedures.TrainingMode, Is.True, "A new learner starts in guided practice.");

            yield return Select("Begin training");
            CheckPage(ExperiencePage.Training, "Pause training", "Finish training");
            Assert.That(flow.Review.Manager.AcceptsInput, Is.True);
            Assert.That(flow.Review.Manager.MedicalDefinition.id, Is.EqualTo(scenarioId));
            var firstAttempt = flow.Review.Manager.MedicalSession;

            // Inspect each information drawer through the same XR pointer, without submitting a
            // clinical action. This catches clipping hidden behind the compact training tabs.
            string[] drawers = definition.clinicalV2?.capabilities.usesObjectiveBasedEvaluation == true
                ? new[] { "Open actions", "Open dialogue", "Open help conversation", "Toggle observations" }
                : new[] { "Open actions", "Open patient" };
            foreach (string drawer in drawers)
            {
                yield return Select(drawer);
                CheckPage(ExperiencePage.Training, drawer, "Pause training", "Finish training");
                visited.Add("Drawer:" + drawer);
                yield return Select(drawer);
                CheckPage(ExperiencePage.Training, drawer, "Pause training", "Finish training");
            }

            yield return Select("Pause training");
            CheckPage(ExperiencePage.Pause, "Resume training", "Request finish");
            double pausedAt = flow.Review.Manager.ElapsedSeconds;
            Assert.That(AudioListener.pause, Is.True);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(flow.Review.Manager.ElapsedSeconds, Is.EqualTo(pausedAt).Within(.002));
            yield return Select("Resume training");
            CheckPage(ExperiencePage.Training, "Pause training", "Finish training");
            Assert.That(flow.Review.Manager.AcceptsInput, Is.True);
            Assert.That(AudioListener.pause, Is.False);

            yield return Select("Finish training");
            CheckPage(ExperiencePage.Finish, "Cancel confirmation", "Confirm finish");
            yield return Select("Cancel confirmation");
            CheckPage(ExperiencePage.Pause, "Resume training", "Request finish");
            yield return Select("Request finish");
            CheckPage(ExperiencePage.Finish, "Cancel confirmation", "Confirm finish");
            yield return Select("Confirm finish");
            CheckPage(ExperiencePage.Results, "Repeat training", "Return catalog", "Results home");
            Assert.That(flow.Review.HasResult, Is.True);
            Assert.That(flow.Review.Manager.IsRunning, Is.False);
            Assert.That(AudioListener.pause, Is.False);

            int tabs = definition.clinicalV2?.capabilities.usesObjectiveBasedEvaluation == true ? 4 : 5;
            for (int tab = 1; tab < tabs; tab++)
            {
                yield return Select("Results tab " + tab);
                CheckPage(ExperiencePage.Results, "Repeat training", "Return catalog", "Results home");
            }
            yield return Select("Repeat training");
            CheckPage(ExperiencePage.Briefing, "Back catalog", "Begin training");
            yield return Select("Begin training");
            CheckPage(ExperiencePage.Training, "Pause training", "Finish training");
            Assert.That(flow.Review.Manager.MedicalSession, Is.Not.SameAs(firstAttempt));
            Assert.That(flow.Review.Manager.MedicalSession.Completed, Is.Empty, "Repeating starts with independent evidence.");
            yield return Select("Finish training");
            CheckPage(ExperiencePage.Finish, "Cancel confirmation", "Confirm finish");
            yield return Select("Confirm finish");
            CheckPage(ExperiencePage.Results, "Repeat training", "Return catalog", "Results home");
            yield return Select("Return catalog");
            CheckPage(ExperiencePage.Catalog, "Back environments", "Case " + scenarioId);
            yield return Select("Back environments");
            CheckPage(ExperiencePage.Environments, "Back home");
            yield return Select("Back home");
            CheckPage(ExperiencePage.Welcome, "Start learning", "Learn controls");
            Assert.That(errors, Is.Empty);
            LogAssert.NoUnexpectedReceived();
            Debug.Log("XR_WALKTHROUGH_PASS " + scenarioId + " hand=" + activeHand + " actualTriggerClicks=" + clicks + " pages=" + string.Join(",", visited) + " verification=simulation-only");
        }

        Button FindButton(string name)
        {
            var buttons = flow.GetComponentsInChildren<Button>().Where(b => b.gameObject.activeInHierarchy && b.name == name).ToArray();
            Assert.That(buttons, Has.Length.EqualTo(1), "Unique active control required: " + name + " on " + flow.Page);
            Assert.That(buttons[0].IsInteractable(), Is.True, "Control disabled: " + name + " on " + flow.Page);
            return buttons[0];
        }

        IEnumerator Select(string name)
        {
            // A redraw may replace a Button while the tracked pose and XR UI model catch up.
            // Wait for the current instance to receive a stable, unobstructed ray before one click.
            // Never disable colliders, invoke events or retry a missed trigger.
            simulation.SetTrigger(activeHand, false);
            var handedness = activeHand == XRNode.LeftHand ? InteractorHandedness.Left : InteractorHandedness.Right;
            // Starter Assets 3.3 authors NearFarInteractor for controller UI. Its separate
            // XRRayInteractor is for teleportation and deliberately has UI disabled.
            var rays = Object.FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None)
                .Where(ray => ray.handedness == handedness && ray.enableUIInteraction).ToArray();
            Assert.That(rays, Is.Not.Empty, "No authored UI ray for " + activeHand);
            Button button = null;
            int stableFrames = 0;
            string lastHit = "no ray result";
            float deadline = Time.realtimeSinceStartup + 2;
            while (Time.realtimeSinceStartup < deadline && stableFrames < 2)
            {
                if (flow.IsTransitioning) { stableFrames = 0; yield return null; continue; }
                var current = FindButton(name);
                if (current != button) { button = current; stableFrames = 0; }
                Canvas.ForceUpdateCanvases();
                var center = button.transform.TransformPoint(((RectTransform)button.transform).rect.center);
                var forward = flow.InterfaceCanvas.transform.forward;
                simulation.SetControllerPose(activeHand, center - forward * .8f, Quaternion.LookRotation(forward, Vector3.up));
                yield return null;
                if (flow.IsTransitioning || button == null || !button.gameObject.activeInHierarchy || FindButton(name) != button)
                { stableFrames = 0; continue; }
                bool hovered = false;
                foreach (var ray in rays)
                {
                    if (!ray.isActiveAndEnabled) continue;
                    bool hasUiHit = ray.TryGetCurrentUIRaycastResult(out var uiHit);
                    var hitObject = hasUiHit ? uiHit.gameObject : null;
                    var hitButton = hitObject == null ? null : hitObject.GetComponentInParent<Button>();
                    // The endpoint includes NearFarInteractor's own nearer-physics-hit priority.
                    // A UI model hit alone would also exist when a nearer object blocks the ray.
                    var endpoint = ray.TryGetCurveEndPoint(out _);
                    lastHit = "interactor=" + ray.name + ", ui=" + (hitObject == null ? "none" : hitObject.name) + ", endpoint=" + endpoint;
                    if (endpoint == EndPointType.UI && hitButton == button) { hovered = true; break; }
                }
                stableFrames = hovered ? stableFrames + 1 : 0;
            }
            Assert.That(stableFrames, Is.EqualTo(2), "Authored XR ray did not settle on " + name + " within 2 s: " + lastHit);
            int received = 0;
            // Observe the event, never invoke it. A missing/occluded XR ray therefore fails this test.
            button.onClick.AddListener(() => received++);
            simulation.SetTrigger(activeHand, true);
            yield return new WaitForSecondsRealtime(.08f);
            simulation.SetTrigger(activeHand, false);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(received, Is.EqualTo(1), "Authored XR ray + trigger did not select " + name + " on " + flow.Page);
            clicks++;
        }

        void CheckPage(ExperiencePage expected, params string[] exits)
        {
            Assert.That(flow.Page, Is.EqualTo(expected));
            Assert.That(flow.InterfaceCanvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(flow.IsDesktop, Is.False);
            foreach (string exit in exits) FindButton(exit);
            Canvas.ForceUpdateCanvases();
            AssertTextFits();
            Assert.That(errors, Is.Empty);
            visited.Add(expected.ToString());
        }

        void AssertTextFits()
        {
            var failures = new List<string>();
            using (var measure = new TextGenerator())
            {
                foreach (var label in flow.GetComponentsInChildren<Text>())
                {
                    if (!label.isActiveAndEnabled || string.IsNullOrWhiteSpace(label.text)) continue;
                    var rect = label.rectTransform.rect;
                    if (rect.width <= 0 || rect.height <= 0) { failures.Add(label.name + " has an empty text rectangle"); continue; }
                    var settings = label.GetGenerationSettings(rect.size);
                    // Respect the font actually chosen by best-fit before measuring the complete text.
                    int actualSize = label.cachedTextGenerator.fontSizeUsedForBestFit;
                    settings.fontSize = label.resizeTextForBestFit && actualSize > 0 ? actualSize : label.fontSize;
                    settings.resizeTextForBestFit = false;
                    settings.verticalOverflow = VerticalWrapMode.Overflow;
                    float height = measure.GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
                    if (height > rect.height + 2)
                        failures.Add(label.transform.parent.name + "/" + label.name + " needs " + height.ToString("0.0") + " px, has " + rect.height.ToString("0.0"));
                    // Authored ellipsis is part of the measured visible text, so it is allowed when it fits.
                    // Scroll content is also checked against its own expanded rect, not the clipped viewport.
                }
            }
            Assert.That(failures, Is.Empty, "Text clipped on " + flow.Page + ":\n" + string.Join("\n", failures));
        }
    }
}
#endif
