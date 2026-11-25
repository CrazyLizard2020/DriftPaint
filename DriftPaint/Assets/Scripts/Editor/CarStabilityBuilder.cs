using System.IO;
using System.Linq;
using DriftPaint.Cars;
using DriftPaint.Sandbox;
using DriftPaint.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Pone al día la estabilidad en los recursos del juego: componentes en los prefabs de los autos, datos en las
    /// fichas y panel de desarrollo en la escena de prueba. Se puede correr varias veces.
    /// </summary>
    static class CarStabilityBuilder
    {
        public const string SandboxScene = "Assets/Scenes/Sandbox_Car.unity";
        const string PanelName = "StabilityPanel";
        const string DataFolder = "Assets/ScriptableObjects/Cars";

        static string[] PrefabPaths() => Directory.GetFiles("Assets/Prefabs/Cars", "Car_*.prefab").Select(path => path.Replace('\\', '/')).ToArray();
        static string[] DataPaths() => Directory.GetFiles(DataFolder, "CarData_*.asset").Select(path => path.Replace('\\', '/')).ToArray();

        internal static bool Outdated() => PrefabPaths().Any(PrefabOutdated) || DataPaths().Any(DataOutdated);

        static bool PrefabOutdated(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab != null && prefab.TryGetComponent(out CarController _) &&
                   !(prefab.TryGetComponent(out CarStabilityController _) && prefab.TryGetComponent(out CarLandingFeedback _));
        }

        static bool DataOutdated(string path) => !File.ReadAllText(path).Contains("_stability:");

        internal static void Apply()
        {
            foreach (string path in PrefabPaths().Where(PrefabOutdated))
            {
                using var contents = new PrefabUtility.EditPrefabContentsScope(path);
                GameObject root = contents.prefabContentsRoot;
                if (!root.TryGetComponent(out CarStabilityController _)) root.AddComponent<CarStabilityController>();
                if (!root.TryGetComponent(out CarLandingFeedback _)) root.AddComponent<CarLandingFeedback>();
            }

            foreach (string path in DataPaths().Where(DataOutdated))
            {
                var data = AssetDatabase.LoadAssetAtPath<CarDataSO>(path);
                if (data != null) EditorUtility.SetDirty(data);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Panel de desarrollo de la escena de prueba. Queda en la escena para editarlo.</summary>
        internal static void InstallDebugger(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            SandboxBootstrap sandbox = roots.Select(root => root.GetComponentInChildren<SandboxBootstrap>(true)).FirstOrDefault(found => found != null);
            SandboxHUD hud = roots.Select(root => root.GetComponentInChildren<SandboxHUD>(true)).FirstOrDefault(found => found != null);
            if (sandbox == null || hud == null) return;

            Transform existing = hud.transform.Find(PanelName);
            GameObject panel = existing != null ? existing.gameObject : CreatePanel(hud.transform);

            if (!sandbox.TryGetComponent(out VehicleStabilityDebugger debugger)) debugger = sandbox.gameObject.AddComponent<VehicleStabilityDebugger>();
            var so = new SerializedObject(debugger);
            so.FindProperty("_sandbox").objectReferenceValue = sandbox;
            so.FindProperty("_panel").objectReferenceValue = panel;
            so.FindProperty("_text").objectReferenceValue = panel.GetComponentInChildren<TextMeshProUGUI>(true);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject CreatePanel(Transform hud)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Oxanium-Medium SDF.asset");
            Image panel = UiFactory.CreatePanel(hud, PanelName, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -230f), new Vector2(620f, 560f), DriftPaintPalette.HudNavy.WithAlpha(0.85f));
            TextMeshProUGUI text = UiFactory.CreateText(panel.transform, "StabilityText", font, 18f, TextAlignmentOptions.TopLeft, DriftPaintPalette.OffWhite);
            UiFactory.Stretch((RectTransform)text.transform, 14f);
            text.text = "ESTABILIDAD (F7 muestra u oculta, F8 exporta)";
            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }
    }
}
