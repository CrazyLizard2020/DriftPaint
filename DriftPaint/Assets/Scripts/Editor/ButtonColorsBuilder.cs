using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DriftPaint.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Le da a cada botón de las escenas y los prefabs colores propios para cuando se pasa el mouse, se elige con
    /// joystick o teclado y se aprieta. Los botones claros (cyan) pasan a naranja y al apretarlos a cyan brillante.
    /// Los oscuros pasan a cyan y al apretarlos a naranja. En los dos casos el texto pasa a azul marino.
    /// Un botón que ya tiene sus colores no se toca, así los ajustes hechos en el inspector se conservan.
    /// </summary>
    public static class ButtonColorsBuilder
    {
        const string ButtonScriptGuid = "4e29b1a8efbd4b44bb3f3716e73f07ff";
        const string ColorsScriptGuid = "7c3e1a9f52d84b6e9a0f4d2b8e61c5a7";
        const string PrefabsFolder = "Assets/Prefabs";

        /// <summary>Hay más botones que componentes de colores en el archivo de la escena o del prefab.</summary>
        public static bool Pending(string assetText) =>
            Count(assetText, ButtonScriptGuid) > Count(assetText, ColorsScriptGuid);

        static int Count(string text, string guid) => Regex.Matches(text, "guid: " + guid).Count;

        public static void Install(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                Install(root);
        }

        public static bool PrefabsOutdated() => PendingPrefabs().Any();

        public static void UpdatePrefabs()
        {
            foreach (string path in PendingPrefabs().ToList())
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (Install(contents) > 0) PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        static IEnumerable<string> PendingPrefabs()
        {
            if (!Directory.Exists(PrefabsFolder)) yield break;
            foreach (string file in Directory.GetFiles(PrefabsFolder, "*.prefab", SearchOption.AllDirectories))
                if (Pending(File.ReadAllText(file))) yield return file.Replace('\\', '/');
        }

        /// <summary>Agrega los colores a los botones que no los tienen. Devuelve cuántos agregó.</summary>
        public static int Install(GameObject root)
        {
            int added = 0;
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.GetComponent<ButtonStateColors>() != null) continue;
                Style(button);
                EditorUtility.SetDirty(button.gameObject);
                added++;
            }
            return added;
        }

        /// <summary>Elige los colores según el fondo actual del botón y apaga el tinte de Unity, que los reemplazaba.</summary>
        public static ButtonStateColors Style(Button button)
        {
            var colors = button.GetComponent<ButtonStateColors>() ?? button.gameObject.AddComponent<ButtonStateColors>();
            Graphic fill = button.targetGraphic;
            bool light = fill != null && Luminance(fill.color) > 0.3f;

            var data = new SerializedObject(colors);
            data.FindProperty("_fill").objectReferenceValue = fill;
            TMP_Text[] labels = LabelsOn(button).ToArray();
            SerializedProperty list = data.FindProperty("_labels");
            list.arraySize = labels.Length;
            for (int i = 0; i < labels.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];
            SetLook(data.FindProperty("_highlighted"), light ? DriftPaintPalette.Orange : DriftPaintPalette.Cyan);
            SetLook(data.FindProperty("_pressed"), light ? DriftPaintPalette.CyanBright : DriftPaintPalette.Orange);
            data.ApplyModifiedPropertiesWithoutUndo();

            button.transition = Selectable.Transition.None;
            return colors;
        }

        static void SetLook(SerializedProperty look, Color fill)
        {
            look.FindPropertyRelative("fill").colorValue = fill;
            look.FindPropertyRelative("text").colorValue = DriftPaintPalette.HudNavy;
        }

        /// <summary>Textos que se leen sobre el fondo del botón: entre el texto y el botón no hay otra imagen.</summary>
        static IEnumerable<TMP_Text> LabelsOn(Button button)
        {
            foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
            {
                bool ownBackground = false;
                for (Transform t = label.transform.parent; t != null && t != button.transform; t = t.parent)
                    if (t.GetComponent<Image>() != null) { ownBackground = true; break; }
                if (!ownBackground) yield return label;
            }
        }

        static float Luminance(Color color)
        {
            Color linear = color.linear;
            return linear.r * 0.2126f + linear.g * 0.7152f + linear.b * 0.0722f;
        }
    }
}
