using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using EmergencyVR.Desktop;
using UnityEngine;

namespace EmergencyVR.UI
{
    public sealed partial class TrainingExperience
    {
        [Serializable] public sealed class RenderBudgetReport
        {
            public int schemaVersion = 1;
            public string sourceSha256, runId, result, failure;
            public string verification = "Windows Development player, normal single-eye camera; no Quest hardware or FPS claim";
            public List<QuestRenderBudget.Sample> samples = new List<QuestRenderBudget.Sample>();
            public List<string> runtimeErrors = new List<string>();
        }

        IEnumerator RenderBudgetSmoke()
        {
            var args = System.Environment.GetCommandLineArgs();
            int outputIndex = Array.IndexOf(args, "-vital-budget-output");
            int runIndex = Array.IndexOf(args, "-vital-budget-run-id");
            if (outputIndex < 0 || outputIndex + 1 >= args.Length)
            {
                Debug.LogError("Render budget requires -vital-budget-output <new directory>.");
                Application.Quit(1); yield break;
            }
            string directory = Path.GetFullPath(args[outputIndex + 1]);
            Directory.CreateDirectory(directory);
            var report = new RenderBudgetReport { runId = runIndex >= 0 && runIndex + 1 < args.Length ? args[runIndex + 1] : Guid.NewGuid().ToString("N") };
            var steps = RenderBudgetSteps(directory, report);
            bool success = true;
            Application.LogCallback onLog = (message, trace, type) => {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    report.runtimeErrors.Add(message + "\n" + trace);
            };
            Application.logMessageReceived += onLog;
            try
            {
                while (true)
                {
                    object value = null; bool more = false;
                    try { more = steps.MoveNext(); if (more) value = steps.Current; }
                    catch (Exception error) { success = false; report.failure = error.ToString(); Debug.LogException(error); }
                    if (!more || !success) break;
                    yield return value;
                }
            }
            finally { (steps as IDisposable)?.Dispose(); Application.logMessageReceived -= onLog; }
            report.result = success && report.runtimeErrors.Count == 0 && report.samples.Count == 15 && report.samples.All(sample => sample.passed) ? "PASS" : "FAIL";
            File.WriteAllText(Path.Combine(directory, "budget-report.json"), JsonUtility.ToJson(report, true));
            WriteRenderBudgetMarkdown(directory, report);
            File.WriteAllText(Path.Combine(directory, "result.txt"), report.result + "\n" + (report.failure ?? ""));
            Debug.Log("VITAL_RENDER_BUDGET " + report.result + " source=" + report.sourceSha256 + " cases=" + report.samples.Count);
            Application.Quit(report.result == "PASS" ? 0 : 1);
        }

        IEnumerator RenderBudgetSteps(string directory, RenderBudgetReport report)
        {
            Require(!Application.isEditor && Debug.isDebugBuild, "Render budgets require a Development player.");
            Require(QuestLookSimulation.Enabled && !IsDesktop, "Render budgets require -vital-quest-look.");
            Require(!File.Exists(Path.Combine(directory, "budget-report.json")), "Use a fresh output directory for each budget run.");
            var fingerprint = ValidationSourceFingerprint.LoadBuilt();
            Require(fingerprint.developmentBuild, "Fingerprint must belong to a validation Development build.");
            report.sourceSha256 = fingerprint.sourceSha256;
            Application.runInBackground = true; Application.targetFrameRate = 60;
            var simulation = QuestLookSimulation.Instance;
            simulation.ManualControlsEnabled = false;
            yield return new WaitForSecondsRealtime(.45f);
            Display.main.SetRenderingResolution(QuestLookSimulation.CaptureWidth, QuestLookSimulation.CaptureHeight);
            // A windowed player may initially be clamped to the host monitor. The test
            // resizes only its own child window; wait for the real camera/backbuffer to follow.
            float sizeDeadline = Time.realtimeSinceStartup + 15;
            while ((viewer.pixelWidth != QuestLookSimulation.CaptureWidth || viewer.pixelHeight != QuestLookSimulation.CaptureHeight)
                && Time.realtimeSinceStartup < sizeDeadline) yield return null;
            Require(viewer.pixelWidth == QuestLookSimulation.CaptureWidth && viewer.pixelHeight == QuestLookSimulation.CaptureHeight,
                "Player backbuffer did not reach 2064x2208: camera=" + viewer.pixelWidth + "x" + viewer.pixelHeight +
                ", screen=" + Screen.width + "x" + Screen.height + ", display=" + Display.main.renderingWidth + "x" + Display.main.renderingHeight);
            // Let URP retire the temporary attachments from the initial window size.
            for (int frame = 0; frame < 120; frame++) yield return new WaitForEndOfFrame();
            Require(Review.Scope.ScenarioIds.Length == 15, "The validation release must have 15 cases.");
            foreach (string id in Review.Scope.ScenarioIds)
            {
                Prepare(Array.FindIndex(Review.Catalog.entries, entry => entry.medical?.id == id));
                yield return null;
                BeginTraining(); yield return null;
                simulation.FocusPatient(Review.Procedures.Visuals.ChestAnchor.position);
                yield return null; yield return null;
                // The camera renders to its ordinary player backbuffer. No capture target or manual render.
                Require(viewer.enabled && viewer.targetTexture == null, "Budget camera must use the normal player backbuffer.");
                for (int frame = 0; frame < 8; frame++) yield return new WaitForEndOfFrame();
                using (var recorder = new QuestRenderBudget())
                {
                    for (int frame = 0; frame < 40; frame++) yield return new WaitForEndOfFrame();
                    var sample = recorder.Read(viewer, id);
                    sample.sourceSha256 = report.sourceSha256; sample.runId = report.runId;
                    sample.failures = QuestRenderBudget.Failures(sample).ToArray(); sample.passed = sample.failures.Length == 0;
                    report.samples.Add(sample);
                    File.WriteAllText(Path.Combine(directory, "budget-" + id + ".json"), JsonUtility.ToJson(sample, true));
                }
                Review.Manager.FinishCase(); yield return null;
                Navigate(ExperiencePage.Welcome); yield return null;
            }
        }

        static void WriteRenderBudgetMarkdown(string directory, RenderBudgetReport report)
        {
            var text = new StringBuilder("# Rendering budgets — Windows player\n\n");
            text.AppendLine(report.verification + ". Resident texture totals include all loaded Texture objects and render targets; no Editor objects or capture render target are created.");
            text.AppendLine("\nSource SHA256: `" + report.sourceSha256 + "`\n\nResult: **" + report.result + "**\n");
            text.AppendLine("| Case | Batches ≤100 | Triangles ≤300000 | Realtime lights ≤1 | Texture MiB ≤250 | MSAA | Result |");
            text.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | --- |");
            foreach (var sample in report.samples)
                text.AppendLine($"| {sample.scenario} | {sample.peakBatches} | {sample.peakTriangles} | {sample.realtimeLights} | {(sample.textureBytes / 1048576d).ToString("F1", CultureInfo.InvariantCulture)} | {sample.msaaSamples} | {(sample.passed ? "PASS" : "FAIL")} |");
            foreach (var sample in report.samples.Where(sample => !sample.passed))
                text.AppendLine("\n" + sample.scenario + ": " + string.Join("; ", sample.failures));
            if (!string.IsNullOrEmpty(report.failure)) text.AppendLine("\n" + report.failure);
            foreach (string error in report.runtimeErrors) text.AppendLine("\nRuntime error: " + error);
            File.WriteAllText(Path.Combine(directory, "budgets.md"), text.ToString());
        }
    }
}
