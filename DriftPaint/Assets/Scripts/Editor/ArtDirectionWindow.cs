using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DriftPaint.EditorTools
{
    /// <summary>Edita los mismos assets que usan las escenas y la flota.</summary>
    public sealed class ArtDirectionWindow : EditorWindow
    {
        readonly List<string> _paths = new();
        string[] _labels;
        int _selected;
        Editor _inspector;
        Vector2 _scroll;

        [MenuItem("DriftPaint/Arte/Dirección de arte", priority = 0)]
        public static void Open() => GetWindow<ArtDirectionWindow>("Dirección de arte");

        void OnEnable()
        {
            var labels = new List<string>();
            _paths.Clear();
            void Add(string label, string path)
            {
                labels.Add(label);
                _paths.Add(path);
            }

            Add("Conducción, luz y HUD", "Assets/Resources/DrivingArt.asset");
            Add("VFX/Impactos de pintura", "Assets/Resources/PaintHitArt.asset");
            Add("Tribunas/Público y ambiente 3D", CrowdArtBuilder.ProfilePath);
            Add("Line art · Pincelada sumi-e y grosores", ArtDirectionBuilder.OutlineMaterialPath);
            Add("Equipos/Duplas y pigmentos", ScriptableObjectPaths.TeamPalette);
            Add("Postproceso/Partida", ArtDirectionBuilder.GameplayProfilePath);
            Add("Postproceso/Menú", ArtDirectionBuilder.MenuProfilePath);
            Add("Postproceso/Resultados", ArtDirectionBuilder.ResultsProfilePath);
            Add("Render/PC · SSAO y contorno", "Assets/Settings/PC_Renderer.asset");
            Add("Render/Mobile", "Assets/Settings/Mobile_Renderer.asset");
            Add("Render/Calidad PC", "Assets/Settings/PC_RPAsset.asset");
            Add("Render/Calidad Mobile", "Assets/Settings/Mobile_RPAsset.asset");
            foreach (string path in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Art/Materials" })
                         .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path))
            {
                if (path == ArtDirectionBuilder.OutlineMaterialPath) continue;
                string name = path.Substring("Assets/Art/Materials/".Length).Replace("M_", "");
                Add("Materiales/" + System.IO.Path.ChangeExtension(name, null), path);
            }
            _labels = labels.ToArray();
            _selected = Mathf.Clamp(_selected, 0, _paths.Count - 1);
        }

        void OnDisable()
        {
            if (_inspector != null) DestroyImmediate(_inspector);
        }

        void OnGUI()
        {
            int selected = EditorGUILayout.Popup("Ajustar", _selected, _labels);
            if (selected != _selected) _scroll = Vector2.zero;
            _selected = selected;
            Object asset = AssetDatabase.LoadMainAssetAtPath(_paths[_selected]);
            if (asset == null)
            {
                EditorGUILayout.HelpBox("Falta el recurso " + _paths[_selected], MessageType.Warning);
                return;
            }
            Editor.CreateCachedEditor(asset, null, ref _inspector);
            if (GUILayout.Button("Mostrar en Project")) EditorGUIUtility.PingObject(asset);
            if (Application.isPlaying && asset is DriftPaint.Level.CrowdProfile crowd &&
                GUILayout.Button("Aplicar público en esta partida"))
            {
                foreach (var stand in FindObjectsByType<DriftPaint.Level.CrowdStand>(FindObjectsSortMode.None))
                    if (stand.profile == crowd) stand.Rebuild();
                foreach (var voice in FindObjectsByType<DriftPaint.Level.CrowdAmbience>(FindObjectsSortMode.None))
                    if (voice.stand && voice.stand.profile == crowd) voice.RefreshSettings();
            }
            if (_paths[_selected] == ArtDirectionBuilder.OutlineMaterialPath)
                EditorGUILayout.HelpBox("En calidad PC, elegí Continuo o Pincelada. La presión varía el ancho del trazo y el pincel seco deja ver el color entre las fibras. Cada grosor se mide a 1080p. En cero se apaga el trazo de ese grupo.", MessageType.Info);
            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _inspector.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }
    }
}
