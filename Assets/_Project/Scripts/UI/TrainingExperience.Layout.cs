using System;
using UnityEngine;
using UnityEngine.UI;

namespace EmergencyVR.UI
{
    public sealed partial class TrainingExperience
    {
        static readonly Color Background = new Color32(9, 19, 30, 255);
        static readonly Color CardColor = new Color32(18, 34, 48, 255);
        static readonly Color Raised = new Color32(26, 47, 62, 255);
        static readonly Color Ink = new Color32(237, 245, 247, 255);
        static readonly Color Soft = new Color32(155, 180, 192, 255);
        static readonly Color Accent = new Color32(72, 216, 186, 255);
        static readonly Color Amber = new Color32(238, 194, 116, 255);
        static readonly Color Border = new Color32(45, 66, 79, 255);
        Font font;
        Sprite rounded;
        Texture2D roundedTexture;
        Transform content;

        RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
            return rect;
        }
        Sprite Rounded()
        {
            if (rounded != null) return rounded;
            const int size = 64; const float radius = 14;
            roundedTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "VITAL VR surface", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                var p = new Vector2(Mathf.Max(radius - x, x - (size - 1 - radius)), Mathf.Max(radius - y, y - (size - 1 - radius)));
                float distance = new Vector2(Mathf.Max(0, p.x), Mathf.Max(0, p.y)).magnitude;
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - distance + .5f));
            }
            roundedTexture.SetPixels(pixels); roundedTexture.Apply();
            rounded = Sprite.Create(roundedTexture, new UnityEngine.Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(16, 16, 16, 16));
            return rounded;
        }
        Image Box(Transform parent, string name, float x, float y, float w, float h, Color color, bool round = true, bool input = false)
        {
            var image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = input;
            if (round) { image.sprite = Rounded(); image.type = Image.Type.Sliced; }
            return image;
        }
        Text Label(Transform parent, string name, string value, float x, float y, float w, float h, int size = 22, Color? color = null, bool bold = false)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var label = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = size; label.color = color ?? Ink; label.text = value;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false; label.supportRichText = true;
            return label;
        }
        Button Button(Transform parent, string name, string title, float x, float y, float w, float h, Action action, bool primary = false)
        {
            var image = Box(parent, name, x, y, w, h, Color.white, true, true);
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            // Start at the authored color, avoiding a white flash when navigating between pages.
            image.canvasRenderer.SetColor(primary ? Accent : Raised);
            var colors = button.colors;
            colors.normalColor = primary ? Accent : Raised;
            colors.highlightedColor = primary ? new Color32(129, 240, 214, 255) : new Color32(49, 80, 96, 255);
            colors.selectedColor = colors.highlightedColor; colors.pressedColor = primary ? new Color32(41, 170, 146, 255) : Border;
            colors.disabledColor = CardColor; colors.fadeDuration = .08f; button.colors = colors;
            button.onClick.AddListener(() => action());
            var text = Label(image.transform, "Label", title, 14, 0, w - 28, h, 22, primary ? Background : Ink, true);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        void Bind(Text target, Func<string> read)
        { Action update = () => { if (target != null) target.text = read(); }; live.Add(update); update(); }

        void Render()
        {
            redraw = false; live.Clear();
            if (pageRoot != null) { pageRoot.SetActive(false); Destroy(pageRoot); }
            pageRoot = new GameObject("Page " + Page, typeof(RectTransform)); pageRoot.layer = 5;
            var rect = pageRoot.GetComponent<RectTransform>(); rect.SetParent(surface, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(1440, 900);
            content = rect;
            ApplyVisibility();
            if (Page == ExperiencePage.Training) { DrawTraining(); return; }
            Box(content, "Backdrop", 0, 0, 1440, 900, Background, true, true);
            Header();
            switch (Page)
            {
                case ExperiencePage.Welcome: DrawWelcome(); break;
                case ExperiencePage.Environments: DrawEnvironments(); break;
                case ExperiencePage.Catalog: DrawCatalog(); break;
                case ExperiencePage.Briefing: DrawBriefing(); break;
                case ExperiencePage.Results: DrawResults(); break;
                case ExperiencePage.Help: DrawHelp(); break;
                case ExperiencePage.Settings: DrawSettings(); break;
                default: DrawPause(); break;
            }
            Box(content, "Footer line", 48, 836, 1344, 1, Border, false);
            Label(content, "Footer", "VITAL VR   /   CENTRO DE SIMULACIÓN CLÍNICA", 48, 855, 720, 24, 16, Soft);
            var mode = Label(content, "Device", IsDesktop ? "DESKTOP  ·  TECLADO Y RATÓN" : "VR  ·  MANDOS  ·  B / Y: CENTRAR MENÚ", 915, 854, 477, 26, 16, Soft);
            mode.alignment = TextAnchor.MiddleRight;
        }
        void Header()
        {
            Box(content, "Brand symbol", 48, 33, 42, 42, VitalBrand.Red);
            Box(content, "Cross horizontal", 58, 51, 22, 6, Ink, false);
            Box(content, "Cross vertical", 66, 43, 6, 22, Ink, false);
            Label(content, "Product", "VITAL VR", 105, 36, 210, 39, 29, Ink, true);
            Label(content, "Edition", "ENTRENAMIENTO CLÍNICO", 319, 46, 400, 26, 16, Soft);
            Button(content, "Help", "Ayuda", 1130, 32, 118, 48, () => OpenUtility(ExperiencePage.Help));
            Button(content, "Settings", "Ajustes", 1260, 32, 132, 48, () => OpenUtility(ExperiencePage.Settings));
            Box(content, "Header line", 48, 105, 1344, 1, Border, false);
        }
        void PageTitle(string eyebrow, string title, string subtitle)
        {
            title = CleanCopy(title);
            Label(content, "Eyebrow", eyebrow.ToUpperInvariant(), 48, 134, 1280, 27, 17, Accent, true);
            var heading = Label(content, "Page title", title, 48, 171, 1310, 66, 44, Ink, true);
            heading.resizeTextForBestFit = true; heading.resizeTextMinSize = 28; heading.resizeTextMaxSize = 44;
            Label(content, "Subtitle", subtitle, 48, 241, 1300, 57, 21, Soft);
        }
        void Badge(Transform parent, string text, float x, float y, float w, Color? tone = null)
        {
            Box(parent, "Badge", x, y, w, 32, Raised);
            var label = Label(parent, "Badge text", text, x + 9, y + 5, w - 18, 23, 16, tone ?? Accent, true);
            label.alignment = TextAnchor.UpperCenter;
        }
        Transform ScrollArea(float x, float y, float width, float height, float contentHeight)
        {
            var root = Rect(content, "Scroll region", x, y, width, height);
            var scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
            var viewport = Rect(root, "Viewport", 0, 0, width - 18, height);
            var bg = viewport.gameObject.AddComponent<Image>(); bg.color = new Color(0, 0, 0, .005f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var body = Rect(viewport, "Content", 0, 0, width - 18, Mathf.Max(height, contentHeight));
            scroll.viewport = viewport; scroll.content = body;
            var track = Box(root, "Scrollbar", width - 10, 0, 8, height, CardColor, true, true);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            var handle = Box(track.transform, "Handle", 0, 0, 8, height, Soft, true, true);
            handle.rectTransform.anchorMin = Vector2.zero; handle.rectTransform.anchorMax = Vector2.one;
            handle.rectTransform.offsetMin = handle.rectTransform.offsetMax = Vector2.zero;
            scrollbar.handleRect = handle.rectTransform; scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar; scroll.verticalNormalizedPosition = 1;
            return body;
        }
        static string TimeLabel(double seconds)
        {
            var t = TimeSpan.FromSeconds(Math.Max(0, seconds)); return $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
        }
        static string EnvironmentName(string id)
        {
            switch (id) { case "gym": return "Gimnasio"; case "football": return "Campo de fútbol"; case "mall": return "Centro comercial"; case "dental": return "Clínica dental"; default: return "Todos los entornos"; }
        }
        static string Friendly(string value)
        {
            switch (value)
            {
                case "Conscious": return "Consciente"; case "Confused": return "Confusión"; case "Drowsy": return "Somnolencia"; case "Unresponsive": return "Sin respuesta";
                case "male": return "Masculino"; case "female": return "Femenino";
                case "Accepted": return "Registrada"; case "Late": return "Fuera de tiempo"; case "Duplicate": return "Repetida"; case "OutOfOrder": return "Fuera de secuencia"; case "Unknown": return "No reconocida";
                case "normal": return "Normal"; case "fast": return "Rápida"; case "slow": return "Lenta"; case "agonal": return "Agónica"; case "absent": return "Ausente";
                case "FULL_RECOVERY": return "Recuperación completa"; case "PARTIAL_RECOVERY": return "Recuperación parcial"; case "VERBAL_RESPONSE": return "Respuesta verbal";
                case "UNCONSCIOUS_STABLE": return "Inconsciente, estable"; case "ROSC": return "Retorno de circulación"; case "REQUIRES_ADVANCED_CARE": return "Requiere atención avanzada"; case "CRITICAL_FAILURE": return "Resultado crítico";
                case "easy": case "basic": return "Básica"; case "medium": case "intermediate": return "Intermedia"; case "hard": case "advanced": return "Avanzada";
                default: return value ?? "";
            }
        }
        static string CleanCopy(string value) => (value ?? "").Replace(" · piloto migrado", "");
    }
}
