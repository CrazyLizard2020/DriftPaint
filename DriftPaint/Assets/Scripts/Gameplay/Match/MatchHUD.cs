using DriftPaint.Cars;
using DriftPaint.Combat;
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
        [Tooltip("Lugar del cooldown dentro del panel de habilidades. Se ensancha si la palabra LISTO del idioma elegido " +
                 "no entra, así el panel no cambia de tamaño entre la cuenta y el disparo listo.")]
        [SerializeField] LayoutElement _fireCooldownSlot;
        [Tooltip("Image en modo Filled: llena cuando el disparo está listo.")]
        [SerializeField] Image _fireCooldownFill;
        [SerializeField] TMP_Text _barrelsText;

        CarController _car;
        float _fireCooldownSlotWidth;
        // Último estado recibido: el arma solo avisa cuando cambia, así que al cambiar de idioma se reescribe desde acá.
        float _fireRemaining;
        float _fireTotal = 1f;
        int _barrels;
        int _lastClockSeconds = -1;
        int _lastSpeed = -1;
        int _lastCellsA = -1;
        int _lastCellsB = -1;
        float _timeUpSince = -1f;
        int _artRevision = -1;

        RawImage _hitImage;
        Material _hitMaterial;
        static readonly int PaintColor = Shader.PropertyToID("_PaintColor");
        static readonly int Hit = Shader.PropertyToID("_Hit");
        static readonly int Flow = Shader.PropertyToID("_Flow");

        public void SetTarget(CarController car)
        {
            if (_car != car && _hitImage) _hitImage.enabled = false;
            _car = car;
        }

        public void ShowHitPaint(CarController car, PaintHitArt art, Color color, float seed, float strength, float opacity, float drain)
        {
            if (!isActiveAndEnabled || _car != car || !art.screenMaterial || !_clockText || !_clockText.canvas) return;
            if (!_hitImage)
            {
                var go = new GameObject("Salpicadura de impacto", typeof(RectTransform), typeof(RawImage));
                _hitImage = go.GetComponent<RawImage>();
                var rect = _hitImage.rectTransform;
                rect.SetParent(_clockText.canvas.rootCanvas.transform, false);
                rect.SetAsFirstSibling();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                _hitImage.raycastTarget = false;
                _hitMaterial = new Material(art.screenMaterial);
                _hitImage.material = _hitMaterial;
            }
            _hitImage.enabled = opacity * art.screenOpacity > .001f;
            _hitMaterial.SetColor(PaintColor, color);
            _hitMaterial.SetVector(Hit, new Vector4(strength * art.coverage, opacity * art.screenOpacity, drain, seed));
            _hitMaterial.SetVector(Flow, new Vector4(art.dripLength, art.dripSpeed, art.irregularity,
                _hitImage.rectTransform.rect.width / Mathf.Max(1, _hitImage.rectTransform.rect.height)));
        }

        public void HideHitPaint(CarController car) { if (_car == car && _hitImage) _hitImage.enabled = false; }
        void OnDestroy()
        {
            if (_hitImage) Destroy(_hitImage.gameObject);
            if (_hitMaterial) Destroy(_hitMaterial);
        }

        /// <summary>Estado del disparo y del inventario de barriles del auto local.</summary>
        public void SetAbilities(float fireCooldownRemaining, float fireCooldownTotal, int barrels)
        {
            _fireRemaining = fireCooldownRemaining;
            _fireTotal = fireCooldownTotal;
            _barrels = barrels;
            if (_fireCooldownText != null)
                _fireCooldownText.text = fireCooldownRemaining > 0f ? $"{fireCooldownRemaining:0.0} s" : GameLanguage.Text("LISTO");
            if (_fireCooldownFill != null)
                _fireCooldownFill.fillAmount = fireCooldownTotal > 0f ? 1f - Mathf.Clamp01(fireCooldownRemaining / fireCooldownTotal) : 1f;
            if (_barrelsText != null) _barrelsText.text = $"x{barrels}";
        }

        void Awake()
        {
            if (_fireCooldownSlot != null) _fireCooldownSlotWidth = _fireCooldownSlot.minWidth;
            ApplyArt();
        }

        public void ApplyArt()
        {
            var art = DrivingArtProfile.Active;
            if (art == null || _artRevision == art.Revision) return;
            _artRevision = art.Revision;
            foreach (string path in new[] { "Score Bar/Background", "Abilities" })
                if (transform.Find(path) is Transform panel && panel.TryGetComponent<Image>(out var image))
                    image.color = art.hudPanel;
            foreach (TMP_Text label in new[] { _clockText, _speedText, _fireCooldownText, _barrelsText })
                if (label != null) label.color = art.hudText;
            if (_clockText != null) _clockText.fontStyle |= FontStyles.Italic;
            if (_fireCooldownText != null) _fireCooldownText.fontStyle |= FontStyles.Italic;
            if (transform.Find("Speedometer/Unit") is RectTransform unit)
            {
                unit.GetComponent<TMP_Text>().color = art.hudText;
                unit.offsetMin = new Vector2(unit.offsetMin.x, 43f);
            }
        }

        void OnEnable() => GameLanguage.Changed += RefreshLanguage;
        void OnDisable()
        {
            GameLanguage.Changed -= RefreshLanguage;
            if (_hitImage) _hitImage.enabled = false;
        }

        void Start()
        {
            FitFireCooldown();
            SetAbilities(0f, 1f, 0);
            if (_leaveMatchButton != null && _match != null) _leaveMatchButton.onClick.AddListener(_match.Leave);
        }

        void RefreshLanguage()
        {
            FitFireCooldown();
            SetAbilities(_fireRemaining, _fireTotal, _barrels);
        }

        /// <summary>Reserva el ancho de la palabra LISTO en el idioma actual, sin bajar del ancho elegido en la escena.</summary>
        void FitFireCooldown()
        {
            if (_fireCooldownSlot == null || _fireCooldownText == null) return;
            float ready = _fireCooldownText.GetPreferredValues(GameLanguage.Text("LISTO")).x;
            _fireCooldownSlot.minWidth = Mathf.Max(_fireCooldownSlotWidth, Mathf.Ceil(ready));
        }

        void Update()
        {
            ApplyArt();
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
