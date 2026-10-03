using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using EmergencyVR.Desktop;

namespace EmergencyVR.UI
{
    public sealed partial class TrainingExperience
    {
        // Opt-in player acceptance path: real rendered UI, semantic click events and shared case engine.
        IEnumerator ExperienceSmoke()
        {
            var routine = SmokeSteps(); bool passed = true;
            while (true)
            {
                object value = null; bool more = false;
                try { more = routine.MoveNext(); if (more) value = routine.Current; }
                catch (Exception error) { Debug.LogException(error); passed = false; }
                if (!more || !passed) break;
                yield return value;
            }
            Debug.Log("VITAL_UX_SMOKE " + (passed ? "PASS" : "FAIL"));
            Application.Quit(passed ? 0 : 1);
        }
        IEnumerator SmokeSteps()
        {
            Application.targetFrameRate = 60;
            var args = System.Environment.GetCommandLineArgs(); int flag = Array.IndexOf(args, "-vital-capture-directory");
            string directory = flag >= 0 && flag + 1 < args.Length ? args[flag + 1] : Path.Combine(Application.persistentDataPath, "UXPreview");
            Directory.CreateDirectory(directory);
            yield return null; yield return new WaitForSecondsRealtime(.15f); yield return new WaitForEndOfFrame();
            Require(Page == ExperiencePage.Welcome && !Review.Manager.IsRunning && WorldVisible == !IsDesktop, "Welcome must precede the clinical attempt; VR shows the welcome room.");
            CaptureInterface(directory, "01-welcome");
            Click("Start learning"); yield return null; yield return new WaitForSecondsRealtime(.15f); yield return new WaitForEndOfFrame(); CaptureInterface(directory, "02-environments");
            Click("Environment gym"); yield return null; yield return new WaitForSecondsRealtime(.15f); yield return new WaitForEndOfFrame(); CaptureInterface(directory, "03-catalog");
            // This smoke exercises the legacy scored UI; CASE 01 has its own objective-based player validation.
            var index = Array.FindIndex(Review.Catalog.entries, e => e.medical?.id == "gym-faint");
            Click("Case " + Review.Catalog.entries[index].medical.id); yield return null; yield return new WaitForSecondsRealtime(.15f); yield return new WaitForEndOfFrame(); CaptureInterface(directory, "04-briefing");
            Require(!Review.Manager.IsRunning && !WorldVisible, "Briefing must not start a case.");
            Click("Begin training"); yield return null; yield return new WaitForSecondsRealtime(.15f); yield return new WaitForEndOfFrame(); CaptureInterface(directory, "05-training");
            Require(Review.Manager.IsRunning && Page == ExperiencePage.Training, "Start button must open training.");
            Click("Open actions"); yield return null; yield return new WaitForSecondsRealtime(.15f); yield return new WaitForEndOfFrame(); CaptureInterface(directory, "06-actions");
            Click("Pause training"); yield return null;
            double frozen = Review.Manager.ElapsedSeconds;
            yield return new WaitForSecondsRealtime(.3f);
            Require(Math.Abs(Review.Manager.ElapsedSeconds - frozen) < .01, "Pause must freeze simulation time.");
            yield return new WaitForSecondsRealtime(.15f); yield return new WaitForEndOfFrame(); CaptureInterface(directory, "07-pause");
            Click("Resume training"); yield return null;
            foreach (var id in Review.Selected.medical.recommendedSequence)
            {
                var earliest = Review.Manager.MedicalSession.EarliestTime(id);
                if (earliest > Review.Manager.MedicalSession.Elapsed) Review.Manager.AdvanceTrainingTime(earliest - Review.Manager.MedicalSession.Elapsed + .01);
                Review.Submit(id);
            }
            Click("Finish training"); yield return null; Click("Confirm finish"); yield return null; yield return new WaitForSecondsRealtime(.15f); yield return new WaitForEndOfFrame();
            Require(Page == ExperiencePage.Results && Review.Score == 100, "The existing reference sequence must retain its score.");
            CaptureInterface(directory, "08-results");
            Click("Results tab 2"); yield return null; yield return new WaitForSecondsRealtime(.15f); yield return new WaitForEndOfFrame(); CaptureInterface(directory, "09-timeline");
            Click("Repeat training"); yield return null;
            Click("Assessment mode"); yield return null; Click("Begin training"); yield return null;
            Require(MonitorValue("spo2") == "—", "Assessment must not expose unmeasured saturation.");
            yield return new WaitForSecondsRealtime(.15f); yield return new WaitForEndOfFrame(); CaptureInterface(directory, "10-assessment");
            Review.Manager.FinishCase(); yield return null;
            Click("Results home"); yield return null;
            Require(Page == ExperiencePage.Welcome && !Review.Manager.IsRunning && WorldVisible == !IsDesktop, "Returning home must restore the welcome view without an active case.");
        }
        void Click(string name)
        {
            var button = pageRoot.GetComponentsInChildren<Button>().Single(b => b.name == name);
            Require(button.interactable, "Button disabled: " + name);
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            SettleSnapshotTransition();
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        void CaptureInterface(string directory, string name)
        {
            var mode = canvas.renderMode; var previousCamera = canvas.worldCamera; float distance = canvas.planeDistance;
            try
            {
                // A hidden D3D12 player has no readable system backbuffer. Render the real UI to an offscreen camera target.
                if (IsDesktop) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = viewer; canvas.planeDistance = .5f; }
                Canvas.ForceUpdateCanvases();
                SaveCaptureEvidence(directory, name, "learner-view");
            }
            finally { canvas.renderMode = mode; canvas.worldCamera = previousCamera; canvas.planeDistance = distance; Canvas.ForceUpdateCanvases(); }
        }

        int EvidenceWidth => QuestLookSimulation.Enabled ? QuestLookSimulation.CaptureWidth : Screen.width;
        int EvidenceHeight => QuestLookSimulation.Enabled ? QuestLookSimulation.CaptureHeight : Screen.height;

        void SaveCaptureEvidence(string directory, string name, string purpose)
        {
            if (QuestLookSimulation.Enabled)
            {
                Require(!IsDesktop && canvas.renderMode == RenderMode.WorldSpace,
                    "Quest-look evidence requires the VR path and a world-space canvas.");
                Require(Mathf.Abs(viewer.fieldOfView - QuestLookSimulation.FieldOfView) < .1f,
                    "Quest-look evidence requires the configured simulated eye FOV.");
            }
            RuntimeCapture.Save(viewer, Path.Combine(directory, name + ".png"), EvidenceWidth, EvidenceHeight);
            var metadata = CaptureFrameMetadata.Read(viewer, canvas, EvidenceWidth, EvidenceHeight,
                QuestLookSimulation.Enabled, IsDesktop, purpose);
            File.WriteAllText(Path.Combine(directory, name + ".capture.json"), JsonUtility.ToJson(metadata, true));
        }
    }
}
