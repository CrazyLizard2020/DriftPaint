using System.Collections.Generic;
using DriftPaint.Match;
using DriftPaint.Net;
using DriftPaint.Teams;
using UnityEngine;

namespace DriftPaint.UI.Feed
{
    /// <summary>
    /// Avisos de la partida arriba a la derecha: entradas, salidas y cortes de los jugadores, y novedades de la
    /// partida (MVP, cambio de líder, último minuto). El aviso más nuevo entra arriba y empuja a los demás hacia
    /// abajo; se ven a lo sumo unos pocos a la vez y cada uno se desvanece solo. Los avisos de red salen apenas
    /// pasan; los de partida esperan un mínimo entre uno y otro para no tapar el juego, y si mientras esperan
    /// queda viejo (por ejemplo, el MVP volvió a cambiar) se reemplaza por el más reciente.
    /// Los renglones se copian de una muestra del HUD; este componente solo escribe los textos y los mueve.
    /// </summary>
    public sealed class MatchFeed : MonoBehaviour
    {
        enum Topic { None, Mvp, Lead, LastMinute }

        struct Pending
        {
            public Topic Topic;
            public int FromActor;
            public int ToActor;
            public byte Team;
            public string Text;
            public Color Accent;
        }

        [SerializeField] MatchController _match;
        [SerializeField] TeamPaletteSO _palette;
        [Tooltip("Renglón de muestra, desactivado. Se copia uno por aviso dentro del mismo contenedor.")]
        [SerializeField] FeedEntryView _template;

        [Header("Tiempos")]
        [SerializeField, Min(1)] int _maxVisible = 5;
        [Tooltip("Segundos a opacidad completa.")]
        [SerializeField, Min(0.5f)] float _holdSeconds = 4f;
        [Tooltip("Segundos desde que aparece hasta que termina de desvanecerse.")]
        [SerializeField, Min(1f)] float _lifeSeconds = 6f;
        [Tooltip("Fundido de un aviso empujado fuera del límite.")]
        [SerializeField, Min(0.05f)] float _pushedFadeSeconds = 0.25f;
        [Tooltip("Separación mínima entre avisos de partida.")]
        [SerializeField, Min(0f)] float _matchMessageGap = 5f;
        [SerializeField, Min(1f)] float _slideSpeed = 14f;
        [SerializeField] float _spacing = 8f;

        [Header("Puntos")]
        [Tooltip("Ventaja en celdas que necesita un equipo para que se anuncie que pasó adelante.")]
        [SerializeField, Min(1)] int _leadMargin = 150;
        [Tooltip("Segundos de ronda antes de anunciar cambios de líder, para no avisar los primeros cruces.")]
        [SerializeField, Min(0f)] float _leadGraceSeconds = 15f;

        [Header("Colores")]
        [SerializeField] Color _joinedColor = new Color(0.094f, 0.874f, 0.941f);
        [SerializeField] Color _leftColor = new Color(0.91f, 0.29f, 0.23f);
        [SerializeField] Color _infoColor = new Color(0.96f, 0.97f, 0.98f);
        [SerializeField] Color _mvpColor = new Color(1f, 0.78f, 0.2f);

        [Header("Textos de red")]
        [SerializeField] string _joinedText = "{0} se ha conectado";
        [SerializeField] string _rejoinedText = "{0} se ha reconectado";
        [SerializeField] string _disconnectedText = "{0} se ha desconectado";
        [SerializeField] string _leftText = "{0} abandonó la partida";
        [SerializeField] string _timedOutText = "{0} no volvió a tiempo";
        [SerializeField] string _newHostText = "{0} es ahora el host";
        [SerializeField] string _connectionLostText = "Se perdió la conexión. Reconectando...";
        [SerializeField] string _localRejoinedText = "Volviste a la partida";

        [Header("Textos de partida")]
        [SerializeField] string _mvpText = "{0} es ahora el MVP";
        [SerializeField] string _mvpStolenText = "{0} le ha arrebatado el MVP a {1}";
        [SerializeField] string _leadText = "¡El equipo {0} toma la delantera!";
        [SerializeField] string _lastMinuteText = "¡Último minuto!";

        readonly List<FeedEntryView> _shown = new List<FeedEntryView>();
        readonly List<float> _shownAt = new List<float>();
        readonly List<FeedEntryView> _pushed = new List<FeedEntryView>();
        readonly Stack<FeedEntryView> _pool = new Stack<FeedEntryView>();
        readonly List<Pending> _queue = new List<Pending>();

        INetworkMediator _mediator;
        float _lastMatchMessageAt = float.NegativeInfinity;
        string _trackedMatch;
        int _mvpActor = -1;
        byte _leadTeam;
        bool _lastMinuteAnnounced;
        float _nextScan;

