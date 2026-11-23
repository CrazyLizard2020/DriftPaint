using System.Collections.Generic;
using DriftPaint.Net;

namespace DriftPaint.Teams
{
    /// <summary>Reglas de armado de equipos.</summary>
    public static class TeamRules
    {
        /// <summary>
        /// Equipo para un jugador que entra: el que tiene menos integrantes, y el Equipo A si empatan.
        /// Se permiten equipos impares.
        /// </summary>
        public static byte PickTeam(IReadOnlyList<PlayerSummary> players, int enteringActorId)
        {
            int teamA = 0;
            int teamB = 0;
            foreach (PlayerSummary player in players)
            {
                if (player.ActorId == enteringActorId) continue;
                if (player.Team == TeamId.A) teamA++;
                else if (player.Team == TeamId.B) teamB++;
            }
            return teamB < teamA ? TeamId.B : TeamId.A;
        }
    }
}
