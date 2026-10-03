using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using EmergencyVR.Medical;

namespace EmergencyVR.Dialogue
{
    /// <summary>What the learner said, as clinical intents in spoken order plus non-clinical small talk.</summary>
    public sealed class SpokenRequest
    {
        public readonly List<DialogueIntent> Intents = new List<DialogueIntent>();
        public string SmallTalk;   // "name", "place" or null
        public bool Understood => Intents.Count > 0 || SmallTalk != null;
    }

    /// <summary>
    /// Maps free Spanish speech to dialogue intents with accent-insensitive cues. Deterministic and offline:
    /// the patient's answers still come only from the authored clinical runtime.
    /// </summary>
    public static class SpokenIntentParser
    {
        // Each rule: all "need" groups must match (any word in a group), in any position of the utterance.
        sealed class Rule
        {
            public DialogueIntent? Intent; public string SmallTalk; public string[][] Need;
            public Rule(DialogueIntent intent, params string[][] need) { Intent = intent; Need = need; }
            public Rule(string smallTalk, params string[][] need) { SmallTalk = smallTalk; Need = need; }
        }

        static string[] G(params string[] words) => words;

        static readonly Rule[] Rules =
        {
            new Rule(DialogueIntent.CHEST_PAIN, G("pecho", "torax", "opresion"), G("duele", "dolor", "molest", "aprieta", "presion", "opresion", "punzada")),
            new Rule(DialogueIntent.BREATHING_DIFFICULTY, G("respir", "aire", "ahog", "aliento", "falta el aire", "asfix")),
            new Rule(DialogueIntent.PALPITATIONS, G("palpita", "acelera", "late rapido", "late fuerte", "taquicard", "corazon a mil", "brinca")),
            new Rule(DialogueIntent.CARDIAC_HISTORY, G("corazon", "cardi", "infarto", "arritmia", "tension alta", "hipertens"), G("problema", "enfermedad", "antecedente", "historia", "tienes", "has tenido", "padeces", "sufres")),
            new Rule(DialogueIntent.MEDICATION, G("medica", "pastilla", "farmaco", "tratamiento", "tomas algo", "tomas alguna")),
            new Rule(DialogueIntent.FOOD_DRINK, G("comido", "comer", "desayun", "almorz", "bebido", "beber", "agua", "hidrat", "comiste", "bebiste")),
            new Rule(DialogueIntent.LOSS_OF_CONSCIOUSNESS, G("desmay", "conocimiento", "te caiste", "has caido", "golpe", "perdiste", "te has ido", "sin sentido")),
            new Rule(DialogueIntent.ONSET, G("cuando", "desde cuando", "empezo", "empezaste", "comenzo", "hace cuanto", "cuanto tiempo lleva")),
            new Rule(DialogueIntent.SYMPTOM_DESCRIPTION, G("como es", "describ", "que tipo", "gira", "da vueltas", "como sientes el mareo", "explicame el mareo")),
            new Rule(DialogueIntent.CONSENT_HELP, G("puedo ayudar", "te ayudo", "dejas ayudar", "dejame ayudar", "permiso", "tumbar", "acostar", "recostar", "echarte", "tumbate")),
            new Rule(DialogueIntent.CURRENT_STATUS, G("como te encuentras", "como estas ahora", "como sigues", "te sientes mejor", "estas mejor", "como vas", "sigues mareado", "como te notas")),
            new Rule(DialogueIntent.MAIN_SYMPTOM, G("que te pasa", "que te ocurre", "que te sucede", "que sientes", "que tienes", "que notas", "estas bien", "te encuentras bien", "que ha pasado", "que te paso")),
            new Rule("name", G("como te llamas", "tu nombre", "como te llaman")),
            new Rule("place", G("donde estas", "donde estamos", "sabes donde", "que lugar")),
            new Rule(DialogueIntent.GREETING, G("hola", "buenas", "me llamo", "soy ", "vengo a ayudar", "estoy aqui", "me escuchas", "me oyes", "tranquilo")),
        };

        public static SpokenRequest Parse(string utterance)
        {
            var request = new SpokenRequest();
            string text = " " + Normalize(utterance) + " ";
            var found = new List<(int position, Rule rule)>();
            foreach (var rule in Rules)
            {
                int position = int.MaxValue; bool all = true;
                foreach (var group in rule.Need)
                {
                    int best = group.Select(w => text.IndexOf(Normalize(w), System.StringComparison.Ordinal)).Where(i => i >= 0).DefaultIfEmpty(-1).Min();
                    if (best < 0) { all = false; break; }
                    position = System.Math.Min(position, best);
                }
                if (all) found.Add((position, rule));
            }
            foreach (var item in found.OrderBy(x => x.position))
            {
                if (item.rule.SmallTalk != null) { request.SmallTalk ??= item.rule.SmallTalk; continue; }
                var intent = item.rule.Intent.Value;
                // "¿Cómo estás?" alone means the current state; with symptom words it is the main complaint.
                if (intent == DialogueIntent.MAIN_SYMPTOM && request.Intents.Contains(DialogueIntent.CURRENT_STATUS)) continue;
                if (!request.Intents.Contains(intent)) request.Intents.Add(intent);
            }
            // A greeting mixed with a real question is just politeness; answer the question.
            if (request.Intents.Count > 1) request.Intents.Remove(DialogueIntent.GREETING);
            return request;
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category == UnicodeCategory.NonSpacingMark) continue;
                builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
            }
            return string.Join(" ", builder.ToString().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
