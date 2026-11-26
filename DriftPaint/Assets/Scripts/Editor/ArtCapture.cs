using System.Collections.Generic;
using System.IO;
using System.Linq;
using DriftPaint.Cars;
using DriftPaint.Paint;
using DriftPaint.Teams;
using DriftPaint.Match;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Saca capturas a 1920 x 1080 de las escenas de partida desde puntos de vista fijos, con autos de los dos
    /// equipos y pintura de prueba sobre el piso. Sirve para comparar el arte contra los mockups antes y
    /// después de cada cambio, siempre con el mismo encuadre. Las imágenes quedan en la carpeta Captures del
    /// proyecto, fuera de Assets, y la escena no se guarda.
    /// </summary>
    public static class ArtCapture
    {
        const int Width = 1920, Height = 1080;

        /// <summary>Encuadre de una captura: dónde está el auto propio y desde dónde lo mira la cámara.</summary>
        readonly struct Shot
        {
            public readonly string Name;
            public readonly Vector3 Car;
            public readonly float Yaw;
            public readonly float Distance, Height, Fov;

            public Shot(string name, Vector3 car, float yaw, float distance, float height, float fov)
            {
                Name = name;
                Car = car;
                Yaw = yaw;
                Distance = distance;
                Height = height;
                Fov = fov;
            }
        }

        static readonly Shot[] Shots =
        {
            // Como el mockup: detrás del auto, mirando hacia la rampa y las tribunas.
            new Shot("persecucion", new Vector3(-30f, 0.3f, -40f), 35f, 5.2f, 2.1f, 56f),
            new Shot("rampa", new Vector3(-36f, 0.3f, -30f), 0f, 6.5f, 2.6f, 58f),
            new Shot("panoramica", new Vector3(0f, 0.3f, -50f), 0f, 14f, 9f, 60f),
        };

        static readonly string[] Scenes = { MatchSceneBuilder.ScenePath, RoyalDriftRoadMap.ScenePath };
        static readonly Vector3 MockupCar = new Vector3(-7.5f, .11f, -42.4f);
        const float MockupYaw = -14;
        const int MockupPair = 4;

        [MenuItem("DriftPaint/Arte/Capturas de referencia")]
        static void CaptureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try { EditorUtility.RevealInFinder(Capture("editor")); }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

        /// <summary>Entrada para Unity en modo batch: -executeMethod DriftPaint.EditorTools.ArtCapture.RunFromCommandLine -captureLabel antes</summary>
        public static void RunFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int index = System.Array.IndexOf(args, "-captureLabel");
            string label = index >= 0 && index + 1 < args.Length ? args[index + 1] : "batch";
            Capture(label);
        }

        static string Capture(string label)
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", label));
            Directory.CreateDirectory(folder);
            foreach (string scenePath in Scenes)
            {
                if (!File.Exists(scenePath)) continue;
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                string sceneName = Path.GetFileNameWithoutExtension(scenePath);
                Stage(scene, out Camera camera, out List<GameObject> cars);
                var palette = AssetDatabase.LoadAssetAtPath<TeamPaletteSO>(ScriptableObjectPaths.TeamPalette);
                var paint = PaintTestPattern(scene, palette);
                try
                {
                    foreach (Shot shot in Shots)
                    {
                        Place(cars, shot);
                        Frame(camera, shot);
                        string file = Path.Combine(folder, $"{sceneName}_{shot.Name}.png");
                        Render(camera, file);
                        Debug.Log($"Captura guardada en {file}");
                    }
                    if (scenePath == MatchSceneBuilder.ScenePath)
                        CaptureMockup(scene, camera, cars, folder);
                }
                finally { Object.DestroyImmediate(paint); }
            }
            return folder;
        }

        /// <summary>Prepara la escena abierta: pintura de prueba, tres autos y la cámara sin su seguimiento.</summary>
        internal static void Stage(Scene scene, out Camera camera, out List<GameObject> cars)
        {
            camera = scene.GetRootGameObjects().Select(root => root.GetComponentInChildren<Camera>(true))
                          .FirstOrDefault(c => c != null && c.CompareTag("MainCamera"));
            if (camera == null) throw new System.InvalidOperationException("La escena no tiene cámara principal: " + scene.path);
            if (camera.TryGetComponent<CarCameraRig>(out var rig)) rig.enabled = false;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var stand in root.GetComponentsInChildren<DriftPaint.Level.CrowdStand>())
                    stand.Rebuild();

            string[] models = { "Car_RX7", "Car_AE86", "Car_Gc14" };
            byte[] teams = { TeamId.A, TeamId.A, TeamId.B };
            cars = new List<GameObject>();
            for (int i = 0; i < models.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CarPrefabBuilder.PrefabFolder}/{models[i]}.prefab");
                if (prefab == null) continue;
                var car = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                if (car.TryGetComponent(out CarTeamColor color)) color.SetTeam(teams[i]);
                cars.Add(car);
            }
        }

        static void Place(List<GameObject> cars, Shot shot)
        {
            Quaternion facing = Quaternion.Euler(0f, shot.Yaw, 0f);
            // El auto propio adelante y los otros dos más lejos, a los costados, como en el mockup.
            Vector3[] offsets = { Vector3.zero, new Vector3(-7f, 0f, 16f), new Vector3(6f, 0f, 24f) };
            float[] turns = { 0f, 12f, 200f };
            for (int i = 0; i < cars.Count; i++)
            {
                cars[i].transform.SetPositionAndRotation(shot.Car + facing * offsets[i],
                                                         facing * Quaternion.Euler(0f, turns[i], 0f));
            }
        }

        static void Frame(Camera camera, Shot shot)
        {
            Quaternion facing = Quaternion.Euler(0f, shot.Yaw, 0f);
            Vector3 target = shot.Car + Vector3.up * 0.9f;
            Vector3 eye = shot.Car + facing * new Vector3(0f, shot.Height, -shot.Distance);
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target + facing * Vector3.forward * 9f - eye));
            camera.fieldOfView = shot.Fov;
        }

        static void Render(Camera camera, string file, Canvas hud = null) => Render(camera, file, Width, Height, hud);

        internal static void Render(Camera camera, string file, int width, int height, Canvas hud = null)
        {
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.DefaultHDR);
            var display = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RawImage plate = null;
            Texture2D image = null;
            try
            {
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                // Dos cuadros: el primero calienta los efectos que dependen del cuadro anterior.
                var request = new RenderPipeline.StandardRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderPipeline.SubmitRenderRequest(camera, request);
                if (hud == null) Graphics.Blit(target, display);
                else
                {
                    // Componer la escena ya revelada detrás del HUD, igual que el Canvas overlay de la partida.
                    plate = new GameObject("Fondo de captura", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                    plate.gameObject.layer = hud.gameObject.layer;
                    plate.rectTransform.SetParent(hud.transform, false);
                    plate.rectTransform.SetAsFirstSibling();
                    plate.rectTransform.anchorMin = Vector2.zero;
                    plate.rectTransform.anchorMax = Vector2.one;
                    plate.rectTransform.offsetMin = plate.rectTransform.offsetMax = Vector2.zero;
                    plate.texture = target;
                    var hudCamera = hud.worldCamera;
                    hudCamera.targetTexture = display;
                    hudCamera.clearFlags = CameraClearFlags.SolidColor;
                    Canvas.ForceUpdateCanvases();
                    RenderPipeline.SubmitRenderRequest(hudCamera, new RenderPipeline.StandardRequest { destination = display });
                    hudCamera.targetTexture = null;
                }
                RenderTexture.active = display;
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(file, image.EncodeToPNG());
            }
            finally
            {
                if (plate != null) Object.DestroyImmediate(plate.gameObject);
                if (image != null) Object.DestroyImmediate(image);
                if (hud != null && hud.worldCamera != null) hud.worldCamera.targetTexture = null;
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                RenderTexture.ReleaseTemporary(display);
            }
        }

        static void CaptureMockup(Scene scene, Camera camera, List<GameObject> cars, string folder)
        {
            var palette = AssetDatabase.LoadAssetAtPath<TeamPaletteSO>(ScriptableObjectPaths.TeamPalette);
            var car = cars.Single(c => c.name.StartsWith("Car_AE86"));
            foreach (var other in cars) other.SetActive(other == car);
            var facing = Quaternion.Euler(0, MockupYaw, 0);
            car.transform.SetPositionAndRotation(MockupCar, facing);
            car.GetComponent<CarTeamColor>().PreviewTeam(TeamId.A, MockupPair);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var stand in root.GetComponentsInChildren<DriftPaint.Level.CrowdStand>())
                    stand.Rebuild(MockupPair);

            var view = Quaternion.Euler(0, 32, 0);
            camera.transform.SetPositionAndRotation(MockupCar + view * new Vector3(-.12f, 1.61f, -4.50f),
                                                   Quaternion.Euler(5.6f, 32, 0));
            camera.fieldOfView = 52;
            var hud = scene.GetRootGameObjects().Select(root => root.GetComponent<MatchHUD>()).First(h => h != null);
            hud.ApplyArt();
            var canvas = hud.GetComponent<Canvas>();
            var uiCamera = new GameObject("Cámara HUD de captura", typeof(Camera)).GetComponent<Camera>();
            uiCamera.CopyFrom(camera);
            uiCamera.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
            int uiLayer = LayerMask.NameToLayer("UI");
            foreach (Transform element in hud.GetComponentsInChildren<Transform>(true))
                element.gameObject.layer = uiLayer;
            uiCamera.cullingMask = 1 << uiLayer;
            camera.cullingMask &= ~uiCamera.cullingMask;
            var uiData = uiCamera.GetUniversalAdditionalCameraData();
            uiData.renderPostProcessing = false;
            uiCamera.allowHDR = false;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = camera.nearClipPlane + .1f;
            foreach (Transform child in hud.transform)
                child.gameObject.SetActive(child.name == "Score Bar" || child.name == "Abilities" || child.name == "Speedometer");
            void Label(string path, string value) => hud.transform.Find(path).GetComponent<TMP_Text>().text = value;
            Label("Score Bar/Clock", "01:53");
            Label("Score Bar/Left Team/Percent", "0%");
            Label("Score Bar/Right Team/Percent", "100%");
            Label("Speedometer/Value", "0");
            Label("Abilities/Barrels", "x3");
            var cooldown = new SerializedObject(hud).FindProperty("_fireCooldownText").objectReferenceValue as TMP_Text;
            cooldown.text = "0.4 s";
            foreach (var side in new[] { ("Left Team", TeamId.B), ("Right Team", TeamId.A) })
            {
                var panel = hud.transform.Find("Score Bar/" + side.Item1);
                panel.GetComponent<Image>().color = palette.Get(side.Item2, MockupPair).Main;
                panel.GetComponentInChildren<TMP_Text>().color = side.Item2 == TeamId.B ? Color.white : UI.DriftPaintPalette.HudNavy;
            }
            hud.transform.Find("Speedometer/Speed Bar").GetComponent<Image>().fillAmount = 0;
            var paint = PaintTestPattern(scene, palette, true);
            try { Render(camera, Path.Combine(folder, "Match_DriftArena_mockup.png"), canvas); }
            finally
            {
                Object.DestroyImmediate(paint);
                Object.DestroyImmediate(uiCamera.gameObject);
            }
        }

        /// <summary>
        /// Manchas de los dos equipos sobre la grilla, con bordes de celda como los que confirma la partida.
        /// Se ponen con un bloque de propiedades, igual que la textura real, sin tocar el material.
        /// </summary>
        static Texture2D PaintTestPattern(Scene scene, TeamPaletteSO palette, bool mockup = false)
        {
            Color32 a = palette != null ? (Color32)palette.Get(TeamId.A).Main : new Color32(24, 223, 240, 255);
            Color32 b = palette != null ? (Color32)palette.Get(TeamId.B).Main : new Color32(255, 122, 26, 255);
            if (mockup) a = palette.Get(TeamId.A, MockupPair).Main;
            int size = PaintGrid.Size;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.07f + 3.1f, y * 0.07f + 7.4f);
                    float band = Mathf.PerlinNoise(x * 0.16f + 11f, y * 0.16f + 2f);
                    Color32 cell = new Color32(0, 0, 0, 0);
                    if (n > 0.62f) cell = a;
                    else if (n < 0.34f) cell = b;
                    else if (band > 0.72f) cell = x < size / 2 ? a : b;
                    if (mockup)
                    {
                        Vector3 relative = new Vector3(x + .5f - size / 2, 0, y + .5f - size / 2) - MockupCar;
                        Vector3 local = Quaternion.Euler(0, -MockupYaw, 0) * relative;
                        bool trail = local.z < -.3f && local.z > -18 && Mathf.Abs(local.x) < 1.2f + .4f * n;
                        cell = trail || (relative.x < -14 && n > .69f) ? a : default;
                    }
                    pixels[y * size + x] = cell;
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            ApplyPaint(scene, texture);
            return texture;
        }

        /// <summary>Muestra una grilla de pintura (una celda por píxel) sobre el piso y las rampas de la escena.</summary>
        internal static void ApplyPaint(Scene scene, Texture2D texture)
        {
            var block = new MaterialPropertyBlock();
            block.SetTexture("_BaseMap", texture);
            block.SetTexture("_MainTex", texture);
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (PaintSurfaceView view in root.GetComponentsInChildren<PaintSurfaceView>(true))
                {
                    view.GetComponent<MeshRenderer>().SetPropertyBlock(block);
                    var ramps = new SerializedObject(view).FindProperty("_ramps");
                    for (int i = 0; i < ramps.arraySize; i++)
                        if (ramps.GetArrayElementAtIndex(i).objectReferenceValue is MeshRenderer ramp)
                            ramp.SetPropertyBlock(block);
                }
        }
    }
}
