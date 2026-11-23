using System.Collections.Generic;
using System.Linq;
using DriftPaint.Cars;
using DriftPaint.Teams;
using DriftPaint.UI;
using DriftPaint.UI.Menu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Genera la escena Menu con el menú principal, la lista de salas y el lobby.
    /// Todo queda como objetos de la escena: después se ajusta a mano en el editor.
    /// </summary>
    public static class MenuSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Menu.unity";
        const string ArtFolder = "Assets/Art/Generated";
        const string LogoPath = "Assets/Sprites/driftpaint_logo.png";
        const string PalettePath = ScriptableObjectPaths.TeamPalette;

        internal static readonly Color PanelColor = DriftPaintPalette.HudNavy.WithAlpha(0.9f);
        internal static readonly Color RowColor = DriftPaintPalette.Hex(0x2A2F4A);
        internal static readonly Color MutedText = DriftPaintPalette.OffWhite.WithAlpha(0.6f);
        static readonly Color InputColor = DriftPaintPalette.Hex(0x3A3F5C);

        static Sprite _white;
        static TMP_FontAsset _titleFont;
        static TMP_FontAsset _labelFont;
        static TMP_FontAsset _bodyFont;
        static Texture2D _logo;

        internal static Sprite WhiteSprite => _white;
        internal static TMP_FontAsset TitleFont => _titleFont;
        internal static TMP_FontAsset LabelFont => _labelFont;
        internal static TMP_FontAsset BodyFont => _bodyFont;
        internal static Color FieldColor => InputColor;

        [MenuItem("DriftPaint/Generar escena de menú")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        static void Build()
        {
            LoadAssets();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildCamera();
            BuildEventSystem();

            Canvas canvas = UiFactory.CreateOverlayCanvas(null, "Menu UI");
            Transform root = canvas.transform;
            BuildBackground(root);

            // Las pantallas van en un área fija de 16:9; el fondo y los avisos cubren toda la pantalla.
            RectTransform content = UiFactory.CreateContentRoot(root);
            GameObject mainMenu = BuildMainMenu(content);
            GameObject rooms = BuildRooms(content);
            GameObject lobby = BuildLobby(content);
            GameObject busy = BuildBusyOverlay(root, out TextMeshProUGUI busyText);
            GameObject error = BuildErrorOverlay(root, out TextMeshProUGUI errorText, out Button errorClose);

            var flow = canvas.gameObject.AddComponent<MenuFlow>();
            Bind(flow,
                ("_mainMenu", mainMenu), ("_rooms", rooms), ("_lobby", lobby),
                ("_busyRoot", busy), ("_busyText", busyText),
                ("_errorRoot", error), ("_errorText", errorText), ("_errorCloseButton", errorClose));

            rooms.SetActive(false);
            lobby.SetActive(false);
            busy.SetActive(false);
            error.SetActive(false);
            OptionsMenuBuilder.Install(scene);

            EditorAssetUtility.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            PutSceneFirstInBuild(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Escena de menú generada en {ScenePath}. Está primera en la lista de escenas del build.");
        }

        #region Recursos

        /// <summary>Carga el sprite blanco y las fuentes que usan las piezas de UI compartidas, como los botones.</summary>
        internal static void LoadAssets()
        {
            string whitePath = ArtFolder + "/T_White.png";
            if (AssetDatabase.LoadAssetAtPath<Sprite>(whitePath) == null)
            {
                var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                texture.SetPixels(Enumerable.Repeat(Color.white, 64).ToArray());
                texture.Apply();
                EditorAssetUtility.SaveTexture(texture, whitePath, true, TextureWrapMode.Clamp);
            }
            _white = AssetDatabase.LoadAssetAtPath<Sprite>(whitePath);

            _titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/BarlowCondensed-SemiBold SDF.asset");
            _labelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/BarlowCondensed-Medium SDF.asset");
            _bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Oxanium-Medium SDF.asset");
            _logo = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);
        }

        static Texture2D CreateBackgroundTexture()
        {
            // Cielo violeta, franja cálida de atardecer y piso oscuro.
            var stops = new (float t, Color color)[]
            {
                (0.00f, DriftPaintPalette.Hex(0x1B1A2E)),
                (0.38f, DriftPaintPalette.Hex(0x2E2B45)),
                (0.46f, DriftPaintPalette.Hex(0xD9875A)),
                (0.55f, DriftPaintPalette.Hex(0x9A6FC6)),
                (0.80f, DriftPaintPalette.Hex(0x5A4BA6)),
                (1.00f, DriftPaintPalette.Hex(0x34327A))
            };

            const int height = 256;
            var texture = new Texture2D(4, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                float t = y / (height - 1f);
                int i = 0;
                while (i < stops.Length - 2 && t > stops[i + 1].t) i++;
                float local = Mathf.InverseLerp(stops[i].t, stops[i + 1].t, t);
                Color color = Color.Lerp(stops[i].color, stops[i + 1].color, local);
                for (int x = 0; x < 4; x++) texture.SetPixel(x, y, color);
            }
            texture.Apply();
            return texture;
        }

        #endregion

        #region Escena

        static void BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = DriftPaintPalette.HudNavy;
            go.AddComponent<AudioListener>();
        }

        internal static void BuildEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.transform.SetAsLastSibling();
        }

        static void BuildBackground(Transform root)
        {
            Texture2D gradient = EditorAssetUtility.SaveTexture(CreateBackgroundTexture(), ArtFolder + "/T_MenuBackground.png",
                false, TextureWrapMode.Clamp);

            var rect = UiFactory.CreateRect(root, "Background", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = gradient;
            image.raycastTarget = false;

            // Manchas de pintura de los dos equipos en las esquinas inferiores.
            Image cyan = UiFactory.CreatePanel(root, "Paint Cyan", Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(-80f, -60f), new Vector2(520f, 150f), DriftPaintPalette.Cyan.WithAlpha(0.55f));
            Skew(cyan, -20f);
            cyan.transform.localEulerAngles = new Vector3(0f, 0f, 8f);
            Image orange = UiFactory.CreatePanel(root, "Paint Orange", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(80f, -60f), new Vector2(520f, 150f), DriftPaintPalette.Orange.WithAlpha(0.55f));
            Skew(orange, 20f);
            orange.transform.localEulerAngles = new Vector3(0f, 0f, -8f);
        }

        static RawImage CreateLogo(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, float width)
        {
            float aspect = _logo != null && _logo.width > 0 ? _logo.height / (float)_logo.width : 0.35f;
            RectTransform rect = UiFactory.CreateRect(parent, name, anchor, anchor, pivot, position, new Vector2(width, width * aspect));
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = _logo;
            image.raycastTarget = false;
            return image;
        }

        static void CreateScreenHeader(Transform parent, string title, out TextMeshProUGUI subtitle)
        {
            CreateLogo(parent, "Logo", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -10f), 280f);

            Image accent = UiFactory.CreatePanel(parent, "Accent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(400f, -50f), new Vector2(22f, 96f), DriftPaintPalette.Cyan);
            Skew(accent, 18f);

            TextMeshProUGUI titleText = CreateLabel(parent, "Title", _titleFont, 110f, TextAlignmentOptions.Left,
                DriftPaintPalette.OffWhite, FontStyles.Bold | FontStyles.Italic,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(450f, -30f), new Vector2(700f, 110f));
            titleText.text = title;

            subtitle = CreateLabel(parent, "Subtitle", _labelFont, 34f, TextAlignmentOptions.Left,
                DriftPaintPalette.OffWhite, FontStyles.Bold | FontStyles.Italic,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(454f, -138f), new Vector2(800f, 44f));
        }

        #endregion

        #region Menú principal

        static GameObject BuildMainMenu(Transform root)
        {
            RectTransform screen = CreateScreen(root, "Main Menu");

            CreateLogo(screen, "Logo", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 270f), 740f);

            NicknameField nickname = CreateNicknameField(screen, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), 700f);
            nickname.name = "Nickname";

            Button rooms = CreateButton(screen, "Rooms Button", "SALAS", ButtonStyle.Primary, 76f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), new Vector2(540f, 116f));
            Button quit = CreateButton(screen, "Quit Button", "SALIR", ButtonStyle.Outline, 42f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -340f), new Vector2(440f, 78f));

            var view = screen.gameObject.AddComponent<MainMenuScreen>();
            Bind(view, ("_roomsButton", rooms), ("_quitButton", quit));
            ConnectRejoinButton(view);
            return screen.gameObject;
        }

        static void ConnectRejoinButton(MainMenuScreen view)
        {
            Transform screen = view.transform;
            Button button = screen.Find("Rejoin Button")?.GetComponent<Button>();
            if (button == null)
                button = CreateButton(screen, "Rejoin Button", "VOLVER A LA PARTIDA", ButtonStyle.Outline, 42f,
                    new Vector2(0.5f, 0.5f), new Vector2(0f, -326f), new Vector2(540f, 78f));
            Bind(view, ("_rejoinButton", button));
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Cars" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path)
                .Concat(new[] { "Assets/Prefabs/Combat/Barrel.prefab" })
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(prefab => prefab != null).ToArray();
            var serializedView = new SerializedObject(view);
            var pool = serializedView.FindProperty("_rejoinPrefabs");
            pool.arraySize = prefabs.Length;
            for (int i = 0; i < prefabs.Length; i++) pool.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            serializedView.ApplyModifiedPropertiesWithoutUndo();
            button.gameObject.SetActive(false);
            if (screen.Find("Quit Button") is RectTransform quit)
                quit.anchoredPosition = new Vector2(0f, -432f);
        }

        [MenuItem("DriftPaint/Conectar botón de volver a la partida")]
        static void ConnectRejoinInOpenScene()
        {
            LoadAssets();
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MainMenuScreen menu in root.GetComponentsInChildren<MainMenuScreen>(true))
                {
                    ConnectRejoinButton(menu);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
                foreach (DriftPaint.Match.MatchHUD hud in root.GetComponentsInChildren<DriftPaint.Match.MatchHUD>(true))
                {
                    MatchSceneBuilder.ConnectLeaveButton(hud);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }
        }

        #endregion

        #region Salas

        static GameObject BuildRooms(Transform root)
        {
            RectTransform screen = CreateScreen(root, "Rooms");
            CreateScreenHeader(screen, "SALAS", out TextMeshProUGUI subtitle);
            subtitle.text = "BUSCAR Y UNIRTE A UNA PARTIDA";

            Image panel = UiFactory.CreatePanel(screen, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -40f), new Vector2(1640f, 690f), PanelColor);
            Transform p = panel.transform;

            NicknameField nickname = CreateNicknameField(p, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -30f), 620f);
            nickname.name = "Nickname";

            TMP_InputField search = CreateInput(p, "Search", "Buscar sala o ID...", 30f,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(720f, -34f), new Vector2(470f, 66f), DriftPaintPalette.OffWhite.WithAlpha(0.92f),
                DriftPaintPalette.HudNavy);
            search.characterLimit = 32;

            Button create = CreateButton(p, "Create Button", "+  CREAR SALA", ButtonStyle.Primary, 40f,
                new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(380f, 76f), new Vector2(1f, 1f));

            // Encabezados de columna
            CreateColumnHeader(p, "Header Room", "SALA", 110f, 400f, TextAlignmentOptions.Left);
            CreateColumnHeader(p, "Header Mode", "MODO", 640f, 200f, TextAlignmentOptions.Left);
            CreateColumnHeader(p, "Header Players", "JUGADORES", 860f, 200f, TextAlignmentOptions.Center);
            CreateColumnHeader(p, "Header Ping", "PING", 1110f, 160f, TextAlignmentOptions.Left);

            RectTransform list = UiFactory.CreateRect(p, "Room List", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -180f), new Vector2(-60f, 5 * 72f + 4 * 10f));
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            RoomRowView template = BuildRoomRow(list);

            TextMeshProUGUI empty = CreateLabel(p, "Empty Text", _labelFont, 34f, TextAlignmentOptions.Center, MutedText,
                FontStyles.Italic, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1200f, 60f));
            empty.text = "No hay salas abiertas. Creá una para empezar.";

            Button previous = CreateButton(p, "Previous Page", "<", ButtonStyle.Dark, 40f,
                new Vector2(0.5f, 0f), new Vector2(-130f, 26f), new Vector2(96f, 54f), new Vector2(0.5f, 0f));
            Button next = CreateButton(p, "Next Page", ">", ButtonStyle.Dark, 40f,
                new Vector2(0.5f, 0f), new Vector2(130f, 26f), new Vector2(96f, 54f), new Vector2(0.5f, 0f));
            TextMeshProUGUI page = CreateLabel(p, "Page Text", _labelFont, 34f, TextAlignmentOptions.Center, DriftPaintPalette.OffWhite,
                FontStyles.Bold, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(160f, 54f));
            page.text = "1 / 1";

            Button back = CreateButton(screen, "Back Button", "VOLVER", ButtonStyle.Outline, 44f,
                new Vector2(0f, 0f), new Vector2(80f, 50f), new Vector2(380f, 90f), new Vector2(0f, 0f));
            Button refresh = CreateButton(screen, "Refresh Button", "ACTUALIZAR", ButtonStyle.Outline, 44f,
                new Vector2(1f, 0f), new Vector2(-80f, 50f), new Vector2(380f, 90f), new Vector2(1f, 0f));

            GameObject createPanel = BuildCreatePanel(screen, out TMP_InputField titleInput, out TextMeshProUGUI durationText,
                out Button minus, out Button plus, out Button confirm, out Button cancel);

            var view = screen.gameObject.AddComponent<RoomsScreen>();
            Bind(view,
                ("_rowTemplate", template), ("_rowContainer", list), ("_emptyText", empty), ("_searchInput", search),
                ("_previousPageButton", previous), ("_nextPageButton", next), ("_pageText", page),
                ("_backButton", back), ("_refreshButton", refresh), ("_openCreateButton", create),
                ("_createPanel", createPanel), ("_createTitleInput", titleInput), ("_createDurationText", durationText),
                ("_createDurationMinus", minus), ("_createDurationPlus", plus),
                ("_createConfirmButton", confirm), ("_createCancelButton", cancel));

            createPanel.SetActive(false);
            return screen.gameObject;
        }

        static void CreateColumnHeader(Transform parent, string name, string text, float x, float width, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI label = CreateLabel(parent, name, _labelFont, 28f, alignment, MutedText, FontStyles.Bold,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -130f), new Vector2(width, 40f));
            label.text = text;
        }

        static RoomRowView BuildRoomRow(Transform list)
        {
            Image row = UiFactory.CreatePanel(list, "Room Row Template", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 72f), RowColor);
            Skew(row, 4f);
            Transform r = row.transform;

            TextMeshProUGUI title = CreateLabel(r, "Title", _titleFont, 40f, TextAlignmentOptions.Left, DriftPaintPalette.OffWhite,
                FontStyles.Bold | FontStyles.Italic, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(80f, 0f), new Vector2(520f, 60f));
            title.text = "Sala";
            title.overflowMode = TextOverflowModes.Ellipsis;

            TextMeshProUGUI mode = CreateLabel(r, "Mode", _labelFont, 30f, TextAlignmentOptions.Left, DriftPaintPalette.OffWhite,
                FontStyles.Bold, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(610f, 0f), new Vector2(200f, 60f));
            mode.text = "PINTURA";

            TextMeshProUGUI players = CreateLabel(r, "Players", _titleFont, 38f, TextAlignmentOptions.Center, DriftPaintPalette.OffWhite,
                FontStyles.Bold, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(830f, 0f), new Vector2(200f, 60f));
            players.text = "0 / 8";

            TextMeshProUGUI ping = CreateLabel(r, "Ping", _bodyFont, 28f, TextAlignmentOptions.Left, DriftPaintPalette.OffWhite,
                FontStyles.Normal, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(1080f, 0f), new Vector2(160f, 60f));
            ping.text = "0 ms";

            Button join = CreateButton(r, "Join Button", "UNIRSE", ButtonStyle.Primary, 32f,
                new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(210f, 54f), new Vector2(1f, 0.5f));

            var view = row.gameObject.AddComponent<RoomRowView>();
            Bind(view, ("_titleText", title), ("_modeText", mode), ("_playersText", players), ("_pingText", ping),
                ("_joinButton", join), ("_joinLabel", join.GetComponentInChildren<TextMeshProUGUI>()));
            row.gameObject.SetActive(false);
            return view;
        }

        static GameObject BuildCreatePanel(Transform screen, out TMP_InputField titleInput, out TextMeshProUGUI durationText,
            out Button minus, out Button plus, out Button confirm, out Button cancel)
        {
            Image blocker = UiFactory.CreatePanel(screen, "Create Room Panel", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, Color.black.WithAlpha(0.6f));
            blocker.raycastTarget = true;

            Image box = UiFactory.CreatePanel(blocker.transform, "Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(820f, 600f), DriftPaintPalette.HudNavy);
            Transform b = box.transform;
            UiFactory.CreatePanel(b, "Top Stripe", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 10f), DriftPaintPalette.Cyan);

            TextMeshProUGUI heading = CreateLabel(b, "Title", _titleFont, 64f, TextAlignmentOptions.Center, DriftPaintPalette.OffWhite,
                FontStyles.Bold | FontStyles.Italic, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(700f, 80f));
            heading.text = "CREAR SALA";

            CreateFormLabel(b, "Name Label", "NOMBRE", -140f);
            titleInput = CreateInput(b, "Name Input", "Sala de Piloto", 32f, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -190f), new Vector2(700f, 68f), InputColor, DriftPaintPalette.OffWhite);
            titleInput.characterLimit = 32;

            CreateFormLabel(b, "Duration Label", "DURACIÓN", -285f);
            minus = CreateButton(b, "Duration Minus", "-", ButtonStyle.Dark, 44f, new Vector2(0.5f, 1f), new Vector2(-150f, -330f),
                new Vector2(80f, 64f), new Vector2(0.5f, 1f));
            plus = CreateButton(b, "Duration Plus", "+", ButtonStyle.Dark, 44f, new Vector2(0.5f, 1f), new Vector2(150f, -330f),
                new Vector2(80f, 64f), new Vector2(0.5f, 1f));
            durationText = CreateLabel(b, "Duration Value", _titleFont, 48f, TextAlignmentOptions.Center, DriftPaintPalette.OffWhite,
                FontStyles.Bold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -328f), new Vector2(200f, 64f));
            durationText.text = "02:00";

            TextMeshProUGUI info = CreateLabel(b, "Info", _labelFont, 30f, TextAlignmentOptions.Center, MutedText, FontStyles.Normal,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -420f), new Vector2(700f, 44f));
            info.text = "Mapa: Drift Arena   /   Modo: Pintura   /   Máx. 8 jugadores";

            cancel = CreateButton(b, "Cancel Button", "CANCELAR", ButtonStyle.Outline, 38f, new Vector2(0.5f, 0f),
                new Vector2(-180f, 40f), new Vector2(300f, 80f), new Vector2(0.5f, 0f));
            confirm = CreateButton(b, "Confirm Button", "CREAR", ButtonStyle.Primary, 44f, new Vector2(0.5f, 0f),
                new Vector2(180f, 40f), new Vector2(300f, 80f), new Vector2(0.5f, 0f));

            return blocker.gameObject;
        }

        static void CreateFormLabel(Transform parent, string name, string text, float y)
        {
            TextMeshProUGUI label = CreateLabel(parent, name, _labelFont, 30f, TextAlignmentOptions.Left, MutedText, FontStyles.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(700f, 40f));
            label.text = text;
        }

        #endregion

        #region Lobby

        static GameObject BuildLobby(Transform root)
        {
            RectTransform screen = CreateScreen(root, "Lobby");
            CreateScreenHeader(screen, "SALA", out TextMeshProUGUI roomTitle);
            roomTitle.text = "Sala";
            roomTitle.fontSize = 40f;

            // ID de sala
            Image idBox = UiFactory.CreatePanel(screen, "Room Id", Vector2.one, Vector2.one, Vector2.one,
                new Vector2(-70f, -50f), new Vector2(420f, 76f), PanelColor);
            TextMeshProUGUI idText = CreateLabel(idBox.transform, "Id Text", _bodyFont, 34f, TextAlignmentOptions.Left,
                DriftPaintPalette.OffWhite, FontStyles.Bold, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(240f, 60f));
            idText.text = "ID: -----";
            Button copy = CreateButton(idBox.transform, "Copy Button", "COPIAR", ButtonStyle.Primary, 26f,
                new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(130f, 52f), new Vector2(1f, 0.5f));

            // Jugadores
            Image playersPanel = UiFactory.CreatePanel(screen, "Players Panel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(110f, -230f), new Vector2(820f, 552f), PanelColor);
            TextMeshProUGUI playersHeader = CreateLabel(playersPanel.transform, "Header", _titleFont, 48f, TextAlignmentOptions.Left,
                DriftPaintPalette.OffWhite, FontStyles.Bold | FontStyles.Italic, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(30f, -14f), new Vector2(760f, 64f));
            playersHeader.text = "JUGADORES  (0 / 8)";

            var slots = new List<PlayerSlotView>();
            for (int i = 0; i < 8; i++)
                slots.Add(BuildPlayerSlot(playersPanel.transform, i));

            // Configuración
            Image configPanel = UiFactory.CreatePanel(screen, "Config Panel", Vector2.one, Vector2.one, Vector2.one,
                new Vector2(-110f, -230f), new Vector2(860f, 296f), PanelColor);
            Transform c = configPanel.transform;
            TextMeshProUGUI configHeader = CreateLabel(c, "Header", _titleFont, 38f, TextAlignmentOptions.Left, DriftPaintPalette.OffWhite,
                FontStyles.Bold | FontStyles.Italic, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -14f), new Vector2(800f, 54f));
            configHeader.text = "CONFIGURACIÓN DE LA SALA";

            Image preview = UiFactory.CreatePanel(c, "Map Preview", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -80f), new Vector2(330f, 206f), DriftPaintPalette.SkyLavender);
            UiFactory.CreatePanel(preview.transform, "Ground", new Vector2(0f, 0f), new Vector2(1f, 0.45f), new Vector2(0.5f, 0f),
                Vector2.zero, Vector2.zero, DriftPaintPalette.Asphalt);
            Image mapBar = UiFactory.CreatePanel(preview.transform, "Name Bar", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(0f, 52f), DriftPaintPalette.HudNavy.WithAlpha(0.85f));
            TextMeshProUGUI mapText = CreateLabel(mapBar.transform, "Map Name", _titleFont, 34f, TextAlignmentOptions.Left,
                DriftPaintPalette.OffWhite, FontStyles.Bold | FontStyles.Italic, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(18f, 0f), new Vector2(300f, 50f));
            mapText.text = "Drift Arena";

            TextMeshProUGUI modeText = CreateConfigRow(c, "Mode Row", "Modo", 0, "Pintura");
            TextMeshProUGUI maxText = CreateConfigRow(c, "Max Players Row", "Máx. jugadores", 1, "8");
            TextMeshProUGUI durationText = CreateConfigRow(c, "Duration Row", "Duración", 2, "02:00");
            Transform durationRow = durationText.transform.parent;
            Button minus = CreateButton(durationRow, "Duration Minus", "-", ButtonStyle.Dark, 34f, new Vector2(1f, 0.5f),
                new Vector2(-152f, 0f), new Vector2(50f, 44f), new Vector2(1f, 0.5f));
            Button plus = CreateButton(durationRow, "Duration Plus", "+", ButtonStyle.Dark, 34f, new Vector2(1f, 0.5f),
                new Vector2(-8f, 0f), new Vector2(50f, 44f), new Vector2(1f, 0.5f));
            RectTransform durationRect = (RectTransform)durationText.transform;
            durationRect.anchoredPosition = new Vector2(-60f, 0f);
            durationRect.sizeDelta = new Vector2(86f, 50f);
            durationText.alignment = TextAlignmentOptions.Center;

            // Chat
            Image chatPanel = UiFactory.CreatePanel(screen, "Chat Panel", Vector2.one, Vector2.one, Vector2.one,
                new Vector2(-110f, -540f), new Vector2(860f, 242f), PanelColor);
            Transform ch = chatPanel.transform;
            TextMeshProUGUI chatHeader = CreateLabel(ch, "Header", _titleFont, 38f, TextAlignmentOptions.Left, DriftPaintPalette.OffWhite,
                FontStyles.Bold | FontStyles.Italic, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -12f), new Vector2(800f, 54f));
            chatHeader.text = "CHAT DE SALA";

            Image logBox = UiFactory.CreatePanel(ch, "Log", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -68f), new Vector2(812f, 92f), DriftPaintPalette.Hex(0x171A2B));
            logBox.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI log = UiFactory.CreateText(logBox.transform, "Log Text", _bodyFont, 24f, TextAlignmentOptions.BottomLeft,
                DriftPaintPalette.OffWhite);
            UiFactory.Stretch((RectTransform)log.transform, 14f);
            log.textWrappingMode = TextWrappingModes.Normal;
            log.overflowMode = TextOverflowModes.Overflow;
            log.text = "";

            TMP_InputField chatInput = CreateInput(ch, "Chat Input", "Escribí un mensaje...", 26f,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 18f), new Vector2(660f, 58f), InputColor,
                DriftPaintPalette.OffWhite);
            chatInput.characterLimit = 160;
            Button send = CreateButton(ch, "Send Button", "ENVIAR", ButtonStyle.Primary, 28f,
                new Vector2(1f, 0f), new Vector2(-24f, 18f), new Vector2(136f, 58f), new Vector2(1f, 0f));

            // Equipo y auto
            Button switchTeam = CreateButton(screen, "Switch Team Button", "CAMBIAR EQUIPO", ButtonStyle.Outline, 38f,
                new Vector2(0.5f, 0f), new Vector2(-270f, 196f), new Vector2(460f, 72f), new Vector2(0.5f, 0f));
            AddAccent(switchTeam);
            Button changeCar = CreateButton(screen, "Change Car Button", "CAMBIAR AUTO", ButtonStyle.Outline, 38f,
                new Vector2(0.5f, 0f), new Vector2(270f, 196f), new Vector2(460f, 72f), new Vector2(0.5f, 0f));
            AddAccent(changeCar);

            // Acciones
            Button back = CreateButton(screen, "Back Button", "VOLVER", ButtonStyle.Outline, 44f,
                new Vector2(0f, 0f), new Vector2(150f, 50f), new Vector2(380f, 100f), new Vector2(0f, 0f));
            Button start = CreateButton(screen, "Start Button", "INICIAR PARTIDA", ButtonStyle.Primary, 64f,
                new Vector2(0.5f, 0f), new Vector2(150f, 44f), new Vector2(820f, 116f), new Vector2(0.5f, 0f));

            // La ventana de autos va al final para quedar por encima del resto del lobby.
            CarSelectPanel carPanel = BuildCarSelectPanel(screen);

            var view = screen.gameObject.AddComponent<LobbyScreen>();
            var so = new SerializedObject(view);
            SetRef(so, "_switchTeamButton", switchTeam);
            SetRef(so, "_changeCarButton", changeCar);
            SetRef(so, "_carPanel", carPanel);
            SetRef(so, "_roomTitleText", roomTitle);
            SetRef(so, "_roomIdText", idText);
            SetRef(so, "_copyIdButton", copy);
            SetRef(so, "_playersHeaderText", playersHeader);
            SetRef(so, "_teamPalette", LoadOrCreatePalette());
            SetRef(so, "_mapText", mapText);
            SetRef(so, "_modeText", modeText);
            SetRef(so, "_maxPlayersText", maxText);
            SetRef(so, "_durationText", durationText);
            SetRef(so, "_durationMinus", minus);
            SetRef(so, "_durationPlus", plus);
            SetRef(so, "_chatLog", log);
            SetRef(so, "_chatInput", chatInput);
            SetRef(so, "_chatSendButton", send);
            SetRef(so, "_backButton", back);
            SetRef(so, "_startButton", start);
            SetRef(so, "_startLabel", start.GetComponentInChildren<TextMeshProUGUI>());
            SerializedProperty slotsProperty = so.FindProperty("_slots");
            slotsProperty.arraySize = slots.Count;
            for (int i = 0; i < slots.Count; i++)
                slotsProperty.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            return screen.gameObject;
        }

        static PlayerSlotView BuildPlayerSlot(Transform panel, int index)
        {
            const float height = 50f;
            const float spacing = 6f;
            RectTransform slot = UiFactory.CreateRect(panel, $"Slot {index + 1}", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -86f - index * (height + spacing)), new Vector2(-50f, height));

            Image highlight = UiFactory.CreatePanel(slot, "Local Highlight", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, DriftPaintPalette.Cyan);
            UiFactory.Stretch((RectTransform)highlight.transform, -3f);
            Skew(highlight, 4f);

            Image background = UiFactory.CreatePanel(slot, "Background", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, RowColor);
            Skew(background, 4f);

            Image stripe = UiFactory.CreatePanel(slot, "Team Stripe", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(8f, 0f), new Vector2(16f, 0f), DriftPaintPalette.Cyan);
            Skew(stripe, 18f);

            Image car = UiFactory.CreatePanel(slot, "Car Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(60f, 0f), new Vector2(50f, 22f), DriftPaintPalette.Cyan);
            Skew(car, 10f);

            TextMeshProUGUI name = CreateLabel(slot, "Name", _titleFont, 33f, TextAlignmentOptions.Left, DriftPaintPalette.OffWhite,
                FontStyles.Bold | FontStyles.Italic, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(140f, 0f), new Vector2(480f, 50f));
            name.text = "Esperando jugador...";
            name.overflowMode = TextOverflowModes.Ellipsis;

            Image host = UiFactory.CreatePanel(slot, "Host Badge", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-20f, 0f), new Vector2(100f, 34f), DriftPaintPalette.SunsetGold);
            Skew(host, 12f);
            TextMeshProUGUI hostLabel = UiFactory.CreateText(host.transform, "Label", _titleFont, 26f, TextAlignmentOptions.Center,
                DriftPaintPalette.HudNavy, FontStyles.Bold);
            hostLabel.text = "HOST";

            var view = slot.gameObject.AddComponent<PlayerSlotView>();
            Bind(view, ("_teamStripe", stripe), ("_carIcon", car), ("_nameText", name),
                ("_hostBadge", host.gameObject), ("_localHighlight", highlight.gameObject));

            highlight.gameObject.SetActive(false);
            host.gameObject.SetActive(false);
            return view;
        }

        /// <summary>Franjas de color en los bordes del botón: cian a la izquierda y naranja a la derecha.</summary>
        static void AddAccent(Button button)
        {
            Transform t = button.transform;
            Image left = UiFactory.CreatePanel(t, "Accent Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(22f, 0f), new Vector2(10f, -14f), DriftPaintPalette.Cyan);
            Skew(left, 14f);
            Image right = UiFactory.CreatePanel(t, "Accent Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(-4f, 0f), new Vector2(10f, 0f), DriftPaintPalette.Orange);
            Skew(right, 14f);
        }

        /// <summary>
        /// Ventana de elección de auto: vista grande del auto marcado, una tarjeta plantilla que se clona por
        /// cada auto del catálogo, y los botones de cerrar, cancelar y confirmar.
        /// </summary>
        static CarSelectPanel BuildCarSelectPanel(Transform screen)
        {
            CarCatalogSO catalog = CarCatalogBuilder.LoadOrBuild();

            Image blocker = UiFactory.CreatePanel(screen, "Car Select", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, Color.black.WithAlpha(0.65f));
            // El lobby vive en el área de 16:9: el oscurecido se extiende más allá para tapar toda la pantalla.
            UiFactory.Stretch((RectTransform)blocker.transform, -1500f);
            blocker.raycastTarget = true;

            Image box = UiFactory.CreatePanel(blocker.transform, "Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1240f, 780f), DriftPaintPalette.HudNavy);
            box.raycastTarget = true;
            var boxOutline = box.gameObject.AddComponent<Outline>();
            boxOutline.effectColor = DriftPaintPalette.Cyan.WithAlpha(0.8f);
            boxOutline.effectDistance = new Vector2(3f, -3f);
            Transform b = box.transform;
            UiFactory.CreatePanel(b, "Top Stripe", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 8f), DriftPaintPalette.Cyan);
            Image corner = UiFactory.CreatePanel(b, "Corner Accent", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-150f, 0f), new Vector2(26f, 90f), DriftPaintPalette.Orange);
            Skew(corner, 20f);

            Image titleStripe = UiFactory.CreatePanel(b, "Title Stripe", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -30f), new Vector2(12f, 60f), DriftPaintPalette.Cyan);
            Skew(titleStripe, 16f);
            TextMeshProUGUI title = CreateLabel(b, "Title", _titleFont, 60f, TextAlignmentOptions.Left, DriftPaintPalette.OffWhite,
                FontStyles.Bold | FontStyles.Italic, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(72f, -22f), new Vector2(700f, 76f));
            title.text = "ELEGIR AUTO";

            Button close = CreateButton(b, "Close Button", "X", ButtonStyle.Dark, 40f, new Vector2(1f, 1f),
                new Vector2(-34f, -28f), new Vector2(76f, 64f), new Vector2(1f, 1f));

            // Vista grande
            Image hero = UiFactory.CreatePanel(b, "Preview", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -118f), new Vector2(1160f, 330f), RowColor);
            hero.gameObject.AddComponent<RectMask2D>();
            Image heroCar = UiFactory.CreatePanel(hero.transform, "Car Image", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-10f, 0f), new Vector2(660f, 330f), Color.white);
            heroCar.preserveAspect = true;
            Image heroStripe = UiFactory.CreatePanel(hero.transform, "Name Stripe", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(46f, 30f), new Vector2(14f, 84f), DriftPaintPalette.Cyan);
            Skew(heroStripe, 16f);
            TextMeshProUGUI heroName = CreateLabel(hero.transform, "Car Name", _titleFont, 80f, TextAlignmentOptions.Left, DriftPaintPalette.OffWhite,
                FontStyles.Bold | FontStyles.Italic, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(80f, 30f), new Vector2(440f, 96f));
            heroName.text = "Auto";
            heroName.overflowMode = TextOverflowModes.Ellipsis;
            Image currentBadge = UiFactory.CreatePanel(hero.transform, "Current Badge", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(80f, -52f), new Vector2(200f, 52f), DriftPaintPalette.Cyan);
            Skew(currentBadge, 14f);
            TextMeshProUGUI currentLabel = UiFactory.CreateText(currentBadge.transform, "Label", _titleFont, 34f, TextAlignmentOptions.Center,
                DriftPaintPalette.HudNavy, FontStyles.Bold | FontStyles.Italic);
            currentLabel.text = "ACTUAL";

            // Tarjetas
            RectTransform cards = UiFactory.CreateRect(b, "Cards", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -472f), new Vector2(1160f, 200f));
            var layout = cards.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 22f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            CarCardView template = BuildCarCard(cards);

            Button cancel = CreateButton(b, "Cancel Button", "CANCELAR", ButtonStyle.Outline, 38f, new Vector2(0.5f, 0f),
                new Vector2(-290f, 36f), new Vector2(340f, 76f), new Vector2(0.5f, 0f));
            Button confirm = CreateButton(b, "Confirm Button", "CONFIRMAR", ButtonStyle.Primary, 48f, new Vector2(0.5f, 0f),
                new Vector2(190f, 32f), new Vector2(520f, 86f), new Vector2(0.5f, 0f));

            var panel = blocker.gameObject.AddComponent<CarSelectPanel>();
            Bind(panel, ("_catalog", catalog), ("_teamPalette", LoadOrCreatePalette()),
                ("_previewImage", heroCar), ("_previewName", heroName), ("_currentBadge", currentBadge.gameObject),
                ("_cardTemplate", template), ("_closeButton", close), ("_cancelButton", cancel), ("_confirmButton", confirm));

            template.gameObject.SetActive(false);
            blocker.gameObject.SetActive(false);
            return panel;
        }

        static CarCardView BuildCarCard(Transform cards)
        {
            RectTransform card = UiFactory.CreateRect(cards, "Card Template", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(270f, 196f));

            Image frame = UiFactory.CreatePanel(card, "Selected Frame", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, DriftPaintPalette.Cyan);
            UiFactory.Stretch((RectTransform)frame.transform, -5f);

            Image background = UiFactory.CreatePanel(card, "Background", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, RowColor);
            background.sprite = _white;
            background.raycastTarget = true;
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.85f, 0.95f, 1f);
            colors.pressedColor = new Color(0.7f, 0.75f, 0.8f);
            button.colors = colors;

            Image preview = UiFactory.CreatePanel(card, "Car Image", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -6f), new Vector2(-12f, 132f), Color.white);
            preview.preserveAspect = true;

            Image nameBar = UiFactory.CreatePanel(card, "Name Bar", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(0f, 54f), DriftPaintPalette.HudNavy.WithAlpha(0.85f));
            Image stripe = UiFactory.CreatePanel(nameBar.transform, "Stripe", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(18f, 0f), new Vector2(9f, -14f), DriftPaintPalette.Cyan);
            Skew(stripe, 16f);
            TextMeshProUGUI name = CreateLabel(nameBar.transform, "Name", _titleFont, 38f, TextAlignmentOptions.Left, DriftPaintPalette.OffWhite,
                FontStyles.Bold | FontStyles.Italic, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(42f, 0f), new Vector2(220f, 52f));
            name.text = "Auto";
            name.overflowMode = TextOverflowModes.Ellipsis;

            Image current = UiFactory.CreatePanel(card, "Current Mark", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-8f, -8f), new Vector2(104f, 30f), DriftPaintPalette.Cyan);
            TextMeshProUGUI currentLabel = UiFactory.CreateText(current.transform, "Label", _titleFont, 22f, TextAlignmentOptions.Center,
                DriftPaintPalette.HudNavy, FontStyles.Bold);
            currentLabel.text = "ACTUAL";

            var view = card.gameObject.AddComponent<CarCardView>();
            Bind(view, ("_button", button), ("_preview", preview), ("_nameText", name), ("_stripe", stripe),
                ("_selectedFrame", frame.gameObject), ("_currentMark", current.gameObject));

            frame.gameObject.SetActive(false);
            current.gameObject.SetActive(false);
            return view;
        }

        static TextMeshProUGUI CreateConfigRow(Transform panel, string name, string label, int index, string value)
        {
            Image row = UiFactory.CreatePanel(panel, name, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(185f, -80f - index * 72f), new Vector2(-394f, 62f), RowColor);
            TextMeshProUGUI labelText = CreateLabel(row.transform, "Label", _labelFont, 30f, TextAlignmentOptions.Left, DriftPaintPalette.OffWhite,
                FontStyles.Bold, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(260f, 50f));
            labelText.text = label;
            TextMeshProUGUI valueText = CreateLabel(row.transform, "Value", _titleFont, 34f, TextAlignmentOptions.Right, DriftPaintPalette.OffWhite,
                FontStyles.Bold | FontStyles.Italic, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(200f, 50f));
            valueText.text = value;
            return valueText;
        }

        static TeamPaletteSO LoadOrCreatePalette()
        {
            return EditorAssetUtility.LoadOrCreate(PalettePath, ScriptableObject.CreateInstance<TeamPaletteSO>);
        }

        #endregion

        #region Avisos

        static GameObject BuildBusyOverlay(Transform root, out TextMeshProUGUI text)
        {
            Image blocker = UiFactory.CreatePanel(root, "Busy Overlay", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, Color.black.WithAlpha(0.55f));
            blocker.raycastTarget = true;
            text = UiFactory.CreateText(blocker.transform, "Message", _titleFont, 64f, TextAlignmentOptions.Center,
                DriftPaintPalette.OffWhite, FontStyles.Bold | FontStyles.Italic);
            text.text = "CONECTANDO...";
            return blocker.gameObject;
        }

        static GameObject BuildErrorOverlay(Transform root, out TextMeshProUGUI text, out Button close)
        {
            Image blocker = UiFactory.CreatePanel(root, "Error Overlay", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, Color.black.WithAlpha(0.6f));
            blocker.raycastTarget = true;

            Image box = UiFactory.CreatePanel(blocker.transform, "Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(860f, 360f), DriftPaintPalette.HudNavy);
            UiFactory.CreatePanel(box.transform, "Top Stripe", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 10f), DriftPaintPalette.Orange);

            text = UiFactory.CreateText(box.transform, "Message", _bodyFont, 30f, TextAlignmentOptions.Center, DriftPaintPalette.OffWhite);
            RectTransform textRect = (RectTransform)text.transform;
            textRect.offsetMin = new Vector2(40f, 120f);
            textRect.offsetMax = new Vector2(-40f, -30f);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.text = "Error";

            close = CreateButton(box.transform, "Close Button", "ACEPTAR", ButtonStyle.Primary, 40f,
                new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(320f, 80f), new Vector2(0.5f, 0f));
            return blocker.gameObject;
        }

        #endregion

        #region Piezas de UI

        internal enum ButtonStyle
        {
            Primary,
            Outline,
            Dark
        }

        static RectTransform CreateScreen(Transform root, string name)
        {
            RectTransform screen = UiFactory.CreateRect(root, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return screen;
        }

        internal static TextMeshProUGUI CreateLabel(Transform parent, string name, TMP_FontAsset font, float size, TextAlignmentOptions alignment,
            Color color, FontStyles style, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 rectSize)
        {
            TextMeshProUGUI text = UiFactory.CreateText(parent, name, font, size, alignment, color, style);
            var rect = (RectTransform)text.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = rectSize;
            return text;
        }

        internal static Button CreateButton(Transform parent, string name, string label, ButtonStyle style, float fontSize,
            Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            Color fill = style == ButtonStyle.Primary ? DriftPaintPalette.Cyan
                : style == ButtonStyle.Outline ? DriftPaintPalette.HudNavy.WithAlpha(0.92f)
                : DriftPaintPalette.Hex(0x3A3F5C);
            Color textColor = style == ButtonStyle.Primary ? DriftPaintPalette.HudNavy : DriftPaintPalette.OffWhite;

            Image image = UiFactory.CreatePanel(parent, name, anchor, anchor, pivot ?? new Vector2(0.5f, 0.5f), position, size, fill);
            image.sprite = _white;
            image.raycastTarget = true;
            Skew(image, style == ButtonStyle.Dark ? 8f : 14f);
            if (style == ButtonStyle.Outline)
            {
                var outline = image.gameObject.AddComponent<Outline>();
                outline.effectColor = DriftPaintPalette.OffWhite;
                outline.effectDistance = new Vector2(3f, -3f);
            }

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.85f, 0.95f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.7f, 0.75f, 0.8f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.7f);
            button.colors = colors;

            TextMeshProUGUI text = UiFactory.CreateText(image.transform, "Label", _titleFont, fontSize, TextAlignmentOptions.Center,
                textColor, FontStyles.Bold | FontStyles.Italic);
            text.text = label;
            return button;
        }

        static TMP_InputField CreateInput(Transform parent, string name, string placeholder, float fontSize, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size, Color background, Color textColor)
        {
            Image image = UiFactory.CreatePanel(parent, name, anchor, anchor, pivot, position, size, background);
            image.sprite = _white;
            image.raycastTarget = true;

            RectTransform area = UiFactory.CreateRect(image.transform, "Text Area", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            area.offsetMin = new Vector2(22f, 6f);
            area.offsetMax = new Vector2(-22f, -6f);
            area.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI hint = UiFactory.CreateText(area, "Placeholder", _bodyFont, fontSize, TextAlignmentOptions.Left,
                textColor.WithAlpha(0.5f), FontStyles.Italic);
            hint.text = placeholder;

            TextMeshProUGUI text = UiFactory.CreateText(area, "Text", _bodyFont, fontSize, TextAlignmentOptions.Left, textColor);
            text.richText = false;

            var input = image.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = image;
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = hint;
            input.fontAsset = _bodyFont;
            input.pointSize = fontSize;
            input.richText = false;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.caretColor = textColor;
            input.customCaretColor = true;
            return input;
        }

        static NicknameField CreateNicknameField(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, float width)
        {
            const float height = 74f;
            Image box = UiFactory.CreatePanel(parent, "Nickname", anchor, anchor, pivot, position, new Vector2(width, height), PanelColor);
            Skew(box, 14f);
            Transform b = box.transform;

            Image stripe = UiFactory.CreatePanel(b, "Stripe", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(10f, 0f), new Vector2(14f, 0f), DriftPaintPalette.Cyan);
            Skew(stripe, 14f);

            TextMeshProUGUI label = CreateLabel(b, "Label", _labelFont, 30f, TextAlignmentOptions.Left, MutedText,
                FontStyles.Bold | FontStyles.Italic, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(180f, 60f));
            label.text = "JUGADOR  /";

            float nameWidth = width - 220f - 120f;
            TextMeshProUGUI name = CreateLabel(b, "Name", _titleFont, 42f, TextAlignmentOptions.Left, DriftPaintPalette.OffWhite,
                FontStyles.Bold | FontStyles.Italic, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(220f, 0f), new Vector2(nameWidth, 64f));
            name.text = "Piloto";
            name.overflowMode = TextOverflowModes.Ellipsis;

            TMP_InputField input = CreateInput(b, "Name Input", "Nickname", 32f, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(214f, 0f), new Vector2(nameWidth, 58f), InputColor, DriftPaintPalette.OffWhite);

            Button edit = CreateButton(b, "Edit Button", "EDITAR", ButtonStyle.Primary, 26f, new Vector2(1f, 0.5f),
                new Vector2(-10f, 0f), new Vector2(110f, height - 14f), new Vector2(1f, 0.5f));

            var view = box.gameObject.AddComponent<NicknameField>();
            Bind(view, ("_nameText", name), ("_editButton", edit), ("_input", input));
            input.gameObject.SetActive(false);
            return view;
        }

        internal static void Skew(Graphic graphic, float angle)
        {
            var skew = graphic.gameObject.AddComponent<UISkew>();
            var so = new SerializedObject(skew);
            so.FindProperty("_angle").floatValue = angle;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void Bind(Object target, params (string field, Object value)[] references)
        {
            var so = new SerializedObject(target);
            foreach ((string field, Object value) in references)
                SetRef(so, field, value);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetRef(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"{so.targetObject.GetType().Name} no tiene el campo {field}.");
                return;
            }
            property.objectReferenceValue = value;
        }

        static void PutSceneFirstInBuild(string path)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        #endregion
    }
}
