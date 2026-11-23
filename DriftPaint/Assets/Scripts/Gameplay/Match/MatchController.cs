using System;
using DriftPaint.Cars;
using DriftPaint.Net;
using UnityEngine;

namespace DriftPaint.Match
{
    /// <summary>
    /// Ciclo de la partida. El estado oficial vive en la sala y solo lo cambia el Master, pero cada
    /// cliente calcula el reloj por su cuenta con el tiempo del servidor: todos arrancan y terminan en
    /// el mismo instante sin mensajes por segundo. También bloquea el manejo fuera de la partida.
    /// Sin sala (jugando solo) simula el mismo ciclo localmente.
    /// </summary>
    public class MatchController : MonoBehaviour, IDrivingGate
    {
        [Tooltip("Segundos que se muestra el cierre antes de volver al lobby.")]
        [SerializeField, Min(1f)] float _resultsSeconds = 6f;

        [Header("Sin sala")]
        [SerializeField, Min(0f)] float _offlineCountdown = 3f;
        [SerializeField, Min(10)] int _offlineDurationSeconds = 120;

        INetworkMediator _mediator;
        bool _online;
        MatchPhase _offlinePhase = MatchPhase.Countdown;
        double _offlineStartTime;
        MatchPhase _phase = MatchPhase.Countdown;
        double _resultsSince = -1;
        bool _closing;

        /// <summary>Estado efectivo: el publicado, adelantado si el reloj ya cruzó el límite.</summary>
        public MatchPhase Phase => _phase;
        public bool AllowsDriving => _phase == MatchPhase.Playing || _phase == MatchPhase.Tiebreak;
        public bool IsOnline => _online;

        public double Now => _online ? _mediator.ServerTime : Time.timeAsDouble;
        public double StartTime => _online ? _mediator.MatchStartTime : _offlineStartTime;
        public int DurationSeconds => _online ? _mediator.CurrentRoom.DurationSeconds : _offlineDurationSeconds;

        /// <summary>Segundos hasta que termina la cuenta regresiva.</summary>
        public float CountdownRemaining => Mathf.Max(0f, (float)(StartTime - Now));
        /// <summary>Segundos desde que arrancó la partida (negativo durante la cuenta regresiva).</summary>
        public float TimeSinceStart => (float)(Now - StartTime);
        public float TimeRemaining => Mathf.Clamp(DurationSeconds - TimeSinceStart, 0f, DurationSeconds);

        public event Action<MatchPhase> PhaseChanged;
        /// <summary>Se dispara justo antes de volver al menú, para limpiar los objetos propios.</summary>
        public event Action Closing;

        void Awake()
        {
            _mediator = NetworkServices.Mediator;
            _online = _mediator != null && _mediator.State == ConnectionState.InRoom;
            _offlineStartTime = Time.timeAsDouble + _offlineCountdown;
        }

        void Update()
        {
            if (_closing) return;

            if (_online && _mediator.State != ConnectionState.InRoom)
            {
                Close();
                return;
            }

            MatchPhase published = _online ? _mediator.Phase : _offlinePhase;
            if (_online && published == MatchPhase.Lobby)
            {
                Close();
                return;
            }

            MatchPhase effective = Evaluate(published);
            if (effective != published) Publish(effective);
            SetPhase(effective);

            if (_phase == MatchPhase.Results)
            {
                if (_resultsSince < 0) _resultsSince = Time.unscaledTimeAsDouble;
                if (Time.unscaledTimeAsDouble - _resultsSince >= _resultsSeconds) FinishResults();
            }
        }

        MatchPhase Evaluate(MatchPhase published)
        {
            if (published != MatchPhase.Countdown && published != MatchPhase.Playing) return published;
            if (StartTime <= 0) return published;

            double now = Now;
            if (now >= StartTime + DurationSeconds) return MatchPhase.Results;
            if (now >= StartTime) return MatchPhase.Playing;
            return MatchPhase.Countdown;
        }

        /// <summary>
        /// Solo el Master escribe el estado en la sala; los demás solo lo adelantan localmente.
        /// El cambio tarda un viaje al servidor, así que no se repite el mismo pedido en cada frame.
        /// </summary>
        void Publish(MatchPhase phase)
        {
            if (!_online)
            {
                _offlinePhase = phase;
                return;
            }

            if (!_mediator.IsMaster) return;
            if (phase == _requestedPhase && Time.unscaledTimeAsDouble - _requestedAt < RequestRetrySeconds) return;

            _requestedPhase = phase;
            _requestedAt = Time.unscaledTimeAsDouble;
            _mediator.SetMatchPhase(phase);
        }

        const double RequestRetrySeconds = 1.0;
        MatchPhase? _requestedPhase;
        double _requestedAt;

        void SetPhase(MatchPhase phase)
        {
            if (_phase == phase) return;
            _phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        void FinishResults()
        {
            // Los clientes que no son Master esperan a que el Master publique Lobby.
            if (!_online) Close();
            else Publish(MatchPhase.Lobby);
        }

        void Close()
        {
            if (_closing) return;
            _closing = true;
            Closing?.Invoke();
            if (!SceneLoader.Load(MapCatalog.MenuScene))
                Debug.LogError($"No se pudo volver a la escena \"{MapCatalog.MenuScene}\". Revisá la lista de escenas del build.");
        }
    }
}
