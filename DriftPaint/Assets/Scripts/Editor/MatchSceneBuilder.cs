using System.Collections.Generic;
using System.Linq;
using DriftPaint.Cars;
using DriftPaint.Level;
using DriftPaint.Match;
using DriftPaint.Teams;
using DriftPaint.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Genera la escena de partida de Drift Arena: arena de 128 x 128 m, spawns por equipo,
    /// volumen de caída, cámara, HUD de partida, plantilla de nombres flotantes y el controlador de partida.
    /// Todo queda como objetos de la escena para ajustarlo a mano.
    /// </summary>
    public static class MatchSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Match_DriftArena.unity";
        const string DataPath = "Assets/Data/Cars/CarData_Default.asset";
        const string PalettePath = "Assets/Data/Teams/TeamPalette_Default.asset";
        const string PrefabFolder = "Assets/Prefabs/Cars";
        const string ArtFolder = "Assets/Art/Generated";
        const string MaterialFolder = "Assets/Art/Materials";
        const float ArenaSize = MatchRules.ArenaSize;

        static Sprite _white;
        static TMP_FontAsset _titleFont;
        static TMP_FontAsset _labelFont;
        static TMP_FontAsset _bodyFont;

        [MenuItem("DriftPaint/Generar escena de partida")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            TeamPaletteSO palette = EditorAssetUtility.LoadOrCreate(PalettePath, ScriptableObject.CreateInstance<TeamPaletteSO>);
            List<GameObject> prefabs = LoadCarPrefabs(palette);
            if (prefabs.Count == 0)
            {
                EditorUtility.DisplayDialog("DriftPaint", "No se pudo armar ningún prefab de auto. Revisá la consola.", "OK");
                return;
            }

            LoadUiAssets();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CarSandboxSceneBuilder.ConfigureLighting();
            List<TeamSpawnPoint> spawns = BuildArena();
            CarCameraRig cameraRig = CarSandboxSceneBuilder.BuildCamera();

            var matchGo = new GameObject("Match");
            var match = matchGo.AddComponent<MatchController>();
            MatchHUD hud = BuildHud(match, palette);
            CarNameplate nameplate = BuildNameplateTemplate();

            var spawner = matchGo.AddComponent<MatchSpawner>();
            var so = new SerializedObject(spawner);
            SerializedProperty prefabsProp = so.FindProperty("_carPrefabs");
            prefabsProp.arraySize = prefabs.Count;
            for (int i = 0; i < prefabs.Count; i++)
                prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            SerializedProperty spawnsProp = so.FindProperty("_spawnPoints");
            spawnsProp.arraySize = spawns.Count;
            for (int i = 0; i < spawns.Count; i++)
                spawnsProp.GetArrayElementAtIndex(i).objectReferenceValue = spawns[i];
            MenuSceneBuilder.SetRef(so, "_match", match);
            MenuSceneBuilder.SetRef(so, "_cameraRig", cameraRig);
            MenuSceneBuilder.SetRef(so, "_hud", hud);
            MenuSceneBuilder.SetRef(so, "_palette", palette);
            MenuSceneBuilder.SetRef(so, "_nameplateTemplate", nameplate);
            so.ApplyModifiedPropertiesWithoutUndo();

            nameplate.gameObject.SetActive(false);

            EditorAssetUtility.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Escena de partida generada en {ScenePath}.");
        }

        static List<GameObject> LoadCarPrefabs(TeamPaletteSO palette)
        {
            List<GameObject> prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(p => p != null && p.GetComponent<CarController>() != null)
                .OrderBy(p => p.name)
                .ToList();
            if (prefabs.Count > 0) return prefabs;

            CarDataSO carData = EditorAssetUtility.LoadOrCreate(DataPath, ScriptableObject.CreateInstance<CarDataSO>);
            return CarPrefabBuilder.BuildAll(carData, palette);
        }

        static void LoadUiAssets()
        {
            _white = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/T_White.png");
            if (_white == null)
            {
                var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                texture.SetPixels(Enumerable.Repeat(Color.white, 64).ToArray());
                texture.Apply();
                EditorAssetUtility.SaveTexture(texture, ArtFolder + "/T_White.png", true, TextureWrapMode.Clamp);
                _white = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/T_White.png");
            }

            _titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/BarlowCondensed-SemiBold SDF.asset");
            _labelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/BarlowCondensed-Medium SDF.asset");
            _bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Oxanium-Medium SDF.asset");
        }

        #region Arena

        static List<TeamSpawnPoint> BuildArena()
        {
            Texture2D grid = EditorAssetUtility.SaveTexture(CarSandboxSceneBuilder.CreateGridTexture(), ArtFolder + "/T_FloorGrid.png",
                false, TextureWrapMode.Repeat);
            Material floorMat = EditorAssetUtility.LoadOrCreateLitMaterial(MaterialFolder + "/M_Floor.mat", Color.white, grid,
                new Vector2(ArenaSize, ArenaSize), 0.15f);
            Material wallMat = EditorAssetUtility.LoadOrCreateLitMaterial(MaterialFolder + "/M_Wall.mat", DriftPaintPalette.Slate);
            Material cyanMat = EditorAssetUtility.LoadOrCreateLitMaterial(MaterialFolder + "/M_TeamCyan.mat", DriftPaintPalette.Cyan, smoothness: 0.4f);
            Material orangeMat = EditorAssetUtility.LoadOrCreateLitMaterial(MaterialFolder + "/M_TeamOrange.mat", DriftPaintPalette.Orange, smoothness: 0.4f);

            var arena = new GameObject("Arena").transform;

            // El piso coincide con la grilla de pintura: de -64 a 64 m en X y Z, celdas de 1 m.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(arena);
            floor.transform.localScale = new Vector3(ArenaSize / 10f, 1f, ArenaSize / 10f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;
            floor.isStatic = true;

            var walls = new GameObject("Walls").transform;
            walls.SetParent(arena);
            float half = ArenaSize * 0.5f;
            CarSandboxSceneBuilder.CreateBlock(walls, "Wall_N", new Vector3(0f, 1f, half + 0.5f), new Vector3(ArenaSize + 2f, 2f, 1f), Vector3.zero, wallMat);
            CarSandboxSceneBuilder.CreateBlock(walls, "Wall_S", new Vector3(0f, 1f, -half - 0.5f), new Vector3(ArenaSize + 2f, 2f, 1f), Vector3.zero, wallMat);
            CarSandboxSceneBuilder.CreateBlock(walls, "Wall_E", new Vector3(half + 0.5f, 1f, 0f), new Vector3(1f, 2f, ArenaSize + 2f), Vector3.zero, wallMat);
            CarSandboxSceneBuilder.CreateBlock(walls, "Wall_W", new Vector3(-half - 0.5f, 1f, 0f), new Vector3(1f, 2f, ArenaSize + 2f), Vector3.zero, wallMat);

            // El mapa es simétrico respecto del centro: cada pieza de un lado tiene su espejo del otro.
            var ramps = new GameObject("Ramps").transform;
            ramps.SetParent(arena);
            CarSandboxSceneBuilder.CreateRamp(ramps, "Ramp_Cyan", new Vector3(-36f, 0f, -12f), 0f, 14f, 8f, 13f, cyanMat);
            CarSandboxSceneBuilder.CreateRamp(ramps, "Ramp_Orange", new Vector3(36f, 0f, 12f), 180f, 14f, 8f, 13f, orangeMat);
            CarSandboxSceneBuilder.CreateRamp(ramps, "Ramp_Center_S", new Vector3(-6f, 0f, -24f), 90f, 12f, 9f, 10f, wallMat);
            CarSandboxSceneBuilder.CreateRamp(ramps, "Ramp_Center_N", new Vector3(6f, 0f, 24f), 270f, 12f, 9f, 10f, wallMat);

            var obstacles = new GameObject("Obstacles").transform;
            obstacles.SetParent(arena);
            CarSandboxSceneBuilder.CreateBlock(obstacles, "Center_Block", new Vector3(0f, 1f, 0f), new Vector3(6f, 2f, 6f), new Vector3(0f, 45f, 0f), wallMat);
            CarSandboxSceneBuilder.CreateBlock(obstacles, "Pillar_W", new Vector3(-18f, 1.5f, 6f), new Vector3(3f, 3f, 3f), Vector3.zero, wallMat);
            CarSandboxSceneBuilder.CreateBlock(obstacles, "Pillar_E", new Vector3(18f, 1.5f, -6f), new Vector3(3f, 3f, 3f), Vector3.zero, wallMat);
            CarSandboxSceneBuilder.CreateBlock(obstacles, "Barrier_Cyan", new Vector3(-40f, 0.5f, -36f), new Vector3(16f, 1f, 1f), new Vector3(0f, 30f, 0f), cyanMat);
            CarSandboxSceneBuilder.CreateBlock(obstacles, "Barrier_Orange", new Vector3(40f, 0.5f, 36f), new Vector3(16f, 1f, 1f), new Vector3(0f, 30f, 0f), orangeMat);

            var killVolume = new GameObject("KillVolume");
            killVolume.transform.SetParent(arena);
            killVolume.transform.position = new Vector3(0f, -20f, 0f);
            var killBox = killVolume.AddComponent<BoxCollider>();
            killBox.isTrigger = true;
            killBox.size = new Vector3(ArenaSize * 4f, 10f, ArenaSize * 4f);
            killVolume.AddComponent<KillVolume>();

            var spawnRoot = new GameObject("Spawns").transform;
            var spawns = new List<TeamSpawnPoint>();
            // Primero los del medio, así con pocos jugadores arrancan centrados.
            float[] offsets = { -3f, 3f, -9f, 9f };
            for (int i = 0; i < offsets.Length; i++)
            {
                spawns.Add(CreateSpawn(spawnRoot, TeamId.A, i, new Vector3(offsets[i], 0.3f, -56f), 0f));
                spawns.Add(CreateSpawn(spawnRoot, TeamId.B, i, new Vector3(-offsets[i], 0.3f, 56f), 180f));
            }
            return spawns;
        }

        static TeamSpawnPoint CreateSpawn(Transform parent, byte team, int order, Vector3 position, float yaw)
        {
            var go = new GameObject($"Spawn_{(team == TeamId.A ? "A" : "B")}{order + 1}");
            go.transform.SetParent(parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var spawn = go.AddComponent<TeamSpawnPoint>();
            var so = new SerializedObject(spawn);
            so.FindProperty("_team").intValue = team;
            so.FindProperty("_order").intValue = order;
            so.ApplyModifiedPropertiesWithoutUndo();
            return spawn;
        }

        #endregion

        #region HUD

        static MatchHUD BuildHud(MatchController match, TeamPaletteSO palette)
        {
            Canvas canvas = UiFactory.CreateOverlayCanvas(null, "Match HUD");
            Transform root = canvas.transform;
            Color panel = DriftPaintPalette.HudNavy.WithAlpha(0.88f);
            Color leftColor = palette.Get(TeamId.B).Main;
            Color rightColor = palette.Get(TeamId.A).Main;

            // Marcador
            RectTransform scoreBar = UiFactory.CreateRect(root, "Score Bar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(920f, 84f));
            Image scoreBack = UiFactory.CreatePanel(scoreBar, "Background", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, panel);
            MenuSceneBuilder.Skew(scoreBack, 10f);

            Image leftPanel = UiFactory.CreatePanel(scoreBar, "Left Team", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(14f, 0f), new Vector2(320f, 62f), leftColor);
            MenuSceneBuilder.Skew(leftPanel, 14f);
            TextMeshProUGUI leftText = UiFactory.CreateText(leftPanel.transform, "Percent", _titleFont, 54f, TextAlignmentOptions.Center,
                DriftPaintPalette.OffWhite, FontStyles.Bold | FontStyles.Italic);
            leftText.text = "0%";

            Image rightPanel = UiFactory.CreatePanel(scoreBar, "Right Team", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-14f, 0f), new Vector2(320f, 62f), rightColor);
            MenuSceneBuilder.Skew(rightPanel, 14f);
            TextMeshProUGUI rightText = UiFactory.CreateText(rightPanel.transform, "Percent", _titleFont, 54f, TextAlignmentOptions.Center,
                DriftPaintPalette.HudNavy, FontStyles.Bold | FontStyles.Italic);
            rightText.text = "0%";

            TextMeshProUGUI clock = MenuSceneBuilder.CreateLabel(scoreBar, "Clock", _titleFont, 64f, TextAlignmentOptions.Center,
                DriftPaintPalette.OffWhite, FontStyles.Bold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 80f));
            clock.text = "02:00";

            // Cuenta regresiva
            Image countdown = UiFactory.CreatePanel(root, "Countdown", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 120f), new Vector2(420f, 260f), DriftPaintPalette.HudNavy.WithAlpha(0.6f));
            MenuSceneBuilder.Skew(countdown, 10f);
            TextMeshProUGUI countdownText = UiFactory.CreateText(countdown.transform, "Value", _titleFont, 220f, TextAlignmentOptions.Center,
                DriftPaintPalette.OffWhite, FontStyles.Bold | FontStyles.Italic);
            countdownText.text = "3";

            // Fin de la partida
            Image results = UiFactory.CreatePanel(root, "Time Up", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 80f), new Vector2(980f, 220f), panel);
            MenuSceneBuilder.Skew(results, 10f);
            UiFactory.CreatePanel(results.transform, "Stripe", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 10f), DriftPaintPalette.SunsetGold);
            TextMeshProUGUI timeUp = MenuSceneBuilder.CreateLabel(results.transform, "Title", _titleFont, 120f, TextAlignmentOptions.Center,
                DriftPaintPalette.OffWhite, FontStyles.Bold | FontStyles.Italic, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -16f), new Vector2(900f, 130f));
            timeUp.text = "¡TIEMPO!";
            TextMeshProUGUI back = MenuSceneBuilder.CreateLabel(results.transform, "Subtitle", _labelFont, 36f, TextAlignmentOptions.Center,
                DriftPaintPalette.OffWhite.WithAlpha(0.75f), FontStyles.Italic, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 20f), new Vector2(900f, 50f));
            back.text = "Volviendo al lobby...";

            // Velocímetro
            Image speedPanel = UiFactory.CreatePanel(root, "Speedometer", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-40f, 40f), new Vector2(300f, 180f), panel);
            MenuSceneBuilder.Skew(speedPanel, 8f);
            Image speedFill = UiFactory.CreatePanel(speedPanel.transform, "Speed Bar", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 12f), DriftPaintPalette.Cyan);
            speedFill.sprite = _white;
            speedFill.type = Image.Type.Filled;
            speedFill.fillMethod = Image.FillMethod.Horizontal;
            speedFill.fillAmount = 0f;
            TextMeshProUGUI speedText = UiFactory.CreateText(speedPanel.transform, "Value", _titleFont, 104f, TextAlignmentOptions.Center,
                DriftPaintPalette.OffWhite, FontStyles.Bold | FontStyles.Italic);
            ((RectTransform)speedText.transform).offsetMin = new Vector2(0f, 46f);
            speedText.text = "0";
            TextMeshProUGUI unit = UiFactory.CreateText(speedPanel.transform, "Unit", _labelFont, 32f, TextAlignmentOptions.Bottom,
                DriftPaintPalette.OffWhite, FontStyles.Italic);
            ((RectTransform)unit.transform).offsetMin = new Vector2(0f, 20f);
            unit.text = "km/h";

            // Disparo y barriles
            Image abilities = UiFactory.CreatePanel(root, "Abilities", Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(40f, 40f), new Vector2(440f, 110f), panel);
            MenuSceneBuilder.Skew(abilities, 8f);
            Transform a = abilities.transform;

            Image fireBack = UiFactory.CreatePanel(a, "Fire Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(20f, 0f), new Vector2(76f, 76f), DriftPaintPalette.Slate);
            Image fireFill = UiFactory.CreatePanel(fireBack.transform, "Ready Fill", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, DriftPaintPalette.Cyan);
            fireFill.sprite = _white;
            fireFill.type = Image.Type.Filled;
            fireFill.fillMethod = Image.FillMethod.Vertical;
            fireFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fireFill.fillAmount = 1f;
            TextMeshProUGUI fireText = MenuSceneBuilder.CreateLabel(a, "Fire Cooldown", _titleFont, 44f, TextAlignmentOptions.Left,
                DriftPaintPalette.OffWhite, FontStyles.Bold, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(120f, 70f));
            fireText.text = "LISTO";

            UiFactory.CreatePanel(a, "Separator", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(245f, 0f), new Vector2(3f, 70f), DriftPaintPalette.OffWhite.WithAlpha(0.4f));

            Image barrelIcon = UiFactory.CreatePanel(a, "Barrel Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(270f, 0f), new Vector2(56f, 72f), DriftPaintPalette.Hex(0xD93A2B));
            UiFactory.CreatePanel(barrelIcon.transform, "Band", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(0f, 12f), DriftPaintPalette.SunsetGold);
            TextMeshProUGUI barrels = MenuSceneBuilder.CreateLabel(a, "Barrels", _titleFont, 48f, TextAlignmentOptions.Left,
                DriftPaintPalette.OffWhite, FontStyles.Bold, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(340f, 0f), new Vector2(90f, 70f));
            barrels.text = "x0";

            var hud = canvas.gameObject.AddComponent<MatchHUD>();
            MenuSceneBuilder.Bind(hud,
                ("_match", match),
                ("_leftPercentText", leftText), ("_rightPercentText", rightText), ("_clockText", clock),
                ("_countdownRoot", countdown.gameObject), ("_countdownText", countdownText), ("_resultsRoot", results.gameObject),
                ("_speedText", speedText), ("_speedFill", speedFill),
                ("_fireCooldownText", fireText), ("_fireCooldownFill", fireFill), ("_barrelsText", barrels));

            results.gameObject.SetActive(false);
            return hud;
        }

        static CarNameplate BuildNameplateTemplate()
        {
            var root = new GameObject("Nameplates").transform;

            var go = new GameObject("Nameplate Template", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(root, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 5;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(420f, 70f);
            rect.localScale = Vector3.one * 0.01f;

            Image marker = UiFactory.CreatePanel(rect, "Team Marker", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(30f, 0f), new Vector2(26f, 26f), DriftPaintPalette.Cyan);
            marker.transform.localEulerAngles = new Vector3(0f, 0f, 45f);

            TextMeshProUGUI name = UiFactory.CreateText(rect, "Name", _bodyFont, 46f, TextAlignmentOptions.Left,
                DriftPaintPalette.OffWhite, FontStyles.Bold);
            ((RectTransform)name.transform).offsetMin = new Vector2(60f, 0f);
            name.text = "Piloto";

            var plate = go.AddComponent<CarNameplate>();
            MenuSceneBuilder.Bind(plate, ("_nameText", name), ("_marker", marker));
            return plate;
        }

        #endregion

        static void AddSceneToBuild(string path)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
