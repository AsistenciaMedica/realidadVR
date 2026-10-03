using System;
using System.Collections.Generic;
using System.Linq;
using EmergencyVR.Medical;
using EmergencyVR.Patient.Presentation;
using EmergencyVR.Scenarios;
using UnityEngine;

namespace EmergencyVR.Dialogue
{
    public enum PhoneCallStage { Idle, Ringing, Talking, EnRoute, Arrived, Done, Delegated }

    /// <summary>
    /// A voiced, simulated 112 call over ClinicalHelpController. The learner dials, hears the ringback and a
    /// dispatcher, and answers with what they actually observed. Clinical recording stays in the runtime;
    /// this layer only adds the conversation, audio and the ambulance arrival. No real call is placed.
    /// </summary>
    public sealed class PhoneCallController : MonoBehaviour
    {
        public const string EmergencyNumber = "112";
        const float RingSeconds = 4.6f;
        const double AmbulanceSeconds = 75;

        public sealed class Option
        {
            public string Text;
            public Func<bool> Recommended;
            public Action Chosen;
        }

        public sealed class Question
        {
            public string Id, Voice, Text;
            public List<Option> Options = new List<Option>();
            // Controls the guide highlights while this question waits for a prerequisite observation.
            public Func<string[]> Prerequisite;
        }

        ReviewCaseSession review;
        ClinicalHelpController help;
        string attemptId;
        AudioSource line, tones, scene;
        AudioClip ringback, hangup, unavailable;
        readonly Dictionary<char, AudioClip> keys = new Dictionary<char, AudioClip>();
        readonly Queue<Question> script = new Queue<Question>();
        float ringLeft, delegateWait;
        double arrivalAt;
        bool pausedAudio;
        Transform handset, deskPhone;
        TextMesh screen;
        float callStarted;

        public PhoneCallStage Stage { get; private set; }
        public string Dialed { get; private set; } = "";
        public Question Current { get; private set; }
        public string Subtitle { get; private set; } = "";
        public string Notice { get; private set; } = "";
        public bool InCall => Stage == PhoneCallStage.Ringing || Stage == PhoneCallStage.Talking;
        public double SecondsToArrival => Stage == PhoneCallStage.EnRoute && Runtime != null ? Math.Max(0, arrivalAt - Runtime.Elapsed) : 0;
        public event Action Changed;

        MedicalScenarioRuntime Runtime => review?.Manager?.MedicalSession;
        Case01PatientPresentation Body => review == null ? null : review.GetComponent<Case01PatientPresentation>();
        bool Ready => review != null && review.Manager.AcceptsInput && Runtime != null && !Runtime.IsFinished && help != null && help.IsActive;

        public void Initialize(ReviewCaseSession owner)
        {
            review = owner;
            help = owner.GetComponent<ClinicalHelpController>();
            line = Source("VITAL VR phone line", false);
            // Band-limited like a phone line so the dispatcher is heard through the handset, not the room.
            line.gameObject.AddComponent<AudioHighPassFilter>().cutoffFrequency = 320;
            line.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 3400;
            tones = Source("VITAL VR phone tones", false);
            scene = Source("VITAL VR scene voice", true);
            ringback = Tone("Ringback 425 Hz", 425, 0, 4.5f, 1.5f, 3f);
            hangup = Tone("Hang-up 425 Hz", 425, 0, 1.2f, .2f, .2f);
            unavailable = Tone("Unavailable 950 Hz", 950, 0, 1.2f, .33f, .07f);
            var dtmf = new Dictionary<char, (float, float)>
            {
                ['1'] = (697, 1209), ['2'] = (697, 1336), ['3'] = (697, 1477), ['4'] = (770, 1209), ['5'] = (770, 1336),
                ['6'] = (770, 1477), ['7'] = (852, 1209), ['8'] = (852, 1336), ['9'] = (852, 1477), ['0'] = (941, 1336)
            };
            foreach (var pair in dtmf) keys[pair.Key] = Tone("DTMF " + pair.Key, pair.Value.Item1, pair.Value.Item2, .16f, .16f, 0);
            Reset();
        }

        AudioSource Source(string name, bool spatial)
        {
            var go = new GameObject(name, typeof(AudioSource));
            go.transform.SetParent(transform, false);
            var source = go.GetComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = spatial ? 1 : 0;
            source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = 1; source.maxDistance = 14;
            return source;
        }

