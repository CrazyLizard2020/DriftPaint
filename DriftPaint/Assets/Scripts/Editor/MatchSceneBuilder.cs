using System.Collections.Generic;
using System.Linq;
using DriftPaint.Cars;
using DriftPaint.Combat;
using DriftPaint.Level;
using DriftPaint.Match;
using DriftPaint.Paint;
using DriftPaint.Scoring;
using DriftPaint.Teams;
using DriftPaint.UI;
using DriftPaint.UI.Score;
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
    /// volumen de caÃ­da, cÃ¡mara, HUD de partida, plantilla de nombres flotantes y el controlador de partida.
    /// Todo queda como objetos de la escena para ajustarlo a mano.
    /// </summary>
    public static class MatchSceneBuilder
    {
        internal const string ScenePath = "Assets/Scenes/Match_DriftArena.unity";
        const string DataPath = ScriptableObjectPaths.CarData;
        const string PalettePath = ScriptableObjectPaths.TeamPalette;
        const string PrefabFolder = "Assets/Prefabs/Cars";
        const string ArtFolder = "Assets/Art/Generated";
        const string MaterialFolder = "Assets/Art/Materials";
        const float ArenaSize = MatchRules.ArenaSize;

        static Sprite _white;
        static TMP_FontAsset _titleFont;
        static TMP_FontAsset _labelFont;
        static TMP_FontAsset _bodyFont;

        /// <summary>Trazado de un mapa: lo que cambia entre escenas de partida. Todo lo demÃ¡s (HUD, sistemas, cÃ¡mara) es comÃºn.</summary>
        public class MapLayout
        {
            public string ScenePath;
            /// <summary>Arma piso, paredes, rampas y obstÃ¡culos bajo "Arena" y devuelve los spawns por equipo.</summary>
            public System.Func<List<TeamSpawnPoint>> BuildArena;
            /// <summary>Pickups de un lado del mapa; el otro lado se arma con su espejo (-x, -z).</summary>
            public Vector2[] PickupHalf;
        }

        static readonly MapLayout DriftArena = new MapLayout
        {
            ScenePath = ScenePath,
            BuildArena = BuildArena,
            PickupHalf = new[] { new Vector2(-24f, -40f), new Vector2(-48f, 20f), new Vector2(-20f, -12f) }
        };

        [MenuItem("DriftPaint/Generar escena de partida")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(DriftArena, NewSceneMode.Single);
        }

        [MenuItem("DriftPaint/Generar escena Royal Drift Road")]
        public static void BuildRoyalDriftRoadFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(RoyalDriftRoadMap.Layout, NewSceneMode.Single);
        }

        /// <summary>Falta la escena de Royal Drift Road.</summary>
        public static bool RoyalDriftRoadMissing() => !System.IO.File.Exists(RoyalDriftRoadMap.Layout.ScenePath);

        /// <summary>
        /// Crea la escena de Royal Drift Road al lado de las abiertas, sin cerrarlas ni tocarlas, y la guarda.
        /// Es la versiÃ³n que usa la puesta al dÃ­a automÃ¡tica.
        /// </summary>
        public static void BuildRoyalDriftRoad()
        {
            // Unity no deja crear una escena nueva al lado de otra sin título: en ese caso se reemplaza,
            // salvo que tenga cambios sin guardar, que no se tiran.
            Scene[] open = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            if (open.Any(s => string.IsNullOrEmpty(s.path)))
            {
                if (!Application.isBatchMode && open.Any(s => string.IsNullOrEmpty(s.path) && s.isDirty))
                {
                    Debug.LogWarning("Royal Drift Road se genera cuando no haya una escena sin título con cambios abierta.");
                    return;
                }
                Build(RoyalDriftRoadMap.Layout, NewSceneMode.Single);
                return;
            }

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = Build(RoyalDriftRoadMap.Layout, NewSceneMode.Additive);
            if (!scene.IsValid()) return;
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
        }

        static Scene Build(MapLayout layout, NewSceneMode mode)
        {
            TeamPaletteSO palette = EditorAssetUtility.LoadOrCreate(PalettePath, ScriptableObject.CreateInstance<TeamPaletteSO>);
            List<GameObject> prefabs = LoadCarPrefabs(palette);
            if (prefabs.Count == 0)
            {
                Debug.LogError("MatchSceneBuilder: no se pudo armar ningÃºn prefab de auto. RevisÃ¡ la consola.");
                return default;
            }

            LoadUiAssets();
            MenuSceneBuilder.LoadAssets();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            // Los generadores crean los objetos en la escena activa.
            SceneManager.SetActiveScene(scene);

            CarSandboxSceneBuilder.ConfigureLighting();
            List<TeamSpawnPoint> spawns = layout.BuildArena();
            CarCameraRig cameraRig = CarSandboxSceneBuilder.BuildCamera();

            var matchGo = new GameObject("Match");
            var match = matchGo.AddComponent<MatchController>();
            PaintSystem paint = matchGo.AddComponent<PaintSystem>();
            MatchScore score = matchGo.AddComponent<MatchScore>();
            MenuSceneBuilder.Bind(paint, ("_match", match));
            MenuSceneBuilder.Bind(score, ("_paint", paint), ("_match", match));
            MenuSceneBuilder.Bind(match, ("_score", score));
            BuildPaintSurface(paint, palette);
            MatchHUD hud = BuildHud(match, score, palette);
            BuildResults(match, score, palette);
            CarNameplate nameplate = BuildNameplateTemplate();
            BuildCombat(matchGo, match, paint, hud, palette, layout.PickupHalf);
            MenuSceneBuilder.BuildEventSystem();

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
            DisconnectIndicatorBuilder.Connect(spawner);

            nameplate.gameObject.SetActive(false);
            OptionsMenuBuilder.Install(scene);
            ArenaArtBuilder.Install(scene);
            PaintRingBuilder.Install(scene);
            SfxBuilder.InstallMatch(scene);

            EditorAssetUtility.EnsureFolder("Assets/Scenes");
            LocalizedInterfaceBuilder.Install(scene);
            EditorSceneManager.SaveScene(scene, layout.ScenePath);
            AddSceneToBuild(layout.ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Escena de partida generada en {layout.ScenePath}.");
            return scene;
        }

        static List<GameObject> LoadCarPrefabs(TeamPaletteSO palette)
        {
            List<GameObject> prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(p => p != null && p.GetComponent<CarController>() != null)
                .OrderBy(p => p.name, System.StringComparer.Ordinal)
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

            // El mapa es simÃ©trico respecto del centro: cada pieza de un lado tiene su espejo del otro.
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
            // Primero los del medio, asÃ­ con pocos jugadores arrancan centrados.
            float[] offsets = { -3f, 3f, -9f, 9f };
            for (int i = 0; i < offsets.Length; i++)
            {
                spawns.Add(CreateSpawn(spawnRoot, TeamId.A, i, new Vector3(offsets[i], 0.3f, -56f), 0f));
                spawns.Add(CreateSpawn(spawnRoot, TeamId.B, i, new Vector3(-offsets[i], 0.3f, 56f), 180f));
            }
            return spawns;
        }

        /// <summary>
        /// Plano apenas arriba del piso que muestra la textura de pintura. Usa recorte por alfa: las celdas
        /// vacÃ­as son transparentes y las pintadas opacas, sin costo de transparencia.
        /// </summary>
        static void BuildPaintSurface(PaintSystem paint, TeamPaletteSO palette)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/M_PaintSurface.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "M_PaintSurface" };
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.5f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                EditorAssetUtility.EnsureFolder(MaterialFolder);
                AssetDatabase.CreateAsset(material, MaterialFolder + "/M_PaintSurface.mat");
            }

            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surface.name = "Paint Surface";
            Object.DestroyImmediate(surface.GetComponent<Collider>());
            surface.transform.SetPositionAndRotation(new Vector3(0f, 0.02f, 0f), Quaternion.Euler(90f, 0f, 0f));
            surface.transform.localScale = new Vector3(ArenaSize, ArenaSize, 1f);

            var meshRenderer = surface.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            var view = surface.AddComponent<PaintSurfaceView>();
            MenuSceneBuilder.Bind(view, ("_paint", paint), ("_palette", palette));
        }

        #endregion

        #region Combate

        const string CombatPrefabFolder = "Assets/Prefabs/Combat";

        /// <summary>
        /// Disparos y barriles: los sistemas en el objeto Match, el prefab del barril, las plantillas
        /// inactivas de proyectil y explosiÃ³n, y los pickups del mapa (simÃ©tricos respecto del centro).
        /// </summary>
        static void BuildCombat(GameObject matchGo, MatchController match, PaintSystem paint, MatchHUD hud, TeamPaletteSO palette,
            Vector2[] pickupHalf)
        {
            Material fxMat = LoadOrCreateUnlitMaterial(MaterialFolder + "/M_CombatFx.mat");

            // Los valores ya editados en los assets se conservan al regenerar la escena.
            ProjectileSettingsSO projectileSettings = CombatSettingsBuilder.LoadOrCreateProjectileSettings();
            BarrelSettingsSO barrelSettings = CombatSettingsBuilder.LoadOrCreateBarrelSettings();

            GameObject barrelPrefab = BuildBarrelPrefab(barrelSettings);

            var fxRoot = new GameObject("Combat FX").transform;

            GameObject projectile = CreateVisual(fxRoot, "Projectile Template", PrimitiveType.Sphere, Vector3.zero,
                Vector3.one * 0.7f, fxMat);
            ProjectileView projectileView = projectile.AddComponent<ProjectileView>();
            BindRenderers(projectileView, "_tinted", projectile.GetComponent<Renderer>());
            projectile.SetActive(false);

            GameObject burst = CreateVisual(fxRoot, "Impact Template", PrimitiveType.Cylinder, Vector3.zero, Vector3.one, fxMat);
            ImpactBurst burstView = burst.AddComponent<ImpactBurst>();
            BindRenderers(burstView, "_tinted", burst.GetComponent<Renderer>());
            burst.SetActive(false);

            List<BarrelPickup> pickups = BuildPickups(pickupHalf);

            BarrelSystem barrels = matchGo.AddComponent<BarrelSystem>();
            WeaponSystem weapons = matchGo.AddComponent<WeaponSystem>();

            var so = new SerializedObject(barrels);
            MenuSceneBuilder.SetRef(so, "_match", match);
            MenuSceneBuilder.SetRef(so, "_paint", paint);
            MenuSceneBuilder.SetRef(so, "_barrelPrefab", barrelPrefab);
            MenuSceneBuilder.SetRef(so, "_palette", palette);
            MenuSceneBuilder.SetRef(so, "_settings", barrelSettings);
            SerializedProperty pickupsProp = so.FindProperty("_pickups");
            pickupsProp.arraySize = pickups.Count;
            for (int i = 0; i < pickups.Count; i++)
                pickupsProp.GetArrayElementAtIndex(i).objectReferenceValue = pickups[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            MenuSceneBuilder.Bind(weapons, ("_match", match), ("_paint", paint), ("_barrels", barrels), ("_hud", hud),
                ("_palette", palette), ("_settings", projectileSettings), ("_projectileTemplate", projectileView),
                ("_burstTemplate", burstView));
            AssetDatabase.SaveAssets();
        }

        /// <summary>Barril soltado: modelo con la banda del equipo y el aro del piso. Sin colisionador.</summary>
        static GameObject BuildBarrelPrefab(BarrelSettingsSO settings)
        {
            var root = new GameObject(BarrelSystem.PrefabId);
            root.AddComponent<BarrelView>();
            BarrelArtBuilder.DressBarrel(root, settings);

            EditorAssetUtility.EnsureFolder(CombatPrefabFolder);
            string path = CombatPrefabFolder + "/" + BarrelSystem.PrefabId + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static List<BarrelPickup> BuildPickups(Vector2[] half)
        {
            // Cada pickup tiene su espejo (-x, -z), igual que el resto del mapa.
            var root = new GameObject("Barrel Pickups").transform;
            var pickups = new List<BarrelPickup>();
            int id = 1;
            foreach (Vector2 p in half)
            {
                pickups.Add(CreatePickup(root, id++, new Vector3(p.x, 0f, p.y)));
                pickups.Add(CreatePickup(root, id++, new Vector3(-p.x, 0f, -p.y)));
            }
            return pickups;
        }

        static BarrelPickup CreatePickup(Transform parent, int id, Vector3 position)
        {
            var go = new GameObject($"Pickup_{id}");
            go.transform.SetParent(parent);
            go.transform.position = position;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            visual.localPosition = new Vector3(0f, 0.9f, 0f);
            BarrelArtBuilder.DressPickup(visual);
            visual.localRotation = Quaternion.Euler(0f, 0f, 20f);

            BarrelPickup pickup = go.AddComponent<BarrelPickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("_id").intValue = id;
            MenuSceneBuilder.SetRef(so, "_visual", visual.gameObject);
            so.ApplyModifiedPropertiesWithoutUndo();
            return pickup;
        }

        /// <summary>Primitiva solo visual: sin colisionador ni sombras, para que no afecte la fÃ­sica.</summary>
        static GameObject CreateVisual(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static void BindRenderers(Object target, string field, params Renderer[] renderers)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            prop.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Material sin iluminaciÃ³n para efectos: blanco, el color lo pone cada objeto.</summary>
        static Material LoadOrCreateUnlitMaterial(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                name = System.IO.Path.GetFileNameWithoutExtension(path)
            };
            material.SetColor("_BaseColor", Color.white);
            EditorAssetUtility.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        #endregion

        #region Spawns

        internal static TeamSpawnPoint CreateSpawn(Transform parent, byte team, int order, Vector3 position, float yaw)
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

        /// <summary>Conecta la salida disponible cuando el jugador libera el cursor durante la partida.</summary>
        internal static void ConnectLeaveButton(MatchHUD hud)
        {
            Button button = hud.transform.Find("Leave Match Button")?.GetComponent<Button>();
            if (button == null)
                button = MenuSceneBuilder.CreateButton(hud.transform, "Leave Match Button", "SALIR AL MENÚ",
                    MenuSceneBuilder.ButtonStyle.Outline, 32f, new Vector2(1f, 1f), new Vector2(-154f, -54f),
                    new Vector2(244f, 60f));
            MenuSceneBuilder.Bind(hud, ("_leaveMatchButton", button));
            button.gameObject.SetActive(false);
        }

        static MatchHUD BuildHud(MatchController match, MatchScore score, TeamPaletteSO palette)
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

            // Cartel de fin de tiempo y de desempate
            Image banner = UiFactory.CreatePanel(root, "Banner", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 80f), new Vector2(980f, 220f), panel);
            MenuSceneBuilder.Skew(banner, 10f);
            UiFactory.CreatePanel(banner.transform, "Stripe", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 10f), DriftPaintPalette.SunsetGold);
            TextMeshProUGUI bannerTitle = MenuSceneBuilder.CreateLabel(banner.transform, "Title", _titleFont, 120f, TextAlignmentOptions.Center,
                DriftPaintPalette.OffWhite, FontStyles.Bold | FontStyles.Italic, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -16f), new Vector2(900f, 130f));
            bannerTitle.text = "¡TIEMPO!";
            TextMeshProUGUI bannerSubtitle = MenuSceneBuilder.CreateLabel(banner.transform, "Subtitle", _labelFont, 36f, TextAlignmentOptions.Center,
                DriftPaintPalette.OffWhite.WithAlpha(0.75f), FontStyles.Italic, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 20f), new Vector2(900f, 50f));
            bannerSubtitle.text = "Contando celdas...";

            // VelocÃ­metro
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
            ConnectLeaveButton(hud);
            MenuSceneBuilder.Bind(hud,
                ("_match", match), ("_score", score),
                ("_leftPercentText", leftText), ("_rightPercentText", rightText), ("_clockText", clock),
                ("_countdownRoot", countdown.gameObject), ("_countdownText", countdownText),
                ("_bannerRoot", banner.gameObject), ("_bannerTitle", bannerTitle), ("_bannerSubtitle", bannerSubtitle),
                ("_speedText", speedText), ("_speedFill", speedFill),
                ("_fireCooldownText", fireText), ("_fireCooldownFill", fireFill), ("_barrelsText", barrels));
            AbilitiesLayoutBuilder.Connect(hud);

            BuildScoreboard(canvas, match, score, palette);

            banner.gameObject.SetActive(false);
            return hud;
        }

        /// <summary>Tabla que se abre con Tab durante la partida.</summary>
        static void BuildScoreboard(Canvas canvas, MatchController match, MatchScore score, TeamPaletteSO palette)
        {
            Image panel = UiFactory.CreatePanel(canvas.transform, "Scoreboard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1400f, 560f), DriftPaintPalette.HudNavy.WithAlpha(0.94f));
            MenuSceneBuilder.Skew(panel, 6f);
            UiFactory.CreatePanel(panel.transform, "Stripe", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 8f), DriftPaintPalette.SunsetGold);
            TextMeshProUGUI title = MenuSceneBuilder.CreateLabel(panel.transform, "Title", _titleFont, 54f, TextAlignmentOptions.Center,
                DriftPaintPalette.OffWhite, FontStyles.Bold | FontStyles.Italic, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -18f), new Vector2(800f, 70f));
            title.text = "PUNTAJE";

            ScoreTableView table = BuildScoreTable(panel.transform, "Table", palette, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -110f), new Vector2(1320f, 420f));

            var scoreboard = canvas.gameObject.AddComponent<ScoreboardPanel>();
            MenuSceneBuilder.Bind(scoreboard, ("_match", match), ("_score", score), ("_root", panel.gameObject), ("_table", table));
            panel.gameObject.SetActive(false);
        }

        #endregion

        #region Resultados

        static void BuildResults(MatchController match, MatchScore score, TeamPaletteSO palette)
        {
            Canvas canvas = UiFactory.CreateOverlayCanvas(null, "Results", 20);
            Image overlay = UiFactory.CreatePanel(canvas.transform, "Screen", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, DriftPaintPalette.HudNavy.WithAlpha(0.93f));
            overlay.raycastTarget = true;
            // El fondo cubre toda la pantalla; el contenido va en un Ã¡rea fija de 16:9 para que no se separe.
            Transform root = UiFactory.CreateContentRoot(overlay.transform);
            Vector2 top = new Vector2(0.5f, 1f);
            Vector2 bottom = new Vector2(0.5f, 0f);

            UiFactory.CreatePanel(overlay.transform, "Stripe", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 10f), DriftPaintPalette.SunsetGold);

            TextMeshProUGUI title = MenuSceneBuilder.CreateLabel(root, "Title", _titleFont, 150f, TextAlignmentOptions.Center,
                DriftPaintPalette.SunsetGold, FontStyles.Bold | FontStyles.Italic, top, top, new Vector2(0f, -36f), new Vector2(1400f, 170f));
            title.text = "¡VICTORIA!";
            TextMeshProUGUI winner = MenuSceneBuilder.CreateLabel(root, "Winner", _labelFont, 44f, TextAlignmentOptions.Center,
                DriftPaintPalette.OffWhite, FontStyles.Bold | FontStyles.Italic, top, top, new Vector2(0f, -200f), new Vector2(1200f, 60f));
            winner.text = "GANA EL EQUIPO NARANJA";

            TextMeshProUGUI leftPercent = BuildPercentBlock(root, "Left Percent", palette.Get(TeamId.B).Main, new Vector2(-300f, -280f));
            TextMeshProUGUI rightPercent = BuildPercentBlock(root, "Right Percent", palette.Get(TeamId.A).Main, new Vector2(300f, -280f));

            ScoreTableView table = BuildScoreTable(root, "Table", palette, top, top, new Vector2(0f, -420f), new Vector2(1500f, 400f));

            Button rematch = MenuSceneBuilder.CreateButton(root, "Rematch Button", "REVANCHA", MenuSceneBuilder.ButtonStyle.Primary, 46f,
                bottom, new Vector2(-210f, 110f), new Vector2(360f, 92f), new Vector2(0.5f, 0f));
            Button leave = MenuSceneBuilder.CreateButton(root, "Leave Button", "SALIR", MenuSceneBuilder.ButtonStyle.Outline, 46f,
                bottom, new Vector2(210f, 110f), new Vector2(360f, 92f), new Vector2(0.5f, 0f));
            TextMeshProUGUI timeout = MenuSceneBuilder.CreateLabel(root, "Timeout", _bodyFont, 28f, TextAlignmentOptions.Center,
                MenuSceneBuilder.MutedText, FontStyles.Normal, bottom, bottom, new Vector2(0f, 40f), new Vector2(800f, 44f));
            timeout.text = "Saliendo en 30 s";

            var screen = canvas.gameObject.AddComponent<ResultsScreen>();
            MenuSceneBuilder.Bind(screen,
                ("_match", match), ("_score", score), ("_palette", palette), ("_root", overlay.gameObject),
                ("_titleText", title), ("_winnerText", winner),
                ("_leftPercentText", leftPercent), ("_rightPercentText", rightPercent),
                ("_table", table), ("_rematchButton", rematch), ("_leaveButton", leave), ("_timeoutText", timeout));
            overlay.gameObject.SetActive(false);
        }

        static TextMeshProUGUI BuildPercentBlock(Transform parent, string name, Color color, Vector2 position)
        {
            Image block = UiFactory.CreatePanel(parent, name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                position, new Vector2(380f, 110f), color);
            MenuSceneBuilder.Skew(block, 14f);
            TextMeshProUGUI text = UiFactory.CreateText(block.transform, "Percent", _titleFont, 84f, TextAlignmentOptions.Center,
                DriftPaintPalette.HudNavy, FontStyles.Bold | FontStyles.Italic);
            text.text = "50%";
            return text;
        }

        #endregion

        #region Tabla de puntaje

        const float ScoreRowHeight = 60f;

        /// <summary>Tabla con una columna por equipo (izquierda Naranja, derecha Cyan) y una fila plantilla inactiva.</summary>
        static ScoreTableView BuildScoreTable(Transform parent, string name, TeamPaletteSO palette, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            RectTransform root = UiFactory.CreateRect(parent, name, anchor, anchor, pivot, position, size);
            const float gap = 60f;
            float columnWidth = (size.x - gap) * 0.5f;

            RectTransform leftRows = BuildScoreColumn(root, "Left Team", palette.Get(TeamId.B).Main, 0f, columnWidth, size.y,
                out TextMeshProUGUI leftHeader);
            RectTransform rightRows = BuildScoreColumn(root, "Right Team", palette.Get(TeamId.A).Main, columnWidth + gap, columnWidth, size.y,
                out TextMeshProUGUI rightHeader);
            ScoreRowView template = BuildScoreRow(root, columnWidth);

            var table = root.gameObject.AddComponent<ScoreTableView>();
            MenuSceneBuilder.Bind(table, ("_palette", palette), ("_leftColumn", leftRows), ("_rightColumn", rightRows),
                ("_leftHeader", leftHeader), ("_rightHeader", rightHeader), ("_rowTemplate", template));
            template.gameObject.SetActive(false);
            return table;
        }

        static RectTransform BuildScoreColumn(RectTransform table, string name, Color color, float x, float width, float height,
            out TextMeshProUGUI header)
        {
            RectTransform column = UiFactory.CreateRect(table, name, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(x, 0f), new Vector2(width, height));

            Image headerBack = UiFactory.CreatePanel(column, "Header", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 56f), color);
            MenuSceneBuilder.Skew(headerBack, 10f);
            header = UiFactory.CreateText(headerBack.transform, "Label", _titleFont, 36f, TextAlignmentOptions.Center,
                DriftPaintPalette.HudNavy, FontStyles.Bold | FontStyles.Italic);
            header.text = "EQUIPO";

            RectTransform rows = UiFactory.CreateRect(column, "Rows", Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            rows.offsetMin = Vector2.zero;
            rows.offsetMax = new Vector2(0f, -70f);
            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rows;
        }

        static ScoreRowView BuildScoreRow(RectTransform parent, float width)
        {
            RectTransform row = UiFactory.CreateRect(parent, "Row Template", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(width, ScoreRowHeight));

            Image highlight = UiFactory.CreatePanel(row, "Local Highlight", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, DriftPaintPalette.OffWhite);
            UiFactory.Stretch((RectTransform)highlight.transform, -3f);
            MenuSceneBuilder.Skew(highlight, 4f);

            Image background = UiFactory.CreatePanel(row, "Background", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, MenuSceneBuilder.RowColor);
            MenuSceneBuilder.Skew(background, 4f);

            Image car = UiFactory.CreatePanel(row, "Car Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(20f, 0f), new Vector2(54f, 24f), DriftPaintPalette.Cyan);
            MenuSceneBuilder.Skew(car, 10f);

            // Corona: base dorada con tres puntas.
            RectTransform crown = UiFactory.CreateRect(row, "Crown", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(86f, 0f), new Vector2(34f, 30f));
            UiFactory.CreatePanel(crown, "Base", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(0f, 12f), DriftPaintPalette.SunsetGold);
            foreach (float x in new[] { 0.12f, 0.5f, 0.88f })
            {
                Image tip = UiFactory.CreatePanel(crown, "Tip", new Vector2(x, 0f), new Vector2(x, 0f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 14f), new Vector2(11f, 11f), DriftPaintPalette.SunsetGold);
                tip.transform.localEulerAngles = new Vector3(0f, 0f, 45f);
            }

            TextMeshProUGUI name = UiFactory.CreateText(row, "Name", _titleFont, 32f, TextAlignmentOptions.Left,
                DriftPaintPalette.OffWhite, FontStyles.Bold | FontStyles.Italic);
            var nameRect = (RectTransform)name.transform;
            nameRect.offsetMin = new Vector2(132f, 8f);
            nameRect.offsetMax = new Vector2(-190f, 0f);
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.text = "Piloto";

            TextMeshProUGUI disconnected = MenuSceneBuilder.CreateLabel(row, "Disconnected", _labelFont, 20f, TextAlignmentOptions.Left,
                MenuSceneBuilder.MutedText, FontStyles.Italic, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(132f, 2f), new Vector2(240f, 22f));
            disconnected.text = "DESCONECTADO";

            TextMeshProUGUI points = MenuSceneBuilder.CreateLabel(row, "Points", _titleFont, 38f, TextAlignmentOptions.Right,
                DriftPaintPalette.OffWhite, FontStyles.Bold | FontStyles.Italic, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-24f, 0f), new Vector2(160f, 50f));
            points.text = "0";

            var view = row.gameObject.AddComponent<ScoreRowView>();
            MenuSceneBuilder.Bind(view, ("_carIcon", car), ("_nameText", name), ("_pointsText", points),
                ("_crown", crown.gameObject), ("_localHighlight", highlight.gameObject), ("_disconnectedMark", disconnected.gameObject));

            crown.gameObject.SetActive(false);
            highlight.gameObject.SetActive(false);
            disconnected.gameObject.SetActive(false);
            return view;
        }

        #endregion

        #region Nombres sobre los autos

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
            NameplateBuilder.Center(plate);
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
