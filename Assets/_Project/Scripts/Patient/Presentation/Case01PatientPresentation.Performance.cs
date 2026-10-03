using System.Collections.Generic;
using UnityEngine;

namespace EmergencyVR.Patient.Presentation
{
    /// <summary>
    /// Daniel's performance layer, applied after the authored pose and attention: lip movement from his real voice,
    /// optional motion-capture detail and autonomous, anatomically targeted seated gestures.
    /// The acting clock pauses independently of physiology and does not require animation resources.
    /// </summary>
    public sealed partial class Case01PatientPresentation
    {
        static readonly string[] Trunk = { "Spine", "Spine1", "Spine2", "Neck", "Head" };
        const string IdleClip = "m_sit_chair_idle_nervous_01";

        Transform sampler;
        readonly Dictionary<string, Transform> liveBones = new Dictionary<string, Transform>();
        readonly Dictionary<string, Transform> sampledBones = new Dictionary<string, Transform>();
        readonly Dictionary<string, Quaternion> idleReference = new Dictionary<string, Quaternion>();
        AnimationClip idle;
        readonly float[] voiceSamples = new float[256];
        enum PresenceGesture { Settle, Temple, Speaking }
        PresenceGesture presenceGesture;
        static readonly float[] PresenceIntervals = { 7.2f, 9.6f, 6.7f, 10.3f };
        float mouth, idleWeight, gestureAt = -1, nextGesture = 1.6f, performanceTime, nextSpeechGesture;
        int presenceSequence;
        bool samplerAttempted;
        float PerformanceDelta => review != null && !review.Manager.IsPaused ? Time.deltaTime : 0;
        bool HasVoluntaryPresence => visual != null && !visual.VisualState.Seizure &&
            visual.VisualState.Consciousness != PatientConsciousness.Unresponsive &&
            visual.VisualState.Consciousness != PatientConsciousness.Drowsy;
        bool IsSymptomatic => Clinical?.ClinicalStateId == "HYP_00_INITIAL_PRESYNCOPE" ||
            Clinical?.ClinicalStateId == "HYP_03_PERSISTENT_SYMPTOMS" || Clinical?.ClinicalStateId == "HYP_04_RECURRENT_PRESYNCOPE";

        void PresentPerformance()
        {
            performanceTime += PerformanceDelta;
            if (!HasVoluntaryPresence)
            {
                gestureAt = -1; idleWeight = 0;
                return; // The medical layer retains closed eyes, jaw and unsupported/unresponsive signs.
            }
            PresentSpeech();
            PresentNaturalBlink();
            bool seated = !busy && !risePending && !standing && !regaining && progress <= 0 && !completed &&
                review.Procedures?.CPR?.Touching != true && visual.CompressionDepthMetres <= .001f;
            idleWeight = Mathf.MoveTowards(idleWeight, seated ? 1 : 0, PerformanceDelta * 1.5f);
            // Clips enrich the idle; they must never gate basic human presence or a procedural gesture.
            if (seated && EnsureSampler() && idleWeight > .001f && idle != null && idle.length > .01f)
            {
                CharacterAnimationLibrary.Sample(idle, sampler.gameObject, performanceTime % idle.length);
                foreach (var name in Trunk)
                {
                    // Attending to the learner keeps the head on them; the trunk keeps breathing and shifting.
                    float weight = idleWeight * (name == "Head" || name == "Neck" ? .32f * (1 - attention * .7f) : .38f);
                    AddDelta(name, idleReference, weight);
                }
            }
            if (!seated) { gestureAt = -1; return; }
            PresentSupportedTorsoMotion();
            bool speaking = dialogue != null && dialogue.IsSpeaking;
            if (gestureAt < 0 && speaking && performanceTime >= nextSpeechGesture)
            {
                presenceGesture = PresenceGesture.Speaking; gestureAt = performanceTime;
                nextSpeechGesture = performanceTime + 7;
            }
            else if (gestureAt < 0 && !speaking && performanceTime >= nextGesture)
            {
                presenceGesture = IsSymptomatic && presenceSequence % 2 == 0 ? PresenceGesture.Temple : PresenceGesture.Settle;
                gestureAt = performanceTime;
            }
            if (gestureAt >= 0)
            {
                float duration = presenceGesture == PresenceGesture.Temple ? 4.2f : 2.8f;
                float t = performanceTime - gestureAt;
                if (t >= duration)
                {
                    gestureAt = -1;
                    nextGesture = performanceTime + PresenceIntervals[presenceSequence++ % PresenceIntervals.Length];
                }
                else
                {
                    float envelope = Smooth(t / .85f) * Smooth((duration - t) / 1.05f);
                    if (presenceGesture == PresenceGesture.Temple)
                    {
                        PoseDizzyGesture(envelope);
                        rig.head.rotation = Quaternion.AngleAxis(5 * envelope, Right) * rig.head.rotation;
                    }
                    else if (presenceGesture == PresenceGesture.Speaking) PoseSpeakingHand(envelope, t);
                    else PoseSettlingGesture(envelope);
                }
            }
        }

