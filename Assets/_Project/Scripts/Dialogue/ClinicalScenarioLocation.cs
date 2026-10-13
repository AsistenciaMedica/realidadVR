using EmergencyVR.Medical;

namespace EmergencyVR.Dialogue
{
    /// <summary>Authored setting details shared by dialogue, communication records and practice guidance.</summary>
    public static class ClinicalScenarioLocation
    {
        public static string PatientPlace(string environment) => environment == "football"
            ? "En la cancha… acababa de terminar el entrenamiento."
            : environment == "street" ? "En la calle… estaba caminando y me senté aquí."
            : "En el gimnasio… estaba en la cinta.";

        public static string ShortLocation(string environment) => environment == "football"
            ? "En la cancha de fútbol."
            : environment == "street" ? "En la calle, en la acera."
            : "En el gimnasio.";

        public static string Location(string environment) => environment == "football"
            ? "Cancha del polideportivo Vital, avenida Deportiva 5. Estamos junto al banco de suplentes, en el acceso principal."
            : environment == "street" ? "Calle Mayor 12, en la acera junto al paso peatonal."
            : "Gimnasio Vital, calle Mayor 12, planta baja. Estamos en la zona de cardio.";

        public static string Incident(string environment) => environment == "football"
            ? "Hay un hombre que se encuentra mal en la cancha de fútbol. Está muy mareado."
            : environment == "street" ? "Hay un hombre que se encuentra mal en la acera. Está muy mareado."
            : "Hay un hombre que se encuentra mal en un gimnasio. Está muy mareado.";

        public static string SceneSafety(string environment) => environment == "football"
            ? "Comprueba que el juego está detenido y que no hay balones ni material deportivo en el paso."
            : environment == "street" ? "Comprueba que estás en una zona segura de la acera, fuera del tráfico y con el paso despejado."
            : "Comprueba que la cinta está parada y que no hay pesas en el paso.";

        public static void ApplyDialogueContext(ClinicalScenarioV2Definition definition, string environment)
        {
            if (definition?.dialogue == null || (environment != "football" && environment != "street")) return;
            foreach (var intent in definition.dialogue)
                foreach (var response in intent.responses)
                {
                    if (response.id == "DANIEL_ONSET_01")
                        response.text = environment == "football"
                            ? "Al terminar el entrenamiento y quedarme quieto."
                            : "Al detenerme después de caminar y quedarme quieto.";
                    else if (environment == "street" && response.id == "DANIEL_FOOD_DRINK_01")
                        response.text = "Comí antes de salir… y he bebido mientras caminaba.";
                    else continue;
                    response.subtitle = response.text;
                    // Never fall back to the gym recording when the setting-specific line has no recording yet.
                    response.id += "_" + environment.ToUpperInvariant();
                    response.audioReference = response.id;
                }
        }
    }
}
