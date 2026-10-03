using System;
using UnityEngine;

namespace EmergencyVR.Medical.Interaction
{
    // Reads only the existing device display. No procedure state, actions, timing or scoring are changed.
    public sealed class AEDVoiceController : MonoBehaviour
    {
        [Serializable] public sealed class Manifest { public Line[] lines = Array.Empty<Line>(); }
        [Serializable] public sealed class Line { public string display, text, resource; }
        MedicalProcedureRig rig;
        Manifest manifest;
        AudioSource source;
        string lastDisplay;
        public string LastSpokenText { get; private set; }

        public void Initialize(MedicalProcedureRig target)
        {
            rig = target;
            var data = Resources.Load<TextAsset>("Audio/AEDVoices");
            manifest = data == null ? new Manifest() : JsonUtility.FromJson<Manifest>(data.text);
        }

        public void Present(string display, Transform device, bool acceptsInput)
        {
            if (display == "DEA\nABRIR / ENCENDER") { Stop(); return; }
            if (display == lastDisplay || !acceptsInput || device == null) return;
            lastDisplay = display;
            var line = Array.Find(manifest.lines, value => value.display == display);
            if (line == null) return;
            var clip = Resources.Load<AudioClip>(line.resource);
            if (clip == null) return;
            if (source == null)
            {
                var speaker = new GameObject("DEA Spanish voice");
                speaker.transform.SetParent(device, false);
                source = speaker.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1;
                source.minDistance = 1;
                source.maxDistance = 12;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.dopplerLevel = 0;
                source.volume = .65f;
                source.priority = 30;
            }
            source.Stop(); // A new contact warning must interrupt an obsolete instruction.
            source.clip = clip;
            source.Play();
            LastSpokenText = line.text;
        }

        void Update()
        {
            if (rig != null && rig.Manager != null && !rig.Manager.IsRunning) Stop();
        }

        void Stop()
        {
            if (source != null) source.Stop();
            lastDisplay = null;
            LastSpokenText = null;
        }

        void OnDisable() { Stop(); }
        void OnDestroy() { if (source != null) Destroy(source.gameObject); }
    }
}
