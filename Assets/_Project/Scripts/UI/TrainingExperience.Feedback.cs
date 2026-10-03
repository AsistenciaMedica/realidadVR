using UnityEngine;

namespace EmergencyVR.UI
{
    public sealed partial class TrainingExperience
    {
        AudioSource interfaceSound;
        AudioClip interfaceHover, interfaceClick;

        void PlayInterfaceFeedback(bool clicked)
        {
            if (interfaceSound == null)
            {
                interfaceSound = gameObject.AddComponent<AudioSource>();
                interfaceSound.playOnAwake = false; interfaceSound.spatialBlend = 0;
                interfaceSound.ignoreListenerPause = true; // Controls remain audible while the clinical audio is paused.
                interfaceSound.volume = .10f;
                interfaceHover = InterfaceTone("VITAL interface hover", .018f, 720);
                interfaceClick = InterfaceTone("VITAL interface select", .032f, 960);
            }
            interfaceSound.PlayOneShot(clicked ? interfaceClick : interfaceHover, clicked ? 1 : .45f);
        }

        static AudioClip InterfaceTone(string name, float duration, float frequency)
        {
            const int rate = 22050;
            int count = Mathf.CeilToInt(rate * duration); var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float phase = (float)i / Mathf.Max(1, count - 1);
                samples[i] = Mathf.Sin(i * frequency * 2 * Mathf.PI / rate) * Mathf.Sin(phase * Mathf.PI) * .25f;
            }
            var clip = AudioClip.Create(name, count, 1, rate, false); clip.SetData(samples, 0); return clip;
        }

        void DisposeInterfaceFeedback()
        {
            if (interfaceHover != null) Destroy(interfaceHover);
            if (interfaceClick != null) Destroy(interfaceClick);
        }
    }
}
