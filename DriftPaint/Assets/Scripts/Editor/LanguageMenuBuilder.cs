using DriftPaint.UI;
using DriftPaint.UI.Options;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Pone en el menú de opciones el desplegable de idioma, con el mismo estilo que el de resoluciones. Si la
    /// escena todavía tiene el botón que recorría los idiomas, el desplegable toma su lugar.
    /// </summary>
    public static class LanguageMenuBuilder
    {
        const string ButtonName = "Language Button";
        const string DropdownName = "Language";
        const float Height = 52f;
        const float ItemHeight = 48f;
        const int ListedLanguages = 4;

        public static void Install(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (OptionsMenu menu in root.GetComponentsInChildren<OptionsMenu>(true))
                Install(menu);
        }

        /// <summary>Se puede llamar varias veces: si el menú ya tiene el desplegable, no cambia nada.</summary>
        public static void Install(OptionsMenu menu)
        {
            Transform box = menu.transform.Find("Options Panel/Box");
            if (box == null || box.GetComponentInChildren<LanguageOptions>(true) != null) return;

            TMP_Dropdown dropdown = OptionsMenuBuilder.CreateDropdown(box, DropdownName);
            var rect = (RectTransform)dropdown.transform;
            if (box.Find(ButtonName) is RectTransform button)
            {
                // Conserva la posición del botón en la escena, centrado en la misma altura.
                rect.anchorMin = button.anchorMin;
                rect.anchorMax = button.anchorMax;
                rect.pivot = button.pivot;
                float centerY = button.anchoredPosition.y + (0.5f - button.pivot.y) * button.sizeDelta.y;
                rect.sizeDelta = new Vector2(button.sizeDelta.x, Height);
                rect.anchoredPosition = new Vector2(button.anchoredPosition.x, centerY - (0.5f - button.pivot.y) * Height);
                rect.SetSiblingIndex(button.GetSiblingIndex());
                Object.DestroyImmediate(button.gameObject);
            }
            else
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
                rect.anchoredPosition = new Vector2(-60f, -34f);
                rect.sizeDelta = new Vector2(300f, Height);
            }
            dropdown.template.sizeDelta = new Vector2(0f, ListedLanguages * ItemHeight + 8f);

            // El rótulo es hijo del desplegable, así se mueven juntos al acomodarlos en la escena.
            TextMeshProUGUI caption = MenuSceneBuilder.CreateLabel(rect, "Caption", MenuSceneBuilder.LabelFont, 28f,
                TextAlignmentOptions.Right, DriftPaintPalette.OffWhite, FontStyles.Bold,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(200f, Height));
            caption.text = "Idioma";
            caption.raycastTarget = false;
            var translation = caption.gameObject.AddComponent<TranslatedLabel>();
            var data = new SerializedObject(translation);
            data.FindProperty("_key").stringValue = "Idioma";
            data.ApplyModifiedPropertiesWithoutUndo();

            var options = dropdown.gameObject.AddComponent<LanguageOptions>();
            MenuSceneBuilder.Bind(options, ("_dropdown", dropdown));
        }
    }
}
