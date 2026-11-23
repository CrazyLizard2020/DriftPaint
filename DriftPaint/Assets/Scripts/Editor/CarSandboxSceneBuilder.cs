using System.Collections.Generic;
using System.Linq;
using DriftPaint.Cars;
using DriftPaint.Sandbox;
using DriftPaint.Teams;
using DriftPaint.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Genera la escena de prueba del auto: datos del auto, prefabs de los modelos,
    /// materiales, arena con rampas y obstáculos, cámara y HUD.
    /// Todo queda guardado en assets y en la escena, listo para editar en Unity.
    /// </summary>
    public static class CarSandboxSceneBuilder
    {
        const string DataPath = "Assets/Data/Cars/CarData_Default.asset";
        const string PalettePath = "Assets/Data/Teams/TeamPalette_Default.asset";
        const string ArtFolder = "Assets/Art/Generated";
        const string MaterialFolder = "Assets/Art/Materials";
        const string ScenePath = "Assets/Scenes/Sandbox_Car.unity";
        const float ArenaSize = 128f;

        [MenuItem("DriftPaint/Generar escena de prueba del auto")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            CarDataSO carData = EditorAssetUtility.LoadOrCreate(DataPath, ScriptableObject.CreateInstance<CarDataSO>);
            TeamPaletteSO palette = EditorAssetUtility.LoadOrCreate(PalettePath, ScriptableObject.CreateInstance<TeamPaletteSO>);
            List<GameObject> prefabs = CarPrefabBuilder.BuildAll(carData, palette);
            if (prefabs.Count == 0)
            {
                EditorUtility.DisplayDialog("DriftPaint", "No se pudo armar ningún prefab de auto. Revisá la consola.", "OK");
                return;
            }

            BuildScene(prefabs);
            AssetDatabase.SaveAssets();
            Debug.Log($"Escena de prueba generada en {ScenePath}.");
        }

        [MenuItem("DriftPaint/Regenerar prefabs de autos")]
        public static void RebuildPrefabsFromMenu()
        {
            CarDataSO carData = EditorAssetUtility.LoadOrCreate(DataPath, ScriptableObject.CreateInstance<CarDataSO>);
            TeamPaletteSO palette = EditorAssetUtility.LoadOrCreate(PalettePath, ScriptableObject.CreateInstance<TeamPaletteSO>);
            CarPrefabBuilder.BuildAll(carData, palette);
            AssetDatabase.SaveAssets();
        }

        static void BuildScene(List<GameObject> carPrefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigureLighting();
            BuildArena(out Transform spawnPoint);

            CarCameraRig cameraRig = BuildCamera();
            SandboxHUD hud = BuildHud();

            var sandbox = new GameObject("Sandbox");
            var telemetry = sandbox.AddComponent<CarTelemetry>();
            var bootstrap = sandbox.AddComponent<SandboxBootstrap>();

            var so = new SerializedObject(bootstrap);
            SerializedProperty prefabsProp = so.FindProperty("_carPrefabs");
            prefabsProp.arraySize = carPrefabs.Count;
            for (int i = 0; i < carPrefabs.Count; i++)
                prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = carPrefabs[i];
            so.FindProperty("_spawnPoint").objectReferenceValue = spawnPoint;
            so.FindProperty("_cameraRig").objectReferenceValue = cameraRig;
            so.FindProperty("_hud").objectReferenceValue = hud;
            so.FindProperty("_telemetry").objectReferenceValue = telemetry;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorAssetUtility.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
        }

        static void ConfigureLighting()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.Lerp(Color.white, DriftPaintPalette.SunsetGold, 0.35f);
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(38f, -35f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = DriftPaintPalette.SkyLavender;
            RenderSettings.ambientEquatorColor = DriftPaintPalette.WarmPeach;
            RenderSettings.ambientGroundColor = DriftPaintPalette.Slate;
            RenderSettings.sun = light;
        }

        static void BuildArena(out Transform spawnPoint)
        {
            Texture2D grid = EditorAssetUtility.SaveTexture(CreateGridTexture(), ArtFolder + "/T_FloorGrid.png", false, TextureWrapMode.Repeat);
            Material floorMat = EditorAssetUtility.LoadOrCreateLitMaterial(MaterialFolder + "/M_Floor.mat", Color.white, grid, new Vector2(ArenaSize, ArenaSize), 0.15f);
            Material wallMat = EditorAssetUtility.LoadOrCreateLitMaterial(MaterialFolder + "/M_Wall.mat", DriftPaintPalette.Slate);
            Material cyanMat = EditorAssetUtility.LoadOrCreateLitMaterial(MaterialFolder + "/M_TeamCyan.mat", DriftPaintPalette.Cyan, smoothness: 0.4f);
            Material orangeMat = EditorAssetUtility.LoadOrCreateLitMaterial(MaterialFolder + "/M_TeamOrange.mat", DriftPaintPalette.Orange, smoothness: 0.4f);

            var arena = new GameObject("Arena").transform;

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(arena);
            floor.transform.localScale = new Vector3(ArenaSize / 10f, 1f, ArenaSize / 10f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;
            floor.isStatic = true;

            var walls = new GameObject("Walls").transform;
            walls.SetParent(arena);
            float half = ArenaSize * 0.5f;
            CreateBlock(walls, "Wall_N", new Vector3(0f, 1f, half + 0.5f), new Vector3(ArenaSize + 2f, 2f, 1f), Vector3.zero, wallMat);
            CreateBlock(walls, "Wall_S", new Vector3(0f, 1f, -half - 0.5f), new Vector3(ArenaSize + 2f, 2f, 1f), Vector3.zero, wallMat);
            CreateBlock(walls, "Wall_E", new Vector3(half + 0.5f, 1f, 0f), new Vector3(1f, 2f, ArenaSize + 2f), Vector3.zero, wallMat);
            CreateBlock(walls, "Wall_W", new Vector3(-half - 0.5f, 1f, 0f), new Vector3(1f, 2f, ArenaSize + 2f), Vector3.zero, wallMat);

            var ramps = new GameObject("Ramps").transform;
            ramps.SetParent(arena);
            CreateRamp(ramps, "Ramp_Cyan", new Vector3(-30f, 0f, 10f), 0f, 14f, 8f, 13f, cyanMat);
            CreateRamp(ramps, "Ramp_Orange", new Vector3(30f, 0f, 10f), 180f, 14f, 8f, 13f, orangeMat);
            CreateRamp(ramps, "Ramp_Long", new Vector3(0f, 0f, 35f), 90f, 20f, 10f, 9f, wallMat);

            var obstacles = new GameObject("Obstacles").transform;
            obstacles.SetParent(arena);
            CreateBlock(obstacles, "Pillar_1", new Vector3(-12f, 1.5f, -10f), new Vector3(3f, 3f, 3f), Vector3.zero, wallMat);
            CreateBlock(obstacles, "Pillar_2", new Vector3(12f, 1.5f, -10f), new Vector3(3f, 3f, 3f), Vector3.zero, wallMat);
            CreateBlock(obstacles, "Pillar_3", new Vector3(0f, 1.5f, 5f), new Vector3(3f, 3f, 3f), new Vector3(0f, 45f, 0f), wallMat);
            CreateBlock(obstacles, "Barrier_Cyan", new Vector3(-40f, 0.5f, -35f), new Vector3(16f, 1f, 1f), new Vector3(0f, 30f, 0f), cyanMat);
            CreateBlock(obstacles, "Barrier_Orange", new Vector3(40f, 0.5f, -35f), new Vector3(16f, 1f, 1f), new Vector3(0f, -30f, 0f), orangeMat);

            spawnPoint = new GameObject("SpawnPoint").transform;
            spawnPoint.position = new Vector3(0f, 0.3f, -45f);
            spawnPoint.rotation = Quaternion.identity;
        }

        static GameObject CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale, Vector3 euler, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            block.isStatic = true;
            return block;
        }

        /// <summary>
        /// Rampa de un bloque inclinado. El extremo bajo queda a ras del piso y sube hacia
        /// la dirección indicada por yaw.
        /// </summary>
        static void CreateRamp(Transform parent, string name, Vector3 lowEndPosition, float yaw, float angle, float width, float length, Material material)
        {
            const float thickness = 0.6f;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-angle, 0f, 0f);
            Vector3 forward = rotation * Vector3.forward;
            Vector3 up = rotation * Vector3.up;
            Vector3 center = lowEndPosition + forward * (length * 0.5f) - up * (thickness * 0.5f);

            GameObject ramp = CreateBlock(parent, name, center, new Vector3(width, thickness, length), Vector3.zero, material);
            ramp.transform.rotation = rotation;
        }

        static Texture2D CreateGridTexture()
        {
            const int size = 128;
            const int line = 3;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            Color fill = DriftPaintPalette.Asphalt;
            Color border = DriftPaintPalette.Slate;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool edge = x < line || y < line || x >= size - line || y >= size - line;
                texture.SetPixel(x, y, edge ? border : fill);
            }
            texture.Apply();
            return texture;
        }

        static Texture2D CreateWhiteTexture()
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            texture.SetPixels(Enumerable.Repeat(Color.white, 64).ToArray());
            texture.Apply();
            return texture;
        }

        static CarCameraRig BuildCamera()
        {
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = DriftPaintPalette.SkyLavender;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            cameraGo.AddComponent<AudioListener>();
            cameraGo.transform.position = new Vector3(0f, 4f, -55f);
            return cameraGo.AddComponent<CarCameraRig>();
        }

        static SandboxHUD BuildHud()
        {
            EditorAssetUtility.SaveTexture(CreateWhiteTexture(), ArtFolder + "/T_White.png", true, TextureWrapMode.Clamp);
            Sprite white = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/T_White.png");

            var speedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/BarlowCondensed-SemiBold SDF.asset");
            var dataFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Oxanium-Medium SDF.asset");
            var labelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/BarlowCondensed-Medium SDF.asset");

            Canvas canvas = UiFactory.CreateOverlayCanvas(null, "HUD");
            Transform root = canvas.transform;
            Color panelColor = DriftPaintPalette.HudNavy.WithAlpha(0.85f);

            // Velocímetro
            Image speedPanel = UiFactory.CreatePanel(root, "Speedometer", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(260f, 170f), panelColor);
            Image speedBar = UiFactory.CreatePanel(speedPanel.transform, "SpeedBar", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 10f), DriftPaintPalette.Cyan);
            MakeFilled(speedBar, white);
            TextMeshProUGUI speedText = UiFactory.CreateText(speedPanel.transform, "SpeedValue", speedFont, 96f,
                TextAlignmentOptions.Center, DriftPaintPalette.OffWhite, FontStyles.Bold | FontStyles.Italic);
            ((RectTransform)speedText.transform).offsetMin = new Vector2(0f, 40f);
            speedText.text = "0";
            TextMeshProUGUI unit = UiFactory.CreateText(speedPanel.transform, "SpeedUnit", labelFont, 30f,
                TextAlignmentOptions.Bottom, DriftPaintPalette.OffWhite, FontStyles.Italic);
            ((RectTransform)unit.transform).offsetMin = new Vector2(0f, 18f);
            unit.text = "km/h";

            // Estados
            Image statusPanel = UiFactory.CreatePanel(root, "StatusPanel", Vector2.zero, Vector2.zero,
                Vector2.zero, new Vector2(40f, 40f), new Vector2(380f, 230f), panelColor);
            TextMeshProUGUI statusText = UiFactory.CreateText(statusPanel.transform, "StatusText", dataFont, 26f,
                TextAlignmentOptions.TopLeft, DriftPaintPalette.OffWhite);
            UiFactory.Stretch((RectTransform)statusText.transform, 18f);

            // Mediciones
            Image telemetryPanel = UiFactory.CreatePanel(root, "TelemetryPanel", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(560f, 170f), panelColor);
            TextMeshProUGUI telemetryText = UiFactory.CreateText(telemetryPanel.transform, "TelemetryText", dataFont, 24f,
                TextAlignmentOptions.TopLeft, DriftPaintPalette.OffWhite);
            UiFactory.Stretch((RectTransform)telemetryText.transform, 18f);

            // Ayuda de controles (texto fijo, se edita en la escena)
            Image helpPanel = UiFactory.CreatePanel(root, "HelpPanel", Vector2.one, Vector2.one,
                Vector2.one, new Vector2(-40f, -40f), new Vector2(430f, 455f), DriftPaintPalette.HudNavy.WithAlpha(0.7f));
            TextMeshProUGUI help = UiFactory.CreateText(helpPanel.transform, "HelpText", dataFont, 21f,
                TextAlignmentOptions.TopLeft, DriftPaintPalette.OffWhite);
            UiFactory.Stretch((RectTransform)help.transform, 16f);
            help.text =
                "<b>CONTROLES</b>\n" +
                "W / RT  acelerar\n" +
                "S / LT  frenar y marcha atrás\n" +
                "A D / stick izq.  girar\n" +
                "Shift / RB  drift (mantener)\n" +
                "Mouse / stick der.  panear cámara\n" +
                "C / R3  mirar atrás\n" +
                "R / Y (1 s)  reubicar auto\n\n" +
                "<b>PRUEBAS</b>\n" +
                "F1 stun   F2 slow\n" +
                "F3 boost de pintura on/off\n" +
                "F4 cambiar modelo   F5 volver al spawn\n" +
                "F6 cambiar equipo\n" +
                "Esc liberar / bloquear cursor";

            // Reubicar auto
            Image respawnPanel = UiFactory.CreatePanel(root, "RespawnPanel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(420f, 56f), panelColor);
            Image respawnFill = UiFactory.CreatePanel(respawnPanel.transform, "Fill", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, DriftPaintPalette.Orange);
            MakeFilled(respawnFill, white);
            respawnFill.fillAmount = 0f;
            TextMeshProUGUI respawnLabel = UiFactory.CreateText(respawnPanel.transform, "Label", labelFont, 28f,
                TextAlignmentOptions.Center, DriftPaintPalette.OffWhite, FontStyles.Bold | FontStyles.Italic);
            respawnLabel.text = "REUBICANDO AUTO";
            respawnPanel.gameObject.SetActive(false);

            var hud = canvas.gameObject.AddComponent<SandboxHUD>();
            var so = new SerializedObject(hud);
            so.FindProperty("_speedText").objectReferenceValue = speedText;
            so.FindProperty("_speedBar").objectReferenceValue = speedBar;
            so.FindProperty("_statusText").objectReferenceValue = statusText;
            so.FindProperty("_telemetryText").objectReferenceValue = telemetryText;
            so.FindProperty("_respawnRoot").objectReferenceValue = respawnPanel.gameObject;
            so.FindProperty("_respawnFill").objectReferenceValue = respawnFill;
            so.ApplyModifiedPropertiesWithoutUndo();
            return hud;
        }

        static void MakeFilled(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        static void AddSceneToBuildSettings(string path)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
