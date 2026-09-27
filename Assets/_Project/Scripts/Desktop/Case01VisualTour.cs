using System;
using System.Collections;
using System.IO;
using System.Linq;
using EmergencyVR.Dialogue;
using EmergencyVR.Medical;
using EmergencyVR.Patient.Presentation;
using EmergencyVR.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EmergencyVR.Desktop
{
    /// <summary>
    /// Visual QA tour of CASE 01 in a real desktop player (-vital-case01-tour &lt;dir&gt;): walks the guided
    /// practice through the voiced 112 call, the assisted transition and the paramedics' arrival, capturing
    /// fixed viewpoints. Normal launches never install it.
    /// </summary>
    public sealed class Case01VisualTour : MonoBehaviour
    {
        string output;
        TrainingExperience flow;
        DesktopDemoController desktop;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (!System.Environment.GetCommandLineArgs().Contains("-vital-case01-tour")) return;
            SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
        }
        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "TrainingRoom") new GameObject("CASE01 visual tour").AddComponent<Case01VisualTour>();
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            var args = System.Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-vital-case01-tour");
            output = index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("-") ? args[index + 1] : Path.Combine(Application.persistentDataPath, "Case01Tour");
            Directory.CreateDirectory(output);
            var run = Run(); Exception failure = null;
            while (true)
            {
                object next = null; bool more = false;
                try { more = run.MoveNext(); if (more) next = run.Current; } catch (Exception error) { failure = error; }
                if (failure != null || !more) break;
                yield return next;
            }
            File.WriteAllText(Path.Combine(output, "result.txt"), failure == null ? "PASS" : "FAIL\n" + failure);
            if (failure != null) { Debug.LogException(failure); yield return Capture("99-failure", true); }
            Debug.Log("CASE01_TOUR " + (failure == null ? "PASS" : "FAIL " + failure.Message));
            Application.Quit(failure == null ? 0 : 1);
        }

        IEnumerator Run()
        {
            yield return null; yield return null; yield return null;
            flow = FindFirstObjectByType<TrainingExperience>(); desktop = FindFirstObjectByType<DesktopDemoController>();
            int index = Array.FindIndex(flow.Review.Catalog.entries, e => e.medical?.id == "review-hypotension-v2");
            if (index < 0) throw new InvalidOperationException("CASE01 missing.");
            flow.Review.Procedures.TrainingMode = true;
            flow.Prepare(index); yield return null;
            flow.BeginTraining(); yield return Wait(1.5f);
            var manager = flow.Review.Manager;
            var body = flow.Review.GetComponent<Case01PatientPresentation>();
            var phone = flow.Review.GetComponent<PhoneCallController>();
            desktop.Body.enabled = false;

            View(new Vector3(-2.6f, 1.62f, -2.4f), body.HeadPosition + Vector3.left * .3f);
            yield return Wait(1); yield return Capture("01-overview", false);
            yield return Capture("02-overview-guided", true);
            View(new Vector3(-.35f, 1.62f, -.45f), body.HeadPosition);
            yield return Wait(.5f); yield return Capture("03-daniel", false);
            View(new Vector3(.05f, 1.5f, .05f), body.HeadPosition);
            manager.Dialogue.Ask(DialogueIntent.GREETING);
            manager.TrySubmitAction("AssessResponsiveness");
            yield return Wait(.6f); yield return Capture("04-touch-shoulder", false);
            manager.TrySubmitAction("ObserveBreathing");
            foreach (var intent in new[] { DialogueIntent.MAIN_SYMPTOM, DialogueIntent.ONSET, DialogueIntent.LOSS_OF_CONSCIOUSNESS, DialogueIntent.CHEST_PAIN, DialogueIntent.BREATHING_DIFFICULTY })
                manager.Dialogue.Ask(intent);
            yield return Wait(.5f); yield return Capture("05-guided-step", true);

            foreach (var key in "112") phone.Press(key);
            phone.Dial(); yield return Wait(1); yield return Capture("06-dialing-handset", true);
            float deadline = Time.realtimeSinceStartup + 20;
            while (phone.Stage == PhoneCallStage.Ringing && Time.realtimeSinceStartup < deadline) yield return null;
            yield return Wait(.5f); yield return Capture("07-operator", true);
            for (int guard = 0; guard < 20 && phone.Stage == PhoneCallStage.Talking && phone.Current != null; guard++)
            {
                int choice = phone.Current.Options.FindIndex(o => o.Recommended == null || o.Recommended());
                phone.Choose(Math.Max(0, choice)); yield return Wait(.3f);
            }
            if (phone.Stage != PhoneCallStage.EnRoute) throw new InvalidOperationException("Call did not dispatch: " + phone.Stage + " / " + phone.Subtitle);

            manager.Dialogue.Ask(DialogueIntent.CONSENT_HELP);
            if (!body.BeginAssistance()) throw new InvalidOperationException("Assist: " + body.LastValidationFailure + " / " + body.Instruction);
            body.SetSupportHeld(true);
            deadline = Time.realtimeSinceStartup + body.assistanceSeconds + 15; bool halfway = false;
            while (!body.FinalPositionValidated && Time.realtimeSinceStartup < deadline)
            {
                if (!halfway && body.Progress > .45f) { halfway = true; yield return Capture("08-assisting", true); }
                yield return null;
            }
            body.SetSupportHeld(false);
            yield return Wait(.5f); yield return Capture("09-supine", false);

            manager.AdvanceTrainingTime(80); yield return Wait(1);
            View(new Vector3(-2.2f, 1.62f, -1.6f), body.PelvisPosition);
            yield return Wait(4); yield return Capture("10-paramedics-walking", false);
            yield return Wait(4); yield return Capture("11-paramedics-arrived", false);
            View(new Vector3(-1.2f, 1.62f, .8f), new Vector3(3.6f, 1.6f, .4f));
            yield return Wait(.5f); yield return Capture("12-windows", false);
            View(new Vector3(2.4f, 1.62f, -2.2f), new Vector3(-1.5f, 1.2f, 3.8f));
            yield return Wait(.5f); yield return Capture("13-strength-area", false);
        }

        void View(Vector3 eye, Vector3 target)
        {
            desktop.transform.position = new Vector3(eye.x, 0, eye.z);
            var direction = target - eye; var flat = new Vector3(direction.x, 0, direction.z);
            desktop.transform.rotation = Quaternion.LookRotation(flat.sqrMagnitude > .0001f ? flat : Vector3.forward);
            float pitch = -Mathf.Atan2(direction.y, flat.magnitude) * Mathf.Rad2Deg;
            desktop.View.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            desktop.View.transform.localPosition = Vector3.up * eye.y;
            Physics.SyncTransforms();
        }

        static IEnumerator Wait(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }

        IEnumerator Capture(string name, bool interfaceVisible)
        {
            bool previous = flow.InterfaceCanvas.enabled;
            flow.InterfaceCanvas.enabled = interfaceVisible;
            yield return null;
            yield return new WaitForEndOfFrame();
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), RuntimeCapture.EncodePng(texture)); Destroy(texture);
            flow.InterfaceCanvas.enabled = previous;
        }
    }
}
