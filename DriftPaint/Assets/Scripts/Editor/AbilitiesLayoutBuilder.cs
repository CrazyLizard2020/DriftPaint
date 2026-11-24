using System.Linq;
using System.Text.RegularExpressions;
using DriftPaint.Match;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Ordena el panel de disparo y barriles con un Horizontal Layout Group, así el panel se ensancha cuando la
    /// palabra LISTO de un idioma ocupa más que el lugar reservado y nada se pisa con el separador. El orden,
    /// los tamaños y las separaciones salen de cómo está armado el panel en la escena.
    /// </summary>
    public static class AbilitiesLayoutBuilder
    {
        const float Spacing = 16f;

        /// <summary>La escena tiene el HUD de partida pero el cooldown todavía no está conectado a su lugar en el panel.</summary>
        public static bool Pending(string sceneText) =>
            Regex.IsMatch(sceneText, @"_fireCooldownText: \{fileID: [1-9]") &&
            !Regex.IsMatch(sceneText, @"_fireCooldownSlot: \{fileID: [1-9]");

        public static void Install(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MatchHUD hud in root.GetComponentsInChildren<MatchHUD>(true))
                Connect(hud);
        }

        /// <summary>Arma el layout del panel y conecta el lugar del cooldown al HUD.</summary>
        public static void Connect(MatchHUD hud)
        {
            var data = new SerializedObject(hud);
            if (data.FindProperty("_fireCooldownText").objectReferenceValue is not TMP_Text cooldown) return;
            LayoutElement slot = Arrange((RectTransform)cooldown.transform.parent, cooldown);
            data.FindProperty("_fireCooldownSlot").objectReferenceValue = slot;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Se puede llamar varias veces: si el panel ya tiene el layout, solo devuelve el lugar del cooldown.</summary>
        static LayoutElement Arrange(RectTransform panel, TMP_Text cooldown)
        {
            if (panel.GetComponent<HorizontalLayoutGroup>() != null)
                return cooldown.GetComponent<LayoutElement>() ?? cooldown.gameObject.AddComponent<LayoutElement>();

            RectTransform[] items = panel.Cast<Transform>().OfType<RectTransform>()
                .Where(child => child.gameObject.activeSelf)
                .OrderBy(child => LeftEdge(panel, child))
                .ToArray();
            if (items.Length == 0) return null;

            // Cada elemento conserva su ancho actual como mínimo. Los textos pueden crecer hasta su ancho preferido.
            foreach (RectTransform item in items)
            {
                var element = item.GetComponent<LayoutElement>() ?? item.gameObject.AddComponent<LayoutElement>();
                element.minWidth = item.rect.width;
                element.preferredWidth = item.GetComponent<TMP_Text>() != null ? -1f : item.rect.width;
                element.flexibleWidth = 0f;
            }

            RectTransform last = items[items.Length - 1];
            float left = LeftEdge(panel, items[0]);
            float right = panel.rect.width - (LeftEdge(panel, last) + last.rect.width);

            var group = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.padding = new RectOffset(Mathf.RoundToInt(left), Mathf.Max(0, Mathf.RoundToInt(right)), 0, 0);
            group.spacing = Spacing;
            group.childAlignment = TextAnchor.MiddleLeft;
            group.childControlWidth = true;
            group.childControlHeight = false;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;

            var fitter = panel.GetComponent<ContentSizeFitter>() ?? panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            return cooldown.GetComponent<LayoutElement>();
        }

        /// <summary>Borde izquierdo del hijo medido desde el borde izquierdo del panel.</summary>
        static float LeftEdge(RectTransform panel, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            return panel.InverseTransformPoint(corners[0]).x - panel.rect.xMin;
        }
    }
}
