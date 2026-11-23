using DriftPaint.Cars;
using DriftPaint.Net;
using DriftPaint.Scoring;
using DriftPaint.Teams;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DriftPaint.Match
{
    /// <summary>
    /// Escribe los valores del HUD de partida: porcentaje por equipo, reloj, cuenta regresiva, carteles
    /// de fin de tiempo y desempate, velocímetro, cooldown de disparo y barriles. Los elementos viven en la escena.
    /// </summary>
    public class MatchHUD : MonoBehaviour
    {
        [SerializeField] MatchController _match;
        [SerializeField] MatchScore _score;

        [Header("Marcador")]
        [Tooltip("Equipo que se muestra a la izquierda del reloj.")]
        [SerializeField] byte _leftTeam = TeamId.B;
        [SerializeField] TMP_Text _leftPercentText;
        [SerializeField] TMP_Text _rightPercentText;
        [SerializeField] TMP_Text _clockText;

        [Header("Cuenta regresiva")]
        [SerializeField] GameObject _countdownRoot;
        [SerializeField] TMP_Text _countdownText;
        [Tooltip("Segundos que queda el cartel de arranque después de la cuenta regresiva.")]
        [SerializeField] float _goMessageSeconds = 0.8f;

        [Header("Cartel de fin de tiempo y desempate")]
        [FormerlySerializedAs("_resultsRoot")]
        [SerializeField] GameObject _bannerRoot;
        [SerializeField] TMP_Text _bannerTitle;
        [SerializeField] TMP_Text _bannerSubtitle;
        [SerializeField] string _timeUpTitle = "¡TIEMPO!";
        [SerializeField] string _timeUpSubtitle = "Contando celdas...";
        [Tooltip("Cartel que se ve entre el empate y el arranque del desempate.")]
        [SerializeField] string _tieTitle = "¡EMPATE!";
        [SerializeField] string _tieSubtitle = "Preparando ronda de desempate...";

        [Header("Velocímetro")]
        [SerializeField] TMP_Text _speedText;
        [Tooltip("Image en modo Filled que se llena con la velocidad.")]
        [SerializeField] Image _speedFill;

        [Header("Habilidades")]
        [SerializeField] TMP_Text _fireCooldownText;
        [Tooltip("Image en modo Filled: llena cuando el disparo está listo.")]
        [SerializeField] Image _fireCooldownFill;
        [SerializeField] TMP_Text _barrelsText;

        CarController _car;
        int _lastClockSeconds = -1;
        int _lastSpeed = -1;
        int _lastCellsA = -1;
        int _lastCellsB = -1;

        public void SetTarget(CarController car) => _car = car;

        /// <summary>Estado del disparo y del inventario de barriles del auto local.</summary>
        public void SetAbilities(float fireCooldownRemaining, float fireCooldownTotal, int barrels)
        {
            if (_fireCooldownText != null)
                _fireCooldownText.text = fireCooldownRemaining > 0f ? $"{fireCooldownRemaining:0.0} s" : "LISTO";
            if (_fireCooldownFill != null)
                _fireCooldownFill.fillAmount = fireCooldownTotal > 0f ? 1f - Mathf.Clamp01(fireCooldownRemaining / fireCooldownTotal) : 1f;
            if (_barrelsText != null) _barrelsText.text = $"x{barrels}";
        }

        void Start() => SetAbilities(0f, 1f, 0);

        void Update()
        {
            if (_match == null) return;

            UpdateScore();
            UpdateClock();
            UpdateBanner();
            UpdateSpeed();
        }

        void UpdateScore()
        {
            if (_score == null) return;

            int cellsA = _score.CellsA;
            int cellsB = _score.CellsB;
            if (cellsA == _lastCellsA && cellsB == _lastCellsB) return;
            _lastCellsA = cellsA;
            _lastCellsB = cellsB;

            ScoreRules.Percentages(cellsA, cellsB, out int percentA, out int percentB);
            int left = _leftTeam == TeamId.A ? percentA : percentB;
            int right = _leftTeam == TeamId.A ? percentB : percentA;
            if (_leftPercentText != null) _leftPercentText.text = $"{left}%";
            if (_rightPercentText != null) _rightPercentText.text = $"{right}%";
        }

        void UpdateClock()
        {
            MatchPhase phase = _match.Phase;
            float seconds = phase == MatchPhase.Countdown ? _match.DurationSeconds : _match.TimeRemaining;
            int whole = Mathf.CeilToInt(seconds);
            if (whole != _lastClockSeconds && _clockText != null)
            {
                _lastClockSeconds = whole;
                _clockText.text = $"{whole / 60:00}:{whole % 60:00}";
            }

            bool countdown = phase == MatchPhase.Countdown;
            // El "¡YA!" se ve al arrancar la partida y también al arrancar cada desempate.
            bool go = phase.IsRound() && _match.TimeSinceStart >= 0f && _match.TimeSinceStart < _goMessageSeconds;
            if (_countdownRoot != null && _countdownRoot.activeSelf != (countdown || go))
                _countdownRoot.SetActive(countdown || go);
            if (_countdownText != null && (countdown || go))
                _countdownText.text = countdown ? Mathf.CeilToInt(_match.CountdownRemaining).ToString() : "¡YA!";
        }

        void UpdateBanner()
        {
            bool timeUp = _match.TimeUp;
            bool tie = !timeUp && _match.TiebreakPending;
            bool show = timeUp || tie;

            if (_bannerRoot != null && _bannerRoot.activeSelf != show) _bannerRoot.SetActive(show);
            if (!show) return;

            if (_bannerTitle != null) _bannerTitle.text = timeUp ? _timeUpTitle : _tieTitle;
            if (_bannerSubtitle != null) _bannerSubtitle.text = timeUp ? _timeUpSubtitle : _tieSubtitle;
        }

        void UpdateSpeed()
        {
            if (_car == null || _car.Stats == null) return;

            float speed = Mathf.Abs(_car.ForwardSpeed);
            int kmh = Mathf.RoundToInt(speed * 3.6f);
            if (kmh != _lastSpeed && _speedText != null)
            {
                _lastSpeed = kmh;
                _speedText.text = kmh.ToString();
            }
            if (_speedFill != null) _speedFill.fillAmount = Mathf.Clamp01(speed / _car.Stats.MaxSpeed);
        }
    }
}
