using System;
using UnityEngine;

namespace EmergencyVR.Patient.Presentation
{
    /// <summary>Motion-capture clips (Rocketbox, MIT) addressable by file name at runtime.</summary>
    public sealed class CharacterAnimationLibrary : ScriptableObject
    {
        [Serializable] public sealed class Entry { public string name; public AnimationClip clip; }
        public Entry[] clips = Array.Empty<Entry>();

        public AnimationClip Find(string name)
        {
            foreach (var entry in clips) if (entry != null && entry.name == name) return entry.clip;
            return null;
        }

        static CharacterAnimationLibrary cached;
        public static AnimationClip Load(string name)
        {
            if (cached == null) cached = Resources.Load<CharacterAnimationLibrary>("Visual/Characters/AnimationLibrary");
            return cached == null ? null : cached.Find(name);
        }

        // Generic clips can be sampled on a bare transform hierarchy in the Editor, but the
        // Player requires an Animator on this exact target. An empty Animator supplies that
        // runtime binding without a controller or an automatic pose competing with our late pose.
        // Keep shared clips non-Legacy: CharacterAnimator also plays them through Playables.
        public static void Sample(AnimationClip clip, GameObject target, float time)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (!clip.legacy && target.GetComponent<Animator>() == null)
            {
                var animator = target.AddComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            clip.SampleAnimation(target, time);
        }
    }
}
