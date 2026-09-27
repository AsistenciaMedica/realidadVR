using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace EmergencyVR.Patient.Presentation
{
    /// <summary>
    /// Plays motion-capture clips on a Rocketbox character without an AnimatorController: a two-input mixer
    /// cross-fades between the current and the next clip, and clips loop by wrapping their local time.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class CharacterAnimator : MonoBehaviour
    {
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable current, previous;
        float fade = 1, fadeSeconds;
        public string Playing { get; private set; }

        void Awake()
        {
            graph = PlayableGraph.Create(name + " motion");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            AnimationPlayableOutput.Create(graph, "Motion", GetComponent<Animator>()).SetSourcePlayable(mixer);
            graph.Play();
        }

        public void Play(string clipName, float crossFade = .35f)
        {
            if (Playing == clipName) return;
            var clip = CharacterAnimationLibrary.Load(clipName);
            if (clip == null) { Debug.LogWarning("Missing animation clip " + clipName); return; }
            Playing = clipName;
            if (previous.IsValid()) { mixer.DisconnectInput(1); previous.Destroy(); }
            if (current.IsValid()) { mixer.DisconnectInput(0); previous = current; mixer.ConnectInput(1, previous, 0); }
            current = AnimationClipPlayable.Create(graph, clip);
            current.SetApplyFootIK(false);
            mixer.ConnectInput(0, current, 0);
            fadeSeconds = previous.IsValid() ? Mathf.Max(.01f, crossFade) : 0;
            fade = fadeSeconds > 0 ? 0 : 1;
            Weights();
        }

        void Update()
        {
            if (!graph.IsValid()) return;
            Wrap(current); Wrap(previous);
            if (fade < 1) { fade = Mathf.Min(1, fade + Time.deltaTime / fadeSeconds); Weights(); }
        }

        static void Wrap(AnimationClipPlayable playable)
        {
            if (!playable.IsValid()) return;
            float length = playable.GetAnimationClip().length;
            if (length > 0 && playable.GetTime() > length) playable.SetTime(playable.GetTime() % length);
        }

        void Weights()
        {
            mixer.SetInputWeight(0, fade);
            mixer.SetInputWeight(1, previous.IsValid() ? 1 - fade : 0);
        }

        void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }
    }
}