        static AudioClip Tone(string name, float a, float b, float seconds, float on, float off)
        {
            const int rate = 22050;
            var data = new float[Mathf.CeilToInt(seconds * rate)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                bool audible = off <= 0 || t % (on + off) < on;
                if (!audible) continue;
                float edge = Mathf.Clamp01(Mathf.Min(t % (on + off), on - t % (on + off)) * 200);
                data[i] = edge * .22f * (Mathf.Sin(2 * Mathf.PI * a * t) + (b > 0 ? Mathf.Sin(2 * Mathf.PI * b * t) : 0)) / (b > 0 ? 2 : 1);
            }
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void Reset()
        {
            attemptId = Runtime?.ClinicalState?.AttemptId;
            Stage = PhoneCallStage.Idle; Dialed = ""; Current = null; Subtitle = ""; Notice = "";
            script.Clear(); ringLeft = 0; delegateWait = 0; arrivalAt = 0;
            foreach (var source in new[] { line, tones, scene }) if (source != null) source.Stop();
            if (handset != null) handset.gameObject.SetActive(false);
            Changed?.Invoke();
        }

        void Update()
        {
            if (review == null) return;
            if (attemptId != Runtime?.ClinicalState?.AttemptId) Reset();
            var reception = Body != null && Body.IsActive ? Body.PhoneAnchor : null;
            if (reception != null && deskPhone == null)
            {
                // Lives under the case assembly, so it disappears with it when another case is selected.
                deskPhone = EmergencyVR.Environment.Presentation.GymPropSet.Spawn("desk_phone", .23f, reception);
                if (deskPhone != null) { deskPhone.localPosition = Vector3.zero; deskPhone.localRotation = Quaternion.Euler(0, 180, 0); deskPhone.name = "Reception desk phone"; }
            }
            bool paused = review.Manager.IsPaused || !review.Manager.IsRunning;
            if (paused != pausedAudio)
            {
                pausedAudio = paused;
                foreach (var source in new[] { line, tones, scene }) { if (paused) source.Pause(); else source.UnPause(); }
            }
            if (screen != null && InCall)
                screen.text = Stage == PhoneCallStage.Ringing ? "112" + System.Environment.NewLine + "Llamando…" :
                    "112 · Emergencias" + System.Environment.NewLine + TimeSpan.FromSeconds(Time.time - callStarted).ToString(@"mm\:ss");
            if (!Ready) return;
            if (Stage == PhoneCallStage.Ringing)
            {
                ringLeft -= Time.deltaTime;
                if (ringLeft <= 0 && help.Status == ClinicalHelpStage.AwaitingLocation)
                {
                    tones.Stop(); Stage = PhoneCallStage.Talking;
                    BuildDispatcherScript(); Advance();
                }
            }
            else if (Stage == PhoneCallStage.Delegated && delegateWait > 0)
            {
                delegateWait -= Time.deltaTime;
                if (delegateWait <= 0 && help.ConfirmOperator())
                {
                    Say(scene, "CO_CONFIRMED", "Compañero: «Ya está. La ambulancia viene de camino. Me han dicho que no le dejemos solo y que lo mantengamos tumbado».");
                    StartAmbulance();
                }
            }
            else if (Stage == PhoneCallStage.EnRoute && Runtime.Elapsed >= arrivalAt)
            {
                Stage = PhoneCallStage.Arrived;
                scene.transform.position = new Vector3(0, 1.6f, -2.4f);
                Say(scene, "PA_ARRIVE", "Técnico de emergencias: «Hola, somos del equipo de emergencias. ¿Qué ha pasado?»");
                Current = new Question { Id = "handover", Text = "Técnico de emergencias: «¿Qué ha pasado?»" };
                Current.Options.Add(new Option
                {
                    Text = "Entregar el relevo: " + help.SituationSummary,
                    Recommended = () => true,
                    Chosen = () =>
                    {
                        if (!help.PerformHandover()) return;
                        Say(scene, "PA_THANKS", "Técnico de emergencias: «Muy bien, gracias. Nos hacemos cargo a partir de aquí».");
                        Current = null; Stage = PhoneCallStage.Done;
                    }
                });
                Changed?.Invoke();
            }
        }

        // ---- Learner actions -------------------------------------------------------------------------

        public void Press(char digit)
        {
            if (!Ready || Stage != PhoneCallStage.Idle || Dialed.Length >= 12 || !keys.TryGetValue(digit, out var clip)) return;
            Dialed += digit; Notice = "";
            tones.PlayOneShot(clip);
            Changed?.Invoke();
        }

        public void Erase()
        {
            if (Stage != PhoneCallStage.Idle || Dialed.Length == 0) return;
            Dialed = Dialed.Substring(0, Dialed.Length - 1); Changed?.Invoke();
        }

        public void Dial()
        {
            if (!Ready || Stage != PhoneCallStage.Idle || Dialed.Length == 0) return;
            if (Dialed != EmergencyNumber)
            {
                // A wrong number is a realistic outcome, recorded as communication, not a scoring event.
                review.Manager.PerformClinical((runtime, time) => runtime.RecordCommunication("Phone", "Número marcado sin respuesta: " + Dialed, time, "Player"));
                tones.clip = unavailable; tones.loop = false; tones.Play();
                Notice = "El número " + Dialed + " no responde." + (review.Procedures.TrainingMode ? " En España, el número de emergencias es el 112." : "");
                Dialed = ""; Changed?.Invoke(); return;
            }
            if (!help.RequestCall(false)) return;
            Stage = PhoneCallStage.Ringing; ringLeft = RingSeconds;
            tones.clip = ringback; tones.loop = true; tones.Play();
            Subtitle = "Llamando al 112…";
            ShowHandset(true);
            Changed?.Invoke();
        }

        public void Delegate()
        {
            if (!Ready || Stage != PhoneCallStage.Idle || !help.RequestCall(true)) return;
            Stage = PhoneCallStage.Delegated;
            var reception = Body?.PhoneAnchor;
            scene.transform.position = reception != null ? reception.position + Vector3.up * .7f : transform.position;
            Say(scene, "CO_ACK", "Compañero: «Vale, llamo yo al 112. ¿Qué les digo?»");
            Current = new Question { Id = "delegate", Text = "Compañero: «¿Qué les digo?»" };
            Current.Options.Add(new Option
            {
                Text = "Dale la ubicación y lo que has observado: " + help.SituationSummary,
                Recommended = () => true,
                Chosen = () =>
                {
                    if (!WaitForContact()) return;
                    help.CommunicateLocation(); help.CommunicateSituation();
                    Current = null; delegateWait = 6;
                    Subtitle = "El compañero está llamando al 112…";
                }
            });
            Changed?.Invoke();
        }

        public void Choose(int index)
        {
            if (!Ready || Current == null || index < 0 || index >= Current.Options.Count) return;
            var option = Current.Options[index];
            review.Manager.PerformClinical((runtime, time) => runtime.RecordCommunication(
                Stage == PhoneCallStage.Arrived ? "HandoverTeam" : Stage == PhoneCallStage.Delegated ? "Companion" : "Operator", option.Text, time, "Player"));
            option.Chosen?.Invoke();
            Changed?.Invoke();
        }

        public void HangUp()
        {
            if (!InCall) return;
            // Hanging up before confirmation leaves help unconfirmed; the runtime keeps that state for the debrief.
            review.Manager.PerformClinical((runtime, time) => runtime.RecordCommunication("Operator", "Llamada colgada antes de terminar.", time, "Player"));
            EndCall(PhoneCallStage.Idle);
            Notice = "Has colgado antes de que el operador confirmara la ayuda.";
            Changed?.Invoke();
        }

        // ---- Guidance --------------------------------------------------------------------------------

        public string[] GuideControls()
        {
            if (Stage == PhoneCallStage.Idle)
            {
                if (Dialed.Length > 0 && !EmergencyNumber.StartsWith(Dialed)) return new[] { "Phone erase", "Open help conversation" };
                if (Dialed == EmergencyNumber) return new[] { "Phone call", "Open help conversation" };
                return new[] { "Phone key " + EmergencyNumber[Dialed.Length], "Open help conversation" };
            }
            if (Current == null) return Array.Empty<string>();
            if (Current.Prerequisite != null)
            {
                var needed = Current.Prerequisite();
                if (needed != null) return needed;
            }
            for (int i = 0; i < Current.Options.Count; i++)
                if (Current.Options[i].Recommended == null || Current.Options[i].Recommended()) return new[] { "Call option " + i, "Open help conversation" };
            return new[] { "Open help conversation" };
        }

        // ---- Dispatcher script -----------------------------------------------------------------------

        void BuildDispatcherScript()
        {
            script.Clear();
            var observations = Runtime.Observations;
            bool Observed(string type) => observations.Physical.Any(x => x.type == type && x.valid);
            bool Asked(DialogueIntent intent) => observations.Interviews.Any(x => x.intent == intent);

            var greeting = Q("greeting", "OP_GREETING", "Operadora 112: «Emergencias, 112. ¿Qué le ocurre?»");
            greeting.Options.Add(Next("Hay un hombre que se encuentra mal en un gimnasio. Está muy mareado."));
            greeting.Options.Add(Repeat(greeting, "Hola, quería hacer una consulta.", "OP_CLARIFY",
                "Operadora 112: «Entiendo. Si hay una persona que se encuentra mal, dígame qué le pasa y dónde está».", false));

            var location = Q("location", "OP_LOCATION", "Operadora 112: «¿Dónde se encuentran exactamente? Necesito la dirección».");
            location.Options.Add(Next("Gimnasio Vital, calle Mayor 12, planta baja. Estamos en la zona de cardio.", () => help.CommunicateLocation()));
            location.Options.Add(Repeat(location, "En el gimnasio.", "OP_LOCATION_MORE",
                "Operadora 112: «Necesito la dirección completa para enviar la ambulancia. ¿En qué calle está el gimnasio?»", false));

            var conscious = Q("conscious", "OP_CONSCIOUS", "Operadora 112: «¿La persona está consciente? ¿Le contesta cuando le habla?»");
            conscious.Options.Add(Next("Sí, está consciente y me contesta.", null, () => Observed("PatientResponsive")));
            conscious.Options.Add(Repeat(conscious, "Todavía no lo he comprobado.", "OP_CHECK",
                "Operadora 112: «De acuerdo. Compruébelo ahora y dígamelo. No cuelgue».", !Observed("PatientResponsive")));
            conscious.Prerequisite = () => Observed("PatientResponsive") || conscious.Options.Count > 1 ? null : new[] { "Action AssessResponsiveness", "Open actions" };

            var breathing = Q("breathing", "OP_BREATHING", "Operadora 112: «¿Respira con normalidad?»");
            breathing.Options.Add(Next("Sí, respira con normalidad, sin esfuerzo.", null, () => Observed("BreathingNormal")));
            breathing.Options.Add(Repeat(breathing, "Todavía no lo he comprobado.", "OP_CHECK",
                "Operadora 112: «De acuerdo. Compruébelo ahora y dígamelo. No cuelgue».", !Observed("BreathingNormal")));
            breathing.Prerequisite = () => Observed("BreathingNormal") || breathing.Options.Count > 1 ? null : new[] { "Action ObserveBreathing", "Open actions" };

            var what = Q("what", "OP_WHAT", "Operadora 112: «Cuénteme qué ha pasado».");
            what.Options.Add(Next("Contar lo que has observado.", () => help.CommunicateSituation()));

            var age = Q("age", "OP_AGE", "Operadora 112: «¿Qué edad tiene, aproximadamente?»");
            age.Options.Add(Next("Unos cuarenta años."));
            age.Options.Add(Next("No lo sé.", null, () => false));

            var flags = Q("red-flags", "OP_RED_FLAGS", "Operadora 112: «¿Le duele el pecho o le falta el aire?»");
            bool flagsKnown() => Asked(DialogueIntent.CHEST_PAIN) && Asked(DialogueIntent.BREATHING_DIFFICULTY);
            flags.Options.Add(Next("No, dice que no le duele el pecho ni le falta el aire.", null, flagsKnown));
            flags.Options.Add(Repeat(flags, "No se lo he preguntado.", "OP_RED_FLAGS_ASK",
                "Operadora 112: «Pregúnteselo, por favor. Es importante».", !flagsKnown()));
            flags.Prerequisite = () => flagsKnown() || flags.Options.Count > 1 ? null :
                new[] { Asked(DialogueIntent.CHEST_PAIN) ? "Intent BREATHING_DIFFICULTY" : "Intent CHEST_PAIN", "Next dialogue", "Open dialogue" };

            var dispatch = Q("dispatch", "OP_DISPATCH", "Operadora 112: «De acuerdo. Le envío una ambulancia, ya está en camino. Mientras llega, " +
                "manténgalo tumbado, no le dé nada de comer ni de beber y no le deje solo. Si pierde el conocimiento o deja de respirar con normalidad, vuelva a llamar al 112».");
            dispatch.Options.Add(new Option
            {
                Text = "Entendido. Me quedo con él.",
                Recommended = () => true,
                Chosen = () =>
                {
                    if (!help.ConfirmOperator()) return;
                    Say(line, "OP_GOODBYE", "Operadora 112: «La ayuda está en camino. Hasta ahora».");
                    EndCall(PhoneCallStage.EnRoute);
                    StartAmbulance();
                }
            });
            foreach (var question in new[] { greeting, location, conscious, breathing, what, age, flags, dispatch }) script.Enqueue(question);
        }

        Question Q(string id, string voice, string text) => new Question { Id = id, Voice = voice, Text = text };

        Option Next(string text, Action effect = null, Func<bool> recommended = null) => new Option
        {
            Text = text,
            Recommended = recommended ?? (() => true),
            Chosen = () => { effect?.Invoke(); Advance(); }
        };

        // An answer that makes the dispatcher ask again; it is removed after use so the call cannot loop.
        Option Repeat(Question question, string text, string voice, string reply, bool recommended)
        {
            Option option = null;
            option = new Option
            {
                Text = text,
                Recommended = () => recommended && question.Options.Contains(option),
                Chosen = () => { question.Options.Remove(option); Say(line, voice, reply); }
            };
            return option;
        }

        void Advance()
        {
            Current = script.Count > 0 ? script.Dequeue() : null;
            if (Current != null) Say(line, Current.Voice, Current.Text);
            Changed?.Invoke();
        }

        bool WaitForContact() => help.Status == ClinicalHelpStage.AwaitingLocation;

        void StartAmbulance()
        {
            arrivalAt = Runtime.Elapsed + AmbulanceSeconds;
            Stage = PhoneCallStage.EnRoute;
            Changed?.Invoke();
        }

        void EndCall(PhoneCallStage next)
        {
            Stage = next;
            if (next != PhoneCallStage.EnRoute) { Current = null; line.Stop(); }
            tones.clip = hangup; tones.loop = false; tones.PlayDelayed(line.isPlaying ? line.clip.length - line.time : 0);
            ShowHandset(false);
        }

        void Say(AudioSource source, string voice, string text)
        {
            Subtitle = text;
            var clip = string.IsNullOrEmpty(voice) ? null : Resources.Load<AudioClip>("Audio/Case01/" + voice);
            source.Stop();
            if (clip != null) { source.clip = clip; source.loop = false; source.Play(); }
        }

        // ---- First-person handset --------------------------------------------------------------------

        void ShowHandset(bool visible)
        {
            var hands = review.Procedures?.Hands;
            if (visible && handset == null)
            {
                handset = EmergencyVR.Environment.Presentation.GymPropSet.Spawn("smartphone", .146f, transform);
                if (handset != null)
                {
                    handset.name = "Learner phone";
                    // Call screen on the face turned toward the learner (+Z of the held phone).
                    screen = new GameObject("Call screen", typeof(TextMesh)).GetComponent<TextMesh>();
                    screen.transform.SetParent(handset, false);
                    screen.transform.localPosition = new Vector3(0, .085f, .0062f);
                    screen.transform.localRotation = Quaternion.Euler(0, 180, 0);
                    screen.fontSize = 64; screen.characterSize = .0021f; screen.anchor = TextAnchor.MiddleCenter;
                    screen.alignment = TextAlignment.Center; screen.color = new Color(.92f, .97f, 1f);
                }
            }
            if (handset != null) handset.gameObject.SetActive(visible);
            if (hands != null) hands.HeldProp = visible ? handset : null;
            if (visible) callStarted = Time.time;
        }

        void OnDestroy()
        {
            foreach (var clip in keys.Values.Concat(new[] { ringback, hangup, unavailable })) if (clip != null) Destroy(clip);
        }
    }
}
