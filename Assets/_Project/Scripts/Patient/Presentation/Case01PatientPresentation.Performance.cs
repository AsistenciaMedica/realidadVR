using System.Collections.Generic;
using UnityEngine;

namespace EmergencyVR.Patient.Presentation
{
    /// <summary>
    /// Daniel's performance layer, applied after the authored pose and attention: lip movement from his real voice,
    /// additive motion-capture micro-movement while seated and an occasional hand-to-face gesture while dizzy.
    /// Motion is added as deltas from each clip's first frame, so the validated seated/assisted pose is preserved.
    /// </summary>
    public sealed partial class Case01PatientPresentation
    {
        static readonly string[] Trunk = { "Spine", "Spine1", "Spine2", "Neck", "Head" };
        static readonly string[] RightArm = { "R Clavicle", "R UpperArm", "R Forearm", "R Hand" };
        const string IdleClip = "m_sit_chair_idle_nervous_01", FaceClip = "m_sit_chair_idle_touch_face";

        Transform sampler;
        readonly Dictionary<string, Transform> liveBones = new Dictionary<string, Transform>();
        readonly Dictionary<string, Transform> sampledBones = new Dictionary<string, Transform>();
        readonly Dictionary<string, Quaternion> idleReference = new Dictionary<string, Quaternion>();
        readonly Dictionary<string, Quaternion> faceReference = new Dictionary<string, Quaternion>();
        AnimationClip idle, face;
        readonly float[] voiceSamples = new float[256];
        float mouth, idleWeight, gestureAt = -1, nextGesture = 12, performanceTime;

        void PresentPerformance()
        {
            PresentSpeech();
            if (!EnsureSampler()) return;
            performanceTime += Time.deltaTime;
            bool seated = !busy && !risePending && !standing && !regaining && progress <= 0 && !completed;
            idleWeight = Mathf.MoveTowards(idleWeight, seated ? 1 : 0, Time.deltaTime * 1.5f);
            if (idleWeight > .001f && idle != null)
            {
                CharacterAnimationLibrary.Sample(idle, sampler.gameObject, performanceTime % idle.length);
                foreach (var name in Trunk)
                {
                    // Attending to the learner keeps the head on them; the trunk keeps breathing and shifting.
                    float weight = idleWeight * (name == "Head" || name == "Neck" ? .75f * (1 - attention * .7f) : .8f);
                    AddDelta(name, idleReference, weight);
                }
            }
            string state = Clinical?.ClinicalStateId;
            bool dizzy = state == "HYP_00_INITIAL_PRESYNCOPE" || state == "HYP_03_PERSISTENT_SYMPTOMS" || state == "HYP_04_RECURRENT_PRESYNCOPE";
            bool speaking = dialogue != null && dialogue.IsSpeaking;
            if (gestureAt < 0 && seated && dizzy && !speaking && face != null && performanceTime >= nextGesture) gestureAt = performanceTime;
            if (gestureAt >= 0 && face != null)
            {
                float t = performanceTime - gestureAt;
                if (t >= face.length || !seated) { gestureAt = -1; nextGesture = performanceTime + Random.Range(15f, 25f); }
                else
                {
                    float envelope = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .8f)) * Mathf.SmoothStep(0, 1, Mathf.Clamp01((face.length - t) / .8f));
                    CharacterAnimationLibrary.Sample(face, sampler.gameObject, t);
                    foreach (var name in RightArm) AddDelta(name, faceReference, envelope);
                    AddDelta("Head", faceReference, envelope * .5f);
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
            mouth = Mathf.Lerp(mouth, target, 1 - Mathf.Exp(-Time.deltaTime * (target > mouth ? 28 : 16)));
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
            if (model == null) return false;
            idle = CharacterAnimationLibrary.Load(IdleClip);
            face = CharacterAnimationLibrary.Load(FaceClip);
            if (idle == null && face == null) return false;
            // A transform copy receives sampled clips through a controller-free Animator in Players;
            // nothing of it renders or simulates, and only the selected deltas reach the live pose.
            sampler = CopyHierarchy(model, null);
            sampler.gameObject.hideFlags = HideFlags.HideAndDontSave;
            sampler.gameObject.SetActive(true);
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) liveBones[Strip(t.name)] = t;
            foreach (var t in sampler.GetComponentsInChildren<Transform>(true)) sampledBones[Strip(t.name)] = t;
            Capture(idle, idleReference); Capture(face, faceReference);
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
            sampler = null; liveBones.Clear(); sampledBones.Clear(); idleReference.Clear(); faceReference.Clear();
            gestureAt = -1; nextGesture = 12; idleWeight = 0; mouth = 0;
        }
    }
}
