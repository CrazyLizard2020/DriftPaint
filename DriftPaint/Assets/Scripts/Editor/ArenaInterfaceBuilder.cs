using System.Linq;
using DriftPaint.Teams;
using DriftPaint.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DriftPaint.EditorTools
{
    /// <summary>Conecta los recursos de interfaz a las pantallas editables de cada escena.</summary>
    internal static class ArenaInterfaceBuilder
    {
        const string Plate = "Assets/Art/Generated/T_ArenaSunset.png";

        static TMP_FontAsset Font(string family)
        {
            string path = "Assets/Fonts/" + family;
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path + " SDF.asset");
            if (existing != null)
                return existing;
            var source = AssetDatabase.LoadAssetAtPath<Font>(path + ".ttf");
            var font = TMP_FontAsset.CreateFontAsset(source);
            font.name = family + " SDF";
            AssetDatabase.CreateAsset(font, path + " SDF.asset");
            AssetDatabase.AddObjectToAsset(font.material, font);
            AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
            font.TryAddCharacters(string.Concat(Enumerable.Range(32, 95).Select(c => (char)c)) +
                                  "áéíóúÁÉÍÓÚüÜñÑ¡¿ºª–…×");
            EditorUtility.SetDirty(font);
            return font;
        }

        internal static void Install(Scene scene, TeamPaletteSO palette)
        {
            var title = Font("BarlowCondensed-BlackItalic");
            var label = Font("BarlowCondensed-BoldItalic");
            var score = Font("BarlowCondensed-ExtraBoldItalic");
            var clock = Font("Oxanium-Bold");
            var controls = Font("Oxanium-SemiBold");
            Texture2D plate = AssetDatabase.LoadAssetAtPath<Texture2D>(Plate);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    bool numeric = t.name == "Clock" || t.name == "Fire Cooldown";
                    bool action = t.GetComponentInParent<Button>(true) != null;
                    bool input = t.GetComponentInParent<TMP_InputField>(true) != null;
                    t.font = numeric               ? clock
                             : t.name == "Percent" || t.name == "Points" ? score
                             : input || action     ? controls
                             : t.fontSize >= 52    ? title
                                                   : label;
                    t.fontStyle &= ~(FontStyles.Bold | FontStyles.Italic);
                    if (action && !input)
                        t.fontStyle |= FontStyles.Italic;
                }
                foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rect.name == "Background" && rect.parent?.name == "Menu UI" &&
                        rect.TryGetComponent(out RawImage bg))
                    {
                        bg.texture = plate;
                        bg.color = new Color(.77f, .77f, .84f);
                    }
                    if (rect.parent?.name == "Menu UI" &&
                        (rect.name == "Paint Cyan" || rect.name == "Paint Orange"))
                        rect.gameObject.SetActive(false);
                    if (rect.name == "Map Preview" || (rect.name == "Preview" && rect.parent?.name == "Box") ||
                        (rect.name == "Screen" && root.name == "Results"))
                        AddPlate(rect, plate, root.name == "Results" ? .72f : .9f);
                    if (rect.name == "Card Template" && rect.parent?.name == "Cards")
                        rect.sizeDelta = new Vector2(205f, rect.sizeDelta.y);
                    if (rect.name == "Logo" && rect.parent?.name == "Main Menu")
                        rect.sizeDelta *= 1140f / rect.sizeDelta.x;
                    if (rect.name == "Map Preview" && rect.Find("Ground") is Transform ground)
                        ground.gameObject.SetActive(false);
                    if (root.name == "Results" && rect.name == "Title")
                        DecorateResults(rect, root.GetComponent<UI.Score.ResultsScreen>());
                    if (root.name == "Results" && rect.parent?.name == "Table" &&
                        (rect.name == "Left Team" || rect.name == "Right Team") &&
                        rect.Find("Fondo de equipo") == null)
                    {
                        var frame = UiFactory.CreatePanel(rect, "Fondo de equipo", Vector2.zero, Vector2.one,
                                                          new Vector2(.5f, .5f), Vector2.zero, Vector2.zero,
                                                          DriftPaintPalette.HudNavy.WithAlpha(.93f));
                        frame.sprite = SpriteFor("Panel");
                        frame.type = Image.Type.Sliced;
                        frame.transform.SetAsFirstSibling();
                        frame.raycastTarget = false;
                    }
                    if (!rect.TryGetComponent<Image>(out var image))
                        continue;
                    string part = rect.name == "Header" ? rect.parent.name : rect.name;
                    if (rect.name == "Car Icon")
                    {
                        image.sprite = SpriteFor("Coupe");
                        image.preserveAspect = false;
                    }
                    if (part == "Left Team" || part == "Left Percent" || part == "Right Team" ||
                        part == "Right Percent")
                    {
                        if (!image.TryGetComponent(out TeamPanelColor tint))
                            tint = image.gameObject.AddComponent<TeamPanelColor>();
                        MenuSceneBuilder.Bind(tint, ("_palette", palette), ("_panel", image),
                                              ("_label", image.GetComponentInChildren<TMP_Text>(true)));
                        var so = new SerializedObject(tint);
                        so.FindProperty("_team").intValue = part.StartsWith("Left") ? TeamId.B : TeamId.A;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    if (rect.name == "Speed Bar")
                    {
                        image.sprite = SpriteFor("Dial");
                        image.type = Image.Type.Filled;
                        image.fillMethod = Image.FillMethod.Radial360;
                        image.fillOrigin = (int)Image.Origin360.Bottom;
                        rect.anchorMin = Vector2.zero;
                        rect.anchorMax = Vector2.one;
                        rect.offsetMin = rect.offsetMax = Vector2.zero;
                    }
                    if (rect.name == "Speedometer")
                    {
                        rect.sizeDelta = new Vector2(238, 238);
                        image.sprite = SpriteFor("Gauge");
                        image.color = Color.white;
                    }
                    if (rect.name == "Barrel Icon" || rect.name == "Fire Icon")
                    {
                        image.sprite = SpriteFor(rect.name == "Barrel Icon" ? "Barrel" : "Shot");
                        image.color = Color.white;
                        if (rect.Find("Band") is Transform band)
                            band.gameObject.SetActive(false);
                        if (rect.Find("Ready Fill") is RectTransform fill)
                        {
                            fill.anchorMin = Vector2.zero;
                            fill.anchorMax = new Vector2(1, 0);
                            fill.pivot = new Vector2(.5f, 0);
                            fill.sizeDelta = new Vector2(0, 5);
                            fill.anchoredPosition = Vector2.zero;
                        }
                    }
                    if ((rect.name.EndsWith("Panel") || rect.name == "Box" || rect.name == "Abilities" ||
                         rect.name == "Log") &&
                        image.type == Image.Type.Simple)
                    {
                        image.sprite = SpriteFor("Panel");
                        image.type = Image.Type.Sliced;
                    }
                }
            }
        }

        static void AddPlate(RectTransform parent, Texture texture, float brightness)
        {
            Transform found = parent.Find("Atardecer");
            var rect = found != null ? (RectTransform)found
                                     : UiFactory.CreateRect(parent, "Atardecer", Vector2.zero, Vector2.one,
                                                            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            if (!rect.TryGetComponent(out RawImage image))
                image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.color = new Color(brightness, brightness, brightness);
            image.raycastTarget = false;
            rect.SetAsFirstSibling();
        }

        static void DecorateResults(RectTransform title, UI.Score.ResultsScreen results)
        {
            if (results == null)
                return;
            Transform parent = title.parent;
            Image brush = parent.Find("Trazo ganador")?.GetComponent<Image>();
            if (brush == null)
            {
                brush =
                    UiFactory.CreatePanel(parent, "Trazo ganador", title.anchorMin, title.anchorMax, title.pivot,
                                          title.anchoredPosition, new Vector2(1060, 209), DriftPaintPalette.Cyan);
                brush.transform.SetSiblingIndex(title.GetSiblingIndex());
                brush.raycastTarget = false;
            }
            brush.sprite = SpriteFor("Brochazo");
            if (!title.TryGetComponent(out Shadow shadow))
                shadow = title.gameObject.AddComponent<Shadow>();
            shadow.effectColor = DriftPaintPalette.HudNavy;
            shadow.effectDistance = new Vector2(4, -5);
            MenuSceneBuilder.Bind(results, ("_winnerBrush", brush));
        }

        static Sprite SpriteFor(string name)
        {
            string path = "Assets/Art/Generated/T_" + name + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                return sprite;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    texture.SetPixel(x, y, Pixel(name, (x + .5f) / 64 - 1, (y + .5f) / 64 - 1));
            texture.Apply();
            EditorAssetUtility.SaveTexture(texture, path, true, TextureWrapMode.Clamp);
            Object.DestroyImmediate(texture);
            if (name == "Panel")
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.spriteBorder = new Vector4(20, 20, 20, 20);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Color Pixel(string name, float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            if (name == "Coupe")
            {
                float wheel = Mathf.Min(Vector2.Distance(new Vector2(x, y), new Vector2(-.55f, -.33f)),
                                        Vector2.Distance(new Vector2(x, y), new Vector2(.55f, -.33f)));
                if (wheel < .19f)
                    return Color.white;
                if (wheel < .245f)
                    return Color.clear;
                bool body = Mathf.Abs(x) < .94f && y > -.31f && y < .13f - .13f * Mathf.Abs(x);
                bool roof = y >= 0 && y < .48f && x > -.77f + .85f * y && x < .60f - .85f * y;
                bool window = y > .11f && y < .38f && x > -.67f + .85f * y && x < .48f - .85f * y;
                return (body || roof) && !window ? Color.white : Color.clear;
            }
            if (name == "Brochazo")
            {
                float slant = y - x * .16f;
                float tip =
                    .83f - .22f * Mathf.Abs(slant) + .12f * Mathf.Sin(23 * slant) + .055f * Mathf.Sin(57 * slant);
                float band = .71f - .13f * x * x + .035f * Mathf.Sin(14 * x);
                float edge = Mathf.Min(tip - Mathf.Abs(x), band - Mathf.Abs(slant));
                return new Color(1, 1, 1, Mathf.Clamp01(edge * 64));
            }
            if (name == "Panel")
            {
                float dx = Mathf.Max(0, Mathf.Abs(x) - .72f), dy = Mathf.Max(0, Mathf.Abs(y) - .72f);
                float edge = Mathf.Sqrt(dx * dx + dy * dy);
                return new Color(1, 1, 1, Mathf.Clamp01((.275f - edge) * 65));
            }
            if (name == "Gauge" || name == "Dial")
            {
                if (r > .98f || y < -.62f)
                    return Color.clear;
                if (name == "Gauge")
                    return new Color(.075f, .086f, .15f, .86f);
                if (r < .80f || (Mathf.Atan2(y, x) * 18 / Mathf.PI + 18) % 1 < .045f)
                    return Color.clear;
                return Color.white;
            }
            if (name == "Shot")
            {
                float u = (x + y) * .707f, v = (y - x) * .707f;
                float cap = Mathf.Max(0, Mathf.Abs(u) - .38f);
                if (cap * cap + v * v > .095f)
                    return Color.clear;
                return u < -.12f || v > .17f ? Color.white : DriftPaintPalette.Cyan;
            }
            if (Mathf.Abs(x) > .60f || Mathf.Abs(y) > .69f + .15f * (1 - x * x / .36f))
                return Color.clear;
            if (Mathf.Abs(y) > .56f)
                return DriftPaintPalette.SunsetGold;
            if (Mathf.Abs(y) > .48f)
                return DriftPaintPalette.Orange;
            float a = Mathf.Atan2(y, x);
            if (r < .24f + .11f * Mathf.Cos(a * 7))
                return Color.white;
            return new Color(.69f + .15f * (1 - Mathf.Abs(x)), .16f, .12f);
        }
    }
}
