using DriftPaint.Teams;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.UI
{
    /// <summary>Aplica la tinta del equipo y elige el texto con mayor contraste sobre ese fondo.</summary>
    public sealed class TeamPanelColor : MonoBehaviour
    {
        [SerializeField]
        TeamPaletteSO _palette;
        [SerializeField]
        byte _team;
        [SerializeField]
        Image _panel;
        [SerializeField]
        TMP_Text _label;

        void OnEnable()
        {
            if (_palette == null || _panel == null)
                return;
            Color color = _palette.Get(_team).Main;
            _panel.color = color;
            Color linear = color.linear;
            float luminance = linear.r * .2126f + linear.g * .7152f + linear.b * .0722f;
            if (_label != null)
                _label.color = luminance > .22f ? DriftPaintPalette.HudNavy : Color.white;
        }
    }
}
