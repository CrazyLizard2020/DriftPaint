using System.Linq;
using DriftPaint.Match;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Centra el contenido del nombre flotante sobre el auto. El ícono de desconexión, el marcador de equipo
    /// y el nombre van dentro de un contenedor con un HorizontalLayoutGroup centrado: así el grupo queda
    /// justo encima del pivot del auto sea cual sea el largo del nombre. Todo queda editable en la escena.
    /// </summary>
    public static class NameplateBuilder
    {
        const string TemplateName = "Nameplate Template";
        const string ContentName = "Nameplate Content";
        const float Spacing = 12f;
        const float NameHeight = 70f;

        /// <summary>La escena tiene la plantilla de nombres pero todavía sin el contenedor centrado.</summary>
        public static bool Pending(string sceneText) =>
            sceneText.Contains("m_Name: " + TemplateName) && !sceneText.Contains("m_Name: " + ContentName);

        public static void Install(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (CarNameplate plate in root.GetComponentsInChildren<CarNameplate>(true))
                Center(plate);
        }

        /// <summary>Arma o actualiza el contenedor centrado. Se puede llamar varias veces sin duplicar nada.</summary>
        public static void Center(CarNameplate plate)
        {
            if (plate == null) return;
            var rect = (RectTransform)plate.transform;

            // Los valores de diseño se ponen solo al crear el contenedor: después se ajustan a mano en la
            // escena y volver a llamar a esta función no los pisa.
            var content = rect.Find(ContentName) as RectTransform;
            bool created = content == null;
            if (created)
            {
                content = (RectTransform)new GameObject(ContentName, typeof(RectTransform)).transform;
                content.SetParent(rect, false);
                content.anchorMin = Vector2.zero;
                content.anchorMax = Vector2.one;
                content.pivot = new Vector2(0.5f, 0.5f);
                content.offsetMin = Vector2.zero;
                content.offsetMax = Vector2.zero;
            }

            // Todo lo que cuelga del cartel pasa al contenedor.
            foreach (Transform child in rect.Cast<Transform>().Where(c => c != content).ToList())
                child.SetParent(content, false);

            if (!content.TryGetComponent(out HorizontalLayoutGroup layout))
            {
                layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.spacing = Spacing;
                layout.childControlWidth = true;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
            }

            var serialized = new SerializedObject(plate);
            var icon = serialized.FindProperty("_disconnectedIcon").objectReferenceValue as GameObject;
            var marker = serialized.FindProperty("_marker").objectReferenceValue as Graphic;
            var name = serialized.FindProperty("_nameText").objectReferenceValue as TMP_Text;

            // Orden de izquierda a derecha: ícono (si está), marcador y nombre.
            int index = 0;
            if (icon != null) Place(icon.transform, content, ref index);
            if (marker != null) Place(marker.transform, content, ref index);
            if (name != null) Place(name.transform, content, ref index);
            if (name != null && created)
            {
                var nameRect = (RectTransform)name.transform;
                nameRect.anchorMin = nameRect.anchorMax = new Vector2(0.5f, 0.5f);
                nameRect.sizeDelta = new Vector2(nameRect.sizeDelta.x, NameHeight);
                name.alignment = TextAlignmentOptions.Midline;
                name.textWrappingMode = TextWrappingModes.NoWrap;
                name.overflowMode = TextOverflowModes.Overflow;
                EditorUtility.SetDirty(name);
            }

            FixedSize(icon != null ? icon.transform as RectTransform : null);
            FixedSize(marker != null ? marker.rectTransform : null);
            EditorUtility.SetDirty(plate);
        }

        static void Place(Transform child, RectTransform content, ref int index)
        {
            if (child.parent != content) child.SetParent(content, false);
            child.SetSiblingIndex(index++);
        }

        /// <summary>Los íconos conservan su tamaño: el layout solo los ubica.</summary>
        static void FixedSize(RectTransform item)
        {
            if (item == null) return;
            if (!item.TryGetComponent(out LayoutElement element))
                element = item.gameObject.AddComponent<LayoutElement>();
            if (element.preferredWidth <= 0f) element.preferredWidth = item.sizeDelta.x;
            if (element.preferredHeight <= 0f) element.preferredHeight = item.sizeDelta.y;
            EditorUtility.SetDirty(element);
        }
    }
}
