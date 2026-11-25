using System.Collections.Generic;
using DriftPaint.Scoring;
using DriftPaint.Teams;
using TMPro;
using UnityEngine;

namespace DriftPaint.UI.Score
{
    /// <summary>
    /// Tabla de puntaje con una columna por equipo. Las filas se clonan de una plantilla inactiva de la
    /// escena, una por jugador, y se reusan entre actualizaciones. La columna acomoda las filas con su
    /// propio layout.
    /// </summary>
    public class ScoreTableView : MonoBehaviour
    {
        [SerializeField] TeamPaletteSO _palette;
        [Tooltip("Equipo de la columna izquierda.")]
        [SerializeField] byte _leftTeam = TeamId.B;
        [SerializeField] RectTransform _leftColumn;
        [SerializeField] RectTransform _rightColumn;
        [SerializeField] TMP_Text _leftHeader;
        [SerializeField] TMP_Text _rightHeader;
        [Tooltip("Fila inactiva que se clona para cada jugador.")]
        [SerializeField] ScoreRowView _rowTemplate;

        readonly List<ScoreRowView> _leftRows = new List<ScoreRowView>();
        readonly List<ScoreRowView> _rightRows = new List<ScoreRowView>();

        public byte LeftTeam => _leftTeam;
        public byte RightTeam => TeamId.Other(_leftTeam);

        void Awake()
        {
            if (_rowTemplate != null) _rowTemplate.gameObject.SetActive(false);
        }

        void OnEnable() { GameLanguage.Changed += RefreshHeaders; RefreshHeaders(); }
        void OnDisable() => GameLanguage.Changed -= RefreshHeaders;

        void RefreshHeaders()
        {
            if (_leftHeader != null) _leftHeader.text = TeamName(LeftTeam);
            if (_rightHeader != null) _rightHeader.text = TeamName(RightTeam);
        }

        /// <summary>Muestra los jugadores ya ordenados, cada uno en la columna de su equipo.</summary>
        public void Show(IReadOnlyList<ScoreEntry> entries, int mvpActorId)
        {
            int left = 0, right = 0;
            foreach (ScoreEntry entry in entries)
            {
                if (entry.Team == LeftTeam) Fill(_leftRows, _leftColumn, left++, entry, mvpActorId);
                else if (entry.Team == RightTeam) Fill(_rightRows, _rightColumn, right++, entry, mvpActorId);
            }
            Trim(_leftRows, left);
            Trim(_rightRows, right);
        }

        void Fill(List<ScoreRowView> rows, RectTransform column, int index, ScoreEntry entry, int mvpActorId)
        {
            if (_rowTemplate == null || column == null) return;

            while (rows.Count <= index)
            {
                ScoreRowView row = Instantiate(_rowTemplate, column);
                row.name = $"Row {rows.Count + 1}";
                rows.Add(row);
            }

            ScoreRowView view = rows[index];
            view.gameObject.SetActive(true);
            view.Show(entry, TeamColor(entry.Team), entry.ActorId == mvpActorId);
        }

        static void Trim(List<ScoreRowView> rows, int used)
        {
            for (int i = used; i < rows.Count; i++)
                if (rows[i] != null) rows[i].gameObject.SetActive(false);
        }

        Color TeamColor(byte team) => _palette != null ? _palette.Get(team).Main : Color.white;

        string TeamName(byte team) => _palette != null ? string.Format(GameLanguage.Text("EQUIPO {0}"), GameLanguage.Text(_palette.Get(team).DisplayName)) : "";
    }
}