        void Awake()
        {
            if (_template != null) _template.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            _mediator = NetworkServices.Mediator;
            if (_mediator != null) _mediator.NoticeRaised += OnNotice;
        }

        void OnDisable()
        {
            if (_mediator != null) _mediator.NoticeRaised -= OnNotice;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (now >= _nextScan)
            {
                _nextScan = now + 0.25f;
                ScanMatch();
            }
            ReleaseQueue(now);
            Animate(now);
        }

        #region Avisos de red

        void OnNotice(RoomNotice notice)
        {
            string name = Paint(notice.PlayerName, notice.Team);
            switch (notice.Kind)
            {
                case RoomNoticeKind.Joined: Push(Format(_joinedText, name), _joinedColor); break;
                case RoomNoticeKind.Rejoined: Push(Format(_rejoinedText, name), _joinedColor); break;
                case RoomNoticeKind.Disconnected: Push(Format(_disconnectedText, name), _leftColor); break;
                case RoomNoticeKind.Left: Push(Format(_leftText, name), _leftColor); break;
                case RoomNoticeKind.TimedOut: Push(Format(_timedOutText, name), _leftColor); break;
                case RoomNoticeKind.NewHost: Push(Format(_newHostText, name), _infoColor); break;
                case RoomNoticeKind.LocalConnectionLost: Push(GameLanguage.Text(_connectionLostText), _leftColor); break;
                case RoomNoticeKind.LocalRejoined: Push(GameLanguage.Text(_localRejoinedText), _joinedColor); break;
            }
        }

        #endregion

        #region Avisos de partida

        /// <summary>Mira puntajes y reloj, y encola lo que cambió desde la última vez.</summary>
        void ScanMatch()
        {
            if (_mediator == null || _mediator.State != ConnectionState.InRoom || _match == null) return;
            if (_trackedMatch != _mediator.MatchId)
            {
                // Partida nueva (o revancha): se arranca de cero, sin avisos viejos en espera.
                _trackedMatch = _mediator.MatchId;
                _mvpActor = -1;
                _leadTeam = TeamId.None;
                _lastMinuteAnnounced = false;
                _queue.Clear();
            }
            if (!_match.Phase.IsRound() || !_match.AllowsDriving) return;

            ScanMvp();
            ScanLead();
            if (!_lastMinuteAnnounced && _match.Phase == MatchPhase.Playing && _match.DurationSeconds > 60 && _match.TimeRemaining <= 60f)
            {
                _lastMinuteAnnounced = true;
                Enqueue(new Pending { Topic = Topic.LastMinute, Text = GameLanguage.Text(_lastMinuteText), Accent = _mvpColor });
            }
        }

        void ScanMvp()
        {
            IReadOnlyList<PlayerSummary> players = _mediator.Players;
            int best = -1, bestPoints = 0, currentPoints = -1;
            foreach (PlayerSummary player in players)
            {
                if (player.ActorId == _mvpActor) currentPoints = player.Points;
                if (player.Points > bestPoints)
                {
                    best = player.ActorId;
                    bestPoints = player.Points;
                }
            }
            if (best < 0 || best == _mvpActor) return;
            // En un empate se queda quien ya lo tenía.
            if (currentPoints >= bestPoints) return;
            int previous = currentPoints >= 0 ? _mvpActor : -1;
            _mvpActor = best;
            Enqueue(new Pending { Topic = Topic.Mvp, FromActor = previous, ToActor = best });
        }

        void ScanLead()
        {
            if (_match.TimeSinceStart < _leadGraceSeconds) return;
            int a = _mediator.ScoreA, b = _mediator.ScoreB;
            byte leader = a - b >= _leadMargin ? TeamId.A : b - a >= _leadMargin ? TeamId.B : TeamId.None;
            if (leader == TeamId.None || leader == _leadTeam) return;
            // El primer líder de la ronda no se anuncia: el aviso es para cuando la partida se da vuelta.
            bool turnaround = _leadTeam != TeamId.None;
            _leadTeam = leader;
            if (turnaround) Enqueue(new Pending { Topic = Topic.Lead, Team = leader });
        }

        void Enqueue(Pending item)
        {
            for (int i = 0; i < _queue.Count; i++)
            {
                if (_queue[i].Topic != item.Topic) continue;
                // Un aviso del mismo tema que todavía no salió se reemplaza: el MVP conserva a quien lo tenía antes.
                if (item.Topic == Topic.Mvp) item.FromActor = _queue[i].FromActor;
                _queue.RemoveAt(i);
                break;
            }
            if (item.Topic == Topic.Mvp && item.FromActor == item.ToActor) return;
            _queue.Add(item);
        }

