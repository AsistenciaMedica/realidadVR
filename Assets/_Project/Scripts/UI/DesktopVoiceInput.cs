using System.Collections.Generic;
using EmergencyVR.Medical;
using EmergencyVR.Scenarios;
using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

namespace EmergencyVR.UI
{
    /// <summary>
    /// Push-to-talk questions for the patient using the offline Windows speech recogniser. Recognised phrases map to
    /// the same dialogue intents as the buttons; nothing is sent over the network. Unsupported systems keep the menu.
    /// </summary>
    public sealed class DesktopVoiceInput : MonoBehaviour
    {
        static readonly Dictionary<string, DialogueIntent> Phrases = new Dictionary<string, DialogueIntent>
        {
            ["hola"] = DialogueIntent.GREETING, ["hola soy"] = DialogueIntent.GREETING, ["vengo a ayudarte"] = DialogueIntent.GREETING,
            ["estoy aquí para ayudarte"] = DialogueIntent.GREETING, ["me escuchas"] = DialogueIntent.GREETING,
            ["qué te pasa"] = DialogueIntent.MAIN_SYMPTOM, ["qué te ocurre"] = DialogueIntent.MAIN_SYMPTOM, ["qué sientes"] = DialogueIntent.MAIN_SYMPTOM,
            ["cómo es el mareo"] = DialogueIntent.SYMPTOM_DESCRIPTION, ["cómo te sientes mareado"] = DialogueIntent.SYMPTOM_DESCRIPTION, ["descríbeme el mareo"] = DialogueIntent.SYMPTOM_DESCRIPTION,
            ["cuándo empezó"] = DialogueIntent.ONSET, ["desde cuándo"] = DialogueIntent.ONSET, ["cuándo comenzó"] = DialogueIntent.ONSET,
            ["te has desmayado"] = DialogueIntent.LOSS_OF_CONSCIOUSNESS, ["has perdido el conocimiento"] = DialogueIntent.LOSS_OF_CONSCIOUSNESS, ["te has caído"] = DialogueIntent.LOSS_OF_CONSCIOUSNESS,
            ["te duele el pecho"] = DialogueIntent.CHEST_PAIN, ["tienes dolor en el pecho"] = DialogueIntent.CHEST_PAIN,
            ["te falta el aire"] = DialogueIntent.BREATHING_DIFFICULTY, ["te cuesta respirar"] = DialogueIntent.BREATHING_DIFFICULTY, ["puedes respirar bien"] = DialogueIntent.BREATHING_DIFFICULTY,
            ["notas palpitaciones"] = DialogueIntent.PALPITATIONS, ["se te acelera el corazón"] = DialogueIntent.PALPITATIONS,
            ["tienes problemas del corazón"] = DialogueIntent.CARDIAC_HISTORY, ["tienes alguna enfermedad"] = DialogueIntent.CARDIAC_HISTORY,
            ["tomas medicación"] = DialogueIntent.MEDICATION, ["tomas algún medicamento"] = DialogueIntent.MEDICATION, ["tomas pastillas"] = DialogueIntent.MEDICATION,
            ["has comido"] = DialogueIntent.FOOD_DRINK, ["has bebido agua"] = DialogueIntent.FOOD_DRINK, ["has comido algo hoy"] = DialogueIntent.FOOD_DRINK,
            ["puedo ayudarte"] = DialogueIntent.CONSENT_HELP, ["te ayudo a tumbarte"] = DialogueIntent.CONSENT_HELP, ["me dejas ayudarte"] = DialogueIntent.CONSENT_HELP,
            ["cómo te encuentras"] = DialogueIntent.CURRENT_STATUS, ["cómo estás ahora"] = DialogueIntent.CURRENT_STATUS, ["te sientes mejor"] = DialogueIntent.CURRENT_STATUS,
        };

        ScenarioManager manager;
        bool listening;
        public string Status { get; private set; } = "";
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        KeywordRecognizer recognizer;
        bool failed;
#endif

        public void Configure(ScenarioManager owner, bool pushToTalk)
        {
            manager = owner;
            if (pushToTalk == listening) return;
            listening = pushToTalk;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (listening && recognizer == null && !failed)
            {
                try
                {
                    if (!PhraseRecognitionSystem.isSupported) throw new System.NotSupportedException();
                    recognizer = new KeywordRecognizer(new List<string>(Phrases.Keys).ToArray(), ConfidenceLevel.Low);
                    recognizer.OnPhraseRecognized += Recognized;
                }
                catch (System.Exception error)
                {
                    failed = true; recognizer = null;
                    Debug.LogWarning("Voice input unavailable: " + error.Message);
                }
            }
            if (failed) { Status = listening ? "Voz no disponible en este equipo: usa «Hablar con el paciente»." : ""; return; }
            if (listening) { recognizer.Start(); Status = "● Escuchando… di tu pregunta"; }
            else if (recognizer != null && recognizer.IsRunning) { recognizer.Stop(); if (Status.StartsWith("●")) Status = ""; }
#else
            Status = listening ? "La voz solo está disponible en Windows." : "";
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        void Recognized(PhraseRecognizedEventArgs args)
        {
            if (!Phrases.TryGetValue(args.text, out var intent) || manager == null) return;
            Status = "Has dicho: «" + args.text + "»";
            manager.Dialogue.Ask(intent);
        }

        void OnDestroy()
        {
            if (recognizer == null) return;
            if (recognizer.IsRunning) recognizer.Stop();
            recognizer.Dispose();
        }
#endif
    }
}
