#if UNITY_EDITOR
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using EmergencyVR.Desktop;
using EmergencyVR.Scenarios;
using EmergencyVR.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EmergencyVR.Tests
{
    [PrebuildSetup(SimulatedXRTestHooks.Setup), PostBuildCleanup(SimulatedXRTestHooks.Setup)]
    public sealed class QuestRenderBudgetTests
    {
        [UnityTest, Timeout(200000)]
        public IEnumerator AllReleaseCasesStayWithinQuestRenderingBudgets()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string executable = Path.Combine(project, "Builds/AstraValidation/VITAL-VR.exe");
            Assert.That(File.Exists(executable), Is.True, "Build the current sources with Invoke-AstraValidation.ps1 -Task BuildBudget first.");
            string sidecarPath = executable + ".fingerprint.json";
            Assert.That(File.Exists(sidecarPath), Is.True, "Validation build fingerprint sidecar is required.");
            var expected = ValidationSourceFingerprint.Compute(project, true);
            var built = JsonUtility.FromJson<ValidationSourceFingerprint.Manifest>(File.ReadAllText(sidecarPath));
            Assert.That(built, Is.Not.Null);
            ValidationSourceFingerprint.RequireMatch(expected.sourceSha256, built.sourceSha256);
            Assert.That(built.developmentBuild, Is.True);
            string runId = Guid.NewGuid().ToString("N");
            string output = Path.Combine(project, "TestResults/astra/player-budget/" + runId);
            Directory.CreateDirectory(output);
            string log = Path.Combine(output, "player.log");
            var start = new ProcessStartInfo {
                FileName = executable, WorkingDirectory = project, UseShellExecute = false,
                CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = "-vital-quest-look -vital-render-budget -vital-budget-output " + Quote(output) +
                    " -vital-budget-run-id " + runId + " -screen-width 2064 -screen-height 2208 -screen-fullscreen 0 -logFile " + Quote(log)
            };
            // No batchmode or nographics flags: the standalone camera must actually render every frame.
            using (var process = Process.Start(start))
            {
                Assert.That(process, Is.Not.Null);
                var elapsed = Stopwatch.StartNew();
#if UNITY_EDITOR_WIN
                var windowSize = new ChildPlayerWindowSize(process);
                double nextWindowCheck = 0;
#endif
                try
                {
                    while (!process.HasExited && elapsed.Elapsed.TotalSeconds < 180)
                    {
#if UNITY_EDITOR_WIN
                        // Unity can recreate/reset its window during startup. Recheck the exact child
                        // for a bounded startup interval; never touch the Editor or another application.
                        if (windowSize.IsStartupWindowCheckNeeded && elapsed.Elapsed.TotalSeconds >= nextWindowCheck)
                        {
                            windowSize.TryResize();
                            nextWindowCheck = elapsed.Elapsed.TotalSeconds + .1;
                        }
#endif
                        yield return null;
                    }
                    Assert.That(process.HasExited, Is.True, "Validation player exceeded 180 seconds. See " + log);
                    string reportPath = Path.Combine(output, "budget-report.json");
                    Assert.That(File.Exists(reportPath), Is.True, "Player emitted no report. See " + log);
                    var report = JsonUtility.FromJson<TrainingExperience.RenderBudgetReport>(File.ReadAllText(reportPath));
                    Assert.That(report, Is.Not.Null);
                    Assert.That(report.schemaVersion, Is.EqualTo(1));
                    ValidationSourceFingerprint.RequireMatch(expected.sourceSha256, report.sourceSha256);
                    Assert.That(report.runId, Is.EqualTo(runId), "Stale evidence from a different invocation is not valid.");
                    // Preserve this invocation's evidence even when a genuine budget fails.
                    // Identity is checked before publishing so an old build cannot replace current reports.
                    string evidence = Path.Combine(project, "TestResults/astra");
                    File.Copy(Path.Combine(output, "budgets.md"), Path.Combine(evidence, "budgets.md"), true);
                    File.Copy(reportPath, Path.Combine(evidence, "budget-result.json"), true);
                    foreach (string samplePath in Directory.GetFiles(output, "budget-*.json"))
                        if (Path.GetFileName(samplePath) != "budget-report.json")
                            File.Copy(samplePath, Path.Combine(evidence, Path.GetFileName(samplePath)), true);
                    Assert.That(report.failure, Is.Null.Or.Empty, report.failure);
                    Assert.That(report.runtimeErrors, Is.Empty, "The validation player emitted runtime errors. See " + log);
                    var scope = JsonUtility.FromJson<ReleaseScope>(Resources.Load<TextAsset>("ReleaseScope").text);
                    CollectionAssert.AreEquivalent(scope.ScenarioIds, report.samples.Select(sample => sample.scenario));
                    Assert.That(report.samples, Has.Count.EqualTo(15));
                    foreach (var sample in report.samples)
                    {
                        ValidationSourceFingerprint.RequireMatch(expected.sourceSha256, sample.sourceSha256);
                        Assert.That(sample.runId, Is.EqualTo(runId));
                        string samplePath = Path.Combine(output, "budget-" + sample.scenario + ".json");
                        Assert.That(File.Exists(samplePath), Is.True);
                        var diskSample = JsonUtility.FromJson<QuestRenderBudget.Sample>(File.ReadAllText(samplePath));
                        Assert.That(JsonUtility.ToJson(diskSample), Is.EqualTo(JsonUtility.ToJson(sample)), "Per-case evidence must equal the summary.");
                        Assert.That(QuestRenderBudget.Failures(sample).ToArray(), Is.Empty, sample.scenario + ": " + string.Join("; ", sample.failures));
                        Assert.That(sample.passed, Is.True);
                    }
                    Assert.That(report.result, Is.EqualTo("PASS"));
                    Assert.That(File.ReadAllText(Path.Combine(output, "result.txt")).Trim(), Is.EqualTo("PASS"));
                    Assert.That(process.ExitCode, Is.Zero, "Player returned failure. See " + log);
                    UnityEngine.Debug.Log("VITAL_PLAYER_BUDGET_PASS source=" + report.sourceSha256 + " evidence=" + output);
                }
                finally
                {
                    try
                    {
#if UNITY_EDITOR_WIN
                        File.WriteAllText(Path.Combine(output, "window-size.json"), JsonUtility.ToJson(windowSize.Evidence, true));
                        UnityEngine.Debug.Log("VITAL_PLAYER_WINDOW_SIZE " + JsonUtility.ToJson(windowSize.Evidence));
#endif
                    }
                    finally
                    {
                        // Only the exact player spawned by this fixture is terminated on timeout/failure.
                        if (!process.HasExited) process.Kill();
                    }
                }
            }
        }

#if UNITY_EDITOR_WIN
        [Serializable]
        sealed class WindowSizeEvidence
        {
            public int processId, targetWidth = 2064, targetHeight = 2208;
            public int inspections, resizeRequests, lastClientWidth, lastClientHeight;
            public bool observedExactClientSize;
            public string windowClass = "UnityWndClass", lastResult = "Waiting for the child Unity window.";
        }

        sealed class ChildPlayerWindowSize
        {
            const uint NoMove = 0x0002, NoZOrder = 0x0004, NoActivate = 0x0010, NoSendChanging = 0x0400;
            readonly Process child;
            readonly int childId;
            readonly Stopwatch sinceWindowFound = new Stopwatch();
            public readonly WindowSizeEvidence Evidence;
            // Start this interval at the first native window, not Process.Start: cold startup may be slow.
            public bool IsStartupWindowCheckNeeded => !sinceWindowFound.IsRunning || sinceWindowFound.Elapsed.TotalSeconds <= 20;

            public ChildPlayerWindowSize(Process childProcess)
            {
                child = childProcess;
                childId = child.Id;
                Evidence = new WindowSizeEvidence { processId = childId };
            }

            public void TryResize()
            {
                if (child.HasExited) return;
                Evidence.inspections++;
                bool found = false;
                EnumWindows((window, _) =>
                {
                    GetWindowThreadProcessId(window, out uint owner);
                    if (owner != (uint)childId || child.HasExited) return true;
                    var name = new StringBuilder(256);
                    if (GetClassName(window, name, name.Capacity) == 0 || name.ToString() != Evidence.windowClass) return true;
                    found = true;
                    if (!sinceWindowFound.IsRunning) sinceWindowFound.Start();
                    if (!GetClientRect(window, out var client) || !GetWindowRect(window, out var outer))
                    {
                        Evidence.lastResult = "Cannot read child window rectangles: Win32 " + Marshal.GetLastWin32Error();
                        return true;
                    }
                    Evidence.lastClientWidth = client.right - client.left;
                    Evidence.lastClientHeight = client.bottom - client.top;
                    if (Evidence.lastClientWidth == Evidence.targetWidth && Evidence.lastClientHeight == Evidence.targetHeight)
                    {
                        Evidence.observedExactClientSize = true;
                        Evidence.lastResult = "Measured the requested client area; camera dimensions remain independently asserted by the player.";
                        return true;
                    }
                    int borderWidth = outer.right - outer.left - Evidence.lastClientWidth;
                    int borderHeight = outer.bottom - outer.top - Evidence.lastClientHeight;
                    if (borderWidth < 0 || borderHeight < 0 || Evidence.lastClientWidth <= 0 || Evidence.lastClientHeight <= 0)
                    {
                        Evidence.lastResult = "Child window geometry is not ready for a client-area resize.";
                        return true;
                    }
                    // Recheck both the handle owner and the original Process immediately before the
                    // native mutation. An unrelated window must never be resized or activated.
                    GetWindowThreadProcessId(window, out owner);
                    if (owner != (uint)childId || child.HasExited) return false;
                    Evidence.resizeRequests++;
                    bool resized = SetWindowPos(window, IntPtr.Zero, 0, 0,
                        Evidence.targetWidth + borderWidth, Evidence.targetHeight + borderHeight,
                        NoMove | NoZOrder | NoActivate | NoSendChanging);
                    Evidence.lastResult = resized ? "Requested the target client area without moving or activating the child window."
                        : "SetWindowPos failed: Win32 " + Marshal.GetLastWin32Error();
                    return true;
                }, IntPtr.Zero);
                if (!found) Evidence.lastResult = "No live UnityWndClass window belonging to the child PID yet.";
            }

            [StructLayout(LayoutKind.Sequential)]
            struct NativeRect { public int left, top, right, bottom; }

            [return: MarshalAs(UnmanagedType.Bool)]
            delegate bool EnumWindowCallback(IntPtr window, IntPtr parameter);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);

            [DllImport("user32.dll", SetLastError = true)]
            static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

            [DllImport("user32.dll", EntryPoint = "GetClassNameW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
            static extern int GetClassName(IntPtr window, StringBuilder className, int maximumLength);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool GetClientRect(IntPtr window, out NativeRect rectangle);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool GetWindowRect(IntPtr window, out NativeRect rectangle);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
        }
#endif

        static string Quote(string value)
        {
            if (value.Contains("\"")) throw new ArgumentException("Quote characters are not supported in validation paths.");
            return "\"" + value + "\"";
        }
    }
}
#endif
