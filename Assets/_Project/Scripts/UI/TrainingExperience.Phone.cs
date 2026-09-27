using System;
using EmergencyVR.Dialogue;
using UnityEngine;

namespace EmergencyVR.UI
{
    public sealed partial class TrainingExperience
    {
        PhoneCallController Phone => Review == null ? null : Review.GetComponent<PhoneCallController>();
        PhoneCallStage phoneStage;
        PhoneCallController.Question phoneQuestion;

        /// <summary>Phone drawer: keypad while idle, then the dispatcher's voice with the learner's answers.</summary>
        void DrawPhone()
        {
            var phone = Phone;
            if (phone == null) { DrawHelpConversation(); return; }
            phoneStage = phone.Stage; phoneQuestion = phone.Current;
            live.Add(() => { if (phone.Stage != phoneStage || phone.Current != phoneQuestion) redraw = true; });

            Box(content, "Phone body", 26, 191, 560, 549, new Color32(6, 12, 20, 255), true, true);
            Box(content, "Phone screen", 44, 207, 524, 517, CardColor, true, false);
            Label(content, "Phone title", phone.InCall ? "● EN LLAMADA" : phone.Stage == PhoneCallStage.Idle ? "TELÉFONO" : "EMERGENCIAS", 66, 222, 300, 30, 18,
                phone.InCall ? Accent : Soft, true);
            var clock = Label(content, "Phone status", "", 330, 222, 216, 30, 18, Soft);
            clock.alignment = TextAnchor.UpperRight;
            Bind(clock, () => phone.Stage == PhoneCallStage.EnRoute ? "Ambulancia · " + TimeLabel(phone.SecondsToArrival) : "");

            if (phone.Stage == PhoneCallStage.Idle) { DrawKeypad(phone); return; }

            var subtitle = Label(content, "Phone subtitle", "", 66, 262, 480, 150, 21, Ink);
            Bind(subtitle, () => phone.Subtitle);
            var question = phone.Current;
            if (question != null)
            {
                float y = 424;
                for (int i = 0; i < question.Options.Count && i < 3; i++)
                {
                    int index = i;
                    var text = question.Options[i].Text;
                    int size = text.Length > 90 ? 15 : 18;
                    float h = text.Length > 90 ? 96 : 62;
                    var button = Button(content, "Call option " + i, text, 62, y, 488, h, () => phone.Choose(index));
                    var label = button.GetComponentInChildren<UnityEngine.UI.Text>();
                    label.fontSize = size; label.fontStyle = FontStyle.Normal;
                    y += h + 10;
                }
            }
            else if (phone.Stage == PhoneCallStage.EnRoute)
                Label(content, "Phone waiting", "La ambulancia está en camino. Quédate con Daniel, vuelve a valorarle y avisa si empeora.", 66, 430, 480, 90, 19, Soft);
            else if (phone.Stage == PhoneCallStage.Done)
                Button(content, "Review completed practice", "Finalizar y revisar la práctica", 62, 600, 488, 62, () => Navigate(ExperiencePage.Finish), true);
            if (phone.InCall)
                Button(content, "Phone hang up", "Colgar", 62, 664, 488, 48, phone.HangUp);
        }

        void DrawKeypad(PhoneCallController phone)
        {
            var display = Label(content, "Phone display", "", 66, 256, 480, 52, 38, Ink, true);
            display.alignment = TextAnchor.MiddleCenter;
            Bind(display, () => phone.Dialed.Length == 0 ? " " : phone.Dialed);
            var notice = Label(content, "Phone notice", "", 66, 310, 480, 40, 15, Amber);
            notice.alignment = TextAnchor.MiddleCenter;
            Bind(notice, () => phone.Notice);
            string keys = "123456789";
            for (int i = 0; i < 9; i++)
            {
                char key = keys[i];
                Button(content, "Phone key " + key, key.ToString(), 128 + i % 3 * 124, 356 + i / 3 * 66, 108, 56, () => phone.Press(key));
            }
            Button(content, "Phone erase", "⌫", 128, 554, 108, 56, phone.Erase);
            Button(content, "Phone key 0", "0", 252, 554, 108, 56, () => phone.Press('0'));
            Button(content, "Phone call", "Llamar", 376, 554, 108, 56, phone.Dial, true);
            Button(content, "Delegate simulated call", "Pedir a un compañero que llame", 62, 634, 488, 56, phone.Delegate);
            Label(content, "Phone disclaimer", "Llamada simulada: no se contacta con ningún servicio real.", 66, 696, 480, 24, 14, Soft);
        }
    }
}
