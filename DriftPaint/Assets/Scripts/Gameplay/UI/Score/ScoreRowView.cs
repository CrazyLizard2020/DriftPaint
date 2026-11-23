using DriftPaint.Scoring;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.UI.Score
{
    /// <summary>Fila de la tabla de puntaje: auto en el color del equipo, nickname, puntaje y corona del MVP.</summary>
    public class ScoreRowView : MonoBehaviour
    {
        [SerializeField] Image _carIcon;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] TMP_Text _pointsText;
        [SerializeField] GameObject _crown;
        [SerializeField] GameObject _localHighlight;
        [Tooltip("Se muestra si el jugador se desconectó.")]
        [SerializeField] GameObject _disconnectedMark;

        public void Show(ScoreEntry entry, Color teamColor, bool isMvp)
        {
            if (_carIcon != null) _carIcon.color = teamColor;
            if (_nameText != null) _nameText.text = entry.Nickname;
            if (_pointsText != null) _pointsText.text = ScoreRules.FormatPoints(entry.Points);
            if (_crown != null) _crown.SetActive(isMvp);
            if (_localHighlight != null) _localHighlight.SetActive(entry.IsLocal);
            if (_disconnectedMark != null) _disconnectedMark.SetActive(entry.IsDisconnected);
        }
    }
}
