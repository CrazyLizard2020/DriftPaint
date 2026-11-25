using System.Collections.Generic;
using DriftPaint.Input;
using DriftPaint.Match;
using DriftPaint.Net;
using DriftPaint.Scoring;
using UnityEngine;

namespace DriftPaint.UI.Score
{
    /// <summary>
    /// Tabla de puntaje que se ve mientras se mantiene Tab (o View en el joystick) durante la partida.
    /// Muestra los valores que publica el Master, que cambian como mucho una vez por segundo.
    /// </summary>
    public class ScoreboardPanel : MonoBehaviour
    {
        [SerializeField] MatchController _match;
        [SerializeField] MatchScore _score;
        [SerializeField] GameObject _root;
        [SerializeField] ScoreTableView _table;
        [Tooltip("Cada cuántos segundos se vuelve a armar la tabla mientras está abierta.")]
        [SerializeField, Min(0.1f)] float _refreshInterval = 0.5f;

        readonly List<ScoreEntry> _entries = new List<ScoreEntry>();
        float _refreshTimer;

        void OnEnable()
        {
            DriftPaintInputMaps.UI.Map.Enable();
            if (_root != null) _root.SetActive(false);
        }

        void Update()
        {
            bool held = DriftPaintInputMaps.UI.Scoreboard.IsPressed();
            bool show = held && _match != null && _match.Phase != MatchPhase.Results;

            if (_root != null && _root.activeSelf != show)
            {
                _root.SetActive(show);
                _refreshTimer = 0f;
            }
            if (!show) return;

            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f) return;
            _refreshTimer = _refreshInterval;

            if (_score == null || _table == null) return;
            _score.BuildTable(_entries);
            _table.Show(_entries, -1);
        }
    }
}
