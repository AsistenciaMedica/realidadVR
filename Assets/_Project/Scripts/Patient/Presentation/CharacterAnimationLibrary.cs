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
    }
}
