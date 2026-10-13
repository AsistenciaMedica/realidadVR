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
        static readonly string[] Trunk = { "Spine", "Spine1", "Spine2", "L Clavicle", "R Clavicle", "Neck", "Head" };
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
        static readonly string[] ExpressiveChannels = { "AA_VI_10_aa", "AA_VI_11_E", "AA_VI_13_O", "AK_19_EyeSquintLeft",
            "AK_20_EyeSquintRight", "AK_30_MouthFrownLeft", "AK_31_MouthFrownRight" };
        float mouth, idleWeight, gestureAt = -1, nextGesture = 1.6f, performanceTime, nextSpeechGesture;
        float speechBrightness;
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
                ClearExpressiveFace();
                return; // The medical layer retains closed eyes, jaw and unsupported/unresponsive signs.
            }
            PresentSpeech();
            PresentExpression();
            PresentNaturalBlink();
            bool seated = !busy && !risePending && !standing && !regaining && progress <= 0 && !completed &&
                review.Procedures?.CPR?.Touching != true && visual.CompressionDepthMetres <= .001f;
            idleWeight = Mathf.MoveTowards(idleWeight, seated ? 1 : 0, PerformanceDelta * 1.5f);
            // Clips enrich the idle; they must never gate basic human presence or a procedural gesture.
            if (seated && EnsureSampler() && idleWeight > .001f && idle != null && idle.length > .01f)
            {
                float clipTime = performanceTime % idle.length;
                CharacterAnimationLibrary.Sample(idle, sampler.gameObject, clipTime);
                float seamDuration = Mathf.Min(.55f, idle.length * .1f);
                float seam = Smooth((clipTime - idle.length + seamDuration) / seamDuration);
                if (seam > 0)
                    foreach (var pair in sampledBones)
                        if (idleReference.TryGetValue(pair.Key, out var start))
                            pair.Value.localRotation = Quaternion.Slerp(pair.Value.localRotation, start, seam);
                foreach (var name in Trunk)
                {
                    // Attending to the learner keeps the head on them; the trunk keeps breathing and shifting.
                    float weight = idleWeight * (name == "Head" || name == "Neck" ? .42f * (1 - attention * .8f) : .72f);
                    AddDelta(name, idleReference, weight);
                }
                // Imported finger joints retain a relaxed captured hand pose. Leaving them in
                // the neutral bind pose makes even a moving arm look like a rigid mannequin.
                foreach (var pair in liveBones)
                    if (pair.Key.Contains("Finger") && sampledBones.TryGetValue(pair.Key, out var captured))
                        pair.Value.localRotation = Quaternion.Slerp(pair.Value.localRotation, captured.localRotation, idleWeight * .85f);
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
                        // The shoulder and chest anticipate the reach, then settle with the hand.
                        // Pelvis/feet remain supported and the left hand is solved back onto the bench.
                        body.spine.rotation = Quaternion.AngleAxis(-3.5f * envelope, Vector3.up) *
                            Quaternion.AngleAxis(2.4f * envelope, Right) * body.spine.rotation;
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
                float sum = 0; int crossings = 0;
                for (int i = 0; i < voiceSamples.Length; i++)
                {
                    sum += voiceSamples[i] * voiceSamples[i];
                    if (i > 0 && (voiceSamples[i] >= 0) != (voiceSamples[i - 1] >= 0)) crossings++;
                }
                target = Mathf.Clamp01(Mathf.Sqrt(sum / voiceSamples.Length) * 9);
                speechBrightness = Mathf.Lerp(speechBrightness, Mathf.Clamp01(crossings / 50f),
                    1 - Mathf.Exp(-PerformanceDelta * 12));
            }
            mouth = Mathf.Lerp(mouth, target, 1 - Mathf.Exp(-PerformanceDelta * (target > mouth ? 28 : 16)));
            if (rig == null) return;
            bool hasVowels = rig.face != null && rig.face.sharedMesh.GetBlendShapeIndex("blendShape1.AA_VI_10_aa") >= 0;
            rig.SetBlendShape(rig.jawOpen, mouth * (hasVowels ? 16 : 55));
            // Audio-reactive articulation, not phoneme recognition: energy opens the mouth,
            // spectral roughness varies the lip shape. All channels close when the voice stops.
            rig.SetBlendShape("blendShape1.AA_VI_10_aa", mouth * 32 * (1 - speechBrightness));
            rig.SetBlendShape("blendShape1.AA_VI_11_E", mouth * 26 * speechBrightness);
            rig.SetBlendShape("blendShape1.AA_VI_13_O", mouth * 18 * (1 - speechBrightness));
            // A phrase-level accent is slower than the jaw. Following every syllable with
            // the entire head produces a mechanical bobbing motion.
            if (rig.head != null && mouth > .02f)
                rig.head.rotation = Quaternion.AngleAxis(Mathf.Sin(performanceTime * 2.1f) * mouth * 1.3f, Right) * rig.head.rotation;
        }

        void PresentExpression()
        {
            float tension = Mathf.Clamp01(discomfort / 18f);
            rig.SetBlendShape(rig.fear, tension * (8 + 3 * Mathf.Sin(performanceTime * .43f)));
            rig.SetBlendShape("blendShape1.AK_19_EyeSquintLeft", tension * 9);
            rig.SetBlendShape("blendShape1.AK_20_EyeSquintRight", tension * 7);
            rig.SetBlendShape("blendShape1.AK_30_MouthFrownLeft", tension * 10 * (1 - mouth));
            rig.SetBlendShape("blendShape1.AK_31_MouthFrownRight", tension * 7 * (1 - mouth));
        }

        void ClearExpressiveFace()
        {
            foreach (var channel in ExpressiveChannels)
                rig.SetBlendShape("blendShape1." + channel, 0);
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
            gestureAt = -1; nextGesture = 1.6f; idleWeight = 0; mouth = 0; speechBrightness = 0; performanceTime = 0;
            nextSpeechGesture = 0; presenceSequence = 0;
            naturalBlinkAt = -1; nextNaturalBlink = 3.1f; naturalBlinkSequence = 0;
            attentionRotation = Quaternion.identity; attention = 0; attentionUntil = 0;
            learnerWasNearby = false; acknowledgementAt = -1;
        }
    }
}
