using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DriftPaint.Cars;
using DriftPaint.Paint;
using DriftPaint.Teams;
using DriftPaint.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Saca las imágenes de la pantalla de carga y la miniatura de Graffithasia: un fondo, una imagen por
    /// paso de las instrucciones y la miniatura del selector. Cada toma arma su propia pintura de prueba,
    /// enciende los efectos que en el editor no corren solos (chorros de tinta, rociado del drift y del aro)
    /// y suma señalética sin palabras (flechas, marcadores, barra de porcentaje) para que se lea en cualquier
    /// idioma. Las imágenes se importan como sprites en la carpeta del mapa; la escena no se guarda.
    /// </summary>
    public static class LoadingScreenCapture
    {
        internal const string Folder = "Assets/Art/LoadingScreens/Graffithasia";
        internal const string Background = Folder + "/Graffithasia_Fondo.png";
        internal const string StepPaint = Folder + "/Graffithasia_Paso1_Pintar.png";
        internal const string StepRings = Folder + "/Graffithasia_Paso2_Aros.png";
        internal const string StepWin = Folder + "/Graffithasia_Paso3_Ganar.png";
        internal const string Thumbnail = Folder + "/Graffithasia_Miniatura.png";

        static readonly string[] All = { Background, StepPaint, StepRings, StepWin, Thumbnail };
        static readonly Color Ink = DriftPaintPalette.HudNavy;

        /// <summary>Faltan imágenes: se generan solas la primera vez que el editor abre el proyecto.</summary>
        internal static bool Missing() => All.Any(path => AssetDatabase.LoadAssetAtPath<Sprite>(path) == null);

        [MenuItem("DriftPaint/Arte/Capturas de pantalla de carga (Graffithasia)")]
        static void CaptureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try { Capture(null); }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

        /// <summary>Modo batch: -executeMethod DriftPaint.EditorTools.LoadingScreenCapture.RunFromCommandLine [-previewFolder carpeta]</summary>
        public static void RunFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-previewFolder");
            Capture(index >= 0 && index + 1 < args.Length ? args[index + 1] : null);
        }

        /// <summary>Con una carpeta de vista previa solo escribe ahí los PNG, sin tocar el proyecto.</summary>
        internal static void Capture(string previewFolder)
        {
            MenuSceneBuilder.LoadAssets();
            Scene scene = EditorSceneManager.OpenScene(MatchSceneBuilder.ScenePath, OpenSceneMode.Single);
            ArtCapture.Stage(scene, out Camera camera, out List<GameObject> cars);
            var palette = AssetDatabase.LoadAssetAtPath<TeamPaletteSO>(ScriptableObjectPaths.TeamPalette);
            Color colorA = palette != null ? palette.Get(TeamId.A).Main : DriftPaintPalette.Cyan;
            Color colorB = palette != null ? palette.Get(TeamId.B).Main : DriftPaintPalette.Orange;
            Color32 teamA = colorA, teamB = colorB;
            PaintRing[] rings = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PaintRing>(true)).ToArray();
            if (rings.Length == 0) throw new InvalidOperationException("La escena de Graffithasia no tiene aros.");
            Physics.SyncTransforms();

            string tempFolder = previewFolder ?? Path.Combine(Path.GetTempPath(), "DriftPaintCarga");
            Directory.CreateDirectory(tempFolder);
            var written = new List<(string temp, string asset)>();
            var disposable = new List<UnityEngine.Object>();
            var signs = new Signage(camera, disposable);

            void Shoot(string asset, int width, int height, Color32[] paint, Action<Signage> decorate = null)
            {
                Texture2D texture = Grid(paint);
                disposable.Add(texture);
                ArtCapture.ApplyPaint(scene, texture);
                string file = Path.Combine(tempFolder, Path.GetFileName(asset));
                if (decorate == null) ArtCapture.Render(camera, file, width, height);
                else
                {
                    Canvas canvas = signs.Begin(width, height);
                    decorate(signs);
                    ArtCapture.Render(camera, file, width, height, canvas);
                    signs.End();
                }
                written.Add((file, asset));
            }

            try
            {
                // Fondo: la arena entera desde la tribuna, con los dos equipos peleando el piso.
                PlaceCars(cars, (new Vector3(-6f, 0.3f, -30f), 20f), (new Vector3(10f, 0.3f, -12f), 210f), (new Vector3(-18f, 0.3f, -4f), 150f));
                foreach (GameObject car in cars) Effects.Drive(car, 0.8f, false);
                Look(camera, new Vector3(0f, 11f, -66f), new Vector3(0f, 0f, -6f), 52f);
                Shoot(Background, 1920, 1080, Territory(teamA, teamB, 0.5f, 11f, out _));

                // Paso 1: un auto derrapando, con su rastro en S sobre el piso limpio.
                Vector3 driftCar = ClearSpot(new Vector3(18f, 0.3f, -30f), 9f);
                const float driftYaw = 62f;
                Quaternion facing = Quaternion.Euler(0f, driftYaw, 0f);
                // La carrocería va cruzada respecto del rastro, como en un derrape.
                PlaceCars(cars, (driftCar, driftYaw + 28f), (driftCar + facing * new Vector3(-16f, 0f, 22f), 200f));
                Effects.Drive(cars[0], 1f, true);
                Effects.Drive(cars[1], 0.7f, false);
                Look(camera, driftCar + facing * new Vector3(-6.2f, 3.6f, 4.4f), driftCar + facing * new Vector3(0.4f, -0.4f, -6.5f), 54f);
                Shoot(StepPaint, 1280, 720, Trail(teamA, teamB, driftCar, driftYaw), s =>
                {
                    s.Pointer(Center(cars[0]) + Vector3.up * 1.3f, colorA, 64f);
                    for (int i = 0; i < 3; i++)
                    {
                        float along = 8f + i * 6.5f;
                        s.Chevron(TrailPoint(driftCar, facing, along), TrailPoint(driftCar, facing, along - 1.5f), colorA, 78f - i * 14f);
                    }
                });

                // Paso 2: un auto en el aire cruzando el aro, que gira y rocía su color alrededor.
                PaintRing ring = rings[0];
                Transform frame = ring.transform;
                Vector3 jump = frame.forward;
                Vector3 side = Vector3.Cross(Vector3.up, jump).normalized;
                PlaceCars(cars, (frame.position, 0f), (frame.position + jump * 17f + side * 9f - Vector3.up * 2.1f, 0f));
                cars[0].transform.rotation = Quaternion.LookRotation(jump) * Quaternion.Euler(-10f, 0f, 0f);
                // El centro visible del auto, no su pivote, tiene que quedar en el centro del aro.
                cars[0].transform.position += frame.position - Vector3.up * 0.35f - Center(cars[0]);
                cars[1].transform.rotation = Quaternion.LookRotation(-side + jump * 0.4f);
                Effects.Drive(cars[0], 1f, false, airborneJets: true);
                Effects.Drive(cars[1], 0.6f, false);
                Effects.Ring(ring, colorA);
                Look(camera, frame.position + side * 25f - jump * 6f + Vector3.up * 6.5f,
                    frame.position + jump * 0.5f - Vector3.up * 1.8f, 40f);
                Vector2 center = new Vector2(frame.position.x, frame.position.z);
                Transform ramp = new SerializedObject(ring).FindProperty("_ramp").objectReferenceValue as Transform;
                Shoot(StepRings, 1280, 720, RingSplash(teamA, teamB, center), s =>
                {
                    s.GroundRing(new Vector3(frame.position.x, 0.05f, frame.position.z), 11f, colorA);
                    if (ramp == null) return;
                    // Trayectoria del salto: de la punta de la rampa hasta el aro, en arco.
                    Vector3 lip = ramp.TransformPoint(new Vector3(0f, 0.5f, 0.5f)) + Vector3.up * 0.6f;
                    Vector3 Arc(float t) => Vector3.Lerp(lip, frame.position, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.2f;
                    for (int i = 0; i < 3; i++)
                    {
                        float t = 0.2f + i * 0.22f;
                        s.Chevron(Arc(t) - Vector3.up * 0.4f, Arc(t + 0.05f) - Vector3.up * 0.4f, Color.white, 58f + i * 8f);
                    }
                });

                // Paso 3: la arena vista desde arriba, con un equipo claramente adelante.
                PlaceCars(cars, (new Vector3(-12f, 0.3f, -20f), 30f), (new Vector3(14f, 0.3f, 4f), 200f), (new Vector3(-4f, 0.3f, 22f), 120f));
                foreach (GameObject car in cars) Effects.Drive(car, 1f, false);
                Look(camera, new Vector3(0f, 78f, -58f), new Vector3(0f, 0f, 2f), 50f);
                Color32[] territory = Territory(teamA, teamB, 0.85f, 3f, out float shareA);
                int percentA = Mathf.RoundToInt(shareA * 100f);
                Shoot(StepWin, 1280, 720, territory, s => s.ScoreBar(colorA, colorB, percentA));

                // Miniatura del selector: la arena entera, en formato de la tarjeta del lobby.
                PlaceCars(cars, (new Vector3(0f, 0.3f, -40f), 0f), (new Vector3(-7f, 0.3f, -24f), 12f), (new Vector3(6f, 0.3f, -16f), 200f));
                foreach (GameObject car in cars) Effects.Drive(car, 1f, false);
                Look(camera, new Vector3(0f, 9f, -64f), new Vector3(0f, 0f, -18f), 58f);
                Shoot(Thumbnail, 640, 400, Territory(teamA, teamB, 0.5f, 7f, out _));
            }
            finally
            {
                signs.End();
                foreach (UnityEngine.Object item in disposable)
                    if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }

            if (previewFolder != null)
            {
                Debug.Log($"Capturas de pantalla de carga en {previewFolder}");
                return;
            }

            EditorAssetUtility.EnsureFolder(Folder);
            foreach ((string temp, string asset) in written)
            {
                File.Copy(temp, asset, true);
                AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceUpdate);
                var importer = (TextureImporter)AssetImporter.GetAtPath(asset);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = false;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Capturas de pantalla de carga importadas en {Folder}.");
        }

        #region Escena

        static void PlaceCars(List<GameObject> cars, params (Vector3 position, float yaw)[] poses)
        {
            for (int i = 0; i < cars.Count; i++)
            {
                bool used = i < poses.Length;
                cars[i].SetActive(used);
                if (used) cars[i].transform.SetPositionAndRotation(poses[i].position, Quaternion.Euler(0f, poses[i].yaw, 0f));
            }
            Physics.SyncTransforms();
        }

        static void Look(Camera camera, Vector3 eye, Vector3 target, float fov)
        {
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
            camera.fieldOfView = fov;
        }

        /// <summary>El lugar libre de obstáculos más cercano al pedido, para que el auto no quede bajo una rampa.</summary>
        static Vector3 ClearSpot(Vector3 preferred, float radius)
        {
            var candidates = new List<Vector3>();
            for (float x = -48f; x <= 48f; x += 4f)
                for (float z = -48f; z <= 48f; z += 4f)
                    candidates.Add(new Vector3(x, preferred.y, z));
            foreach (Vector3 spot in candidates.OrderBy(c => (c - preferred).sqrMagnitude))
            {
                var hits = Physics.OverlapBox(spot + Vector3.up * 1.4f, new Vector3(radius, 0.9f, radius), Quaternion.identity,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
                if (hits.All(hit => hit.attachedRigidbody != null && hit.GetComponentInParent<CarController>() != null)) return spot;
            }
            return preferred;
        }

        /// <summary>Centro de la carrocería visible, sin partículas ni chorros.</summary>
        static Vector3 Center(GameObject car)
        {
            MeshRenderer[] body = car.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
            if (body.Length == 0) return car.transform.position;
            Bounds bounds = body[0].bounds;
            foreach (MeshRenderer part in body) bounds.Encapsulate(part.bounds);
            return bounds.center;
        }

        /// <summary>Punto del rastro en S a cierta distancia detrás del auto, igual que el que se pinta.</summary>
        static Vector3 TrailPoint(Vector3 car, Quaternion facing, float along)
        {
            float curve = Mathf.Sin(along * 0.16f) * 3.2f * Mathf.Clamp01(along / 6f);
            return car + facing * new Vector3(curve, 0f, -along) + Vector3.up * 0.1f;
        }

        #endregion

        #region Efectos

        /// <summary>Pone en pantalla los efectos que en partida maneja CarPaintEffects y que en el editor no corren.</summary>
        static class Effects
        {
            public static void Drive(GameObject car, float flow, bool drifting, bool airborneJets = false)
            {
                var effects = car.GetComponent<CarPaintEffects>();
                var color = car.GetComponent<CarTeamColor>();
                if (effects == null || color == null) return;
                var art = DrivingArtProfile.Active;
                var data = new SerializedObject(effects);
                T Get<T>(string field) where T : UnityEngine.Object => data.FindProperty(field).objectReferenceValue as T;
                TeamPaletteSO.TeamColors tint = color.Colors;

                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", tint.Main);
                block.SetFloat("_StreamWidth", art != null ? (drifting ? art.streamWidths.z : art.streamWidths.y) : 1f);
                block.SetFloat("_TurboGlow", art != null ? art.accelerationEmission * flow * 0.6f : 0.3f);
                float reach = Mathf.Sqrt(flow);
                float seed = 0f;
                foreach (string side in new[] { "_leftStream", "_rightStream" })
                {
                    MeshRenderer stream = Get<MeshRenderer>(side);
                    ParticleSystem drops = Get<ParticleSystem>(side == "_leftStream" ? "_leftJet" : "_rightJet");
                    if (stream == null) continue;
                    Transform nozzle = stream.transform;
                    float length = 1.38f * reach;
                    Vector3 end = nozzle.TransformPoint(Vector3.forward * length);
                    Vector3 landing = new Vector3(end.x, car.transform.position.y - 0.29f, end.z);
                    float attached = airborneJets ? 0f : 1f;
                    Vector3 tip = nozzle.InverseTransformPoint(Vector3.Lerp(end, landing, attached));
                    block.SetVector("_ExhaustShape", new Vector4(reach, tip.x, tip.y, tip.z));
                    block.SetVector("_JetFlow", new Vector4(flow, 0.35f + seed, seed, attached));
                    stream.SetPropertyBlock(block);
                    stream.enabled = true;
                    seed += 0.37f;
                    if (drops != null && !airborneJets) Splash(drops, landing, nozzle, flow, tint);
                }

                ParticleSystem drift = Get<ParticleSystem>("_drift");
                if (drift != null && drifting)
                {
                    var main = drift.main;
                    main.startColor = new ParticleSystem.MinMaxGradient(tint.Main, tint.Glow);
                    var emission = drift.emission;
                    emission.enabled = true;
                    emission.rateOverTime = art != null ? art.driftSprayRate * 2f : 50f;
                    drift.transform.localRotation = Quaternion.Euler(-30f, 180f + 40f, 0f);
                    drift.Simulate(0.7f, true, true);
                }
            }

            /// <summary>Gotas saltando del punto donde el chorro golpea el piso.</summary>
            static void Splash(ParticleSystem drops, Vector3 impact, Transform nozzle, float flow, TeamPaletteSO.TeamColors tint)
            {
                var main = drops.main;
                main.startColor = new ParticleSystem.MinMaxGradient(tint.Main, tint.Glow);
                drops.Clear();
                var random = new System.Random(drops.GetInstanceID());
                float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
                for (int i = 0; i < 18; i++)
                {
                    drops.Emit(new ParticleSystem.EmitParams
                    {
                        position = impact + Vector3.up * 0.02f,
                        velocity = nozzle.forward * Range(1.2f, 3.4f) * (0.5f + flow) + Vector3.up * Range(0.9f, 2.4f) * (0.4f + flow)
                                   + nozzle.right * Range(-1.6f, 1.6f) * flow,
                        startLifetime = Range(0.25f, 0.4f),
                        startSize = Range(0.05f, 0.1f) * (0.6f + 0.6f * flow)
                    }, 1);
                }
                drops.Simulate(0.12f, true, false);
            }

            /// <summary>El aro activado: corona y cargas del color del equipo, girando y rociando pintura.</summary>
            public static void Ring(PaintRing ring, Color color)
            {
                var settings = AssetDatabase.LoadAssetAtPath<PaintRingSettings>(ScriptableObjectPaths.PaintRings);
                var data = new SerializedObject(ring);
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", color);
                block.SetColor("_EmissionColor", color * 0.65f);
                if (data.FindProperty("_rim").objectReferenceValue is Renderer rim) rim.SetPropertyBlock(block);
                var lampBlock = new MaterialPropertyBlock();
                lampBlock.SetColor("_BaseColor", color);
                lampBlock.SetColor("_EmissionColor", color * (DrivingArtProfile.Active != null ? DrivingArtProfile.Active.ringScreenEmission : 1.5f));
                SerializedProperty lamps = data.FindProperty("_lamps");
                for (int i = 0; i < lamps.arraySize; i++)
                    if (lamps.GetArrayElementAtIndex(i).objectReferenceValue is Renderer lamp)
                    {
                        lamp.enabled = true;
                        lamp.SetPropertyBlock(lampBlock);
                    }
                if (data.FindProperty("_rotor").objectReferenceValue is Transform rotor)
                    rotor.localRotation = Quaternion.Euler(0f, 38f, 0f);
                if (data.FindProperty("_spray").objectReferenceValue is ParticleSystem spray)
                {
                    spray.transform.localRotation = Quaternion.Euler(0f, 38f, 0f);
                    var main = spray.main;
                    main.startColor = color;
                    main.startSpeed = settings != null ? settings.reach * 0.6f : 9f;
                    var shape = spray.shape;
                    shape.radius = (settings != null ? settings.diameter : 8f) * 0.5f;
                    var emission = spray.emission;
                    emission.rateOverTime = 80f;
                    spray.Simulate(1.4f, true, true);
                }
            }
        }

        #endregion

        #region Señalética

        /// <summary>
        /// Lienzo que se compone sobre la toma: flechas, marcadores y barras ubicados sobre puntos del mundo.
        /// Todo es forma o número, así la imagen sirve en cualquier idioma.
        /// </summary>
        sealed class Signage
        {
            readonly Camera _scene;
            readonly List<UnityEngine.Object> _disposable;
            Sprite _disc, _ring, _triangle, _chevron;
            Canvas _canvas;
            Camera _ui;
            int _sceneMask;
            Vector2 _size;

            public Signage(Camera scene, List<UnityEngine.Object> disposable)
            {
                _scene = scene;
                _disposable = disposable;
            }

            public Canvas Begin(int width, int height)
            {
                End();
                _size = new Vector2(width, height);
                _disc ??= Shape(128, 128, (x, y) => 0.5f - Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.5f)));
                _ring ??= Shape(512, 512, (x, y) => 0.012f - Mathf.Abs(Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.5f)) - 0.48f));
                _triangle ??= Shape(128, 128, (x, y) => Mathf.Min(y - 0.08f, (0.92f - y) * 0.5f - Mathf.Abs(x - 0.5f) + 0.0f) * 0.7f);
                _chevron ??= Shape(128, 128, (x, y) =>
                {
                    float arm = Mathf.Abs(y - 0.5f);
                    // Punta a la derecha: con ángulo 0 señala hacia +x.
                    float center = 0.68f - arm * 0.9f;
                    return Mathf.Min(0.11f - Mathf.Abs(x - center), 0.42f - arm) * 0.5f;
                });

                _ui = new GameObject("Cámara de señalética", typeof(Camera)).GetComponent<Camera>();
                _disposable.Add(_ui.gameObject);
                _ui.CopyFrom(_scene);
                int layer = LayerMask.NameToLayer("UI");
                _ui.cullingMask = 1 << layer;
                _sceneMask = _scene.cullingMask;
                _scene.cullingMask &= ~_ui.cullingMask;
                _ui.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                _ui.allowHDR = false;

                _canvas = new GameObject("Señalética", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
                _disposable.Add(_canvas.gameObject);
                _canvas.gameObject.layer = layer;
                _canvas.renderMode = RenderMode.ScreenSpaceCamera;
                _canvas.worldCamera = _ui;
                _canvas.planeDistance = _ui.nearClipPlane + 0.1f;
                var scaler = _canvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = _size;
                scaler.matchWidthOrHeight = 0.5f;
                return _canvas;
            }

            public void End()
            {
                if (_canvas != null) UnityEngine.Object.DestroyImmediate(_canvas.gameObject);
                if (_ui != null)
                {
                    UnityEngine.Object.DestroyImmediate(_ui.gameObject);
                    _scene.cullingMask = _sceneMask;
                }
                _canvas = null;
                _ui = null;
            }

            /// <summary>Posición en el lienzo (píxeles desde abajo a la izquierda) de un punto del mundo.</summary>
            Vector2 Screen(Vector3 world)
            {
                Vector3 viewport = _scene.WorldToViewportPoint(world);
                return new Vector2(viewport.x * _size.x, viewport.y * _size.y);
            }

            Image Place(Sprite sprite, Vector2 position, Vector2 size, Color color, float angle = 0f, bool outline = true)
            {
                var rect = new GameObject(sprite.name, typeof(RectTransform)).GetComponent<RectTransform>();
                rect.gameObject.layer = _canvas.gameObject.layer;
                rect.SetParent(_canvas.transform, false);
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
                rect.localRotation = Quaternion.Euler(0f, 0f, angle);
                var image = rect.gameObject.AddComponent<Image>();
                image.sprite = sprite;
                image.color = color;
                if (outline)
                {
                    // Contorno de tinta y sombra corrida, como las viñetas de historieta del juego.
                    var border = rect.gameObject.AddComponent<Outline>();
                    border.effectColor = Ink;
                    border.effectDistance = new Vector2(4f, -4f);
                    var shadow = rect.gameObject.AddComponent<Shadow>();
                    shadow.effectColor = Ink.WithAlpha(0.55f);
                    shadow.effectDistance = new Vector2(8f, -8f);
                }
                return image;
            }

            /// <summary>Triángulo que señala un auto desde arriba.</summary>
            public void Pointer(Vector3 world, Color color, float size)
            {
                Vector2 at = Screen(world) + Vector2.up * size * 0.6f;
                Place(_triangle, at, new Vector2(size, size), color, 180f);
            }

            /// <summary>Flecha en V sobre el piso que marca el sentido del recorrido.</summary>
            public void Chevron(Vector3 world, Vector3 toward, Color color, float size)
            {
                Vector2 from = Screen(world), to = Screen(toward);
                float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
                Place(_chevron, from, new Vector2(size, size), color, angle);
            }

            /// <summary>Círculo sobre el piso: se proyecta su contorno y se encierra en una elipse.</summary>
            public void GroundRing(Vector3 center, float radius, Color color)
            {
                Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
                for (int i = 0; i < 48; i++)
                {
                    float a = i * Mathf.PI * 2f / 48f;
                    Vector3 world = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                    if (_scene.WorldToViewportPoint(world).z <= 0f) continue;
                    Vector2 point = Screen(world);
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
                Place(_ring, (min + max) * 0.5f, max - min, Color.white);
                Place(_ring, (min + max) * 0.5f, (max - min) * 0.985f, color, 0f, false);
            }

            /// <summary>Barra de porcentaje como la del HUD, con el equipo que va ganando marcado.</summary>
            public void ScoreBar(Color colorA, Color colorB, int percentA)
            {
                const float width = 760f, height = 74f;
                Vector2 top = new Vector2(_size.x * 0.5f, _size.y - 70f);
                float leftWidth = width * percentA / 100f;
                Vector2 left = top + Vector2.left * (width * 0.5f - leftWidth * 0.5f);
                Vector2 right = top + Vector2.right * (leftWidth * 0.5f);
                Image a = Place(_disc, left, new Vector2(leftWidth, height), colorA);
                a.sprite = MenuSceneBuilder.WhiteSprite;
                Image b = Place(_disc, right, new Vector2(width - leftWidth, height), colorB);
                b.sprite = MenuSceneBuilder.WhiteSprite;
                Number(a.transform, percentA + "%", Ink);
                Number(b.transform, (100 - percentA) + "%", Color.white);
                Place(_triangle, left + Vector2.down * (height * 0.5f + 40f), new Vector2(58f, 58f), DriftPaintPalette.SunsetGold, 0f);
            }

            void Number(Transform parent, string value, Color color)
            {
                TextMeshProUGUI text = UiFactory.CreateText(parent, "Valor", MenuSceneBuilder.TitleFont, 58f,
                    TextAlignmentOptions.Center, color, FontStyles.Bold | FontStyles.Italic);
                text.gameObject.layer = _canvas.gameObject.layer;
                text.text = value;
            }

            /// <summary>Sprite blanco con borde suave a partir de una distancia con signo (positiva adentro).</summary>
            Sprite Shape(int width, int height, Func<float, float, float> inside)
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                var pixels = new Color32[width * height];
                float soft = 1.5f / Mathf.Max(width, height);
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        float d = inside((x + 0.5f) / width, (y + 0.5f) / height);
                        byte alpha = (byte)(Mathf.Clamp01(d / soft + 0.5f) * 255f);
                        pixels[y * width + x] = new Color32(255, 255, 255, alpha);
                    }
                texture.SetPixels32(pixels);
                texture.Apply();
                var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
                sprite.hideFlags = HideFlags.HideAndDontSave;
                _disposable.Add(texture);
                _disposable.Add(sprite);
                return sprite;
            }
        }

        #endregion

        #region Pintura de prueba

        static Texture2D Grid(Color32[] pixels)
        {
            var texture = new Texture2D(PaintGrid.Size, PaintGrid.Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        static Vector2 World(int x, int z) => new Vector2(x + 0.5f + PaintGrid.Origin, z + 0.5f + PaintGrid.Origin);

        static Color32[] Fill(Func<Vector2, float, Color32> cell)
        {
            int size = PaintGrid.Size;
            var pixels = new Color32[size * size];
            for (int z = 0; z < size; z++)
                for (int x = 0; x < size; x++)
                    pixels[z * size + x] = cell(World(x, z), Mathf.PerlinNoise(x * 0.11f + 5.3f, z * 0.11f + 1.7f));
            return pixels;
        }

        /// <summary>Manchas grandes de los dos equipos. share es cuánto del piso pintado se lleva el equipo A.</summary>
        static Color32[] Territory(Color32 a, Color32 b, float share, float seed, out float shareA)
        {
            float split = Mathf.Lerp(0.72f, 0.38f, share);
            Color32[] pixels = Fill((p, detail) =>
            {
                float n = Mathf.PerlinNoise(p.x * 0.045f + seed, p.y * 0.045f + seed * 0.7f) * 0.8f + detail * 0.2f;
                if (n > split + 0.09f) return a;
                if (n < split - 0.17f) return b;
                return default;
            });
            int countA = pixels.Count(c => c.a > 0 && c.r == a.r && c.g == a.g && c.b == a.b);
            int countB = pixels.Count(c => c.a > 0 && c.r == b.r && c.g == b.g && c.b == b.b);
            shareA = countA + countB > 0 ? (float)countA / (countA + countB) : 0.5f;
            return pixels;
        }

        /// <summary>Rastro en S detrás del auto y algunas manchas sueltas lejos.</summary>
        static Color32[] Trail(Color32 a, Color32 b, Vector3 car, float yaw)
        {
            Quaternion toLocal = Quaternion.Euler(0f, -yaw, 0f);
            return Fill((p, detail) =>
            {
                Vector3 local = toLocal * (new Vector3(p.x, 0f, p.y) - car);
                float along = -local.z;
                float curve = Mathf.Sin(along * 0.16f) * 3.2f * Mathf.Clamp01(along / 6f);
                float width = 1.25f + detail * 0.9f;
                if (along > -0.6f && along < 34f && Mathf.Abs(local.x - curve) < width) return a;
                float n = Mathf.PerlinNoise(p.x * 0.05f + 21f, p.y * 0.05f + 4f);
                float distance = new Vector2(local.x, local.z).magnitude;
                if (distance > 16f && n > 0.72f) return a;
                if (distance > 16f && n < 0.24f) return b;
                return default;
            });
        }

        /// <summary>El círculo que pinta el aro activado, con su borde irregular, y el rival más lejos.</summary>
        static Color32[] RingSplash(Color32 a, Color32 b, Vector2 center)
        {
            return Fill((p, detail) =>
            {
                float d = (p - center).magnitude;
                if (d < 9.5f + detail * 3.5f) return a;
                float n = Mathf.PerlinNoise(p.x * 0.05f + 9f, p.y * 0.05f + 13f);
                if (d > 20f && n < 0.28f) return b;
                if (d > 20f && n > 0.74f) return a;
                return default;
            });
        }

        #endregion
    }
}
