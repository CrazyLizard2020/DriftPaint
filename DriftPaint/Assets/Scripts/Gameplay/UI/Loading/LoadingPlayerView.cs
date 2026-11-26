using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.UI.Loading
{
    /// <summary>Ficha de un jugador en la pantalla de carga: color de su equipo, nombre y si ya cargó.</summary>
    public sealed class LoadingPlayerView : MonoBehaviour
    {
        public enum Status
        {
            Loading,
            Loaded,
            /// <summary>Cargó y marcó las instrucciones como leídas.</summary>
            Read,
            Disconnected
        }

        [SerializeField] CanvasGroup _group;
        [SerializeField] Graphic _teamStripe;
        [SerializeField] TMP_Text _name;
        [SerializeField] TMP_Text _status;
        [Tooltip("Tilde que aparece cuando el jugador terminó de cargar.")]
        [SerializeField] Graphic _check;
        [SerializeField] Color _mutedColor = new Color(0.96f, 0.97f, 0.98f, 0.55f);
        [SerializeField] Color _loadedColor = new Color(0.96f, 0.97f, 0.98f, 1f);
        [Tooltip("Opacidad de la ficha de un jugador desconectado.")]
        [SerializeField, Range(0f, 1f)] float _disconnectedAlpha = 0.45f;

        public void Show(string playerName, Color team, bool isLocal, Status status, string statusText)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (_teamStripe != null) _teamStripe.color = team;
            if (_name != null)
            {
                _name.text = playerName;
                _name.fontStyle = isLocal ? _name.fontStyle | FontStyles.Underline : _name.fontStyle & ~FontStyles.Underline;
            }
            if (_status != null)
            {
                _status.text = statusText;
                _status.color = status == Status.Read ? team : status == Status.Loaded ? _loadedColor : _mutedColor;
            }
            if (_check != null)
            {
                bool loaded = status == Status.Loaded || status == Status.Read;
                _check.enabled = loaded;
                _check.color = status == Status.Read ? team : _loadedColor;
            }
            if (_group != null) _group.alpha = status == Status.Disconnected ? _disconnectedAlpha : 1f;
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
