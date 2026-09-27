using UnityEngine;

namespace EmergencyVR.Medical.Interaction
{
    public sealed class MedicalProcedureAudio : MonoBehaviour
    {
        AudioSource cues,breath,ambience;AudioClip tone,noise,room;
        public void Initialize(Transform patient)
        {
            cues=Source(transform,.24f);breath=Source(patient,.09f);ambience=Source(transform,.025f);
            tone=MakeTone();noise=MakeNoise("Vital procedural breath",1);room=MakeNoise("Vital procedural ventilation",2);
            breath.clip=noise;breath.loop=true;ambience.clip=room;ambience.loop=true;ambience.spatialBlend=0;ambience.Play();
        }
        AudioSource Source(Transform parent,float volume){var go=new GameObject("Vital audio",typeof(AudioSource));go.transform.SetParent(parent,false);var source=go.GetComponent<AudioSource>();source.spatialBlend=1;source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=.7f;source.maxDistance=10;source.volume=volume;source.playOnAwake=false;return source;}
        static AudioClip MakeTone(){int rate=22050;var samples=new float[rate/5];for(int i=0;i<samples.Length;i++){float t=(float)i/samples.Length;samples[i]=Mathf.Sin(2*Mathf.PI*800*i/rate)*Mathf.Sin(t*Mathf.PI)*.3f;}var clip=AudioClip.Create("Vital generated device cue",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;}
        static AudioClip MakeNoise(string name,int seed){int rate=22050;var random=new System.Random(seed);var samples=new float[rate*3];float filtered=0;for(int i=0;i<samples.Length;i++){filtered=Mathf.Lerp(filtered,(float)random.NextDouble()*2-1,.08f);float phase=(float)i/samples.Length;samples[i]=filtered*.3f*Mathf.Pow(Mathf.Sin(phase*Mathf.PI),2);}var clip=AudioClip.Create(name,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;}
        public void SetPatient(PatientSnapshot p,bool runtimeRespiratoryPhase=false)
        {
            // Stage B exposes the shared phase; voiced/phase-driven breathing is integrated in G.
            // Do not play the legacy independent loop over a v2 runtime-owned respiratory cycle.
            if(runtimeRespiratoryPhase){breath.Stop();return;}
            if(p==null||p.respiration=="absent"||p.respiratoryRate<=0){breath.Stop();return;}
            breath.pitch=Mathf.Clamp((float)p.respiratoryRate/20,.5f,2);breath.volume=p.respiration=="fast"?.12f:.05f;if(!breath.isPlaying)breath.Play();
        }
        public void Pulse(float duration,float frequency){if(cues==null)return;cues.pitch=frequency/800;cues.PlayOneShot(tone,Mathf.Clamp(duration/.12f,.2f,1));}
        void OnDestroy(){if(tone!=null)Destroy(tone);if(noise!=null)Destroy(noise);if(room!=null)Destroy(room);if(breath!=null)Destroy(breath.gameObject);}
    }
}
