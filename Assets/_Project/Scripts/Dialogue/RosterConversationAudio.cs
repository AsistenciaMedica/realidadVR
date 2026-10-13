using System;
using System.Linq;
using EmergencyVR.Patient.Presentation;
using EmergencyVR.Scenarios;
using EmergencyVR.UI;
using UnityEngine;

namespace EmergencyVR.Dialogue
{
    /// <summary>Exact transcript-to-clip presentation. Observations and incapacitated patients stay silent.</summary>
    [DefaultExecutionOrder(190)]
    public sealed class RosterConversationAudio : MonoBehaviour
    {
        [Serializable] public sealed class Line { public string scenarioId, speaker, text, resource, voice; }
        [Serializable] public sealed class Manifest
        {
            public Line[] lines = Array.Empty<Line>();
            public Line FindExact(string scenarioId, PatientConversationLine response) => response == null ||
                (!response.SpokenByPatient && response.Speaker != "Testigo") ? null :
                lines.FirstOrDefault(entry => entry.scenarioId == scenarioId && entry.speaker == response.Speaker && entry.text == response.Text);
        }
        ReviewCaseSession review;
        ScenarioSupportingCast cast;
        PatientConversationController conversation;
        AudioSource patientVoice, activeVoice;
        Manifest manifest;
        readonly float[] speech = new float[128];
        float mouthWeight;
        public string LastSpokenText { get; private set; } = "";
        public AudioSource ActiveVoice => activeVoice;
        public bool SubtitleOnly { get; private set; }

        public static void Attach(ReviewCaseSession owner, ScenarioSupportingCast cast)
        {
            var audio = owner.gameObject.AddComponent<RosterConversationAudio>();
            audio.review = owner; audio.cast = cast;
            audio.patientVoice = SupportingActor.CreateVoice(owner.Procedures.Visuals.Rig.head, "Roster patient speech");
            owner.SelectionChanged += audio.Stop;
        }

        void Update()
        {
            if (conversation == null)
            {
                var flow = FindFirstObjectByType<TrainingExperience>();
                if (flow != null && flow.Review == review && flow.PatientConversation != null)
                { conversation = flow.PatientConversation; conversation.LinePresented += Presented; }
            }
            if (review == null || !review.Manager.IsRunning) Stop();
            else if (review.Manager.AcceptsInput && activeVoice == patientVoice && conversation != null && !conversation.CanAskPatient) patientVoice.Stop();
        }

        void Presented(PatientConversationLine line)
        {
            if (line == null) return;
            // A new subtitle replaces any older recording, including observations and unrecorded variants.
            Stop();
            SubtitleOnly = true;
            if (!line.SpokenByPatient && line.Speaker != "Testigo") return;
            if (line.SpokenByPatient && !conversation.CanAskPatient) return;
            var source = line.SpokenByPatient ? patientVoice : cast.Witness?.Voice;
            if (source == null) return;
            if (manifest == null)
            {
                var data = Resources.Load<TextAsset>("Audio/RosterVoices");
                if (data == null) return;
                manifest = JsonUtility.FromJson<Manifest>(data.text);
            }
            var entry = manifest.FindExact(review.Selected.medical?.id, line);
            if (entry == null) return;
            var clip = Resources.Load<AudioClip>(entry.resource);
            if (clip == null) { Debug.LogError("Missing roster speech clip: " + entry.resource); return; }
            activeVoice = source; source.clip = clip; source.Play(); LastSpokenText = line.Text;
            SubtitleOnly = false;
        }

        void LateUpdate()
        {
            if (review == null || review.Procedures.Visuals.ExternalBodyPresentation || Time.deltaTime <= 0 || AudioListener.pause) return;
            var rig = review.Procedures.Visuals.Rig;
            float target = activeVoice == patientVoice ? SupportingActor.SpeechWeight(patientVoice, speech) : 0;
            mouthWeight = Mathf.Lerp(mouthWeight, target, 1 - Mathf.Exp(-Time.deltaTime * 24));
            if (target > 0 || mouthWeight > .05f)
                SupportingActor.ApplySpeech(rig.face, rig.jawOpen, mouthWeight, speech);
            else SupportingActor.ClearVowels(rig.face);
        }

        void Stop()
        {
            if (activeVoice != null) { activeVoice.Stop(); activeVoice.clip = null; }
            activeVoice = null; LastSpokenText = "";
            SubtitleOnly = false;
            mouthWeight = 0;
            if (review != null && review.Procedures != null && !review.Procedures.Visuals.ExternalBodyPresentation)
                SupportingActor.ClearVowels(review.Procedures.Visuals.Rig.face);
        }
        void OnDestroy()
        {
            Stop();
            if (review != null) review.SelectionChanged -= Stop;
            if (conversation != null) conversation.LinePresented -= Presented;
            if (patientVoice != null) Destroy(patientVoice.gameObject);
        }
    }
}
