using System.Collections.Generic;
using System.IO;
using System.Linq;
using DriftPaint.Cars;
using DriftPaint.Paint;
using DriftPaint.Teams;
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

        [MenuItem("DriftPaint/Arte/Capturas de referencia")]
        static void CaptureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string folder = Capture("editor");
            EditorUtility.RevealInFinder(folder);
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
                foreach (Shot shot in Shots)
                {
                    Place(cars, shot);
                    Frame(camera, shot);
                    string file = Path.Combine(folder, $"{sceneName}_{shot.Name}.png");
                    Render(camera, file);
                    Debug.Log($"Captura guardada en {file}");
                }
            }
            return folder;
        }

        /// <summary>Prepara la escena abierta: pintura de prueba, tres autos y la cámara sin su seguimiento.</summary>
        static void Stage(Scene scene, out Camera camera, out List<GameObject> cars)
        {
            camera = scene.GetRootGameObjects().Select(root => root.GetComponentInChildren<Camera>(true))
                          .FirstOrDefault(c => c != null && c.CompareTag("MainCamera"));
            if (camera == null) throw new System.InvalidOperationException("La escena no tiene cámara principal: " + scene.path);
            foreach (MonoBehaviour behaviour in camera.GetComponents<MonoBehaviour>()) behaviour.enabled = false;

            var palette = AssetDatabase.LoadAssetAtPath<TeamPaletteSO>(ScriptableObjectPaths.TeamPalette);
            PaintTestPattern(scene, palette);

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

        static void Render(Camera camera, string file)
        {
            var target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            camera.targetTexture = target;
            // Dos cuadros: el primero calienta los efectos que dependen del cuadro anterior.
            camera.Render();
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            File.WriteAllBytes(file, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
        }

        /// <summary>
        /// Manchas de los dos equipos sobre la grilla, con bordes de celda como los que confirma la partida.
        /// Se ponen con un bloque de propiedades, igual que la textura real, sin tocar el material.
        /// </summary>
        static void PaintTestPattern(Scene scene, TeamPaletteSO palette)
        {
            Color32 a = palette != null ? (Color32)palette.Get(TeamId.A).Main : new Color32(24, 223, 240, 255);
            Color32 b = palette != null ? (Color32)palette.Get(TeamId.B).Main : new Color32(255, 122, 26, 255);
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
