using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace EmergencyVR.UI
{
    public sealed partial class TrainingExperience
    {
        readonly string[] environments = { "gym", "football", "mall", "dental" };
        bool showHints = true;

        void DrawWelcome()
        {
            Badge(content, "APRENDER · PRACTICAR · MEJORAR", 64, 157, 363);
            Label(content, "Welcome brand", "VITAL <color=#ED1939>VR</color>", 60, 224, 655, 111, 86, Ink, true);
            Label(content, "Welcome tagline", VitalBrand.Tagline, 64, 355, 620, 112, 36, Ink, true);
            Label(content, "Welcome description", "Un espacio para entrenar decisiones, practicar procedimientos y revisar tu actuación.", 64, 501, 560, 90, 25, Soft);
            Button(content, "Start learning", "Elegir entrenamiento  →", 64, 622, 405, 66, () => Navigate(ExperiencePage.Environments), true);
            Button(content, "Learn controls", "Conocer los controles", 64, 706, 405, 57, () => OpenUtility(ExperiencePage.Help));
            Box(content, "Welcome collection", 739, 149, 637, 629, CardColor);
            Label(content, "Collection title", "TU PRÓXIMO ENTRENAMIENTO", 767, 178, 570, 32, 19, Accent, true);
            Label(content, "Collection subtitle", "Cuatro entornos. Una experiencia clínica.", 767, 218, 570, 48, 25, Ink, true);
            for (int i = 0; i < environments.Length; i++)
                EnvironmentCard(content, environments[i], 765 + i % 2 * 294, 286 + i / 2 * 224, 280, 208, true);
        }
        void EnvironmentCard(Transform parent, string id, float x, float y, float width, float height, bool compact)
        {
            int count = Review.Catalog.entries.Count(e => e.medical?.environment == id);
            var card = Button(parent, "Environment " + id, "", x, y, width, height, () => Browse(id));
            var p = card.transform;
            float artHeight = compact ? 93 : 169;
            var art = Box(p, "Environment illustration", 14, 14, width - 28, artHeight, Background);
            DrawEnvironmentSymbol(art.transform, id, width - 28, artHeight);
            Label(p, "Environment name", EnvironmentName(id), 20, artHeight + 31, width - 40, compact ? 32 : 72, compact ? 21 : 27, Ink, true);
            Label(p, "Environment count", count + " entrenamientos", 20, compact ? 161 : 285, width - 40, 29, 18, Soft);
            if (!compact) Label(p, "Environment link", "Explorar entrenamientos  →", 20, height - 60, width - 40, 44, 19, Accent, true);
        }
        void DrawEnvironmentSymbol(Transform parent, string id, float width, float height)
        {
            // Native schematic illustrations: no concept images masquerading as captured scenes.
            float sx = width / 260, sy = height / 160;
            Action<float, float, float, float, Color> block = (x, y, w, h, c) => Box(parent, "Schematic", x * sx, y * sy, w * sx, h * sy, c, false);
            Color tone = id == "dental" ? (Color)new Color32(117, 173, 230, 255) : Accent;
            if (id == "football")
            {
                block(28, 24, 204, 2, tone); block(28, 134, 204, 2, tone); block(28, 24, 2, 112, tone); block(230, 24, 2, 112, tone);
                block(129, 24, 2, 110, tone); block(28, 58, 29, 2, tone); block(28, 101, 29, 2, tone); block(55, 58, 2, 45, tone);
                block(204, 58, 28, 2, tone); block(204, 101, 28, 2, tone); block(204, 58, 2, 45, tone);
                Box(parent, "Centre", 117 * sx, 66 * sy, 28 * sx, 28 * sy, tone);
            }
            else if (id == "gym")
            {
                block(61, 77, 140, 8, tone); block(62, 42, 17, 77, tone); block(39, 53, 17, 55, tone);
                block(182, 42, 17, 77, tone); block(205, 53, 17, 55, tone); block(30, 137, 200, 2, Border);
            }
            else if (id == "dental")
            {
                block(86, 52, 14, 48, tone); block(90, 93, 78, 15, tone); block(160, 104, 14, 27, tone);
                block(117, 108, 8, 29, tone); block(94, 136, 60, 4, tone); block(179, 28, 5, 81, Soft);
                block(140, 28, 42, 5, Soft); block(131, 33, 23, 8, tone);
            }
            else
            {
                for (int i = 0; i < 3; i++) { block(32 + i * 70, 50, 57, 64, Border); block(38 + i * 70, 55, 45, 39, tone); }
                block(28, 38, 206, 7, Soft); block(28, 128, 206, 3, tone);
            }
        }
        void DrawEnvironments()
        {
            PageTitle("01 / Entorno", "¿Dónde quieres entrenar?", "Elige un ambiente para explorar sus situaciones clínicas y preparar tu práctica.");
            for (int i = 0; i < environments.Length; i++) EnvironmentCard(content, environments[i], 48 + i * 340, 317, 322, 399, false);
            Button(content, "Back home", "← Inicio", 48, 758, 178, 52, () => Navigate(ExperiencePage.Welcome));
            Button(content, "All scenarios", "Ver todos los entrenamientos", 970, 758, 422, 52, () => Browse(""));
        }
        int[] FilteredCases()
        {
            return Enumerable.Range(0, Review.Catalog.entries.Length).Where(i =>
            {
                var m = Review.Catalog.entries[i].medical;
                return m != null && (SelectedEnvironment == "" || m.environment == SelectedEnvironment) &&
                    (category == "Todas" || m.category == category) && (difficulty == "Todas" || m.difficulty == difficulty) &&
                    (string.IsNullOrWhiteSpace(search) || (m.name + " " + m.description).IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
            }).ToArray();
        }
        void DrawCatalog()
        {
            PageTitle("02 / Entrenamiento", EnvironmentName(SelectedEnvironment), "Selecciona una situación clínica. Podrás revisar la preparación antes de empezar.");
            var candidates = Review.Catalog.entries.Where(e => e.medical != null && (SelectedEnvironment == "" || e.medical.environment == SelectedEnvironment)).ToArray();
            var categories = new[] { "Todas" }.Concat(candidates.Select(e => e.medical.category).Distinct()).ToArray();
            var difficulties = new[] { "Todas" }.Concat(candidates.Select(e => e.medical.difficulty).Distinct()).ToArray();
            Button(content, "Category filter", "Área: " + Friendly(category), 48, 305, 470, 50, () => { category = categories[(Array.IndexOf(categories, category) + 1) % categories.Length]; catalogPage = 0; redraw = true; });
            Button(content, "Difficulty filter", "Dificultad: " + Friendly(difficulty), 534, 305, 380, 50, () => { difficulty = difficulties[(Array.IndexOf(difficulties, difficulty) + 1) % difficulties.Length]; catalogPage = 0; redraw = true; });
            if (IsDesktop)
            {
                var bg = Box(content, "Search field", 930, 305, 462, 50, Raised, true, true);
                var input = bg.gameObject.AddComponent<InputField>(); input.targetGraphic = bg;
                input.textComponent = Label(bg.transform, "Value", "", 16, 12, 430, 32, 20);
                input.placeholder = Label(bg.transform, "Placeholder", "Buscar por nombre · Enter", 16, 12, 430, 32, 20, Soft);
                input.text = search;
                input.onEndEdit.AddListener(value => { if (search != value) { search = value; catalogPage = 0; redraw = true; } });
            }
            else Button(content, "Reset filters", "Restablecer filtros", 930, 305, 462, 50, () => { category = difficulty = "Todas"; catalogPage = 0; redraw = true; });
            var indices = FilteredCases(); int pages = Mathf.Max(1, Mathf.CeilToInt(indices.Length / 6f)); catalogPage = Mathf.Clamp(catalogPage, 0, pages - 1);
            if (indices.Length == 0) Label(content, "Empty search", "No hay entrenamientos con estos filtros.\nPrueba otra búsqueda o restablece los filtros.", 210, 450, 1000, 125, 30, Soft);
            for (int slot = 0; slot < 6; slot++)
            {
                int k = catalogPage * 6 + slot; if (k >= indices.Length) break;
                int index = indices[k]; var m = Review.Catalog.entries[index].medical;
                float x = 48 + slot % 3 * 452, y = 379 + slot / 3 * 180;
                var card = Button(content, "Case " + m.id, "", x, y, 436, 163, () => Prepare(index));
                card.interactable = m.availability == "AVAILABLE";
                Label(card.transform, "Category", Friendly(m.category).ToUpperInvariant(), 20, 16, 396, 27, 15, Accent, true);
                Label(card.transform, "Case name", CleanCopy(m.name), 20, 52, 396, 67, 23, Ink, true);
                Label(card.transform, "Case details", Friendly(m.difficulty) + "  ·  " + (card.interactable ? "Preparar →" : "No disponible"), 20, 124, 396, 26, 17, Soft);
            }
            Button(content, "Back environments", "← Entornos", 48, 760, 205, 52, () => Navigate(ExperiencePage.Environments));
            Label(content, "Page count", $"{indices.Length} entrenamientos   ·   Página {catalogPage + 1} de {pages}", 303, 774, 643, 28, 20, Soft);
            Button(content, "Previous cases", "← Anterior", 994, 760, 190, 52, () => { catalogPage--; redraw = true; }).interactable = catalogPage > 0;
            Button(content, "Next cases", "Siguiente →", 1200, 760, 192, 52, () => { catalogPage++; redraw = true; }).interactable = catalogPage < pages - 1;
        }
        void DrawBriefing()
        {
            if (PendingCaseIndex < 0) { Navigate(ExperiencePage.Catalog); return; }
            var entry = Review.Catalog.entries[PendingCaseIndex]; var m = entry.medical;
            bool showCaseVariation = EmergencyVR.Scenarios.Case01VariationController.Supports(m) && Review.Procedures.TrainingMode;
            PageTitle("03 / Preparación", entry.definition.displayName, m == null ? "Familiarización con la interacción del simulador." : EnvironmentName(m.environment) + "  /  " + Friendly(m.category) + "  /  " + Friendly(m.difficulty));
            Box(content, "Briefing card", 48, 306, 832, 411, CardColor);
            Label(content, "Briefing heading", "La situación", 73, 328, 770, 43, 28, Ink, true);
            var body = ScrollArea(73, 384, 780, showCaseVariation ? 168 : 301, 560);
            var text = Label(body, "Briefing context", CleanCopy(entry.briefing) + "\n\n<b>Tu práctica</b>\nObserva la situación, utiliza el equipo disponible y registra tus decisiones. Al finalizar podrás revisar las acciones, los tiempos y las oportunidades de mejora.\n\nEl caso comienza al pulsar Iniciar entrenamiento.", 0, 0, 742, 560, 23, Soft);
            var height = text.preferredHeight + 20; text.rectTransform.sizeDelta = new Vector2(742, height); ((RectTransform)body).sizeDelta = new Vector2(762, Mathf.Max(301, height));
            if (showCaseVariation) DrawCase01VariationOptions();
            Box(content, "Mode card", 902, 306, 490, 411, CardColor);
            Label(content, "Mode heading", "Cómo quieres practicar", 928, 330, 439, 47, 27, Ink, true);
            bool guided = Review.Procedures.TrainingMode;
            bool observed = m?.clinicalV2?.capabilities.usesObservedPatientData == true;
            Button(content, "Guided mode", (guided ? "●  " : "○  ") + "Práctica guiada", 928, 395, 438, 56, () => { Review.Procedures.TrainingMode = true; redraw = true; }, guided);
            Label(content, "Guided description", observed ? "Práctica con orientación de interacción. La ficha conserva solo la información que obtienes." : "Ayudas de procedimiento y valores simulados visibles para aprender.", 940, 465, 407, 68, 20, Soft);
            Button(content, "Assessment mode", (!guided ? "●  " : "○  ") + "Evaluación", 928, 548, 438, 56, () => { Review.Procedures.TrainingMode = false; redraw = true; }, !guided);
            Label(content, "Assessment description", observed ? "Sin pistas clínicas. Habla con el paciente, observa y actúa según tu formación." : "Sin pistas. Consulta al paciente y realiza las mediciones con los instrumentos.", 940, 618, 407, 77, 20, Soft);
            Label(content, "Review status", m == null ? "Práctica de controles · sin contenido clínico" : (m.medicalValidationStatus == "APPROVED" ? "Contenido revisado" : "Contenido de simulación en revisión clínica"), 50, 725, 1260, 29, 17, Amber);
            Button(content, "Back catalog", "← Volver", 48, 766, 188, 52, () => Navigate(m == null ? ExperiencePage.Help : ExperiencePage.Catalog));
            Label(content, "Start feedback", notice, 255, 770, 670, 48, 18, Amber);
            Button(content, "Begin training", "Iniciar entrenamiento  →", 955, 759, 437, 64, BeginTraining, true);
        }
        void DrawHelp()
        {
            PageTitle("Orientación", "Familiarízate con VITAL VR", "Los controles permanecen disponibles desde el menú durante la práctica.");
            var titles = IsDesktop ? new[] { "Explora", "Utiliza el equipo", "Practica y revisa" } : new[] { "Desplázate", "Interactúa", "Consulta y pausa" };
            var details = IsDesktop ? new[] {
                "W A S D  ·  Caminar\nBotón derecho  ·  Mirar\nR  ·  Volver al punto inicial\n\nHaz clic en los botones de la interfaz para navegar.",
                "E  ·  Coger o soltar\nQ  ·  Usar el objeto\nRueda  ·  Ajustar alcance\nZ / X  ·  Orientar\nShift / Alt  ·  Cambiar eje",
                "C  ·  Activar RCP cerca del paciente\nClic y arrastre sobre el tórax; soltar para retroceso.\n\nEsc  ·  Pausar / continuar\nAcciones  ·  Registrar decisiones"
            } : new[] {
                "Stick hacia arriba para apuntar a una zona de teleportación; suelta para viajar.\n\nGiro lateral por pasos de 30°.\nLa cabeza mantiene seguimiento libre.",
                "Apunta al panel y pulsa el gatillo para seleccionar.\n\nGrip para coger y soltar objetos. Utiliza el equipo sobre sus zonas de contacto.",
                "B / Y  ·  Centrar el panel y abrir la pausa.\n\nEl monitor permanece en el espacio. Puedes volver a centrarlo cuando cambies de posición."
            };
            if(Review.Selected.medical?.clinicalV2?.capabilities.usesObservedPatientData==true)
            {
                titles=new[]{"Acércate y observa","Interacción y comunicación","Tu sesión"};
                details=new[]{IsDesktop?"W A S D · Caminar\nBotón derecho · Mirar\n\nUsa las interacciones para observar y hablar. Cada respuesta se incorpora a tu ficha.":"Usa el desplazamiento XR para acercarte. Apunta al panel y pulsa el gatillo para observar o conversar.",
                    "La asistencia requiere cercanía y apoyo mantenido. Puedes detenerla y cancelar.\n\nEl teléfono y el panel de ayuda permiten una llamada simulada o delegar el aviso.",
                    "La ficha contiene únicamente información obtenida.\n\nPausa: Esc en Desktop o B / Y en VR.\nFinalizar permite revisar incluso un intento incompleto."};
            }
            for (int i = 0; i < 3; i++)
            {
                float x = 48 + i * 452; Box(content, "Help card", x, 314, 436, 383, CardColor);
                Badge(content, "0" + (i + 1), x + 24, 340, 53);
                Label(content, "Help title", titles[i], x + 24, 400, 388, 53, 30, Ink, true);
                Label(content, "Help instructions", details[i], x + 24, 465, 388, 219, 22, Soft);
            }
            Button(content, "Back from help", "← Volver", 48, 756, 189, 56, () => Navigate(returnPage));
            if (!Review.Manager.IsRunning)
                Button(content, "Practice controls", "Practicar los controles", 992, 756, 400, 56, () => Prepare(Array.FindIndex(Review.Catalog.entries, e => e.medical == null)), true);
        }
        void DrawSettings()
        {
            PageTitle("Preferencias", "Tu espacio de entrenamiento", "Ajusta el sonido y las ayudas de interacción.");
            Box(content, "Settings card", 48, 321, 1344, 361, CardColor);
            Label(content, "Audio title", "Volumen general", 81, 356, 530, 44, 28, Ink, true);
            Label(content, "Audio description", "Ambiente, paciente y equipo médico", 81, 409, 800, 39, 23, Soft);
            Button(content, "Volume down", "−", 970, 353, 65, 59, () => { AudioListener.volume = Mathf.Max(0, AudioListener.volume - .1f); redraw = true; });
            Label(content, "Volume", Mathf.RoundToInt(AudioListener.volume * 100) + "%", 1052, 367, 139, 43, 26, Accent, true);
            Button(content, "Volume up", "+", 1239, 353, 65, 59, () => { AudioListener.volume = Mathf.Min(1, AudioListener.volume + .1f); redraw = true; });
            Box(content, "Settings divider", 80, 477, 1280, 1, Border, false);
            Label(content, "Hints title", "Recordatorios de controles", 81, 516, 830, 48, 28, Ink, true);
            Label(content, "Hints description", "Las pistas clínicas dependen del modo elegido al preparar el caso.", 81, 574, 1010, 73, 22, Soft);
            Button(content, "Toggle hints", showHints ? "Activados" : "Desactivados", 1108, 511, 238, 59, () => { showHints = !showHints; redraw = true; }, showHints);
            Button(content, "Back from settings", "← Volver", 48, 756, 189, 56, () => Navigate(returnPage));
            if (!IsDesktop) Button(content, "Recenter interface", "Centrar interfaz", 1000, 756, 392, 56, Recenter);
        }
        void DrawPause()
        {
            bool finish = Page == ExperiencePage.Finish, restart = Page == ExperiencePage.Restart, exit = Page == ExperiencePage.Exit;
            PageTitle("Sesión", finish ? "¿Finalizar el entrenamiento?" : restart ? "¿Preparar un nuevo intento?" : exit ? "¿Salir de VITAL VR?" : "Entrenamiento en pausa",
                "El tiempo, la evolución del paciente y los procedimientos están detenidos.");
            Box(content, "Pause card", 250, 325, 940, 365, CardColor);
            Label(content, "Pause case", CleanCopy(Review.Selected.definition.displayName), 288, 362, 864, 93, 33, Ink, true);
            Label(content, "Pause detail", finish || restart || exit ? "El intento actual se cerrará con las acciones realizadas y las omisiones pendientes. Puedes revisar y guardar su resultado." : "Tómate el tiempo que necesites. Al continuar volverás al mismo punto del ejercicio.", 290, 474, 853, 113, 25, Soft);
            if (finish || restart || exit)
            {
                Button(content, "Cancel confirmation", "Seguir en pausa", 290, 601, 392, 58, () => Navigate(ExperiencePage.Pause));
                Button(content, "Confirm finish", "Cerrar y revisar", 710, 601, 440, 58, FinishTraining, true);
            }
            else
            {
                Button(content, "Resume training", "Continuar entrenamiento", 290, 601, 860, 58, () => Navigate(ExperiencePage.Training), true);
                Button(content, "Request finish", "Finalizar y revisar", 250, 750, 450, 58, () => Navigate(ExperiencePage.Finish));
                Button(content, "Request restart", "Preparar otro intento", 721, 750, 469, 58, () => Navigate(ExperiencePage.Restart));
            }
        }
    }
}
