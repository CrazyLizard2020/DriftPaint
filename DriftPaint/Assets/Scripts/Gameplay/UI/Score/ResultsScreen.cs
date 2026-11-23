using System.Collections.Generic;
using DriftPaint.Match;
using DriftPaint.Net;
using DriftPaint.Scoring;
using DriftPaint.Teams;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.UI.Score
{
    /// <summary>
    /// Pantalla de resultados: victoria o derrota del equipo local, porcentaje de cada equipo, tablas por
    /// equipo con el MVP coronado, y los botones de Revancha y Salir. Muestra solo lo que publicó el Master
    /// y lo toma una sola vez al abrirse, así no cambia si el host vuelve al lobby antes.
    /// </summary>
    public class ResultsScreen : MonoBehaviour
    {
        [SerializeField] MatchController _match;
        [SerializeField] MatchScore _score;
        [SerializeField] TeamPaletteSO _palette;
        [SerializeField] GameObject _root;

        [Header("Título")]
        [SerializeField] TMP_Text _titleText;
        [SerializeField] TMP_Text _winnerText;
        [SerializeField] Image _winnerBrush;
        [SerializeField] string _victoryTitle = "¡VICTORIA!";
        [SerializeField] string _defeatTitle = "¡DERROTA!";
        [Tooltip("Texto debajo del título. {0} es el nombre del equipo ganador.")]
        [SerializeField] string _winnerFormat = "GANA EL EQUIPO {0}";
        [SerializeField] Color _victoryColor = new Color(0.992f, 0.729f, 0.302f);
        [SerializeField] Color _defeatColor = new Color(0.957f, 0.969f, 0.984f, 0.75f);

        [Header("Porcentajes")]
        [Tooltip("Debe coincidir con la columna izquierda de la tabla.")]
        [SerializeField] byte _leftTeam = TeamId.B;
        [SerializeField] TMP_Text _leftPercentText;
        [SerializeField] TMP_Text _rightPercentText;

        [Header("Tabla y botones")]
        [SerializeField] ScoreTableView _table;
        [SerializeField] Button _rematchButton;
        [SerializeField] Button _leaveButton;
        [Tooltip("Cuenta regresiva para salir solo. {0} son los segundos.")]
        [SerializeField] TMP_Text _timeoutText;
        [SerializeField] string _timeoutFormat = "Saliendo en {0} s";

        readonly List<ScoreEntry> _entries = new List<ScoreEntry>();
        int _lastTimeout = -1;

        void Awake()
        {
            if (_rematchButton != null) _rematchButton.onClick.AddListener(OnRematch);
            if (_leaveButton != null) _leaveButton.onClick.AddListener(OnLeave);
            if (_root != null) _root.SetActive(false);
        }

        void OnEnable()
        {
            if (_match != null) _match.PhaseChanged += OnPhaseChanged;
        }

        void OnDisable()
        {
            if (_match != null) _match.PhaseChanged -= OnPhaseChanged;
        }

        void OnPhaseChanged(MatchPhase phase)
        {
            if (phase == MatchPhase.Results) Open();
        }

        void Open()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            byte winner = _match.Winner;
            bool victory = winner != TeamId.None && winner == _score.LocalTeam;
            if (_winnerBrush != null && _palette != null) _winnerBrush.color = _palette.Get(winner).Detail;
            if (_titleText != null)
            {
                _titleText.text = victory ? _victoryTitle : _defeatTitle;
                _titleText.color = _winnerBrush != null ? Color.white : victory ? _victoryColor : _defeatColor;
            }
            if (_winnerText != null)
                _winnerText.text = winner == TeamId.None ? "" : string.Format(_winnerFormat, TeamName(winner));

            ScoreRules.Percentages(_score.CellsA, _score.CellsB, out int percentA, out int percentB);
            if (_leftPercentText != null) _leftPercentText.text = $"{(_leftTeam == TeamId.A ? percentA : percentB)}%";
            if (_rightPercentText != null) _rightPercentText.text = $"{(_leftTeam == TeamId.A ? percentB : percentA)}%";

            _score.BuildTable(_entries);
            if (_table != null) _table.Show(_entries, ScoreRules.FindMvp(_entries, winner));

            _lastTimeout = -1;
            if (_root != null) _root.SetActive(true);
            if (_rematchButton != null) _rematchButton.Select();
        }

        void Update()
        {
            if (_root == null || !_root.activeSelf || _timeoutText == null || _match == null) return;

            int seconds = Mathf.CeilToInt(_match.ResultsTimeRemaining);
            if (seconds == _lastTimeout) return;
            _lastTimeout = seconds;
            _timeoutText.text = string.Format(_timeoutFormat, seconds);
        }

        void OnRematch()
        {
            SetButtonsInteractable(false);
            _match.Rematch();
        }

        void OnLeave()
        {
            SetButtonsInteractable(false);
            _match.Leave();
        }

        void SetButtonsInteractable(bool interactable)
        {
            if (_rematchButton != null) _rematchButton.interactable = interactable;
            if (_leaveButton != null) _leaveButton.interactable = interactable;
        }

        string TeamName(byte team) => _palette != null ? _palette.Get(team).DisplayName : team == TeamId.A ? "A" : "B";
    }
}
