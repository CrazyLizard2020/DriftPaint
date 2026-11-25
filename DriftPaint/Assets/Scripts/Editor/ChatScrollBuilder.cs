using System.Text.RegularExpressions;
using DriftPaint.UI;
using DriftPaint.UI.Menu;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Convierte el registro del chat de la sala en una lista desplazable con barra. El texto queda dentro de un
    /// contenido que crece con los mensajes, apoyado abajo, y la barra aparece cuando los mensajes no entran.
    /// </summary>
    public static class ChatScrollBuilder
    {
        const float ScrollbarWidth = 12f;
        const float ScrollbarInset = 6f;
        const float ScrollSensitivity = 30f;

        /// <summary>La sala tiene registro de chat pero todavía no está conectado a una lista desplazable.</summary>
        public static bool Pending(string sceneText) =>
            Regex.IsMatch(sceneText, @"_chatLog: \{fileID: [1-9]") &&
            !Regex.IsMatch(sceneText, @"_chatScroll: \{fileID: [1-9]");

        public static void Install(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (LobbyScreen lobby in root.GetComponentsInChildren<LobbyScreen>(true))
                Connect(lobby);
        }

        /// <summary>Se puede llamar varias veces: si el registro ya está dentro de una lista, solo la conecta.</summary>
        public static void Connect(LobbyScreen lobby)
        {
            var data = new SerializedObject(lobby);
            if (data.FindProperty("_chatLog").objectReferenceValue is not TMP_Text log) return;
            ScrollRect scroll = log.GetComponentInParent<ScrollRect>(true) ?? Build((RectTransform)log.transform.parent, log);
            data.FindProperty("_chatScroll").objectReferenceValue = scroll;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static ScrollRect Build(RectTransform box, TMP_Text log)
        {
            // La caja recibe la rueda del mouse en toda su superficie, no solo sobre las letras.
            if (box.TryGetComponent(out Image background)) background.raycastTarget = true;

            // Los márgenes que tenía el texto pasan a ser el relleno del contenido.
            var logRect = (RectTransform)log.transform;
            var padding = new RectOffset(
                Mathf.RoundToInt(logRect.offsetMin.x), Mathf.RoundToInt(-logRect.offsetMax.x),
                Mathf.RoundToInt(-logRect.offsetMax.y), Mathf.RoundToInt(logRect.offsetMin.y));

            RectTransform viewport = UiFactory.CreateRect(box, "Viewport", Vector2.zero, Vector2.one, new Vector2(0f, 1f),
                Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();

            // Contenido apoyado abajo: con pocos mensajes quedan pegados al campo de texto, como antes.
            RectTransform content = UiFactory.CreateRect(viewport, "Content", Vector2.zero, new Vector2(1f, 0f), Vector2.zero,
                Vector2.zero, Vector2.zero);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = padding;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            logRect.SetParent(content, false);

            Scrollbar scrollbar = CreateScrollbar(box);

            var scroll = box.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = ScrollSensitivity;
            scroll.verticalScrollbar = scrollbar;
            // La barra aparece solo cuando hay mensajes fuera de la vista, y el texto usa todo el ancho mientras tanto.
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scroll.verticalScrollbarSpacing = 2f;
            scroll.verticalNormalizedPosition = 0f;
            return scroll;
        }

        static Scrollbar CreateScrollbar(RectTransform box)
        {
            Image track = UiFactory.CreatePanel(box, "Scrollbar", new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f),
                Vector2.zero, new Vector2(ScrollbarWidth, 0f), DriftPaintPalette.OffWhite.WithAlpha(0.08f));
            track.raycastTarget = true;

            RectTransform area = UiFactory.CreateRect(track.transform, "Sliding Area", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            area.offsetMin = new Vector2(3f, ScrollbarInset);
            area.offsetMax = new Vector2(-3f, -ScrollbarInset);

            Image handle = UiFactory.CreatePanel(area, "Handle", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, DriftPaintPalette.Cyan);
            handle.raycastTarget = true;

            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            ColorBlock colors = scrollbar.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.75f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = DriftPaintPalette.CyanBright;
            colors.selectedColor = Color.white;
            scrollbar.colors = colors;
            // Con el joystick se navega entre botones y campos; la barra se usa con el mouse.
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
            return scrollbar;
        }
    }
}
