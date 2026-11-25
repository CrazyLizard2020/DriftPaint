using System.Collections.Generic;
using DriftPaint.Audio;
using DriftPaint.Input;
using DriftPaint.Match;
using DriftPaint.UI;
using DriftPaint.UI.Menu;
using DriftPaint.UI.Options;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ButtonStyle = DriftPaint.EditorTools.MenuSceneBuilder.ButtonStyle;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Agrega a una escena el menú de opciones (volumen y controles) y el objeto que pide su música.
    /// Lo usan los generadores de las escenas de menú y de partida, y <see cref="SceneAutoSetup"/> lo aplica
    /// solo a las escenas existentes que no lo tienen. Crea la biblioteca de música con sus pistas si no existe.
    /// </summary>
    public static class OptionsMenuBuilder
    {
        const string MusicFolder = "Assets/Audio/Music";
        const string CanvasName = "Options UI";
        const string SceneMusicName = "Scene Music";
        const int SortingOrder = 100;

        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        static readonly Vector2 TopRight = new Vector2(1f, 1f);
        static readonly Vector2 MiddleLeft = new Vector2(0f, 0.5f);

        // Pistas iniciales: id con el que se piden, nombre del asset y archivo de audio.
        static readonly (string id, string asset, string clip)[] Tracks =
        {
            ("championship_booth", "MusicTrack_ChampionshipBooth", "Main Menu - Championship Booth.ogg"),
            ("synthpaint_1", "MusicTrack_Synthpaint1", "Combat Music - Synthpaint 1.ogg"),
            ("roadrift", "MusicTrack_Roadrift", "Combat Music - Roadrift.ogg"),
        };

        // Contexto de música y id de la pista que suena en él.
        static readonly (string context, string trackId)[] Routes =
        {
            (MusicContexts.Menu, "championship_booth"),
            (MapCatalog.GraffithasiaId, "synthpaint_1"),
            (MapCatalog.RoyalDriftRoadId, "roadrift"),
        };

        // Filas de la lista de controles: nombre visible, acción de teclado, acción de joystick y texto fijo de teclado.
        static readonly (string label, string keyboard, string gamepad, string fixedKeyboard)[] Controls =
        {
            ("Acelerar", "Driving/Accelerate", null, null),
            ("Frenar / reversa", "Driving/Brake", null, null),
            ("Girar", "Driving/Steer", null, null),
            ("Drift", "Driving/Drift", null, null),
            ("Disparar pintura", "Driving/Fire", null, null),
            ("Soltar barril", "Driving/Barrel", null, null),
            ("Mover cámara", null, "Driving/PanStick", "Mouse"),
            ("Mirar atrás", "Driving/LookBack", null, null),
            ("Reaparecer (mantener)", "Driving/Respawn", null, null),
            ("Tabla de puntajes", "UI/Scoreboard", null, null),
            ("Opciones", "UI/Menu", null, null),
        };

        /// <summary>
        /// Agrega lo que le falte a la escena: si ya tiene menú de opciones o música no los toca, así no se
        /// pierden ajustes hechos a mano. Para regenerar el menú, borrar el objeto "Options UI" y volver a aplicar.
        /// </summary>
        internal static void Install(Scene scene)
        {
            MenuSceneBuilder.LoadAssets();
            MusicLibrary library = LoadOrCreateMusicLibrary();
            MatchController match = Find<MatchController>(scene);
            MainMenuScreen mainMenu = Find<MainMenuScreen>(scene);

            SceneMusic music = Find<SceneMusic>(scene);
            if (music == null) CreateSceneMusic(library, match != null);
            else Debug.Log("La escena ya tiene música; se dejó como estaba.", music);

            OptionsMenu existing = Find<OptionsMenu>(scene);
            if (existing == null)
            {
                Button openButton = mainMenu != null ? ConnectOptionsButton(mainMenu) : null;
                BuildOptions(match, openButton);
            }
            else InstallDisplay(existing);

            if (Find<EventSystem>(scene) == null) MenuSceneBuilder.BuildEventSystem();
        }

        static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }

        #region Música

        static void CreateSceneMusic(MusicLibrary library, bool inMatch)
        {
            var go = new GameObject(SceneMusicName);
            var music = go.AddComponent<SceneMusic>();
            var so = new SerializedObject(music);
            so.FindProperty("_library").objectReferenceValue = library;
            so.FindProperty("_context").stringValue = inMatch ? MapCatalog.DefaultMapId : MusicContexts.Menu;
            so.FindProperty("_useRoomMap").boolValue = inMatch;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Carga la biblioteca de música; la primera vez la crea con las pistas y los contextos iniciales.</summary>
        internal static MusicLibrary LoadOrCreateMusicLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<MusicLibrary>(ScriptableObjectPaths.MusicLibrary);
            if (library != null) return library;

            EditorAssetUtility.EnsureFolder(ScriptableObjectPaths.MusicTracksFolder);
            var tracks = new Dictionary<string, MusicTrack>();
            foreach ((string id, string asset, string clip) in Tracks)
                tracks[id] = LoadOrCreateTrack(id, asset, clip);

            library = ScriptableObject.CreateInstance<MusicLibrary>();
            AssetDatabase.CreateAsset(library, ScriptableObjectPaths.MusicLibrary);

            var so = new SerializedObject(library);
            SerializedProperty routes = so.FindProperty("_routes");
            routes.arraySize = Routes.Length;
            for (int i = 0; i < Routes.Length; i++)
            {
                SerializedProperty route = routes.GetArrayElementAtIndex(i);
                route.FindPropertyRelative("Context").stringValue = Routes[i].context;
                route.FindPropertyRelative("Track").objectReferenceValue = tracks[Routes[i].trackId];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            Debug.Log($"Biblioteca de música creada en {ScriptableObjectPaths.MusicLibrary}.", library);
            return library;
        }

        static MusicTrack LoadOrCreateTrack(string id, string assetName, string clipFile)
        {
            string path = $"{ScriptableObjectPaths.MusicTracksFolder}/{assetName}.asset";
            var track = AssetDatabase.LoadAssetAtPath<MusicTrack>(path);
            if (track != null) return track;

            string clipPath = $"{MusicFolder}/{clipFile}";
            UseStreaming(clipPath);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            if (clip == null) Debug.LogWarning($"No se encontró la música {clipPath}; la pista {assetName} quedó sin audio.");

            track = ScriptableObject.CreateInstance<MusicTrack>();
            AssetDatabase.CreateAsset(track, path);
            var so = new SerializedObject(track);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_clip").objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(track);
            return track;
        }

        /// <summary>La música se lee del disco mientras suena: no ocupa memoria con la canción entera descomprimida.</summary>
        static void UseStreaming(string clipPath)
        {
            if (!(AssetImporter.GetAtPath(clipPath) is AudioImporter importer)) return;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            if (settings.loadType == AudioClipLoadType.Streaming) return;
            settings.loadType = AudioClipLoadType.Streaming;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        #endregion

        #region Menú de opciones

        static Button ConnectOptionsButton(MainMenuScreen mainMenu)
        {
            Transform screen = mainMenu.transform;
            Button button = screen.Find("Options Button")?.GetComponent<Button>();
            if (button != null) return button;
            return MenuSceneBuilder.CreateButton(screen, "Options Button", "OPCIONES", ButtonStyle.Outline, 34f,
                TopRight, new Vector2(-56f, -48f), new Vector2(300f, 72f), TopRight);
        }

        static void BuildOptions(MatchController match, Button openButton)
        {
            bool inMatch = match != null;
            Canvas canvas = UiFactory.CreateOverlayCanvas(null, CanvasName, SortingOrder);

            Image panel = UiFactory.CreatePanel(canvas.transform, "Options Panel", Vector2.zero, Vector2.one, Center,
                Vector2.zero, Vector2.zero, Color.black.WithAlpha(0.65f));
            panel.raycastTarget = true;

            Image box = UiFactory.CreatePanel(panel.transform, "Box", Center, Center, Center, Vector2.zero,
                new Vector2(1400f, 860f), DriftPaintPalette.HudNavy);
            box.raycastTarget = true;
            UiFactory.CreatePanel(box.transform, "Top Stripe", TopLeft, TopRight, new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 10f), DriftPaintPalette.Orange);

            TextMeshProUGUI title = MenuSceneBuilder.CreateLabel(box.transform, "Title", MenuSceneBuilder.TitleFont, 76f,
                TextAlignmentOptions.Left, DriftPaintPalette.OffWhite, FontStyles.Bold | FontStyles.Italic,
                TopLeft, TopLeft, new Vector2(60f, -28f), new Vector2(600f, 96f));
            title.text = "OPCIONES";

            TextMeshProUGUI hint = MenuSceneBuilder.CreateLabel(box.transform, "Hint", MenuSceneBuilder.BodyFont, 24f,
                TextAlignmentOptions.Right, MenuSceneBuilder.MutedText, FontStyles.Normal,
                TopRight, TopRight, new Vector2(-60f, -58f), new Vector2(820f, 40f));
            hint.text = inMatch ? "La partida sigue mientras el menú está abierto.  Esc / Start para cerrar." : "Esc / Start para cerrar.";

            Slider master = BuildVolumePanel(box.transform, out TextMeshProUGUI masterValue, out Slider music,
                out TextMeshProUGUI musicValue, out Slider effects, out TextMeshProUGUI effectsValue);
            BuildControlsPanel(box.transform);

            Button close = MenuSceneBuilder.CreateButton(box.transform, "Close Button", "CERRAR", ButtonStyle.Primary, 40f,
                new Vector2(0.5f, 0f), new Vector2(inMatch ? -230f : 0f, 40f), new Vector2(380f, 84f), new Vector2(0.5f, 0f));
            Button leave = null;
            if (inMatch)
                leave = MenuSceneBuilder.CreateButton(box.transform, "Leave Button", "SALIR DE LA PARTIDA", ButtonStyle.Outline, 36f,
                    new Vector2(0.5f, 0f), new Vector2(230f, 40f), new Vector2(440f, 84f), new Vector2(0.5f, 0f));

            var menu = canvas.gameObject.AddComponent<OptionsMenu>();
            MenuSceneBuilder.Bind(menu,
                ("_root", panel.gameObject),
                ("_masterSlider", master), ("_masterValue", masterValue),
                ("_musicSlider", music), ("_musicValue", musicValue),
                ("_effectsSlider", effects), ("_effectsValue", effectsValue),
                ("_closeButton", close), ("_leaveButton", leave), ("_match", match),
                ("_firstSelected", master));

            var so = new SerializedObject(menu);
            SerializedProperty openButtons = so.FindProperty("_openButtons");
            openButtons.arraySize = openButton != null ? 1 : 0;
            if (openButton != null) openButtons.GetArrayElementAtIndex(0).objectReferenceValue = openButton;
            so.FindProperty("_lockCursorOnClose").boolValue = inMatch;
            so.ApplyModifiedPropertiesWithoutUndo();

            InstallDisplay(menu);
            panel.gameObject.SetActive(false);
        }

        /// <summary>Agrega los ajustes de pantalla al panel existente sin reemplazar sus controles.</summary>
        [MenuItem("DriftPaint/Conectar opciones de pantalla")]
        static void ConnectDisplay()
        {
            var scene = SceneManager.GetActiveScene();
            var menu = Find<OptionsMenu>(scene);
            if (menu == null) return;
            MenuSceneBuilder.LoadAssets();
            if (InstallDisplay(menu)) EditorSceneManager.MarkSceneDirty(scene);
        }

        static bool InstallDisplay(OptionsMenu menu)
        {
            Transform box = menu.transform.Find("Options Panel/Box");
            if (box == null || box.GetComponentInChildren<DisplayOptions>(true) != null) return false;
            var volume = (RectTransform)box.Find("Volume");
            volume.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 320f);
            int index = 0;
            foreach (string row in new[] { "Master", "Music", "Effects" })
            {
                float y = -64f - 79f * index++;
                foreach (string part in new[] { " Label", " Value", " Slider" })
                {
                    var rect = (RectTransform)volume.Find(row + part);
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, part == " Slider" ? y - 42f : y);
                }
            }

            Image section = CreateSection(box, "Display", "PANTALLA", volume.anchoredPosition.x, volume.rect.width);
            RectTransform panel = section.rectTransform;
            panel.anchoredPosition = volume.anchoredPosition + Vector2.down * 336f;
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 224f);
            var caption = MenuSceneBuilder.CreateLabel(panel, "Resolution Caption", MenuSceneBuilder.LabelFont, 28f,
                TextAlignmentOptions.Left, DriftPaintPalette.OffWhite, FontStyles.Bold, TopLeft, TopLeft,
                new Vector2(28f, -75f), new Vector2(150f, 48f));
            caption.text = "Resolución";
            TMP_Dropdown resolution = ResolutionDropdown(panel);
            Button fullscreen = DisplayButton(panel, "Fullscreen", "PANTALLA COMPLETA", 32f, -150f, 290f);
            Button windowed = DisplayButton(panel, "Windowed", "VENTANA", 344f, -150f, 185f);
            var options = panel.gameObject.AddComponent<DisplayOptions>();
            MenuSceneBuilder.Bind(options, ("_resolution", resolution),
                ("_fullscreen", fullscreen), ("_windowed", windowed));
            AddRestoreDialog(menu, box, options);
            return true;
        }

        static void AddRestoreDialog(OptionsMenu menu, Transform box, DisplayOptions display)
        {
            var bottomLeft = Vector2.zero;
            Button restore = MenuSceneBuilder.CreateButton(box, "Restore Defaults", "", ButtonStyle.Outline, 26f,
                bottomLeft, new Vector2(68f, 50f), new Vector2(72f, 64f), bottomLeft);
            Image icon = UiFactory.CreatePanel(restore.transform, "Reset Icon", Center, Center, Center,
                Vector2.zero, new Vector2(42f, 42f), DriftPaintPalette.OffWhite);
            icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/T_Restore.png");
            var group = box.gameObject.AddComponent<CanvasGroup>();
            var modal = UiFactory.CreatePanel(box.parent, "Restore Confirmation", Vector2.zero, Vector2.one,
                Center, Vector2.zero, Vector2.zero, Color.black.WithAlpha(.76f));
            modal.raycastTarget = true;
            var card = UiFactory.CreatePanel(modal.transform, "Card", Center, Center, Center, Vector2.zero,
                new Vector2(820f, 260f), DriftPaintPalette.HudNavy);
            var label = MenuSceneBuilder.CreateLabel(card.transform, "Question", MenuSceneBuilder.LabelFont, 38f,
                TextAlignmentOptions.Center, DriftPaintPalette.OffWhite, FontStyles.Bold, Center, Center,
                new Vector2(0f, 48f), new Vector2(780f, 90f));
            label.text = "¿Quieres reiniciar las opciones a default?";
            Button yes = MenuSceneBuilder.CreateButton(card.transform, "Confirm", "Sí", ButtonStyle.Outline,
                34f, Center, new Vector2(-130f, -57f), new Vector2(210f, 64f));
            Button no = MenuSceneBuilder.CreateButton(card.transform, "Cancel", "No", ButtonStyle.Primary,
                34f, Center, new Vector2(130f, -57f), new Vector2(210f, 64f));
            yes.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = no, selectOnRight = no };
            no.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = yes, selectOnRight = yes };
            MenuSceneBuilder.Bind(menu, ("_display", display), ("_settingsGroup", group),
                ("_confirmation", modal.gameObject), ("_restoreButton", restore), ("_confirmButton", yes), ("_cancelButton", no));
            modal.gameObject.SetActive(false);
        }

        static TMP_Dropdown ResolutionDropdown(Transform panel)
        {
            TMP_Dropdown list = CreateDropdown(panel, "Resolution");
            var rect = (RectTransform)list.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = TopLeft;
            rect.anchoredPosition = new Vector2(169f, -75f);
            rect.sizeDelta = new Vector2(360f, 50f);
            list.captionText.text = "Resolución";
            return list;
        }

        /// <summary>Desplegable con el estilo del menú de opciones, sin opciones: las carga su componente al abrirse.</summary>
        internal static TMP_Dropdown CreateDropdown(Transform parent, string name)
        {
            var white = MenuSceneBuilder.WhiteSprite;
            var root = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources
            {
                standard = white, background = white, mask = white, knob = white,
                dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd")
            });
            root.name = name;
            root.transform.SetParent(parent, false);

            var list = root.GetComponent<TMP_Dropdown>();
            list.ClearOptions();
            list.captionText.text = "";
            list.itemText.text = "";
            foreach (var label in new[] { list.captionText, list.itemText })
            {
                label.font = MenuSceneBuilder.BodyFont;
                label.fontSize = 24f;
                label.color = DriftPaintPalette.OffWhite;
                label.textWrappingMode = TextWrappingModes.NoWrap;
            }
            root.GetComponent<Image>().color = MenuSceneBuilder.FieldColor;
            list.template.GetComponent<Image>().color = DriftPaintPalette.HudNavy;
            list.template.sizeDelta = new Vector2(0f, 320f);
            var scroll = list.template.GetComponent<ScrollRect>();
            scroll.scrollSensitivity = 36f;
            scroll.content.sizeDelta = new Vector2(0f, 50f);
            var item = list.template.GetComponentInChildren<Toggle>(true);
            ((RectTransform)item.transform).sizeDelta = new Vector2(0f, 48f);
            ((Image)item.targetGraphic).color = MenuSceneBuilder.FieldColor;
            ((Image)item.graphic).color = DriftPaintPalette.Cyan;
            return list;
        }

        static Button DisplayButton(Transform panel, string name, string label, float x, float y, float width)
        {
            return MenuSceneBuilder.CreateButton(panel, name, label, ButtonStyle.Primary, 26f, TopLeft,
                new Vector2(x, y), new Vector2(width, 48f), TopLeft);
        }

        static Image CreateSection(Transform box, string name, string header, float x, float width)
        {
            Image section = UiFactory.CreatePanel(box, name, TopLeft, TopLeft, TopLeft, new Vector2(x, -150f),
                new Vector2(width, 560f), MenuSceneBuilder.RowColor);
            TextMeshProUGUI title = MenuSceneBuilder.CreateLabel(section.transform, "Header", MenuSceneBuilder.TitleFont, 40f,
                TextAlignmentOptions.Left, DriftPaintPalette.Cyan, FontStyles.Bold | FontStyles.Italic,
                TopLeft, TopLeft, new Vector2(28f, -14f), new Vector2(width - 56f, 56f));
            title.text = header;
            return section;
        }

        static Slider BuildVolumePanel(Transform box, out TextMeshProUGUI masterValue, out Slider music,
            out TextMeshProUGUI musicValue, out Slider effects, out TextMeshProUGUI effectsValue)
        {
            Image section = CreateSection(box, "Volume", "VOLUMEN", 60f, 560f);
            Slider master = BuildVolumeRow(section.transform, "Master", "GENERAL", 0, out masterValue);
            music = BuildVolumeRow(section.transform, "Music", "MÚSICA", 1, out musicValue);
            effects = BuildVolumeRow(section.transform, "Effects", "EFECTOS", 2, out effectsValue);
            return master;
        }

        static Slider BuildVolumeRow(Transform section, string name, string label, int index, out TextMeshProUGUI value)
        {
            float y = -110f - index * 140f;
            TextMeshProUGUI text = MenuSceneBuilder.CreateLabel(section, name + " Label", MenuSceneBuilder.LabelFont, 32f,
                TextAlignmentOptions.Left, DriftPaintPalette.OffWhite, FontStyles.Bold | FontStyles.Italic,
                TopLeft, TopLeft, new Vector2(32f, y), new Vector2(300f, 44f));
            text.text = label;

            value = MenuSceneBuilder.CreateLabel(section, name + " Value", MenuSceneBuilder.BodyFont, 28f,
                TextAlignmentOptions.Right, DriftPaintPalette.OffWhite, FontStyles.Normal,
                TopRight, TopRight, new Vector2(-32f, y), new Vector2(140f, 44f));
            value.text = "100%";

            return CreateSlider(section, name + " Slider", new Vector2(32f, y - 58f), 496f);
        }

        static Slider CreateSlider(Transform parent, string name, Vector2 position, float width)
        {
            Sprite white = MenuSceneBuilder.WhiteSprite;
            RectTransform root = UiFactory.CreateRect(parent, name, TopLeft, TopLeft, TopLeft, position, new Vector2(width, 30f));

            Image background = UiFactory.CreatePanel(root, "Background", Vector2.zero, Vector2.one, Center,
                Vector2.zero, Vector2.zero, MenuSceneBuilder.FieldColor);
            background.sprite = white;
            background.raycastTarget = true;

            RectTransform fillArea = UiFactory.CreateRect(root, "Fill Area", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Image fill = UiFactory.CreatePanel(fillArea, "Fill", Vector2.zero, new Vector2(0f, 1f), Center,
                Vector2.zero, Vector2.zero, DriftPaintPalette.Cyan);
            fill.sprite = white;

            RectTransform handleArea = UiFactory.CreateRect(root, "Handle Slide Area", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            handleArea.offsetMin = new Vector2(12f, 0f);
            handleArea.offsetMax = new Vector2(-12f, 0f);
            Image handle = UiFactory.CreatePanel(handleArea, "Handle", Vector2.zero, new Vector2(0f, 1f), Center,
                Vector2.zero, new Vector2(24f, 18f), DriftPaintPalette.OffWhite);
            handle.sprite = white;
            handle.raycastTarget = true;

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            ColorBlock colors = slider.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = DriftPaintPalette.CyanBright;
            colors.selectedColor = DriftPaintPalette.CyanBright;
            colors.pressedColor = DriftPaintPalette.Cyan;
            slider.colors = colors;
            return slider;
        }

        static void BuildControlsPanel(Transform box)
        {
            const float width = 680f;
            const float rowHeight = 38f;
            Image section = CreateSection(box, "Controls", "CONTROLES", 660f, width);
            Transform s = section.transform;

            CreateColumnHeader(s, "ACCIÓN", 30f, 260f);
            CreateColumnHeader(s, "TECLADO Y MOUSE", 300f, 210f);
            CreateColumnHeader(s, "JOYSTICK", 520f, 140f);

            var view = section.gameObject.AddComponent<ControlsListView>();
            var so = new SerializedObject(view);
            SerializedProperty rows = so.FindProperty("_rows");
            rows.arraySize = Controls.Length;

            for (int i = 0; i < Controls.Length; i++)
            {
                (string label, string keyboard, string gamepad, string fixedKeyboard) = Controls[i];
                float y = -116f - i * rowHeight;
                Color background = i % 2 == 0 ? DriftPaintPalette.HudNavy.WithAlpha(0.55f) : Color.clear;
                Image row = UiFactory.CreatePanel(s, $"Row {label}", TopLeft, TopRight, new Vector2(0.5f, 1f),
                    new Vector2(0f, y), new Vector2(-32f, rowHeight - 2f), background);

                TextMeshProUGUI action = CreateCell(row.transform, "Action", MenuSceneBuilder.LabelFont, 26f, 14f, 260f, DriftPaintPalette.OffWhite);
                action.text = label;

                TextMeshProUGUI keyboardText = CreateCell(row.transform, "Keyboard", MenuSceneBuilder.BodyFont, 21f, 284f, 210f, DriftPaintPalette.OffWhite);
                keyboardText.text = fixedKeyboard ?? DriftPaintInputMaps.BindingsText(DriftPaintInputMaps.Find(keyboard), false);

                TextMeshProUGUI gamepadText = CreateCell(row.transform, "Gamepad", MenuSceneBuilder.BodyFont, 21f, 504f, 140f, DriftPaintPalette.OffWhite);
                gamepadText.text = DriftPaintInputMaps.BindingsText(DriftPaintInputMaps.Find(gamepad ?? keyboard), true);

                SerializedProperty entry = rows.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("KeyboardAction").stringValue = keyboard ?? string.Empty;
                entry.FindPropertyRelative("GamepadAction").stringValue = gamepad ?? string.Empty;
                entry.FindPropertyRelative("KeyboardText").objectReferenceValue = keyboardText;
                entry.FindPropertyRelative("GamepadText").objectReferenceValue = gamepadText;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CreateColumnHeader(Transform section, string text, float x, float width)
        {
            TextMeshProUGUI header = MenuSceneBuilder.CreateLabel(section, $"Column {text}", MenuSceneBuilder.LabelFont, 24f,
                TextAlignmentOptions.Left, MenuSceneBuilder.MutedText, FontStyles.Bold,
                TopLeft, TopLeft, new Vector2(x, -76f), new Vector2(width, 34f));
            header.text = text;
        }

        static TextMeshProUGUI CreateCell(Transform row, string name, TMP_FontAsset font, float size, float x, float width, Color color)
        {
            TextMeshProUGUI cell = MenuSceneBuilder.CreateLabel(row, name, font, size, TextAlignmentOptions.Left, color,
                FontStyles.Normal, MiddleLeft, MiddleLeft, new Vector2(x, 0f), new Vector2(width, 34f));
            cell.overflowMode = TextOverflowModes.Ellipsis;
            return cell;
        }

        #endregion
    }
}
