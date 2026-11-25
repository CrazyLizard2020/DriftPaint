using System.Collections.Generic;
using DriftPaint.Net;

namespace DriftPaint.Teams
{
    /// <summary>Cambio de equipo que decide el reparto: jugador y equipo nuevo.</summary>
    public readonly struct TeamAssignment
    {
        public readonly int ActorId;
        public readonly byte Team;

        public TeamAssignment(int actorId, byte team)
        {
            ActorId = actorId;
            Team = team;
        }
    }

    /// <summary>Reglas de armado de equipos: siempre parejos, con a lo sumo un jugador de diferencia.</summary>
    public static class TeamRules
    {
        /// <summary>
        /// Equipo para un jugador que entra: el que tiene menos integrantes, y el Equipo A si empatan.
        /// Se permiten equipos impares.
        /// </summary>
        public static byte PickTeam(IReadOnlyList<PlayerSummary> players, int enteringActorId)
        {
            Count(players, enteringActorId, out int teamA, out int teamB);
            return teamB < teamA ? TeamId.B : TeamId.A;
        }

        /// <summary>
        /// Reparto parejo. Quien no tiene equipo va al más chico, en orden de entrada. Si moveExisting está
        /// activo (entre partidas) y aun así un equipo tiene dos o más jugadores de ventaja, pasa al otro
        /// equipo al último que entró del más grande, hasta emparejar. Devuelve solo los cambios.
        /// </summary>
        public static List<TeamAssignment> Balance(IReadOnlyList<PlayerSummary> players, bool moveExisting)
        {
            var ordered = new List<PlayerSummary>(players);
            ordered.Sort((a, b) => a.ActorId.CompareTo(b.ActorId));

            var teams = new Dictionary<int, byte>();
            int teamA = 0, teamB = 0;
            foreach (PlayerSummary player in ordered)
            {
                if (player.Team != TeamId.A && player.Team != TeamId.B) continue;
                teams[player.ActorId] = player.Team;
                if (player.Team == TeamId.A) teamA++;
                else teamB++;
            }

            foreach (PlayerSummary player in ordered)
            {
                if (teams.ContainsKey(player.ActorId)) continue;
                byte team = teamB < teamA ? TeamId.B : TeamId.A;
                teams[player.ActorId] = team;
                if (team == TeamId.A) teamA++;
                else teamB++;
            }

            while (moveExisting && System.Math.Abs(teamA - teamB) > 1)
            {
                byte from = teamA > teamB ? TeamId.A : TeamId.B;
                for (int i = ordered.Count - 1; i >= 0; i--)
                {
                    if (teams[ordered[i].ActorId] != from) continue;
                    teams[ordered[i].ActorId] = TeamId.Other(from);
                    break;
                }
                if (from == TeamId.A) { teamA--; teamB++; }
                else { teamB--; teamA++; }
            }

            var changes = new List<TeamAssignment>();
            foreach (PlayerSummary player in ordered)
                if (teams[player.ActorId] != player.Team) changes.Add(new TeamAssignment(player.ActorId, teams[player.ActorId]));
            return changes;
        }

        /// <summary>Un jugador puede pasarse al otro equipo si con eso los equipos siguen parejos.</summary>
        public static bool CanSwitch(IReadOnlyList<PlayerSummary> players, int actorId)
        {
            byte team = TeamId.None;
            foreach (PlayerSummary player in players)
                if (player.ActorId == actorId) team = player.Team;
            if (team != TeamId.A && team != TeamId.B) return false;

            Count(players, -1, out int teamA, out int teamB);
            int own = team == TeamId.A ? teamA : teamB;
            int other = team == TeamId.A ? teamB : teamA;
            return other < own;
        }

        static void Count(IReadOnlyList<PlayerSummary> players, int except, out int teamA, out int teamB)
        {
            teamA = 0;
            teamB = 0;
            foreach (PlayerSummary player in players)
            {
                if (player.ActorId == except) continue;
                if (player.Team == TeamId.A) teamA++;
                else if (player.Team == TeamId.B) teamB++;
            }
        }
    }
}
