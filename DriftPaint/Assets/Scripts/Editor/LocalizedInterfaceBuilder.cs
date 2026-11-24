using System.Collections.Generic;
using System.Linq;
using DriftPaint.UI;
using DriftPaint.UI.Menu;
using DriftPaint.UI.Options;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DriftPaint.EditorTools
{
    /// <summary>Conecta los rótulos y los indicadores de conexión sin reemplazar el contenido editado en la escena.</summary>
    public static class LocalizedInterfaceBuilder
    {
        static readonly Color Navy = new Color32(24, 36, 62, 248);
        static readonly Color Edge = new Color32(110, 143, 181, 190);

        [MenuItem("DriftPaint/Conectar idioma y detalles de interfaz")]
        static void Connect()
        {
            var scene = SceneManager.GetActiveScene();
            Install(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>Las plantillas y los rótulos fijos usan la misma tabla que los mensajes dinámicos.</summary>
        public static void Install(Scene scene)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(GameLanguage.Table);
            if (collection?.GetTable("es") is not StringTable table)
                throw new System.InvalidOperationException("Falta la tabla de español de DriftPaint.");
            MenuSceneBuilder.LoadAssets();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/BarlowCondensed-BlackItalic SDF.asset");
            var panel = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/T_Panel.png");
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var options in root.GetComponentsInChildren<OptionsMenu>(true))
                    InstallLanguage(options);
                foreach (var row in root.GetComponentsInChildren<RoomRowView>(true))
                    InstallSignal(row);
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                    StyleButton(button, font);
                foreach (var image in root.GetComponentsInChildren<Image>(true))
                {
                    string name = image.name;
                    if (name == "Panel" && image.transform.parent.name == "Rooms" ||
                        name == "Players Panel" || name == "Config Panel" || name == "Chat Panel" ||
                        name == "Box" && image.transform.parent.name == "Car Select" ||
                        name == "Fondo de equipo" || name == "Room Row Template" || name == "Log")
                    {
                        image.sprite = panel;
                        image.type = Image.Type.Sliced;
                        image.color = name == "Room Row Template" ? new Color32(43, 60, 89, 255) : Navy;
                        var rim = image.GetComponent<Outline>() ?? image.gameObject.AddComponent<Outline>();
                        rim.effectColor = Edge;
                        rim.effectDistance = new Vector2(1.5f, -1.5f);
                    }
                }
                foreach (var card in root.GetComponentsInChildren<CarCardView>(true))
                {
                    var box = (RectTransform)card.transform.parent.parent;
                    box.sizeDelta = new Vector2(box.sizeDelta.x, 830f);
                    foreach (string name in new[] { "Background", "Selected Frame" })
                    {
                        var slice = card.transform.Find(name).GetComponent<Image>();
                        slice.sprite = panel;
                        slice.type = Image.Type.Sliced;
                    }
                    var mark = (RectTransform)card.transform.Find("Current Mark");
                    mark.sizeDelta = new Vector2(34, 34);
                    mark.GetComponentInChildren<TMP_Text>(true).enabled = false;
                    Icon(mark, MenuGlyph.Shape.Check, new Vector2(.5f, .5f), Vector2.zero, new Vector2(24, 24), Navy);
                }

                if (root.name == "Results")
                {
                    var title = root.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "Title");
                    if (title != null)
                    {
                        Icon(title.transform, MenuGlyph.Shape.Crown, new Vector2(.5f, 1f),
                            new Vector2(0, 6), new Vector2(86, 48), DriftPaintPalette.SunsetGold);
                        foreach (var block in root.GetComponentsInChildren<Image>(true).Where(i => i.name is "Left Percent" or "Right Percent"))
                        {
                            block.rectTransform.sizeDelta = new Vector2(545, 112);
                            block.rectTransform.anchoredPosition = new Vector2(block.name == "Left Percent" ? -284 : 284, -282);
                        }
                    }
                }
                var dynamicText = ControlledLabels(root);
                foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (dynamicText.Contains(label))
                    {
                        if (label.TryGetComponent(out TranslatedLabel previous)) Object.DestroyImmediate(previous);
                        continue;
                    }
                    if (string.IsNullOrEmpty(label.text)) continue;
                    string key = label.text.StartsWith("<b>CONTROLES") ? "Sandbox.Controls" : label.text;
                    if (table.GetEntry(key) == null) continue;
                    var binding = label.GetComponent<TranslatedLabel>() ?? label.gameObject.AddComponent<TranslatedLabel>();
                    var data = new SerializedObject(binding);
                    data.FindProperty("_key").stringValue = key;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        static HashSet<TMP_Text> ControlledLabels(GameObject root)
        {
            var controlled = new HashSet<TMP_Text>();
            foreach (var input in root.GetComponentsInChildren<TMP_InputField>(true))
                controlled.Add(input.textComponent);
            foreach (var screen in root.GetComponentsInChildren<RoomsScreen>(true))
            {
                var field = new SerializedObject(screen).FindProperty("_createTitleInput");
                if (field.objectReferenceValue is TMP_InputField title && title.placeholder is TMP_Text hint)
                    controlled.Add(hint);
            }
            foreach (var owner in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (owner == null) continue;
                switch (owner.GetType().Name)
                {
                    case "RoomRowView": case "ScoreRowView": case "PlayerSlotView": case "CarCardView":
                    case "ResultsScreen": case "ScoreTableView": case "LobbyScreen": case "CarNameplate":
                    case "MatchHUD": case "CarSelectPanel": case "NicknameField": case "MenuFlow":
                        var field = new SerializedObject(owner).GetIterator();
                        while (field.NextVisible(true))
                            if (field.propertyType == SerializedPropertyType.ObjectReference &&
                                field.objectReferenceValue is TMP_Text label) controlled.Add(label);
                        break;
                }
            }
            return controlled;
        }

        static void InstallLanguage(OptionsMenu options)
        {
            var box = options.transform.Find("Options Panel/Box");
            if (box == null) return;
            var hint = (RectTransform)box.Find("Hint");
            hint.anchoredPosition = new Vector2(-60f, -103f);
            var button = box.Find("Language Button")?.GetComponent<Button>() ??
                MenuSceneBuilder.CreateButton(box, "Language Button", "Idioma: Español", MenuSceneBuilder.ButtonStyle.Outline,
                    34f, Vector2.one, new Vector2(-60f, -28f), new Vector2(300f, 64f), Vector2.one);
            MenuSceneBuilder.Bind(options, ("_languageButton", button));
        }

        static void InstallSignal(RoomRowView row)
        {
            var ping = row.transform.Find("Ping").GetComponent<TMP_Text>();
            ping.margin = new Vector4(45f, 0f, 0f, 0f);
            var data = new SerializedObject(row);
            var bars = data.FindProperty("_pingBars");
            bars.arraySize = 4;
            for (int i = 0; i < bars.arraySize; i++)
            {
                string name = "Signal " + (i + 1);
                var anchor = new Vector2(0, .5f);
                var bar = ping.transform.Find(name)?.GetComponent<Image>() ??
                    UiFactory.CreatePanel(ping.transform, name, anchor, anchor, Vector2.zero,
                        new Vector2(i * 10, -12), new Vector2(8, 7 + i * 6), Color.gray);
                bar.raycastTarget = false;
                bars.GetArrayElementAtIndex(i).objectReferenceValue = bar;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Icon(Transform parent, MenuGlyph.Shape shape, Vector2 anchor, Vector2 offset, Vector2 size, Color color)
        {
            var rect = parent.Find("Icono") as RectTransform ??
                UiFactory.CreateRect(parent, "Icono", anchor, anchor, new Vector2(.5f, .5f), offset, size);
            var icon = rect.GetComponent<MenuGlyph>() ?? rect.gameObject.AddComponent<MenuGlyph>();
            icon.color = color;
            icon.raycastTarget = false;
            var data = new SerializedObject(icon);
            data.FindProperty("_shape").enumValueIndex = (int)shape;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static void StyleButton(Button button, TMP_FontAsset font)
        {
            var label = button.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (label == null || !button.TryGetComponent(out Image image)) return;
            if (font != null) label.font = font;
            label.fontStyle = FontStyles.Normal;
            if (image.TryGetComponent(out Outline outline))
            {
                image.color = Navy;
                outline.effectColor = button.name is "Switch Team Button" or "Change Car Button"
                    ? DriftPaintPalette.Cyan : DriftPaintPalette.OffWhite;
            }
            var left = new Vector2(0, .5f);
            if (button.name is "Back Button" or "Refresh Button" or "Start Button" or "Rematch Button" ||
                button.name == "Confirm Button" && button.transform.GetComponentInParent<CarSelectPanel>(true) != null)
            {
                var shape = button.name switch
                {
                    "Back Button" => MenuGlyph.Shape.Back,
                    "Refresh Button" => MenuGlyph.Shape.Refresh,
                    _ => MenuGlyph.Shape.Play
                };
                Icon(button.transform, shape, left, new Vector2(48, 0), new Vector2(31, 31), shape == MenuGlyph.Shape.Refresh ? DriftPaintPalette.Cyan : label.color);
                label.margin = new Vector4(48, 0, 0, 0);
            }
            if (button.name is not ("Start Button" or "Rooms Button" or "Rematch Button" or "Confirm Button")) return;
            var anchor = new Vector2(1, .5f);
            if (button.transform.Find("Accent") == null)
            {
                var accent = UiFactory.CreatePanel(button.transform, "Accent", new Vector2(1, 0), Vector2.one, anchor,
                    new Vector2(14f, 0), new Vector2(8f, 0f), DriftPaintPalette.Cyan);
                MenuSceneBuilder.Skew(accent, 14);
                accent.raycastTarget = false;
            }
            if (!image.GetComponents<Shadow>().Any(s => s.GetType() == typeof(Shadow)))
            {
                var shadow = image.gameObject.AddComponent<Shadow>();
                shadow.effectDistance = new Vector2(4, -7);
                shadow.effectColor = new Color32(10, 18, 39, 225);
            }
        }
    }
}
