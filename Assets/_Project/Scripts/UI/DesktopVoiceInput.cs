using EmergencyVR.Dialogue;
using EmergencyVR.Scenarios;
using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

namespace EmergencyVR.UI
{
    /// <summary>
    /// Push-to-talk speech for the patient (hold M). Windows free dictation is preferred so the learner can phrase
    /// questions naturally; the transcript is mapped to clinical intents by SpokenIntentParser. When dictation is
    /// unavailable (for example Windows online speech recognition is off) a fixed Spanish keyword grammar is used.
    /// </summary>
    public sealed class DesktopVoiceInput : MonoBehaviour
    {
        // Fallback grammar: common phrasings, each still interpreted by the same parser.
        static readonly string[] Grammar =
        {
            "hola", "hola vengo a ayudarte", "me escuchas", "qué te pasa", "qué te ocurre", "cómo es el mareo", "cuándo empezó",
            "te has desmayado", "has perdido el conocimiento", "te duele el pecho", "te falta el aire", "te cuesta respirar",
            "notas palpitaciones", "tienes problemas del corazón", "tomas medicación", "has comido", "has bebido agua",
            "puedo ayudarte", "te ayudo a tumbarte", "cómo te encuentras", "cómo estás ahora", "cómo te llamas", "sabes dónde estás",
        };

        ScenarioManager manager;
        bool listening;
        float lastHeard;
        public string Status { get; private set; } = "";

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        DictationRecognizer dictation;
        KeywordRecognizer keywords;
        bool dictationFailed, keywordsFailed;
#endif

        public void Configure(ScenarioManager owner, bool pushToTalk)
        {
            manager = owner;
            if (!pushToTalk && !listening && Status.Length > 0 && Time.unscaledTime - lastHeard > 5) Status = "";
            if (pushToTalk == listening) return;
            listening = pushToTalk;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (listening) StartListening(); else StopListening();
#else
            Status = listening ? "La voz solo está disponible en Windows." : "";
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        void StartListening()
        {
            if (!dictationFailed)
            {
                try
                {
                    if (dictation == null)
                    {
                        dictation = new DictationRecognizer(ConfidenceLevel.Low, DictationTopicConstraint.Dictation);
                        dictation.InitialSilenceTimeoutSeconds = 8; dictation.AutoSilenceTimeoutSeconds = 3;
                        dictation.DictationHypothesis += text => Status = "● " + text + "…";
                        dictation.DictationResult += (text, confidence) => Heard(text);
                        dictation.DictationError += (error, code) => FallBack("Dictado no disponible (" + error + ").");
                        dictation.DictationComplete += cause =>
                        {
                            if (cause != DictationCompletionCause.Complete && cause != DictationCompletionCause.TimeoutExceeded &&
                                cause != DictationCompletionCause.PauseLimitExceeded && cause != DictationCompletionCause.Canceled) FallBack("Dictado detenido: " + cause + ".");
                        };
                    }
                    if (dictation.Status != SpeechSystemStatus.Running) dictation.Start();
                    Status = "● Escuchando… habla con naturalidad";
                    return;
                }
                catch (System.Exception error) { FallBack(error.Message); }
            }
            if (keywordsFailed) { Status = "Voz no disponible en este equipo: usa «Hablar con el paciente»."; return; }
            try
            {
                if (keywords == null)
                {
                    keywords = new KeywordRecognizer(Grammar, ConfidenceLevel.Low);
                    keywords.OnPhraseRecognized += args => Heard(args.text);
                }
                if (!keywords.IsRunning) keywords.Start();
                Status = "● Escuchando (frases básicas)… activa el reconocimiento de voz en línea de Windows para hablar libremente";
            }
            catch (System.Exception error)
            {
                keywordsFailed = true; Status = "Voz no disponible en este equipo: usa «Hablar con el paciente».";
                Debug.LogWarning("Keyword voice input unavailable: " + error.Message);
            }
        }

        void StopListening()
        {
            // Dictation delivers the final phrase after Stop(); keep the transcript visible briefly.
            if (dictation != null && dictation.Status == SpeechSystemStatus.Running) dictation.Stop();
            if (keywords != null && keywords.IsRunning) keywords.Stop();
            if (Status.StartsWith("● Escuchando")) Status = "";
        }

        void FallBack(string reason)
        {
            if (dictationFailed) return;
            dictationFailed = true;
            Debug.LogWarning("Free dictation unavailable, using keyword grammar: " + reason);
            if (dictation != null) { try { if (dictation.Status == SpeechSystemStatus.Running) dictation.Stop(); dictation.Dispose(); } catch { } dictation = null; }
            if (listening) StartListening();
        }

        void Heard(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || manager == null) return;
            lastHeard = Time.unscaledTime;
            var request = SpokenIntentParser.Parse(text);
            Status = request.Understood ? "Has dicho: «" + text + "»" : "Has dicho: «" + text + "» · Daniel no te ha entendido";
            manager.Dialogue.AskSpoken(request);
        }

        void OnDestroy()
        {
            if (dictation != null) { if (dictation.Status == SpeechSystemStatus.Running) dictation.Stop(); dictation.Dispose(); }
            if (keywords != null) { if (keywords.IsRunning) keywords.Stop(); keywords.Dispose(); }
        }
#endif
    }
}