        void ReleaseQueue(float now)
        {
            if (_queue.Count == 0 || now - _lastMatchMessageAt < _matchMessageGap) return;
            Pending item = _queue[0];
            _queue.RemoveAt(0);
            _lastMatchMessageAt = now;
            switch (item.Topic)
            {
                case Topic.Mvp:
                    string to = PlayerName(item.ToActor);
                    if (item.FromActor >= 0) Push(Format(_mvpStolenText, to, PlayerName(item.FromActor)), _mvpColor);
                    else Push(Format(_mvpText, to), _mvpColor);
                    break;
                case Topic.Lead:
                    TeamPaletteSO.TeamColors colors = _palette != null ? _palette.Get(item.Team) : default;
                    string team = Paint(GameLanguage.Text(colors.DisplayName ?? ""), item.Team);
                    Push(Format(_leadText, team), colors.Main.a > 0f ? colors.Main : _infoColor);
                    break;
                default:
                    Push(item.Text, item.Accent);
                    break;
            }
        }

        string PlayerName(int actorId)
        {
            foreach (PlayerSummary player in _mediator.Players)
                if (player.ActorId == actorId) return Paint(player.Nickname, player.Team);
            return "?";
        }

        #endregion

        #region Renglones

        string Format(string key, params object[] args) => string.Format(GameLanguage.Text(key), args);

        /// <summary>Nombre con el color de su equipo. Se sacan las etiquetas para que un nombre no rompa el formato.</summary>
        string Paint(string text, byte team)
        {
            string clean = string.IsNullOrEmpty(text) ? "?" : text.Replace("<", "").Replace(">", "");
            if (_palette == null || team == TeamId.None) return $"<b>{clean}</b>";
            return $"<b><color=#{ColorUtility.ToHtmlStringRGB(_palette.Get(team).Main)}>{clean}</color></b>";
        }

        void Push(string text, Color accent)
        {
            if (_template == null || string.IsNullOrEmpty(text)) return;
            FeedEntryView entry = _pool.Count > 0 ? _pool.Pop() : Instantiate(_template, _template.transform.parent);
            entry.Show(text, accent);
            entry.Alpha = 0f;
            entry.transform.SetAsLastSibling();
            // Entra desde la derecha, en el primer lugar.
            entry.Rect.anchoredPosition = new Vector2(entry.Rect.rect.width * 0.4f, TargetY(0));
            _shown.Insert(0, entry);
            _shownAt.Insert(0, Time.unscaledTime);

            while (_shown.Count > _maxVisible)
            {
                int last = _shown.Count - 1;
                _pushed.Add(_shown[last]);
                _shown.RemoveAt(last);
                _shownAt.RemoveAt(last);
            }
        }

        float TargetY(int index)
        {
            float height = _template != null ? _template.Rect.rect.height : 50f;
            return -index * (height + _spacing);
        }

        void Animate(float now)
        {
            float blend = 1f - Mathf.Exp(-_slideSpeed * Time.unscaledDeltaTime);
            for (int i = _shown.Count - 1; i >= 0; i--)
            {
                FeedEntryView entry = _shown[i];
                float age = now - _shownAt[i];
                if (age >= _lifeSeconds)
                {
                    Recycle(entry);
                    _shown.RemoveAt(i);
                    _shownAt.RemoveAt(i);
                    continue;
                }
                float fadeIn = Mathf.Clamp01(age / 0.15f);
                float fadeOut = age <= _holdSeconds ? 1f : 1f - (age - _holdSeconds) / Mathf.Max(0.01f, _lifeSeconds - _holdSeconds);
                entry.Alpha = Mathf.Min(fadeIn, fadeOut);
                Vector2 position = entry.Rect.anchoredPosition;
                entry.Rect.anchoredPosition = Vector2.Lerp(position, new Vector2(0f, TargetY(i)), blend);
            }

            for (int i = _pushed.Count - 1; i >= 0; i--)
            {
                FeedEntryView entry = _pushed[i];
                entry.Alpha = Mathf.MoveTowards(entry.Alpha, 0f, Time.unscaledDeltaTime / _pushedFadeSeconds);
                Vector2 position = entry.Rect.anchoredPosition;
                entry.Rect.anchoredPosition = Vector2.Lerp(position, new Vector2(0f, TargetY(_maxVisible)), blend);
                if (entry.Alpha > 0f) continue;
                Recycle(entry);
                _pushed.RemoveAt(i);
            }
        }

        void Recycle(FeedEntryView entry)
        {
            entry.Hide();
            _pool.Push(entry);
        }

        #endregion
    }
}
