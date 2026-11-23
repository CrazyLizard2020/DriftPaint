using System.Collections.Generic;
using DriftPaint.Paint;
using DriftPaint.Scoring;
using DriftPaint.Teams;
using NUnit.Framework;

namespace DriftPaint.Tests
{
    public class ScoreRulesTests
    {
        static ScoreEntry Entry(int actor, byte team, int points, int standing) =>
            new ScoreEntry(actor, $"P{actor}", team, points, standing, false, false);

        [Test]
        public void PlayerScore_IsStandingPlusSteals()
        {
            Assert.AreEqual(1240, ScoreRules.PointsOf(new PlayerPaintCount { ActorId = 1, Standing = 1040, Steals = 200 }));
        }

        [Test]
        public void Table_EmptyCellCountsOnlyWhileStanding()
        {
            var grid = new PaintGrid();
            grid.Paint(10, TeamId.A, 1);
            Assert.AreEqual(1, ScoreRules.PointsOf(grid.GetPlayer(1)));

            grid.Paint(10, TeamId.B, 2);
            Assert.AreEqual(0, ScoreRules.PointsOf(grid.GetPlayer(1)));
            Assert.AreEqual(2, ScoreRules.PointsOf(grid.GetPlayer(2))); // En pie + robo.
        }

        [Test]
        public void Table_StealIsKeptAfterLosingTheCell()
        {
            var grid = new PaintGrid();
            grid.Paint(10, TeamId.A, 1);
            grid.Paint(10, TeamId.B, 2);
            grid.Paint(10, TeamId.A, 1);

            Assert.AreEqual(2, ScoreRules.PointsOf(grid.GetPlayer(1)));
            Assert.AreEqual(1, ScoreRules.PointsOf(grid.GetPlayer(2)));
        }

        [Test]
        public void Table_RepaintingTeammateCellAddsNothing()
        {
            var grid = new PaintGrid();
            grid.Paint(10, TeamId.A, 1);
            grid.Paint(10, TeamId.A, 3);

            Assert.AreEqual(1, ScoreRules.PointsOf(grid.GetPlayer(1)));
            Assert.AreEqual(0, ScoreRules.PointsOf(grid.GetPlayer(3)));
            Assert.AreEqual(1, grid.GetOwner(10));
        }

        [TestCase(0, 0, 0, 0)]
        [TestCase(300, 100, 75, 25)]
        [TestCase(1, 2, 33, 67)]
        [TestCase(1, 1, 50, 50)]
        [TestCase(1001, 1000, 50, 50)]
        [TestCase(1, 7, 13, 87)] // 12,5 % se redondea hacia arriba.
        public void Percentages_AreOverPaintedCellsAndAddUpTo100(int cellsA, int cellsB, int expectedA, int expectedB)
        {
            ScoreRules.Percentages(cellsA, cellsB, out int percentA, out int percentB);
            Assert.AreEqual(expectedA, percentA);
            Assert.AreEqual(expectedB, percentB);
        }

        [Test]
        public void Winner_ComesFromCellsNotRoundedPercent()
        {
            Assert.AreEqual(TeamId.A, ScoreRules.WinnerOf(1001, 1000));
            Assert.AreEqual(TeamId.B, ScoreRules.WinnerOf(0, 1));
            Assert.AreEqual(TeamId.None, ScoreRules.WinnerOf(500, 500));
        }

        [Test]
        public void Sort_ByPointsThenStandingThenJoinOrder()
        {
            var entries = new List<ScoreEntry>
            {
                Entry(4, TeamId.A, 100, 80),
                Entry(2, TeamId.A, 100, 80),
                Entry(3, TeamId.A, 100, 90),
                Entry(1, TeamId.A, 50, 50),
                Entry(5, TeamId.A, 200, 10)
            };

            ScoreRules.Sort(entries);

            CollectionAssert.AreEqual(new[] { 5, 3, 2, 4, 1 }, entries.ConvertAll(e => e.ActorId));
        }

        [Test]
        public void Mvp_IsBestOfWinningTeam()
        {
            var entries = new List<ScoreEntry>
            {
                Entry(1, TeamId.A, 900, 800),
                Entry(2, TeamId.B, 400, 300),
                Entry(3, TeamId.B, 400, 350)
            };

            Assert.AreEqual(3, ScoreRules.FindMvp(entries, TeamId.B));
            Assert.AreEqual(1, ScoreRules.FindMvp(entries, TeamId.A));
            Assert.AreEqual(-1, ScoreRules.FindMvp(entries, TeamId.None));
        }

        [TestCase(0, "0")]
        [TestCase(999, "999")]
        [TestCase(1240, "1.240")]
        [TestCase(1234567, "1.234.567")]
        public void Points_UseDotAsThousandsSeparator(int points, string expected)
        {
            Assert.AreEqual(expected, ScoreRules.FormatPoints(points));
        }
    }
}
