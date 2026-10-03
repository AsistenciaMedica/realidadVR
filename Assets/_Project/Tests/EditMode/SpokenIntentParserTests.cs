using System.Linq;
using EmergencyVR.Dialogue;
using EmergencyVR.Medical;
using NUnit.Framework;

namespace EmergencyVR.Tests.EditMode
{
    public sealed class SpokenIntentParserTests
    {
        static DialogueIntent[] Parse(string text) => SpokenIntentParser.Parse(text).Intents.ToArray();

        [TestCase("¿Qué te pasa?", DialogueIntent.MAIN_SYMPTOM)]
        [TestCase("oye amigo que te ocurre", DialogueIntent.MAIN_SYMPTOM)]
        [TestCase("¿Desde cuándo estás así?", DialogueIntent.ONSET)]
        [TestCase("¿Te has desmayado en algún momento?", DialogueIntent.LOSS_OF_CONSCIOUSNESS)]
        [TestCase("¿Tienes dolor en el pecho?", DialogueIntent.CHEST_PAIN)]
        [TestCase("¿Puedes respirar bien?", DialogueIntent.BREATHING_DIFFICULTY)]
        [TestCase("¿Tomas alguna pastilla?", DialogueIntent.MEDICATION)]
        [TestCase("¿Has bebido agua hoy?", DialogueIntent.FOOD_DRINK)]
        [TestCase("¿Me dejas ayudarte a tumbarte?", DialogueIntent.CONSENT_HELP)]
        [TestCase("¿Cómo te encuentras ahora?", DialogueIntent.CURRENT_STATUS)]
        [TestCase("Hola, soy Juan, vengo a ayudarte", DialogueIntent.GREETING)]
        public void RecognisesNaturalPhrasings(string text, DialogueIntent expected)
        {
            Assert.That(Parse(text), Is.EqualTo(new[] { expected }));
        }

        [Test]
        public void AnswersCombinedQuestionsInSpokenOrder()
        {
            Assert.That(Parse("¿Te duele el pecho o te falta el aire?"),
                Is.EqualTo(new[] { DialogueIntent.CHEST_PAIN, DialogueIntent.BREATHING_DIFFICULTY }));
        }

        [Test]
        public void GreetingWithQuestionAnswersTheQuestion()
        {
            Assert.That(Parse("Hola, ¿qué te pasa?"), Is.EqualTo(new[] { DialogueIntent.MAIN_SYMPTOM }));
        }

        [Test]
        public void SmallTalkAndUnclearSpeech()
        {
            Assert.That(SpokenIntentParser.Parse("¿Cómo te llamas?").SmallTalk, Is.EqualTo("name"));
            Assert.That(SpokenIntentParser.Parse("el partido de ayer estuvo genial").Understood, Is.False);
        }
    }
}
