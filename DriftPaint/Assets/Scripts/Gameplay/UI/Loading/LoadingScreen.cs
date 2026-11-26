using System.Collections.Generic;
using DriftPaint.Audio;
using DriftPaint.Match;
using DriftPaint.Net;
using DriftPaint.Teams;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DriftPaint.UI.Loading
{
    /// <summary>
    /// Pantalla de carga de la partida. Aparece al arrancar la partida, sobrevive al cambio de escena y se
    /// levanta sola cuando la sala publica la hora de arranque y empieza el 3, 2, 1. La barra avanza con la
    /// carga propia y después con la de los demás jugadores. Quien entra a una partida ya empezada (por
    /// ejemplo, al volver después de un corte) solo la ve mientras carga la escena, sin esperar a nadie.
    /// <para>
    /// Mientras espera muestra quién ya cargó, deja marcar las instrucciones como leídas (si todos lo hacen
    /// no se espera el mínimo del mapa), rota consejos y permite salir de la sala si alguien se cuelga.
    /// </para>
    /// Los elementos viven en el prefab; este componente solo escribe los datos del mapa y del progreso.
    /// </summary>
    public sealed class LoadingScreen : MonoBehaviour
    {
        [SerializeField] CanvasGroup _group;

        [Header("Mapa")]
        [SerializeField] Image _background;
        [SerializeField] TMP_Text _mapName;
        [SerializeField] TMP_Text _tagline;
        [SerializeField] LoadingStepView[] _steps;

        [Header("Progreso")]
        [Tooltip("Image en modo Filled horizontal.")]
        [SerializeField] Image _progressFill;
        [SerializeField] TMP_Text _progressPercent;
        [SerializeField] TMP_Text _status;
        [Tooltip("Brillo que recorre la parte llena de la barra. Su padre tiene que recortarlo (RectMask2D).")]
        [SerializeField] RectTransform _shine;
        [SerializeField, Min(0.2f)] float _shineSeconds = 1.3f;
        [Tooltip("Color al que late la barra cuando todos están listos.")]
        [SerializeField] Color _readyPulseColor = Color.white;

        [Header("Textos de estado")]
        [SerializeField] string _loadingText = "CARGANDO PARTIDA...";
        [Tooltip("{0} son los jugadores que ya cargaron y {1} el total.")]
        [SerializeField] string _waitingText = "ESPERANDO JUGADORES {0}/{1}";
        [Tooltip("{0} son los segundos que faltan para la cuenta regresiva.")]
        [SerializeField] string _readyText = "¡TODOS LISTOS! ARRANCA EN {0}";
        [SerializeField] string _reconnectingText = "RECONECTANDO...";

        [Header("Jugadores")]
        [SerializeField] TeamPaletteSO _palette;
        [Tooltip("Ficha de muestra, desactivada. Se copia una por jugador dentro del mismo contenedor.")]
        [SerializeField] LoadingPlayerView _playerTemplate;
        [SerializeField] string _playerLoadingText = "CARGANDO";
        [SerializeField] string _playerLoadedText = "CARGADO";
        [SerializeField] string _playerReadText = "¡LISTO!";
        [SerializeField] string _playerAwayText = "SIN CONEXIÓN";

        [Header("Listo")]
        [Tooltip("Marca las instrucciones como leídas. Si todos lo marcan, la partida arranca sin esperar el mínimo.")]
        [SerializeField] Button _readButton;
        [SerializeField] TMP_Text _readLabel;
        [SerializeField] string _readText = "¡LISTO!";
        [SerializeField] string _readDoneText = "ESPERANDO AL RESTO";

        [Header("Salir")]
        [SerializeField] Button _leaveButton;
        [SerializeField] TMP_Text _leaveLabel;
        [SerializeField] string _leaveText = "SALIR";
        [SerializeField] string _leaveConfirmText = "¿SALIR? TOCÁ DE NUEVO";
        [SerializeField, Min(1f)] float _leaveConfirmSeconds = 3f;

        [Header("Consejos")]
        [SerializeField] CanvasGroup _tipGroup;
        [SerializeField] TMP_Text _tipText;
        [SerializeField, Min(2f)] float _tipSeconds = 6f;

        [Header("Sonido")]
        [SerializeField] AudioSource _audio;
        [Tooltip("Suena una vez cuando todos terminaron de cargar.")]
        [SerializeField] AudioClip _readySting;
        [SerializeField, Range(0f, 1f)] float _stingVolume = 0.8f;

        [Header("Animación")]
        [SerializeField, Min(0.05f)] float _fadeSeconds = 0.4f;
        [Tooltip("Cuánto puede avanzar la barra por segundo, para que no salte.")]
        [SerializeField, Min(0.1f)] float _fillPerSecond = 0.9f;
        [Tooltip("Parte de la barra que corresponde a la carga propia; el resto avanza con los demás jugadores.")]
        [SerializeField, Range(0.3f, 1f)] float _ownShare = 0.7f;
        [Tooltip("Segundos fuera de una partida sin que empiece ninguna carga antes de cerrarse sola (por ejemplo, " +
                 "si la escena del mapa no está en el build).")]
        [SerializeField, Min(1f)] float _pendingGraceSeconds = 4f;

        static LoadingScreen _current;

        readonly List<LoadingPlayerView> _players = new List<LoadingPlayerView>();
        readonly List<string> _tips = new List<string>();

        string _mapId;
        float _shownProgress;
        bool _closing;
        bool _sawLoad;
        int _shownInScene;
        float _pendingSince = -1f;
        int _lastPercent = -1;
        string _lastStatus;
        bool _allReady;
        bool _stingPlayed;
        bool _relockCursor;
        bool _readSelected;
        float _leaveConfirmUntil = -1f;
        int _tipIndex = -1;
        float _tipShownAt;
        float _openedAt;
        Color _fillColor = Color.white;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() => _current = null;

        /// <summary>La pantalla está a la vista (o entrando).</summary>
        public static bool IsOpen => _current != null && !_current._closing;

        /// <summary>Muestra la pantalla del mapa. Si ya está abierta, solo la actualiza.</summary>
        public static void Show(string mapId)
        {
            if (_current == null)
            {
                MapLibrarySO library = MapLibrarySO.Active;
                if (library == null || library.LoadingScreen == null) return;
                GameObject instance = Instantiate(library.LoadingScreen);
                instance.name = library.LoadingScreen.name;
                DontDestroyOnLoad(instance);
                _current = instance.GetComponent<LoadingScreen>();
                if (_current == null)
                {
                    Debug.LogError("El prefab de la pantalla de carga no tiene el componente LoadingScreen.", instance);
                    Destroy(instance);
                    return;
                }
            }
            _current.Open(mapId);
        }

        /// <summary>La cierra con un fundido, por ejemplo si la carga no pudo empezar.</summary>
        public static void Hide()
        {
            if (_current != null) _current.BeginClose();
        }

        void Awake()
        {
            if (_progressFill != null) _fillColor = _progressFill.color;
            if (_playerTemplate != null) _playerTemplate.gameObject.SetActive(false);
            if (_readButton != null) _readButton.onClick.AddListener(OnRead);
            if (_leaveButton != null) _leaveButton.onClick.AddListener(OnLeave);
        }

        void Open(string mapId)
        {
            if (_closing || _mapId != mapId)
            {
                _shownProgress = 0f;
                _sawLoad = false;
                _shownInScene = SceneManager.GetActiveScene().handle;
                _stingPlayed = false;
                _readSelected = false;
                _openedAt = Time.unscaledTime;
                _tipIndex = -1;
            }
            _closing = false;
            _pendingSince = -1f;
            _leaveConfirmUntil = -1f;
            _mapId = mapId;
            if (_group != null)
            {
                _group.alpha = 1f;
                _group.blocksRaycasts = true;
            }
            Bind();
            Apply(GameLanguage.Text(_loadingText), _shownProgress);
        }

        void OnEnable() => GameLanguage.Changed += OnLanguageChanged;
        void OnDisable() => GameLanguage.Changed -= OnLanguageChanged;

        void OnDestroy()
        {
            if (_current == this) _current = null;
        }

        void OnLanguageChanged()
        {
            Bind();
            _lastStatus = null;
            if (_tipIndex >= 0) ShowTip(_tipIndex);
        }

        void Bind()
        {
            MapDefinitionSO map = MapLibrarySO.Find(_mapId);
            if (_background != null && map != null && map.LoadingBackground != null) _background.sprite = map.LoadingBackground;
            if (_mapName != null) _mapName.text = MapCatalog.DisplayName(_mapId);
            if (_tagline != null)
            {
                bool hasTagline = map != null && !string.IsNullOrEmpty(map.Tagline);
                _tagline.gameObject.SetActive(hasTagline);
                if (hasTagline) _tagline.text = GameLanguage.Text(map.Tagline);
            }

            _tips.Clear();
            if (map != null) AddTips(map.Tips);
            if (MapLibrarySO.Active != null) AddTips(MapLibrarySO.Active.Tips);
            if (_tipGroup != null) _tipGroup.gameObject.SetActive(_tips.Count > 0);

            if (_steps == null) return;
            for (int i = 0; i < _steps.Length; i++)
            {
                if (_steps[i] == null) continue;
                MapDefinitionSO.Step step = map != null && i < map.Steps.Length ? map.Steps[i] : null;
                if (step == null) _steps[i].Hide();
                else _steps[i].Show(i + 1, step.image, GameLanguage.Text(step.title), GameLanguage.Text(step.body));
            }
        }

        void AddTips(string[] tips)
        {
            foreach (string tip in tips)
                if (!string.IsNullOrWhiteSpace(tip)) _tips.Add(tip);
        }

        void BeginClose()
        {
            if (_closing) return;
            _closing = true;
            // Se devuelve el cursor a la partida si esta pantalla lo había soltado para usar los botones.
            if (_relockCursor && MapCatalog.MapForScene(SceneManager.GetActiveScene().name) != null && NetworkServices.Mediator?.State == ConnectionState.InRoom)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            _relockCursor = false;
        }

        void Update()
        {
            if (_closing)
            {
                if (_group != null)
                {
                    _group.blocksRaycasts = false;
                    _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, Time.unscaledDeltaTime / _fadeSeconds);
                    if (_group.alpha > 0f) return;
                }
                Destroy(gameObject);
                return;
            }

            float target = Evaluate(out string status, out bool done);
            // La barra nunca retrocede y al cerrar se completa.
            target = done ? 1f : Mathf.Max(_shownProgress, target);
            float speed = done ? _fillPerSecond * 4f : _fillPerSecond;
            _shownProgress = Mathf.MoveTowards(_shownProgress, target, Time.unscaledDeltaTime * speed);
            Apply(status, _shownProgress);

            INetworkMediator net = NetworkServices.Mediator;
            UpdatePlayers(net);
            UpdateButtons(net, done);
            UpdateTips();
            AnimateBar();
            if (_allReady && !_stingPlayed && !done) PlaySting();
            if (done) BeginClose();
            else FreeCursor();
        }

        /// <summary>Avance que debería mostrar la barra, texto de estado y si ya hay que levantar la pantalla.</summary>
        float Evaluate(out string status, out bool done)
        {
            done = false;
            _allReady = false;
            status = GameLanguage.Text(_loadingText);
            INetworkMediator net = NetworkServices.Mediator;

            if (SceneLoader.IsLoading)
            {
                _sawLoad = true;
                _pendingSince = -1f;
                return SceneLoader.Progress * _ownShare;
            }

            bool online = net != null && (net.State == ConnectionState.InRoom || net.State == ConnectionState.Reconnecting);
            // Se cortó la sala, se volvió al lobby o se juega sin sala: no hay partida que esperar.
            if (!online || net.Phase.IsBetweenMatches())
            {
                done = true;
                return 1f;
            }

            if (!InMatchScene())
            {
                // Todavía en la escena de antes o en el menú, esperando que empiece la carga del mapa.
                if (_pendingSince < 0f) _pendingSince = Time.unscaledTime;
                done = Time.unscaledTime - _pendingSince > _pendingGraceSeconds;
                return 0f;
            }
            _pendingSince = -1f;

            if (net.State == ConnectionState.Reconnecting)
            {
                status = GameLanguage.Text(_reconnectingText);
                return _ownShare;
            }

            // Partida ya empezada (se volvió a entrar o se unió tarde): se entra directo.
            if (net.Phase.IsRound())
            {
                done = true;
                return 1f;
            }

            if (net.MatchStartTime > 0)
            {
                double reveal = net.MatchStartTime - MatchController.CountdownSeconds;
                double remaining = reveal - net.ServerTime;
                done = remaining <= 0;
                _allReady = true;
                status = string.Format(GameLanguage.Text(_readyText), Mathf.Max(1, Mathf.CeilToInt((float)remaining)));
                return 1f;
            }

            var (loaded, required) = net.MatchLoading;
            if (net.LocalLoaded && required > 0 && loaded >= required)
            {
                // Todos cargaron: falta que el host publique la hora, que respeta el mínimo para leer.
                _allReady = true;
                MapDefinitionSO map = MapLibrarySO.Find(_mapId);
                float minimum = map != null ? map.MinimumSeconds : 0f;
                bool everyoneRead = net.MatchDoneReading >= required;
                double remaining = net.LoadStartTime > 0 && !everyoneRead
                    ? net.LoadStartTime + minimum - net.ServerTime
                    : 0;
                status = string.Format(GameLanguage.Text(_readyText), Mathf.Max(1, Mathf.CeilToInt((float)remaining)));
                return 1f;
            }

            status = string.Format(GameLanguage.Text(_waitingText), loaded, Mathf.Max(required, 1));
            float others = required > 0 ? Mathf.Clamp01((float)loaded / required) : 1f;
            return _ownShare + (1f - _ownShare) * others;
        }

        bool InMatchScene()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.handle != _shownInScene) _sawLoad = true;
            return _sawLoad && MapCatalog.MapForScene(active.name) != null;
        }

        void Apply(string status, float progress)
        {
            if (_progressFill != null) _progressFill.fillAmount = progress;
            int percent = Mathf.RoundToInt(progress * 100f);
            if (_progressPercent != null && percent != _lastPercent)
            {
                _lastPercent = percent;
                _progressPercent.text = percent + "%";
            }
            if (_status != null && status != _lastStatus)
            {
                _lastStatus = status;
                _status.text = status;
            }
        }

        #region Jugadores

        void UpdatePlayers(INetworkMediator net)
        {
            if (_playerTemplate == null) return;
            IReadOnlyList<PlayerSummary> players = net != null && net.State != ConnectionState.Disconnected ? net.Players : null;
            int count = players?.Count ?? 0;
            while (_players.Count < count)
            {
                LoadingPlayerView view = Instantiate(_playerTemplate, _playerTemplate.transform.parent);
                view.name = $"Player {_players.Count + 1}";
                _players.Add(view);
            }

            // Los puntos suspensivos se mueven para que se note que la carga sigue.
            string dots = new string('.', 1 + (int)(Time.unscaledTime * 2.5f) % 3);
            for (int i = 0; i < _players.Count; i++)
            {
                if (i >= count)
                {
                    _players[i].Hide();
                    continue;
                }
                PlayerSummary player = players[i];
                LoadingPlayerView.Status state = player.IsInactive ? LoadingPlayerView.Status.Disconnected
                    : player.IsLoaded && player.HasRead ? LoadingPlayerView.Status.Read
                    : player.IsLoaded ? LoadingPlayerView.Status.Loaded
                    : LoadingPlayerView.Status.Loading;
                string text = state switch
                {
                    LoadingPlayerView.Status.Disconnected => GameLanguage.Text(_playerAwayText),
                    LoadingPlayerView.Status.Read => GameLanguage.Text(_playerReadText),
                    LoadingPlayerView.Status.Loaded => GameLanguage.Text(_playerLoadedText),
                    _ => GameLanguage.Text(_playerLoadingText) + dots
                };
                Color team = _palette != null ? _palette.Get(player.Team).Main : DriftPaintPalette.Cyan;
                _players[i].Show(player.Nickname, team, player.IsLocal, state, text);
            }
        }

        #endregion

        #region Botones

        void UpdateButtons(INetworkMediator net, bool done)
        {
            bool waiting = !done && net != null && net.State == ConnectionState.InRoom && net.Phase == MatchPhase.Countdown &&
                           !SceneLoader.IsLoading && InMatchScene();
            if (_readButton != null)
            {
                // Solo se puede marcar después de cargar, y hasta que la cuenta ya está por empezar.
                bool canRead = waiting && net.LocalLoaded;
                bool hasRead = net != null && net.LocalDoneReading;
                _readButton.gameObject.SetActive(canRead);
                _readButton.interactable = canRead && !hasRead;
                if (_readLabel != null) _readLabel.text = GameLanguage.Text(hasRead ? _readDoneText : _readText);
                // Con joystick o teclado el botón queda elegido para confirmarlo directo.
                if (canRead && !hasRead && !_readSelected && EventSystem.current != null &&
                    EventSystem.current.currentSelectedGameObject == null)
                {
                    _readSelected = true;
                    EventSystem.current.SetSelectedGameObject(_readButton.gameObject);
                }
            }

            if (_leaveButton != null)
            {
                bool canLeave = !done && net != null && net.State != ConnectionState.Disconnected && !SceneLoader.IsLoading;
                _leaveButton.interactable = canLeave;
                bool confirming = Time.unscaledTime < _leaveConfirmUntil;
                if (_leaveLabel != null) _leaveLabel.text = GameLanguage.Text(confirming ? _leaveConfirmText : _leaveText);
            }
        }

        void OnRead()
        {
            INetworkMediator net = NetworkServices.Mediator;
            if (net == null || !net.LocalLoaded) return;
            net.SetLocalDoneReading();
        }

        /// <summary>Salir pide confirmación: el primer toque cambia el texto y el segundo sale de verdad.</summary>
        void OnLeave()
        {
            if (Time.unscaledTime >= _leaveConfirmUntil)
            {
                _leaveConfirmUntil = Time.unscaledTime + _leaveConfirmSeconds;
                return;
            }
            _leaveConfirmUntil = -1f;
            MatchController match = InMatchScene() ? FindAnyObjectByType<MatchController>() : null;
            if (match != null) match.Leave();
            else NetworkServices.Mediator?.LeaveRoom();
            _relockCursor = false;
            BeginClose();
        }

        /// <summary>La partida bloquea el cursor al cargar; mientras esta pantalla está a la vista se lo suelta.</summary>
        void FreeCursor()
        {
            if (Cursor.lockState == CursorLockMode.None) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _relockCursor = true;
        }

        #endregion

        #region Consejos, brillo y sonido

        void UpdateTips()
        {
            if (_tipText == null || _tips.Count == 0) return;
            float now = Time.unscaledTime;
            if (_tipIndex < 0 || now - _tipShownAt >= _tipSeconds)
            {
                // El primero es al azar para no ver siempre el mismo al empezar.
                int next = _tipIndex < 0 ? Random.Range(0, _tips.Count) : (_tipIndex + 1) % _tips.Count;
                ShowTip(next);
                _tipShownAt = now;
            }
            if (_tipGroup != null)
            {
                // Entra y sale con un fundido corto.
                float age = now - _tipShownAt;
                float fade = Mathf.Min(Mathf.Clamp01(age / 0.35f), Mathf.Clamp01((_tipSeconds - age) / 0.35f));
                _tipGroup.alpha = _tips.Count == 1 ? Mathf.Clamp01(age / 0.35f) : fade;
            }
        }

        void ShowTip(int index)
        {
            if (index < 0 || index >= _tips.Count) return;
            _tipIndex = index;
            _tipText.text = GameLanguage.Text(_tips[index]);
        }

        void AnimateBar()
        {
            if (_progressFill == null) return;
            float time = Time.unscaledTime - _openedAt;
            if (_shine != null && _shine.parent is RectTransform bar)
            {
                // El brillo cruza solo la parte llena y vuelve a empezar.
                float travel = bar.rect.width * _progressFill.fillAmount + _shine.rect.width;
                float t = (time % _shineSeconds) / _shineSeconds;
                _shine.anchoredPosition = new Vector2(-_shine.rect.width + travel * t, _shine.anchoredPosition.y);
            }
            // Con todos listos la barra late hacia el color de aviso.
            float pulse = _allReady ? 0.5f + 0.5f * Mathf.Sin(time * Mathf.PI * 3f) : 0f;
            _progressFill.color = Color.Lerp(_fillColor, _readyPulseColor, pulse * 0.45f);
        }

        void PlaySting()
        {
            _stingPlayed = true;
            if (_audio == null || _readySting == null) return;
            _audio.PlayOneShot(_readySting, _stingVolume * VolumeSettings.EffectsGain);
        }

        #endregion
    }
}
