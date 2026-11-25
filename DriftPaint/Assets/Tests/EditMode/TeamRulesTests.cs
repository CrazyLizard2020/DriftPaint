using System.Collections.Generic;
using System.Linq;
using DriftPaint.Net;
using DriftPaint.Teams;
using NUnit.Framework;

namespace DriftPaint.Tests
{
    public class TeamRulesTests
    {
        static PlayerSummary P(int actorId, byte team) => new PlayerSummary(actorId, $"P{actorId}", team, actorId == 1, false, false);

        static Dictionary<int, byte> Apply(List<PlayerSummary> players, bool moveExisting)
        {
            var result = players.ToDictionary(p => p.ActorId, p => p.Team);
            foreach (TeamAssignment change in TeamRules.Balance(players, moveExisting))
                result[change.ActorId] = change.Team;
            return result;
        }

        static void AssertEven(Dictionary<int, byte> teams)
        {
            int a = teams.Values.Count(t => t == TeamId.A);
            int b = teams.Values.Count(t => t == TeamId.B);
            Assert.LessOrEqual(System.Math.Abs(a - b), 1, $"A={a} B={b}");
            Assert.AreEqual(teams.Count, a + b, "Todos tienen equipo");
        }

        [Test]
        public void NewPlayers_AlternateTeams()
        {
            var players = new List<PlayerSummary> { P(1, TeamId.None), P(2, TeamId.None), P(3, TeamId.None), P(4, TeamId.None) };
            Dictionary<int, byte> teams = Apply(players, true);
            Assert.AreEqual(TeamId.A, teams[1]);
            Assert.AreEqual(TeamId.B, teams[2]);
            Assert.AreEqual(TeamId.A, teams[3]);
            Assert.AreEqual(TeamId.B, teams[4]);
        }

        [Test]
        public void PlayerArrivingWithTheSameTeam_IsMovedBetweenMatches()
        {
            // Caso del lobby: el segundo jugador llegó con el equipo de su sala anterior.
            var players = new List<PlayerSummary> { P(1, TeamId.A), P(2, TeamId.A) };
            Dictionary<int, byte> teams = Apply(players, true);
            Assert.AreEqual(TeamId.A, teams[1]);
            Assert.AreEqual(TeamId.B, teams[2]);
        }

        [Test]
        public void UnevenTeams_MoveTheLatestArrival()
        {
            var players = new List<PlayerSummary> { P(1, TeamId.A), P(2, TeamId.A), P(3, TeamId.B), P(5, TeamId.A), P(7, TeamId.A) };
            List<TeamAssignment> changes = TeamRules.Balance(players, true);
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(7, changes[0].ActorId);
            Assert.AreEqual(TeamId.B, changes[0].Team);
            AssertEven(Apply(players, true));
        }

        [Test]
        public void DuringTheMatch_OnlyNewPlayersAreAssigned()
        {
            var players = new List<PlayerSummary> { P(1, TeamId.A), P(2, TeamId.A), P(3, TeamId.A), P(4, TeamId.None) };
            List<TeamAssignment> changes = TeamRules.Balance(players, false);
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(4, changes[0].ActorId);
            Assert.AreEqual(TeamId.B, changes[0].Team);
        }

        [Test]
        public void EvenTeams_HaveNoChanges()
        {
            var players = new List<PlayerSummary> { P(1, TeamId.A), P(2, TeamId.B), P(3, TeamId.A) };
            Assert.AreEqual(0, TeamRules.Balance(players, true).Count);
        }

        [Test]
        public void Switching_IsAllowedOnlyIfTeamsStayEven()
        {
            var oneVsOne = new List<PlayerSummary> { P(1, TeamId.A), P(2, TeamId.B) };
            Assert.IsFalse(TeamRules.CanSwitch(oneVsOne, 1));

            var twoVsOne = new List<PlayerSummary> { P(1, TeamId.A), P(2, TeamId.B), P(3, TeamId.A) };
            Assert.IsTrue(TeamRules.CanSwitch(twoVsOne, 3));
            Assert.IsFalse(TeamRules.CanSwitch(twoVsOne, 2));

            var alone = new List<PlayerSummary> { P(1, TeamId.A) };
            Assert.IsTrue(TeamRules.CanSwitch(alone, 1));
        }
    }
}
