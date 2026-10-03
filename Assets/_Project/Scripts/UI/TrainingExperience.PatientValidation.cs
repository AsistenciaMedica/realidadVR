using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EmergencyVR.Dialogue;
using EmergencyVR.Desktop;
using EmergencyVR.Medical;
using EmergencyVR.Medical.Interaction;
using EmergencyVR.Patient.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace EmergencyVR.UI
{
    public sealed partial class TrainingExperience
    {
        [Serializable] sealed class PatientRosterSmokeReport
        {
            public int schemaVersion = 2;
            public string purpose = "Identity, presentation and conversation smoke. Clinical outcomes require the clinical suites.";
            public string supportReview = "Legacy seated cases must pass PatientSceneStaging.ValidateSupport before captures. Daniel uses separate CASE 01 choreography. Body captures still require visual review.";
            public string capturedUtc, buildVersion, result, failure;
            public List<PatientRosterSmokeSample> patients = new List<PatientRosterSmokeSample>();
            public bool danielRestored, legacyRestored;
            public bool headsetTested = false, questLookSimulation;
            public string verification, interactionMethod = "Semantic UI events for capture orchestration. XR pointer/trigger validation is a separate suite.";
            public int captureWidth, captureHeight;
        }
        [Serializable] sealed class PatientRosterSmokeSample
        {
            public string scenarioId, patientId, name, sex, appearanceResource, sourceModel, sourceCommit;
            public string briefingTitle, transcript, posture, screenshotPrefix;
            public string supportFailure, supportMeasurement;
            public int age, activePatientSkins;
            public bool provisionalRig, dialogueAvailable, patientAnswered, hasVoiceForResponse, supportRequired, supportVerified;
        }

        // Opt-in Windows/Quest player evidence, using the actual UI and active character renderer.
        IEnumerator PatientRosterSmoke()
        {
            var args = System.Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args, "-vital-capture-directory");
            string directory = flag >= 0 && flag + 1 < args.Length ? args[flag + 1] : QuestLookSimulation.Enabled
                ? Path.Combine(Application.persistentDataPath, "TestResults", "astra", "00-antes")
                : Path.Combine(Application.persistentDataPath, "PatientRosterPreview");
            Directory.CreateDirectory(directory);
            var report = new PatientRosterSmokeReport
            {
                capturedUtc = DateTime.UtcNow.ToString("O"), buildVersion = Application.version,
                questLookSimulation = QuestLookSimulation.Enabled, captureWidth = EvidenceWidth, captureHeight = EvidenceHeight,
                verification = QuestLookSimulation.Enabled ? "Verificado en simulación de apariencia Quest en Windows. Sin visor." : "Verificado en escritorio. Sin visor."
            };
            var steps = PatientRosterSmokeSteps(directory, report);
            bool passed = true;
            while (true)
            {
                object value = null; bool more = false;
                try { more = steps.MoveNext(); if (more) value = steps.Current; }
                catch (Exception error) { passed = false; report.failure = error.ToString(); Debug.LogException(error); }
                if (!more || !passed) break;
                yield return value;
            }
            report.result = passed ? "PASS" : "FAIL";
            File.WriteAllText(Path.Combine(directory, "patient-roster-smoke.json"), JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(directory, "result.txt"), report.result + "\n" + (report.failure ?? ""));
            Debug.Log("VITAL_PATIENT_ROSTER_SMOKE " + report.result);
            Application.Quit(passed ? 0 : 1);
        }

        IEnumerator PatientRosterSmokeSteps(string directory, PatientRosterSmokeReport report)
        {
            Application.targetFrameRate = 60;
            // Preserve the first-visit default in baseline menu evidence before the
            // assessment-only roster probe deliberately hides unacquired readings.
            if (QuestLookSimulation.Enabled)
            {
                yield return null; yield return new WaitForSecondsRealtime(.4f); yield return new WaitForEndOfFrame();
                Require(!IsDesktop && canvas.renderMode == RenderMode.WorldSpace, "Quest-look must use the world-space VR interface.");
                CaptureInterface(directory, "00-welcome");
                Navigate(ExperiencePage.Environments);
                yield return null; yield return new WaitForSecondsRealtime(.4f); yield return new WaitForEndOfFrame();
                CaptureInterface(directory, "00-environments");
                foreach (var environment in Review.Scope.environments)
                {
                    Browse(environment.id);
                    yield return null; yield return new WaitForSecondsRealtime(.4f); yield return new WaitForEndOfFrame();
                    CaptureInterface(directory, "00-catalog-" + environment.id);
                }
                Prepare(Array.FindIndex(Review.Catalog.entries, entry => entry.medical?.id == "review-hypotension-v2"));
                yield return null; yield return new WaitForSecondsRealtime(.4f); yield return new WaitForEndOfFrame();
                CaptureInterface(directory, "00-first-briefing-default-mode");
            }
            Review.Procedures.TrainingMode = false;
            yield return null;
            string[] ids = Review.Scope.ScenarioIds.ToArray();
            Require(ids.Length == 15, "The patient smoke requires the release roster of 15 cases.");
            var firstLegacy = ids.First(id => Review.Catalog.entries.Single(e => e.medical?.id == id).medical.clinicalV2 == null);
            for (int i = 0; i < ids.Length + 2; i++)
            {
                string id = i < ids.Length ? ids[i] : i == ids.Length ? "review-hypotension-v2" : firstLegacy;
                int index = Array.FindIndex(Review.Catalog.entries, e => e.medical?.id == id);
                var definition = Review.Catalog.entries[index].medical;
                var identity = definition.patientIdentity;
                bool captureUi = QuestLookSimulation.Enabled ? i < ids.Length : i == 0 || i == 1 || i == 5 || i == 10;
                string prefix = (i + 1).ToString("00") + "-" + id;
                Prepare(index); yield return null; yield return new WaitForSecondsRealtime(.4f); yield return new WaitForEndOfFrame();
                string title = pageRoot.GetComponentsInChildren<Text>().Single(t => t.name == "Page title").text;
                Require(title == LearnerTitle(definition), "Briefing leaked a diagnostic title: " + id);
                if (captureUi) CaptureInterface(directory, prefix + "-briefing");
                BeginTraining(); yield return null; yield return new WaitForSecondsRealtime(1.2f);
                var runtime = Review.Manager.MedicalSession;
                var visual = Review.Procedures.Visuals;
                var appearance = Resources.Load<PatientAppearance>(identity.appearanceResource);
                var binding = Review.GetComponent<PatientAppearanceController>();
                var skins = visual.Rig.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
                Require(!visual.NeedsHumanAsset && !visual.Rig.provisionalAsset, "Provisional patient: " + id);
                Require(skins.Length == 1 && skins[0] == visual.Rig.face, "Expected one active patient skin: " + id);
                Require(appearance != null && appearance.patientId == identity.id && visual.Rig.face.sharedMesh == appearance.mesh,
                    "Patient appearance does not match identity: " + id);
                Require(!string.IsNullOrWhiteSpace(appearance.sourceModel) && !string.IsNullOrWhiteSpace(appearance.sourceCommit), "Appearance has no source provenance: " + id);
                Require(binding != null && binding.ActivePatientId == identity.id, "Appearance controller was not rebound: " + id);
                Require(runtime.Patient.patientId == identity.id && runtime.Patient.patientName == identity.displayName &&
                    runtime.Patient.age == identity.age && runtime.Patient.sex == identity.sex, "Snapshot identity mismatch: " + id);
                Require(MonitorValue("spo2") == "—" && MonitorValue("bp") == "—", "Assessment leaked a measurement: " + id);
                var sample = new PatientRosterSmokeSample
                {
                    scenarioId = id, patientId = identity.id, name = identity.displayName, age = runtime.Patient.age,
                    sex = runtime.Patient.sex, appearanceResource = identity.appearanceResource,
                    sourceModel = appearance.sourceModel, sourceCommit = appearance.sourceCommit,
                    briefingTitle = title, activePatientSkins = skins.Length, provisionalRig = visual.Rig.provisionalAsset,
                    posture = visual.EffectivePosture.ToString(), screenshotPrefix = prefix,
                    supportRequired = definition.clinicalV2 == null && visual.EffectivePosture == PatientPosture.Seated
                };
                if (i < ids.Length) report.patients.Add(sample);
                if (sample.supportRequired)
                {
                    var staging = Review.GetComponent<PatientSceneStaging>();
                    Require(staging != null, "Missing seated support controller: " + id);
                    float until = Time.realtimeSinceStartup + 5f;
                    while (!staging.SupportVisible && string.IsNullOrEmpty(staging.LastValidationFailure) && Time.realtimeSinceStartup < until)
                        yield return null;
                    sample.supportVerified = staging.SupportVisible && staging.ValidateSupport();
                    sample.supportFailure = staging.LastValidationFailure;
                    sample.supportMeasurement = staging.LastSupportMeasurement;
                    if (!sample.supportVerified && string.IsNullOrEmpty(sample.supportFailure))
                        sample.supportFailure = "The seated pose did not settle with visible support within five seconds.";
                    Require(sample.supportVerified, "Invalid seated support for " + id + ": " + sample.supportFailure);
                }
                if (captureUi) CaptureInterface(directory, prefix + "-session");
                var portrait = CapturePatientPortrait(directory, prefix + "-patient");
                while (portrait.MoveNext()) yield return portrait.Current;
                if (visual.EffectivePosture == PatientPosture.Seated)
                {
                    var contact = CapturePatientPortrait(directory, prefix + "-seat-contact", true);
                    while (contact.MoveNext()) yield return contact.Current;
                }
                if (definition.clinicalV2 == null)
                {
                    Require(PatientConversation.Lines.Length == 0, "Conversation from a previous attempt leaked into " + id);
                    Click("Open patient"); yield return null;
                    Click("Patient name"); yield return null;
                    Click("Patient situation"); yield return null;
                    Click("Patient history"); yield return null;
                    Click("Witness account"); yield return null;
                    sample.dialogueAvailable = PatientConversation.CanAsk;
                    sample.transcript = PatientConversation.Transcript;
                    sample.patientAnswered = PatientConversation.Lines.Any(line => line.SpokenByPatient);
                    bool cannotSpeak = runtime.Patient.consciousness == "Unresponsive" || runtime.Patient.consciousness == "Drowsy" ||
                        runtime.Patient.respiration == "absent" || runtime.Patient.respiration == "agonal" || runtime.Patient.circulation == "pulseless";
                    Require(!cannotSpeak || !sample.patientAnswered, "An unresponsive patient spoke: " + id);
                    Require(runtime.Completed.Length == 0, "Conversation incorrectly submitted clinical actions: " + id);
                }
                else
                {
                    var dialogue = Review.GetComponent<ClinicalDialogueController>();
                    var response = dialogue.Ask(DialogueIntent.MAIN_SYMPTOM);
                    Require(response != null && !string.IsNullOrWhiteSpace(response.text), "Daniel's existing dialogue did not respond after binding.");
                    sample.dialogueAvailable = true; sample.patientAnswered = true;
                    sample.transcript = response.text; sample.hasVoiceForResponse = dialogue.HasVoiceForLastResponse;
                    Click("Open dialogue"); yield return null;
                }
                yield return new WaitForSecondsRealtime(.4f); yield return new WaitForEndOfFrame();
                if (captureUi) CaptureInterface(directory, prefix + "-conversation");
                TogglePause(); yield return null; yield return new WaitForSecondsRealtime(.4f); yield return new WaitForEndOfFrame();
                Require(!PatientConversation.CanAsk, "Conversation accepted input during pause: " + id);
                if (captureUi) CaptureInterface(directory, prefix + "-pause");
                TogglePause(); FinishTraining(); yield return null; yield return new WaitForSecondsRealtime(.4f); yield return new WaitForEndOfFrame();
                if (captureUi) CaptureInterface(directory, prefix + "-debrief");
                if (i == ids.Length) report.danielRestored = true;
                else if (i > ids.Length) report.legacyRestored = true;
            }
            Require(report.patients.Select(p => p.patientId).Distinct().Count() == 15, "Patient identities are not distinct.");
        }

        IEnumerator CapturePatientPortrait(string directory, string name, bool side = false)
        {
            var previousPosition = viewer.transform.position;
            var previousRotation = viewer.transform.rotation;
            bool previousCanvas = canvas.enabled;
            var responderRenderers = Review.Procedures.Hands.GetComponentsInChildren<ArticulatedHand>(true)
                .SelectMany(hand => hand.GetComponentsInChildren<Renderer>(true)).Distinct().ToArray();
            var rendering = responderRenderers.ToDictionary(renderer => renderer, renderer => renderer.forceRenderingOff);
            try
            {
                canvas.enabled = false;
                // This actor record has no operator viewpoint. Hide only the responder's hands,
                // sleeves and watch; retain the patient, equipment, furniture and all scenery.
                foreach (var renderer in responderRenderers) renderer.forceRenderingOff = true;
                var bounds = Review.Procedures.Visuals.Rig.face.bounds;
                Require(FindPatientCaptureView(bounds, side), "No unobstructed full-body capture position for " + name);
                // TrackedPoseDriver, body animation and gaze need frames after the
                // simulated HMD pose changes; rendering immediately captures stale eyes.
                yield return null; yield return new WaitForSecondsRealtime(.5f); yield return new WaitForEndOfFrame();
                Debug.Log("VITAL_PATIENT_CAPTURE " + name + " camera=" + viewer.transform.position.ToString("F3"));
                SaveCaptureEvidence(directory, name, side ? "actor-seat-contact-inspection-no-interface" : "actor-portrait-inspection-no-interface");
            }
            finally
            {
                foreach (var pair in rendering) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
                canvas.enabled = previousCanvas;
                SetCaptureHeadPose(previousPosition, previousRotation);
            }
        }

        bool FindPatientCaptureView(Bounds bounds, bool side)
        {
            var visual = Review.Procedures.Visuals;
            var body = visual.Rig.GetComponent<ArticulatedPatient>();
            var targets = new List<Vector3> { visual.Rig.head.position, visual.ChestAnchor.position, body.pelvis.position };
            targets.AddRange(body.ankles.Select(ankle => ankle.position + Vector3.up * .035f));
            // The first lateral candidate faces the mall's open aisle. Test the opposite
            // side and diagonals as well; a blocked view must never become a successful image.
            var directions = side ? new[] { Vector3.left, Vector3.right, new Vector3(-1, 0, -1), new Vector3(1, 0, -1), Vector3.back, Vector3.forward }
                : new[] { new Vector3(.9f, 0, -1.6f), new Vector3(-.9f, 0, -1.6f), Vector3.left, Vector3.right, Vector3.back, Vector3.forward };
            Physics.SyncTransforms();
            foreach (float distance in new[] { 2.1f, 2.5f, 2.9f })
                foreach (var direction in directions)
                {
                    var position = bounds.center + direction.normalized * distance + Vector3.up * (side ? .45f : 1.1f);
                    if (Physics.OverlapSphere(position, .15f, ~0, QueryTriggerInteraction.Ignore).Any(c => !IgnoreCaptureCollider(c))) continue;
                    bool blocked = false;
                    foreach (var target in targets)
                    {
                        var delta = position - target;
                        // Cast from the patient to the camera: this also catches entering a
                        // wall when the candidate camera would be inside its collider.
                        if (Physics.RaycastAll(target, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore)
                            .Any(hit => !IgnoreCaptureCollider(hit.collider))) { blocked = true; break; }
                    }
                    if (blocked) continue;
                    var rotation = Quaternion.LookRotation(bounds.center - position, Vector3.up);
                    bool framed = true;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        var point = bounds.center + Vector3.Scale(bounds.extents + Vector3.one * .06f,
                            new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                        // Evaluate candidate poses without racing TrackedPoseDriver.
                        var local = Quaternion.Inverse(rotation) * (point - position);
                        float halfHeight = Mathf.Max(.001f, local.z * Mathf.Tan(viewer.fieldOfView * Mathf.Deg2Rad * .5f));
                        var viewport = new Vector3(.5f + local.x / (2 * halfHeight * ((float)EvidenceWidth / EvidenceHeight)),
                            .5f + local.y / (2 * halfHeight), local.z);
                        if (viewport.z <= viewer.nearClipPlane || viewport.x < .03f || viewport.x > .97f || viewport.y < .03f || viewport.y > .97f)
                        { framed = false; break; }
                    }
                    if (framed) { SetCaptureHeadPose(position, rotation); return true; }
                }
            return false;
        }

        void SetCaptureHeadPose(Vector3 position, Quaternion rotation)
        {
            if (QuestLookSimulation.Instance != null) QuestLookSimulation.Instance.SetHeadPose(position, rotation);
            else viewer.transform.SetPositionAndRotation(position, rotation);
        }

        bool IgnoreCaptureCollider(Collider collider)
        {
            if (collider.transform.IsChildOf(Review.Procedures.Visuals.transform)) return true;
            if (collider.GetComponentInParent<ArticulatedHand>() != null) return true;
            return desktop != null && collider.transform.IsChildOf(desktop.transform);
        }
    }
}
