using DriftPaint.Cars;
using DriftPaint.Net;
using DriftPaint.Teams;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.Match
{
    /// <summary>
    /// Escribe los valores del HUD de partida: marcador por equipo, reloj, cuenta regresiva, cierre,
    /// velocímetro, cooldown de disparo y barriles. Los elementos viven en la escena.
    /// </summary>
    public class MatchHUD : MonoBehaviour
    {
        [SerializeField] MatchController _match;

        [Header("Marcador")]
        [Tooltip("Equipo que se muestra a la izquierda del reloj.")]
        [SerializeField] byte _leftTeam = TeamId.B;
        [SerializeField] TMP_Text _leftPercentText;
        [SerializeField] TMP_Text _rightPercentText;
        [SerializeField] TMP_Text _clockText;

        [Header("Cuenta regresiva y cierre")]
        [SerializeField] GameObject _countdownRoot;
        [SerializeField] TMP_Text _countdownText;
        [Tooltip("Segundos que queda el cartel de arranque después de la cuenta regresiva.")]
        [SerializeField] float _goMessageSeconds = 0.8f;
        [SerializeField] GameObject _resultsRoot;

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
        INetworkMediator _mediator;
        int _lastClockSeconds = -1;
        int _lastSpeed = -1;

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

        void Start()
        {
            _mediator = NetworkServices.Mediator;
            SetAbilities(0f, 1f, 0);
        }

        void Update()
        {
            if (_match == null) return;

            UpdateScore();
            UpdateClock();
            UpdateSpeed();
        }

        void UpdateScore()
        {
            int scoreA = _mediator?.ScoreA ?? 0;
            int scoreB = _mediator?.ScoreB ?? 0;
            int left = _leftTeam == TeamId.A ? scoreA : scoreB;
            int right = _leftTeam == TeamId.A ? scoreB : scoreA;

            if (_leftPercentText != null) _leftPercentText.text = Percent(left);
            if (_rightPercentText != null) _rightPercentText.text = Percent(right);
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
            bool go = phase == MatchPhase.Playing && _match.TimeSinceStart < _goMessageSeconds;
            if (_countdownRoot != null && _countdownRoot.activeSelf != (countdown || go))
                _countdownRoot.SetActive(countdown || go);
            if (_countdownText != null && (countdown || go))
                _countdownText.text = countdown ? Mathf.CeilToInt(_match.CountdownRemaining).ToString() : "¡YA!";

            bool results = phase == MatchPhase.Results;
            if (_resultsRoot != null && _resultsRoot.activeSelf != results) _resultsRoot.SetActive(results);
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

        static string Percent(int cells) => $"{Mathf.RoundToInt(cells * 100f / MatchRules.TotalCells)}%";
    }
}
