using System;
using DriftPaint.Cars;
using DriftPaint.Net;
using DriftPaint.Scoring;
using DriftPaint.Teams;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DriftPaint.Match
{
    /// <summary>
    /// Ciclo de la partida. El estado oficial vive en la sala y solo lo cambia el Master, pero cada
    /// cliente calcula el reloj por su cuenta con el tiempo del servidor: todos arrancan y terminan en
    /// el mismo instante sin mensajes por segundo. También bloquea el manejo fuera de la partida.
    /// Al agotarse el tiempo, el Master decide el ganador con su grilla o arranca un desempate.
    /// Sin sala (jugando solo) simula el mismo ciclo localmente.
    /// </summary>
    [DefaultExecutionOrder(-50)] // Antes que la pintura: el último tramo se procesa antes de cortar.
    public class MatchController : MonoBehaviour, IDrivingGate
    {
        public const int TiebreakSeconds = 60;
        /// <summary>Segundos entre el empate y el arranque del desempate, para que todos entiendan qué pasó.</summary>
        public const float TiebreakPauseSeconds = 3f;

        [SerializeField] MatchScore _score;
        [Tooltip("Segundos en la pantalla de resultados sin elegir nada antes de salir solo.")]
        [SerializeField, Min(5f)] float _resultsTimeout = 30f;

        [Header("Sin sala")]
        [SerializeField, Min(0f)] float _offlineCountdown = 3f;
        [SerializeField, Min(10)] int _offlineDurationSeconds = 120;

        const double RequestRetrySeconds = 1.0;
        const double ResolveRetrySeconds = 3.0;

        INetworkMediator _mediator;
        bool _online;

        MatchPhase _offlinePhase = MatchPhase.Countdown;
        double _offlineStartTime;
        int _offlineRound = 1;
        byte _offlineWinner;

        MatchPhase _phase = MatchPhase.Countdown;
        double _resultsSince = -1;
        bool _closing;
        bool _waitingForDisconnect;

        MatchPhase? _requestedPhase;
        double _requestedAt;
        int _resolvedRound = -1;
        double _resolvedAt;
        double _forfeitAt = -1;
        // Equipos que tuvieron jugadores en esta partida: si uno se queda sin nadie, pierde por abandono.
        bool _seenTeamA;
        bool _seenTeamB;

        /// <summary>Estado efectivo: el publicado, adelantado si la cuenta regresiva ya terminó.</summary>
        public MatchPhase Phase => _phase;
        /// <summary>Se terminó el tiempo de la ronda y se espera que el Master publique el resultado.</summary>
        public bool TimeUp => _phase.IsRound() && StartTime > 0 && Now >= StartTime + DurationSeconds;
        /// <summary>Hubo empate y se espera el arranque del desempate, con los autos quietos.</summary>
        public bool TiebreakPending => _phase == MatchPhase.Tiebreak && Now < StartTime;
        public bool AllowsDriving => !_closing && _phase.IsRound() && Now >= StartTime && !TimeUp;
        public bool IsOnline => _online;
        /// <summary>Se cortó la conexión y se intenta volver a la sala; la partida sigue en pantalla.</summary>
        public bool IsReconnecting => _online && _mediator.State == ConnectionState.Reconnecting;
        /// <summary>Segundos que quedan para volver a la sala.</summary>
        public float ReconnectTimeRemaining => IsReconnecting ? _mediator.ReconnectTimeRemaining : 0f;
        /// <summary>El Master, o este cliente si se juega solo.</summary>
        public bool IsAuthority => !_online || _mediator.IsMaster;

        public double Now => _online ? _mediator.ServerTime : Time.timeAsDouble;
        public double StartTime => _online ? _mediator.MatchStartTime : _offlineStartTime;
        public int Round => _online ? _mediator.Round : _offlineRound;
        public byte Winner => _online ? _mediator.Winner : _offlineWinner;
        public int DurationSeconds => _phase == MatchPhase.Tiebreak ? TiebreakSeconds
            : _online ? _mediator.CurrentRoom.DurationSeconds : _offlineDurationSeconds;

        /// <summary>Segundos hasta que termina la cuenta regresiva.</summary>
        public float CountdownRemaining => Mathf.Max(0f, (float)(StartTime - Now));
        /// <summary>Segundos desde que arrancó la ronda (negativo durante la cuenta regresiva).</summary>
        public float TimeSinceStart => (float)(Now - StartTime);
        public float TimeRemaining => Mathf.Clamp(DurationSeconds - TimeSinceStart, 0f, DurationSeconds);
        /// <summary>Segundos hasta salir solo de la pantalla de resultados.</summary>
        public float ResultsTimeRemaining =>
            _resultsSince < 0 ? _resultsTimeout : Mathf.Max(0f, _resultsTimeout - (float)(Time.unscaledTimeAsDouble - _resultsSince));

        public event Action<MatchPhase> PhaseChanged;
        /// <summary>Se dispara justo antes de dejar la escena, para limpiar los objetos propios.</summary>
        public event Action Closing;

        void Awake()
        {
            _mediator = NetworkServices.Mediator;
            _online = _mediator != null && _mediator.State == ConnectionState.InRoom;
            if (!_online) TeamPaletteSO.StartSoloMatch();
            _offlineStartTime = Time.timeAsDouble + _offlineCountdown;
        }

        void OnDestroy()
        {
            if (!_online) TeamPaletteSO.ClearSoloPair();
        }

        void Update()
        {
            if (_waitingForDisconnect && _mediator.State == ConnectionState.Disconnected)
            {
                _waitingForDisconnect = false;
                LoadMenu();
            }
            if (_closing) return;

            if (_online && _mediator.State != ConnectionState.InRoom)
            {
                // Mientras se intenta volver, la escena se queda: el reloj sigue con la hora del servidor.
                if (_mediator.State != ConnectionState.Reconnecting) Close();
                return;
            }

            MatchPhase published = _online ? _mediator.Phase : _offlinePhase;

            if (_phase == MatchPhase.Results)
            {
                // El host ya arrancó otra partida: se pasa por el menú, que carga la escena de nuevo.
                if (_online && (published == MatchPhase.Countdown || published.IsRound()))
                {
                    Close();
                    return;
                }
                if (ResultsTimeRemaining <= 0f) Leave();
                return;
            }

            if (_online && published == MatchPhase.Lobby)
            {
                Close();
                return;
            }

            MatchPhase effective = published;
            if (published == MatchPhase.Countdown && StartTime > 0 && Now >= StartTime)
            {
                effective = MatchPhase.Playing;
                Publish(MatchPhase.Playing);
            }
            SetPhase(effective);

            if (_online && CheckForfeit()) return;
            if (TimeUp && IsAuthority) Resolve();
        }

        /// <summary>
        /// Abandono: si un equipo que jugó se queda sin ningún jugador en la sala (ni siquiera uno
        /// desconectado esperando volver) y el otro sigue, la autoridad le da la victoria al que quedó.
        /// Los desconectados cuentan hasta que vence su reserva, así un corte breve no regala la partida.
        /// </summary>
        bool CheckForfeit()
        {
            if (!(_phase == MatchPhase.Countdown || _phase.IsRound())) return false;

            int playersA = 0, playersB = 0;
            foreach (PlayerSummary player in _mediator.Players)
            {
                if (player.Team == TeamId.A) playersA++;
                else if (player.Team == TeamId.B) playersB++;
            }
            _seenTeamA |= playersA > 0;
            _seenTeamB |= playersB > 0;

            byte winner = TeamId.None;
            if (_seenTeamA && playersA == 0 && playersB > 0) winner = TeamId.B;
            else if (_seenTeamB && playersB == 0 && playersA > 0) winner = TeamId.A;
            if (winner == TeamId.None || !IsAuthority) return false;

            // El resultado tarda un viaje al servidor: no se repite el pedido en cada frame.
            if (_forfeitAt >= 0 && Time.unscaledTimeAsDouble - _forfeitAt < ResolveRetrySeconds) return true;
            _forfeitAt = Time.unscaledTimeAsDouble;

            if (_score != null) _score.CompleteRound();
            int cellsA = _score != null ? _score.AuthorityCellsA : 0;
            int cellsB = _score != null ? _score.AuthorityCellsB : 0;
            _mediator.FinishMatch(cellsA, cellsB, winner);
            return true;
        }

        /// <summary>Solo el Master escribe el estado en la sala; los demás solo lo adelantan localmente.</summary>
        void Publish(MatchPhase phase)
        {
            if (!_online)
            {
                _offlinePhase = phase;
                return;
            }

            if (!_mediator.IsMaster) return;
            // El cambio tarda un viaje al servidor, así que no se repite el mismo pedido en cada frame.
            if (phase == _requestedPhase && Time.unscaledTimeAsDouble - _requestedAt < RequestRetrySeconds) return;

            _requestedPhase = phase;
            _requestedAt = Time.unscaledTimeAsDouble;
            _mediator.SetMatchPhase(phase);
        }

        /// <summary>
        /// Fin de la ronda en la autoridad: cierra la pintura, publica los puntajes finales y después el
        /// resultado. Gana quien tiene más celdas; con la misma cantidad exacta se juega un desempate.
        /// </summary>
        void Resolve()
        {
            int round = Round;
            if (round == _resolvedRound && Time.unscaledTimeAsDouble - _resolvedAt < ResolveRetrySeconds) return;
            _resolvedRound = round;
            _resolvedAt = Time.unscaledTimeAsDouble;

            if (_score != null) _score.CompleteRound();
            int cellsA = _score != null ? _score.AuthorityCellsA : 0;
            int cellsB = _score != null ? _score.AuthorityCellsB : 0;
            byte winner = ScoreRules.WinnerOf(cellsA, cellsB);

            if (_online)
            {
                if (winner == TeamId.None) _mediator.StartTiebreak(Now + TiebreakPauseSeconds);
                else _mediator.FinishMatch(cellsA, cellsB, winner);
                return;
            }

            if (winner == TeamId.None)
            {
                _offlineRound++;
                _offlineStartTime = Now + TiebreakPauseSeconds;
                _offlinePhase = MatchPhase.Tiebreak;
            }
            else
            {
                _offlineWinner = winner;
                _offlinePhase = MatchPhase.Results;
            }
        }

        void SetPhase(MatchPhase phase)
        {
            if (_phase == phase) return;
            _phase = phase;
            if (phase == MatchPhase.Results) _resultsSince = Time.unscaledTimeAsDouble;
            PhaseChanged?.Invoke(phase);
        }

        /// <summary>Revancha: vuelve al lobby de la misma sala. Jugando solo, arranca otra partida.</summary>
        public void Rematch()
        {
            if (_closing) return;

            if (!_online)
            {
                _closing = true;
                Closing?.Invoke();
                SceneLoader.Load(SceneManager.GetActiveScene().name);
                return;
            }

            if (_mediator.IsMaster) _mediator.SetMatchPhase(MatchPhase.Lobby);
            Close();
        }

        /// <summary>Salir: deja la sala y vuelve al menú principal.</summary>
        public void Leave()
        {
            if (_closing) return;

            bool inRoom = _online && (_mediator.State == ConnectionState.InRoom || _mediator.State == ConnectionState.Reconnecting);
            if (!inRoom)
            {
                Close();
                return;
            }

            bool reserve = !_mediator.Phase.IsBetweenMatches();
            // El siguiente Master necesita el último tramo de pintura si quien sale era la autoridad.
            if (reserve && _mediator.IsMaster && _score != null) _score.CompleteRound();
            _closing = true;
            if (!reserve) Closing?.Invoke();
            // La pista conserva los objetos remotos mientras todavía pueden llegar sus eventos de destrucción.
            _waitingForDisconnect = true;
            _mediator.LeaveRoomAndDisconnect();
        }

        void Close()
        {
            if (_closing) return;
            _closing = true;
            Closing?.Invoke();
            LoadMenu();
        }

        static void LoadMenu()
        {
            if (!SceneLoader.Load(MapCatalog.MenuScene))
                Debug.LogError($"No se pudo volver a la escena \"{MapCatalog.MenuScene}\". Revisá la lista de escenas del build.");
        }
    }
}
