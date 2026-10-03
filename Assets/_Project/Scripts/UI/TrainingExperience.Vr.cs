using System;
using System.Linq;
using EmergencyVR.Desktop;
using EmergencyVR.Medical;
using EmergencyVR.Scenarios;
using UnityEngine;
using UnityEngine.UI;

namespace EmergencyVR.UI
{
    public sealed partial class TrainingExperience
    {
        public const float VrMenuWidth = 1.3f, VrMenuDistance = 1.6f;
        public bool UsesSidePanel => !IsDesktop && Page == ExperiencePage.Training;
        Vector2 VrPageSize => UsesSidePanel ? new Vector2(600, 900) : new Vector2(1000, 900);

        void PositionVrInterface()
        {
            surface.sizeDelta = VrPageSize;
            surface.localScale = Vector3.one * ((UsesSidePanel ? .68f : VrMenuWidth) / surface.sizeDelta.x);
            var forward = Vector3.ProjectOnPlane(viewer.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            if (!UsesSidePanel)
            {
                surface.position = viewer.transform.position + forward * VrMenuDistance;
                surface.rotation = Quaternion.LookRotation(forward, Vector3.up);
                return;
            }
            var right = Vector3.Cross(Vector3.up, forward);
            var patient = Review.Procedures.Visuals.ChestAnchor.position - viewer.transform.position;
            float side = Vector3.Dot(patient, right) < 0 ? 1 : -1;
            surface.position = viewer.transform.position + forward * 1.15f + right * side * 1.05f - Vector3.up * .22f;
            surface.rotation = Quaternion.LookRotation(surface.position - viewer.transform.position, Vector3.up);
        }

        void RenderVrPage()
        {
            if (Page == ExperiencePage.Training) { DrawVrTraining(); return; }
            if (Page == ExperiencePage.Tutorial) { DrawVrTutorial(); return; }
            Box(content, "VR backdrop", 0, 0, 1000, 900, Background, true, true);
            Label(content, "Product", "VITAL <color=#ED1939>VR</color>", 36, 28, 470, 52, 36, Ink, true);
            Button(content, "Help", "Ayuda", 666, 22, 140, 60, () => OpenUtility(ExperiencePage.Help));
            Button(content, "Settings", "Ajustes", 822, 22, 142, 60, () => OpenUtility(ExperiencePage.Settings));
            Box(content, "Header line", 36, 98, 928, 2, Border, false);
            switch (Page)
            {
                case ExperiencePage.Welcome: DrawVrWelcome(); break;
                case ExperiencePage.Environments: DrawVrEnvironments(); break;
                case ExperiencePage.Catalog: DrawVrCatalog(); break;
                case ExperiencePage.Briefing: DrawVrBriefing(); break;
                case ExperiencePage.Results: DrawVrResults(); break;
                case ExperiencePage.Help: DrawVrHelp(); break;
                case ExperiencePage.Settings: DrawVrSettings(); break;
                default: DrawVrPause(); break;
            }
        }

        void VrTitle(string title, string subtitle = "")
        {
            Label(content, "Page title", CleanCopy(title), 36, 119, 928, 92, 38, Ink, true);
            if (!string.IsNullOrEmpty(subtitle)) Label(content, "Subtitle", subtitle, 36, 204, 928, 65, 28, Soft);
        }

        void DrawVrWelcome()
        {
            Label(content, "Welcome brand", "Entrena para ayudar.", 36, 138, 928, 76, 54, Ink, true);
            Label(content, "Welcome tagline", VitalBrand.Tagline, 36, 226, 928, 85, 32, Soft);
            Button(content, "Recommended demo", "Demo recomendada  →", 36, 335, 928, 82, StartRecommendedDemo, true);
            Button(content, "Start learning", "Elegir entrenamiento", 36, 435, 928, 76, () => Navigate(ExperiencePage.Environments));
            Button(content, "Learn controls", "Practicar los controles · 30 s", 36, 529, 928, 76, StartIntroTutorial);
            Label(content, "Collection title", "TRES ENTORNOS · QUINCE ENTRENAMIENTOS", 36, 639, 928, 36, 25, Accent, true);
            for (int i = 0; i < environments.Length; i++)
            {
                string id = environments[i];
                Button(content, "Environment " + id, EnvironmentName(id), 36 + i * 315, 693, 298, 104, () => Browse(id));
            }
            Label(content, "Welcome voice notice", "Voz sintética · tutorial opcional", 36, 837, 928, 33, 24, Soft);
        }

        void DrawVrEnvironments()
        {
            VrTitle("¿Dónde quieres entrenar?", "Elige el entorno de tu próxima práctica.");
            for (int i = 0; i < environments.Length; i++)
            {
                string id = environments[i];
                var card = Button(content, "Environment " + id, "", 36, 295 + i * 146, 928, 124, () => Browse(id));
                var art = Box(card.transform, "Environment illustration", 18, 14, 158, 96, Background);
                DrawEnvironmentSymbol(art.transform, id, 158, 96);
                Label(card.transform, "Environment name", EnvironmentName(id), 204, 23, 690, 47, 33, Ink, true);
                Label(card.transform, "Environment count", "5 entrenamientos  →", 205, 77, 690, 33, 25, Soft);
            }
            Button(content, "Back home", "← Inicio", 36, 794, 280, 72, () => Navigate(ExperiencePage.Welcome));
            Button(content, "All scenarios", "Ver todos los entrenamientos", 336, 794, 628, 72, () => Browse(""));
        }

        void DrawVrCatalog()
        {
            VrTitle(EnvironmentName(SelectedEnvironment));
            Button(content, "Choose environment", "Cambiar entorno", 36, 213, 446, 64, () => Navigate(ExperiencePage.Environments));
            var levels = new[] { "Todas" }.Concat(Review.Catalog.entries.Where(entry => entry.medical != null &&
                (SelectedEnvironment == "" || entry.medical.environment == SelectedEnvironment)).Select(entry => entry.medical.difficulty).Distinct()).ToArray();
            Button(content, "Difficulty filter", "Nivel: " + Friendly(difficulty), 500, 213, 464, 64,
                () => { difficulty = levels[(Array.IndexOf(levels, difficulty) + 1) % levels.Length]; catalogPage = 0; redraw = true; });
            var indices = FilteredCases(); int pages = Mathf.Max(1, Mathf.CeilToInt(indices.Length / 6f)); catalogPage = Mathf.Clamp(catalogPage, 0, pages - 1);
            for (int slot = 0; slot < 6 && catalogPage * 6 + slot < indices.Length; slot++)
            {
                int index = indices[catalogPage * 6 + slot]; var medical = Review.Catalog.entries[index].medical;
                var card = Button(content, "Case " + medical.id, "", 36 + slot % 2 * 473, 302 + slot / 2 * 154, 455, 139, () => Prepare(index));
                card.interactable = medical.availability == "AVAILABLE";
                Label(card.transform, "Case name", LearnerTitle(medical), 18, 15, 419, 83, 28, Ink, true);
                Label(card.transform, "Case details", Friendly(medical.difficulty) + " · Preparar →", 18, 103, 419, 31, 23, Soft);
            }
            if (indices.Length == 0) Label(content, "Empty search", "No hay casos con este filtro. Elige otro nivel.", 36, 330, 928, 160, 32, Soft);
            Label(content, "Page count", "Página " + (catalogPage + 1) + " de " + pages, 36, 768, 928, 34, 25, Soft);
            Button(content, "Back environments", "← Entornos", 36, 817, 296, 60, () => Navigate(ExperiencePage.Environments));
            Button(content, "Previous cases", "← Anterior", 350, 817, 296, 60, () => { catalogPage--; redraw = true; }).interactable = catalogPage > 0;
            Button(content, "Next cases", "Siguiente →", 664, 817, 300, 60, () => { catalogPage++; redraw = true; }).interactable = catalogPage < pages - 1;
        }

        void DrawVrBriefing()
        {
            if (PendingCaseIndex < 0) { Navigate(ExperiencePage.Catalog); return; }
            var entry = Review.Catalog.entries[PendingCaseIndex]; var medical = entry.medical;
            VrTitle(medical == null ? entry.definition.displayName : LearnerTitle(medical));
            var body = ScrollArea(36, 225, 928, 244, 244);
            var description = Label(body, "Briefing context", (string.IsNullOrEmpty(notice) ? "" : notice + "\n\n") + CleanCopy(LearnerContext(medical, entry.briefing)) +
                "\n\nObserva, habla con el paciente o su acompañante y utiliza el equipo disponible. El caso empieza al pulsar Iniciar entrenamiento.", 0, 0, 874, 1000, 30, Soft);
            float height = description.preferredHeight + 20;
            description.rectTransform.sizeDelta = new Vector2(874, height);
            ((RectTransform)body).sizeDelta = new Vector2(910, Mathf.Max(244, height));
            bool guided = Review.Procedures.TrainingMode;
            Button(content, "Guided mode", (guided ? "●  " : "○  ") + "Práctica guiada", 36, 494, 455, 74, () => { Review.Procedures.TrainingMode = true; redraw = true; }, guided);
            Button(content, "Assessment mode", (!guided ? "●  " : "○  ") + "Evaluación", 509, 494, 455, 74, () => { Review.Procedures.TrainingMode = false; redraw = true; }, !guided);
            Label(content, "Mode explanation", guided ? "Ayudas para practicar. Mide con los instrumentos para obtener las constantes." :
                "Sin pistas clínicas. Observa y actúa según tu formación.", 36, 585, 928, 86, 27, Soft);
            var variations = Review.GetComponent<Case01VariationController>();
            if (Case01VariationController.Supports(medical) && guided && variations != null)
            {
                var choices = new[] { Case01Variation.Normal, Case01Variation.Persistent, Case01Variation.Recurrence };
                for (int i = 0; i < choices.Length; i++)
                {
                    var choice = choices[i];
                    Button(content, "Practice variation " + choice, Case01VariationController.Label(choice), 36 + i * 315, 684, 298, 76,
                        () => { variations.Configure(choice); redraw = true; }, variations.SelectedVariant == choice).interactable = variations.CanConfigure;
                }
            }
            Label(content, "Review status", medical?.medicalValidationStatus == "APPROVED" ? "Contenido revisado" : "Simulación en revisión clínica", 36, 775, 928, 33, 24, Amber);
            Button(content, "Back catalog", "← Volver", 36, 821, 274, 60, () => Navigate(medical == null ? ExperiencePage.Help : ExperiencePage.Catalog));
            Button(content, "Begin training", "Iniciar entrenamiento  →", 328, 821, 636, 60, BeginTraining, true);
        }

        void DrawVrPause()
        {
            bool confirm = Page == ExperiencePage.Finish || Page == ExperiencePage.Restart || Page == ExperiencePage.Exit;
            VrTitle(confirm ? "¿Cerrar este entrenamiento?" : "Entrenamiento en pausa");
            Label(content, "Pause case", Review.Selected.medical == null ? Review.Selected.definition.displayName : LearnerTitle(Review.Selected.medical), 36, 242, 928, 119, 34, Ink, true);
            var detail = Label(content, "Pause detail", "", 36, 377, 928, 157, 30, Soft);
            Bind(detail, () => controllersUnavailable ? "Recupera el seguimiento de un mando. El caso y el audio permanecen en pausa." :
                confirm ? "Revisarás las acciones realizadas y las oportunidades de mejora. El intento se cerrará al confirmar." :
                "El tiempo y el audio están detenidos. Al continuar volverás al mismo punto.");
            if (confirm)
            {
                Button(content, "Cancel confirmation", "Seguir en pausa", 36, 601, 928, 82, () => Navigate(ExperiencePage.Pause));
                Button(content, "Confirm finish", "Cerrar y revisar", 36, 709, 928, 82, FinishTraining, true);
                return;
            }
            var resume = Button(content, "Resume training", "Continuar entrenamiento", 36, 571, 928, 82, () => Navigate(ExperiencePage.Training), true);
            live.Add(() => resume.interactable = !controllersUnavailable && !applicationInterrupted);
            Button(content, "Request finish", "Finalizar y revisar", 36, 678, 928, 76, () => Navigate(ExperiencePage.Finish));
            Button(content, "Request restart", "Preparar otro intento", 36, 783, 928, 76, () => Navigate(ExperiencePage.Restart));
        }

        void DrawVrHelp()
        {
            VrTitle("Controles siempre a mano");
            string[] titles = { "Apunta y selecciona", "Coge y desplázate", "Consulta y pausa" };
            string[] descriptions = { "Apunta a un botón y pulsa el gatillo. Puedes usar cualquiera de los dos mandos.",
                "Grip para coger o soltar. Stick hacia delante para elegir dónde teletransportarte; suéltalo para viajar.",
                "El panel de consulta está a tu lado. B o Y centra el menú de pausa delante de tus ojos." };
            for (int i = 0; i < titles.Length; i++)
            {
                Label(content, "Help title", titles[i], 36, 238 + i * 180, 928, 46, 33, Ink, true);
                Label(content, "Help instructions", descriptions[i], 36, 293 + i * 180, 928, 107, 28, Soft);
            }
            Button(content, "Back from help", "← Volver", 36, 808, 928, 72, () => Navigate(returnPage));
        }

        void DrawVrSettings()
        {
            VrTitle("Tu espacio de entrenamiento");
            Label(content, "Audio title", "Volumen general", 36, 245, 640, 49, 33, Ink, true);
            Button(content, "Volume down", "−", 36, 315, 190, 76, () => { AudioListener.volume = Mathf.Max(0, AudioListener.volume - .1f); redraw = true; });
            Label(content, "Volume", Mathf.RoundToInt(AudioListener.volume * 100) + "%", 270, 329, 465, 55, 37, Accent, true);
            Button(content, "Volume up", "+", 774, 315, 190, 76, () => { AudioListener.volume = Mathf.Min(1, AudioListener.volume + .1f); redraw = true; });
            Label(content, "Hints title", "Recordatorios de controles", 36, 448, 640, 84, 32, Ink, true);
            Button(content, "Toggle hints", showHints ? "Activados" : "Desactivados", 680, 448, 284, 78, () => { showHints = !showHints; redraw = true; }, showHints);
            Label(content, "Quality title", "Calidad visual", 36, 580, 928, 46, 33, Ink, true);
            bool smooth = QuestQualityControl.Smooth;
            Button(content, "Quality high", (smooth ? "○  " : "●  ") + "Alta", 36, 650, 455, 78, () => { QuestQualityControl.SetSmooth(false); redraw = true; }, !smooth);
            Button(content, "Quality smooth", (smooth ? "●  " : "○  ") + "Fluida", 509, 650, 455, 78, () => { QuestQualityControl.SetSmooth(true); redraw = true; }, smooth);
            Button(content, "Back from settings", "← Volver", 36, 806, 455, 74, () => Navigate(returnPage));
            Button(content, "Recenter interface", "Centrar interfaz", 509, 806, 455, 74, Recenter);
        }
    }
}
