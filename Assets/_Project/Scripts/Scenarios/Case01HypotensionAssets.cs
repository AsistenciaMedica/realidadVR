using System;
using UnityEngine;

namespace EmergencyVR.Scenarios
{
    /// <summary>Presentation resources only. Clinical definitions and attempt state live elsewhere.</summary>
    [CreateAssetMenu(menuName = "VITAL VR/Cases/Case 01 presentation assets")]
    public sealed class Case01HypotensionAssets : ScriptableObject
    {
        [Tooltip("Stage B references the existing provisional patient. This does not certify clothes or animations.")]
        public GameObject patientPrefab;
        public RuntimeAnimatorController animatorController;
        public AnimationClip[] animationClips = Array.Empty<AnimationClip>();
        public AudioClip[] audioClips = Array.Empty<AudioClip>();
        public Material[] materials = Array.Empty<Material>();
        public GameObject[] equipmentPrefabs = Array.Empty<GameObject>();
        public GameObject[] environmentPrefabs = Array.Empty<GameObject>();
        public Sprite[] sprites = Array.Empty<Sprite>();
        public UnityEngine.Object[] uiResources = Array.Empty<UnityEngine.Object>();

        /// <summary>JSON uses a symbolic clip identifier, never an asset path. Missing audio is an explicit text fallback.</summary>
        public AudioClip FindAudio(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference) || audioClips == null) return null;
            foreach (var clip in audioClips)
                if (clip != null && string.Equals(clip.name, reference, StringComparison.Ordinal)) return clip;
            return null;
        }
    }
}
