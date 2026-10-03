using System;
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
        [Serializable] public sealed class Manifest { public Line[] lines = Array.Empty<Line>(); }
        ReviewCaseSession review;
        ScenarioSupportingCast cast;
        PatientConversationController conversation;
        AudioSource patientVoice, activeVoice;
        Manifest manifest;
        readonly float[] speech = new float[128];
        public string LastSpokenText { get; private set; } = "";
        public AudioSource ActiveVoice => activeVoice;

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
            if (line == null || (!line.SpokenByPatient && line.Speaker != "Testigo")) return;
            if (line.SpokenByPatient && !conversation.CanAskPatient) return;
            var source = line.SpokenByPatient ? patientVoice : cast.Witness?.Voice;
            if (source == null) return;
            if (manifest == null)
            {
                var data = Resources.Load<TextAsset>("Audio/RosterVoices");
                if (data == null) return;
                manifest = JsonUtility.FromJson<Manifest>(data.text);
            }
            foreach (var entry in manifest.lines)
            {
                if (entry.scenarioId != review.Selected.medical?.id || entry.speaker != line.Speaker || entry.text != line.Text) continue;
                var clip = Resources.Load<AudioClip>(entry.resource);
                if (clip == null) { Debug.LogError("Missing roster speech clip: " + entry.resource); return; }
                Stop(); activeVoice = source; source.clip = clip; source.Play(); LastSpokenText = line.Text;
                return;
            }
        }

        void LateUpdate()
        {
            if (activeVoice != patientVoice || patientVoice == null || !patientVoice.isPlaying) return;
            var rig = review.Procedures.Visuals.Rig;
            rig.SetBlendShape(rig.jawOpen, SupportingActor.SpeechWeight(patientVoice, speech));
        }

        void Stop()
        {
            if (activeVoice != null) { activeVoice.Stop(); activeVoice.clip = null; }
            activeVoice = null; LastSpokenText = "";
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
