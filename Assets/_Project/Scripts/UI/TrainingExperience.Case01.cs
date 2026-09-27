using EmergencyVR.Scenarios;

namespace EmergencyVR.UI
{
    public sealed partial class TrainingExperience
    {
        void DrawCase01VariationOptions()
        {
            var variations = Review.GetComponent<Case01VariationController>();
            if (variations == null || !Review.Procedures.TrainingMode) return;
            Label(content, "Practice variation heading", "Variante de práctica", 73, 566, 770, 31, 21, Ink, true);
            Label(content, "Practice variation description", "Elige la situación que quieres practicar antes de comenzar.", 73, 601, 770, 28, 17, Soft);
            var choices = new[] { Case01Variation.Normal, Case01Variation.Persistent, Case01Variation.Recurrence };
            for (int i = 0; i < choices.Length; i++)
            {
                var choice = choices[i];
                var button = Button(content, "Practice variation " + choice, Case01VariationController.Label(choice), 73 + i * 259, 643, 244, 53,
                    () => { variations.Configure(choice); redraw = true; }, variations.SelectedVariant == choice);
                button.interactable = variations.CanConfigure;
            }
        }
    }
}
