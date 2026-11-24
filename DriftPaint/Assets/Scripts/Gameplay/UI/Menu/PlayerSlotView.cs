using DriftPaint.Net;
using DriftPaint.Teams;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.UI.Menu
{
    /// <summary>Lugar de la lista de jugadores del lobby: ocupado por un jugador o libre.</summary>
    public class PlayerSlotView : MonoBehaviour
    {
        [SerializeField] Graphic _teamStripe;
        [SerializeField] Graphic _carIcon;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] GameObject _hostBadge;
        [Tooltip("Marco que resalta al jugador local.")]
        [SerializeField] GameObject _localHighlight;
        [SerializeField] Color _nameColor = new Color(0.957f, 0.969f, 0.984f);
        [SerializeField] Color _emptyColor = new Color(0.957f, 0.969f, 0.984f, 0.4f);

        public void ShowPlayer(PlayerSummary player, TeamPaletteSO palette)
        {
            Color teamColor = palette != null ? palette.Get(player.Team).Main : Color.white;

            SetGraphic(_teamStripe, true, teamColor);
            SetGraphic(_carIcon, true, teamColor);
            if (_hostBadge != null) _hostBadge.SetActive(player.IsMaster);
            if (_localHighlight != null) _localHighlight.SetActive(player.IsLocal);

            if (_nameText != null)
            {
                _nameText.text = player.IsInactive ? string.Format(GameLanguage.Text("{0} (reconectando)"), player.Nickname) : player.Nickname;
                _nameText.color = player.IsInactive ? _emptyColor : _nameColor;
            }
        }

        public void ShowEmpty()
        {
            SetGraphic(_teamStripe, false, Color.clear);
            SetGraphic(_carIcon, true, _emptyColor);
            if (_hostBadge != null) _hostBadge.SetActive(false);
            if (_localHighlight != null) _localHighlight.SetActive(false);

            if (_nameText != null)
            {
                _nameText.text = GameLanguage.Text("Esperando jugador...");
                _nameText.color = _emptyColor;
            }
        }

        static void SetGraphic(Graphic graphic, bool visible, Color color)
        {
            if (graphic == null) return;
            graphic.gameObject.SetActive(visible);
            graphic.color = color;
        }
    }
}