        // Jaw follows the loudness of the actual voice clip, with a slight nod on stressed syllables.
        void PresentSpeech()
        {
            var voice = dialogue == null ? null : dialogue.Voice;
            float target = 0;
            if (voice != null && voice.isPlaying)
            {
                voice.GetOutputData(voiceSamples, 0);
                float sum = 0; foreach (var v in voiceSamples) sum += v * v;
                target = Mathf.Clamp01(Mathf.Sqrt(sum / voiceSamples.Length) * 9);
            }
            mouth = Mathf.Lerp(mouth, target, 1 - Mathf.Exp(-PerformanceDelta * (target > mouth ? 28 : 16)));
            if (rig == null) return;
            rig.SetBlendShape(rig.jawOpen, mouth * 55);
            if (rig.head != null && mouth > .02f) rig.head.rotation = Quaternion.AngleAxis(mouth * 2.2f, Right) * rig.head.rotation;
        }

        void AddDelta(string name, Dictionary<string, Quaternion> reference, float weight)
        {
            if (!liveBones.TryGetValue(name, out var live) || !sampledBones.TryGetValue(name, out var sampled) || !reference.TryGetValue(name, out var start)) return;
            var delta = sampled.localRotation * Quaternion.Inverse(start);
            live.localRotation = Quaternion.Slerp(Quaternion.identity, delta, weight) * live.localRotation;
        }

        bool EnsureSampler()
        {
            if (sampler != null) return true;
            if (model == null || samplerAttempted) return false;
            samplerAttempted = true;
            idle = CharacterAnimationLibrary.Load(IdleClip);
            if (idle == null) return false;
            // A transform copy receives sampled clips through a controller-free Animator in Players;
            // nothing of it renders or simulates, and only the selected deltas reach the live pose.
            sampler = CopyHierarchy(model, null);
            sampler.gameObject.hideFlags = HideFlags.HideAndDontSave;
            sampler.gameObject.SetActive(true);
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) liveBones[Strip(t.name)] = t;
            foreach (var t in sampler.GetComponentsInChildren<Transform>(true)) sampledBones[Strip(t.name)] = t;
            Capture(idle, idleReference);
            return true;
        }

        void Capture(AnimationClip clip, Dictionary<string, Quaternion> reference)
        {
            reference.Clear();
            if (clip == null) return;
            CharacterAnimationLibrary.Sample(clip, sampler.gameObject, 0);
            foreach (var pair in sampledBones) reference[pair.Key] = pair.Value.localRotation;
        }

        static Transform CopyHierarchy(Transform source, Transform parent)
        {
            var copy = new GameObject(source.name).transform;
            copy.SetParent(parent, false);
            copy.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
            copy.localScale = source.localScale;
            foreach (Transform child in source) CopyHierarchy(child, copy);
            return copy;
        }

        static string Strip(string bone) => bone.StartsWith("Bip01 ") ? bone.Substring(6) : bone;

        void DestroyPerformance()
        {
            if (sampler != null) Destroy(sampler.gameObject);
            sampler = null; samplerAttempted = false; liveBones.Clear(); sampledBones.Clear(); idleReference.Clear();
            ResetPerformanceTiming();
        }

        void ResetPerformanceTiming()
        {
            gestureAt = -1; nextGesture = 1.6f; idleWeight = 0; mouth = 0; performanceTime = 0;
            nextSpeechGesture = 0; presenceSequence = 0;
            naturalBlinkAt = -1; nextNaturalBlink = 3.1f; naturalBlinkSequence = 0;
            attentionRotation = Quaternion.identity; attention = 0; attentionUntil = 0;
            learnerWasNearby = false; acknowledgementAt = -1;
        }
    }
}
