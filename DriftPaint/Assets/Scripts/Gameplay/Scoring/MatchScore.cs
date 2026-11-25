using System.Collections.Generic;
using DriftPaint.Match;
using DriftPaint.Net;
using DriftPaint.Paint;
using DriftPaint.Profile;
using DriftPaint.Teams;
using UnityEngine;

namespace DriftPaint.Scoring
{
    /// <summary>
    /// Puntaje de la partida. El Master lo saca de su grilla oficial y lo publica cada segundo, solo si
    /// cambió: celdas por equipo en la sala y puntaje de cada jugador. Los demás clientes muestran lo
    /// publicado, nunca lo que cuentan ellos. Jugando solo, se lee directo de la grilla local.
    /// </summary>
    public class MatchScore : MonoBehaviour
    {
        [SerializeField] PaintSystem _paint;
        [SerializeField] PaintRingSystem _rings;
        [SerializeField] MatchController _match;
        [Tooltip("Cada cuántos segundos el Master publica el puntaje.")]
        [SerializeField, Min(0.2f)] float _publishInterval = 1f;

        // Jugadores vistos durante la partida, para seguir mostrando a quien se fue con su último puntaje.
        readonly Dictionary<int, ScoreEntry> _seen = new Dictionary<int, ScoreEntry>();
        readonly HashSet<int> _present = new HashSet<int>();

        INetworkMediator _mediator;
        float _publishTimer;

        bool Online => _mediator != null && _mediator.State == ConnectionState.InRoom;

        /// <summary>Celdas del Equipo A que se muestran: publicadas por el Master, o las locales jugando solo.</summary>
        public int CellsA => Online ? _mediator.ScoreA : _paint != null ? _paint.Confirmed.CountA : 0;
        public int CellsB => Online ? _mediator.ScoreB : _paint != null ? _paint.Confirmed.CountB : 0;

        /// <summary>Celdas de la grilla oficial de este cliente. Solo valen como resultado en la autoridad.</summary>
        public int AuthorityCellsA => _paint != null ? _paint.Confirmed.CountA : 0;
        public int AuthorityCellsB => _paint != null ? _paint.Confirmed.CountB : 0;

        public int LocalActorId => _mediator?.LocalActorId ?? 0;

        /// <summary>Equipo del jugador local. Jugando solo, el auto siempre es del Equipo A.</summary>
        public byte LocalTeam
        {
            get
            {
                if (!Online) return TeamId.A;
                foreach (PlayerSummary player in _mediator.Players)
                    if (player.IsLocal) return player.Team;
                return TeamId.None;
            }
        }

        void Awake() => _mediator = NetworkServices.Mediator;

        void Update()
        {
            if (!Online || !_mediator.IsMaster || _match == null || !_match.Phase.IsRound()) return;

            _publishTimer += Time.unscaledDeltaTime;
            if (_publishTimer < _publishInterval) return;
            _publishTimer = 0f;
            Publish();
        }

        /// <summary>
        /// Lo que hace la autoridad al agotarse el tiempo, antes de decidir el resultado: procesa los tramos
        /// pendientes, manda el último cambio de la grilla y publica los puntajes de jugador finales.
        /// </summary>
        public void CompleteRound()
        {
            if (_paint != null) _paint.CompleteRound();
            if (Online && _mediator.IsMaster) PublishPlayers();
            _publishTimer = 0f;
        }

        void Publish()
        {
            if (_paint == null) return;
            _mediator.PublishTeamScores(_paint.Confirmed.CountA, _paint.Confirmed.CountB);
            PublishPlayers();
        }

        void PublishPlayers()
        {
            if (_paint == null) return;
            foreach (var player in _mediator.Players)
            {
                var count = _paint.Confirmed.GetPlayer(player.ActorId);
                _mediator.PublishPlayerScore(player.ActorId, Points(count), count.Standing);
            }
        }

        int Points(PlayerPaintCount count) => ScoreRules.PointsOf(count) + (_rings != null ? _rings.Credit(count.ActorId) : 0);

        /// <summary>Llena la lista con los jugadores de los dos equipos, ya ordenada.</summary>
        public void BuildTable(List<ScoreEntry> entries)
        {
            entries.Clear();

            if (Online)
            {
                _present.Clear();
                foreach (PlayerSummary player in _mediator.Players)
                {
                    if (player.Team != TeamId.A && player.Team != TeamId.B) continue;
                    _present.Add(player.ActorId);
                    _seen[player.ActorId] = new ScoreEntry(player.ActorId, player.Nickname, player.Team,
                        player.Points, player.Standing, player.IsLocal, player.IsInactive);
                }

                foreach (KeyValuePair<int, ScoreEntry> pair in _seen)
                {
                    ScoreEntry entry = pair.Value;
                    if (!_present.Contains(pair.Key))
                        entry = new ScoreEntry(entry.ActorId, entry.Nickname, entry.Team, entry.Points, entry.Standing, false, true);
                    entries.Add(entry);
                }
            }
            else
            {
                PlayerPaintCount count = _paint != null ? _paint.Confirmed.GetPlayer(LocalActorId) : default;
                string name = _mediator != null && !string.IsNullOrEmpty(_mediator.LocalNickname)
                    ? _mediator.LocalNickname
                    : PlayerProfile.LoadNickname();
                entries.Add(new ScoreEntry(LocalActorId, name, TeamId.A, Points(count), count.Standing, true, false));
            }

            ScoreRules.Sort(entries);
        }
    }
}
