using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Helpers que usan las herramientas de editor para crear UI de uGUI dentro de escenas.
    /// Lo que se crea queda guardado en la escena y se edita normalmente en Unity.
    /// </summary>
    public static class UiFactory
    {
        public static Canvas CreateOverlayCanvas(Transform parent, string name, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            // Expand: el área del canvas nunca es menor que 1920 x 1080 en ninguno de los dos ejes. En pantallas
            // que no son 16:9 sobra espacio en un eje, pero lo diseñado para 16:9 entra entero y no se pisa.
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            return canvas;
        }

        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        /// <summary>
        /// Contenedor centrado de 1920 x 1080 para las pantallas compuestas como una sola pieza (como el
        /// lobby): en otros aspectos queda centrado y sin deformarse, mientras el fondo cubre toda la pantalla.
        /// </summary>
        public static RectTransform CreateContentRoot(Transform canvas, string name = "Content 16x9")
        {
            var half = new Vector2(0.5f, 0.5f);
            return CreateRect(canvas, name, half, half, half, Vector2.zero, ReferenceResolution);
        }

        public static RectTransform CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        public static Image CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            RectTransform rect = CreateRect(parent, name, anchorMin, anchorMax, pivot, anchoredPosition, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font, float fontSize,
            TextAlignmentOptions alignment, Color color, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.fontStyle = style;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch((RectTransform)go.transform);
            return text;
        }

        public static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }
    }
}
