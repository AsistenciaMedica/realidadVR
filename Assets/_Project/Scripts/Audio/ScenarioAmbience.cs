using System.Collections.Generic;
using EmergencyVR.Environment;
using EmergencyVR.Scenarios;
using EmergencyVR.UI;
using UnityEngine;

namespace EmergencyVR.Audio
{
    // Presentation only: none of these sources reads or advances clinical state.
    public sealed class ScenarioAmbience : MonoBehaviour
    {
        readonly List<GameObject> sources = new List<GameObject>();
        ScenarioEnvironmentPresenter environment;
        TrainingExperience experience;
        AudioSource whistle;
        double nextWhistle;
        public string CurrentEnvironment { get; private set; }

        public static ScenarioAmbience Attach(ReviewCaseSession review)
        {
            var existing = review.GetComponent<ScenarioAmbience>();
            return existing != null ? existing : review.gameObject.AddComponent<ScenarioAmbience>();
        }

        void Update()
        {
            if (environment == null) environment = FindFirstObjectByType<ScenarioEnvironmentPresenter>();
            if (experience == null) experience = FindFirstObjectByType<TrainingExperience>();
            SelectEnvironment(experience != null && !experience.WorldVisible ? null : environment?.CurrentEnvironment);
            if (whistle != null && !AudioListener.pause && AudioSettings.dspTime >= nextWhistle)
            {
                whistle.Play();
                nextWhistle = AudioSettings.dspTime + 53;
            }
        }

        public void SelectEnvironment(string id)
        {
            if (id != "gym" && id != "mall" && id != "football") id = null;
            if (id == CurrentEnvironment) return;
            Clear();
            CurrentEnvironment = id;
            switch (id)
            {
                case "gym":
                    Loop("Distant gym loudspeaker", "speaker-music", new Vector3(2.7f, 2.5f, 4.1f), .035f, 16);
                    Loop("Gym machine motor", "gym-motor", new Vector3(2.1f, .6f, -.65f), .025f, 11);
                    break;
                case "mall":
                    Loop("Mall ceiling loudspeaker", "speaker-music", new Vector3(-2.6f, 2.8f, 4.1f), .023f, 18);
                    Loop("Distant mall conversation", "mall-murmur", new Vector3(2.7f, 1.5f, -.7f), .04f, 18);
                    break;
                case "football":
                    Loop("Wind beyond the touchline", "field-wind", new Vector3(-9, 2, 6), .055f, 42);
                    Loop("Distant spectators", "mall-murmur", new Vector3(-5, 1.7f, 10), .025f, 34);
                    // Reuse the licensed walla at a distance, with no intelligible foreground speech.
                    if (sources.Count > 1) sources[sources.Count - 1].AddComponent<AudioLowPassFilter>().cutoffFrequency = 950;
                    whistle = Source("Distant referee whistle", "field-whistle", new Vector3(7, 1.6f, 17), .05f, 48);
                    nextWhistle = AudioSettings.dspTime + 31;
                    break;
            }
        }

        void Loop(string label, string clip, Vector3 position, float volume, float distance)
        {
            var first = Source(label, clip, position, volume, distance);
            if (first == null) return;
            var second = first.gameObject.AddComponent<AudioSource>();
            Configure(second, first.clip, volume, distance);
            first.gameObject.AddComponent<AmbienceLoop>().Initialize(first, second, volume);
        }

        AudioSource Source(string label, string resource, Vector3 position, float volume, float distance)
        {
            var clip = Resources.Load<AudioClip>("Audio/Ambience/" + resource);
            if (clip == null) { Debug.LogWarning("Missing ambient recording: " + resource); return null; }
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            sources.Add(go);
            var source = go.AddComponent<AudioSource>();
            Configure(source, clip, volume, distance);
            return source;
        }

        static void Configure(AudioSource source, AudioClip clip, float volume, float distance)
        {
            source.clip = clip;
            source.playOnAwake = false;
            source.loop = false;
            source.volume = volume;
            source.spatialBlend = 1;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2;
            source.maxDistance = distance;
            source.dopplerLevel = 0;
            source.spread = 35;
            source.priority = 200;
        }

        void Clear()
        {
            foreach (var source in sources)
            {
                if (source == null) continue;
                source.SetActive(false); // Stop immediately, including scheduled playback, before deferred destruction.
                if (Application.isPlaying) Destroy(source); else DestroyImmediate(source);
            }
            sources.Clear();
            whistle = null;
        }

        void OnDisable() { Clear(); CurrentEnvironment = null; }
    }

    // Two overlapping sources remove encoder padding and hard joins from field recordings.
    // DSP time freezes with AudioListener.pause, preserving both the fade and the scheduled seam.
    public sealed class AmbienceLoop : MonoBehaviour
    {
        readonly AudioSource[] voices = new AudioSource[2];
        readonly double[] starts = { double.NegativeInfinity, double.NegativeInfinity };
        double nextStart, duration;
        float level, overlap;
        int nextVoice;

        public void Initialize(AudioSource first, AudioSource second, float volume)
        {
            voices[0] = first; voices[1] = second; level = volume;
            duration = first.clip.length;
            overlap = Mathf.Min(.65f, (float)duration * .15f);
            nextStart = AudioSettings.dspTime + .15;
        }

        void Update()
        {
            if (voices[0] == null || duration <= 0 || AudioListener.pause) return;
            double now = AudioSettings.dspTime;
            if (now + 1 >= nextStart)
            {
                // Recover cleanly if an editor stall or device reset missed the scheduled seam.
                if (nextStart < now) nextStart = now + .05;
                starts[nextVoice] = nextStart;
                voices[nextVoice].PlayScheduled(nextStart);
                nextStart += duration - overlap;
                nextVoice = 1 - nextVoice;
            }
            for (int i = 0; i < 2; i++)
            {
                float elapsed = (float)(now - starts[i]);
                float envelope = Mathf.Min(Mathf.Clamp01(elapsed / overlap), Mathf.Clamp01(((float)duration - elapsed) / overlap));
                voices[i].volume = level * envelope;
            }
        }
    }
}
