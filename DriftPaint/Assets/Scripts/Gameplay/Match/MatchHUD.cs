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
        [SerializeField] Button _leaveMatchButton;

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
        [Tooltip("Segundos de espera del resultado antes de mostrar el cartel de fin de tiempo. Evita un destello ilegible cuando el Master responde enseguida.")]
        [SerializeField, Min(0f)] float _timeUpDelay = 0.6f;
        [Tooltip("Cartel que se ve entre el empate y el arranque del desempate.")]
        [SerializeField] string _tieTitle = "¡EMPATE!";
        [SerializeField] string _tieSubtitle = "Preparando ronda de desempate...";
        [Tooltip("Cartel mientras se intenta volver a la sala después de un corte. {0} son los segundos que quedan.")]
        [SerializeField] string _reconnectingTitle = "SIN CONEXIÓN";
        [SerializeField] string _reconnectingSubtitle = "Volviendo a la partida... {0} s";

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
        float _timeUpSince = -1f;

        public void SetTarget(CarController car) => _car = car;

        /// <summary>Estado del disparo y del inventario de barriles del auto local.</summary>
        public void SetAbilities(float fireCooldownRemaining, float fireCooldownTotal, int barrels)
        {
            if (_fireCooldownText != null)
                _fireCooldownText.text = fireCooldownRemaining > 0f ? $"{fireCooldownRemaining:0.0} s" : GameLanguage.Text("LISTO");
            if (_fireCooldownFill != null)
                _fireCooldownFill.fillAmount = fireCooldownTotal > 0f ? 1f - Mathf.Clamp01(fireCooldownRemaining / fireCooldownTotal) : 1f;
            if (_barrelsText != null) _barrelsText.text = $"x{barrels}";
        }

        void Start()
        {
            SetAbilities(0f, 1f, 0);
            if (_leaveMatchButton != null && _match != null) _leaveMatchButton.onClick.AddListener(_match.Leave);
        }

        void Update()
        {
            if (_match == null) return;
            // Con las opciones abiertas, la salida la ofrece el propio menú.
            bool showLeave = _match.IsOnline && _match.Phase != MatchPhase.Results && Cursor.lockState != CursorLockMode.Locked
                && !UI.Options.OptionsMenu.IsOpen;
            if (_leaveMatchButton != null && _leaveMatchButton.gameObject.activeSelf != showLeave)
                _leaveMatchButton.gameObject.SetActive(showLeave);

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
                _countdownText.text = countdown ? Mathf.CeilToInt(_match.CountdownRemaining).ToString() : GameLanguage.Text("¡YA!");
        }

        void UpdateBanner()
        {
            if (!_match.TimeUp) _timeUpSince = -1f;
            else if (_timeUpSince < 0f) _timeUpSince = Time.unscaledTime;

            bool reconnecting = _match.IsReconnecting;
            bool timeUp = !reconnecting && _match.TimeUp && Time.unscaledTime - _timeUpSince >= _timeUpDelay;
            bool tie = !reconnecting && !_match.TimeUp && _match.TiebreakPending;
            bool show = reconnecting || timeUp || tie;

            if (_bannerRoot != null && _bannerRoot.activeSelf != show) _bannerRoot.SetActive(show);
            if (!show) return;

            if (reconnecting)
            {
                int seconds = Mathf.CeilToInt(_match.ReconnectTimeRemaining);
                if (_bannerTitle != null) _bannerTitle.text = GameLanguage.Text(_reconnectingTitle);
                if (_bannerSubtitle != null) _bannerSubtitle.text = string.Format(GameLanguage.Text(_reconnectingSubtitle), seconds);
                return;
            }

            if (_bannerTitle != null) _bannerTitle.text = GameLanguage.Text(timeUp ? _timeUpTitle : _tieTitle);
            if (_bannerSubtitle != null) _bannerSubtitle.text = GameLanguage.Text(timeUp ? _timeUpSubtitle : _tieSubtitle);
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
