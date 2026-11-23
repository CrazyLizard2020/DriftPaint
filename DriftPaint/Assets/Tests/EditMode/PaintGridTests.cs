using DriftPaint.Paint;
using DriftPaint.Teams;
using NUnit.Framework;
using UnityEngine;

namespace DriftPaint.Tests
{
    public class PaintGridTests
    {
        [Test]
        public void Index_CoversArenaCorners()
        {
            Assert.IsTrue(PaintGrid.TryGetIndex(new Vector3(-64f, 0f, -64f), out int first));
            Assert.AreEqual(0, first);

            Assert.IsTrue(PaintGrid.TryGetIndex(new Vector3(63.99f, 0f, 63.99f), out int last));
            Assert.AreEqual(PaintGrid.CellCount - 1, last);

            Assert.IsTrue(PaintGrid.TryGetIndex(new Vector3(-63.5f, 0f, -62.5f), out int secondRow));
            Assert.AreEqual(PaintGrid.Size, secondRow);
        }

        [Test]
        public void Index_RejectsOutsideArena()
        {
            Assert.IsFalse(PaintGrid.TryGetIndex(new Vector3(64f, 0f, 0f), out _));
            Assert.IsFalse(PaintGrid.TryGetIndex(new Vector3(0f, 0f, -64.01f), out _));
        }

        [Test]
        public void CellCenter_MatchesIndex()
        {
            PaintGrid.TryGetIndex(new Vector3(10.3f, 0f, -20.7f), out int index);
            Vector2 center = PaintGrid.CellCenter(index);
            Assert.AreEqual(10.5f, center.x, 1e-4f);
            Assert.AreEqual(-20.5f, center.y, 1e-4f);
        }

        [Test]
        public void Query_ReturnsPaintedTeam()
        {
            var grid = new PaintGrid();
            var position = new Vector3(3.2f, 0f, 4.8f);
            PaintGrid.TryGetIndex(position, out int index);

            Assert.AreEqual(TeamId.None, grid.GetTeamAt(position));
            grid.Paint(index, TeamId.B, 7);
            Assert.AreEqual(TeamId.B, grid.GetTeamAt(position));
            Assert.AreEqual(7, grid.GetOwner(index));
        }

        [Test]
        public void PaintingEmptyCell_AddsStandingWithoutSteal()
        {
            var grid = new PaintGrid();
            Assert.IsTrue(grid.Paint(100, TeamId.A, 1));

            Assert.AreEqual(1, grid.CountA);
            Assert.AreEqual(1, grid.GetPlayer(1).Standing);
            Assert.AreEqual(0, grid.GetPlayer(1).Steals);
        }

        [Test]
        public void RepaintingOwnTeam_ChangesNothing()
        {
            var grid = new PaintGrid();
            grid.Paint(100, TeamId.A, 1);

            Assert.IsFalse(grid.Paint(100, TeamId.A, 2));
            Assert.AreEqual(1, grid.GetOwner(100));
            Assert.AreEqual(1, grid.GetPlayer(1).Standing);
            Assert.AreEqual(0, grid.GetPlayer(2).Standing);
        }

        [Test]
        public void PaintingRivalCell_CountsStealAndMovesStanding()
        {
            var grid = new PaintGrid();
            grid.Paint(100, TeamId.A, 1);
            grid.Paint(100, TeamId.B, 2);

            Assert.AreEqual(0, grid.CountA);
            Assert.AreEqual(1, grid.CountB);
            Assert.AreEqual(0, grid.GetPlayer(1).Standing);
            Assert.AreEqual(1, grid.GetPlayer(2).Standing);
            Assert.AreEqual(1, grid.GetPlayer(2).Steals);
        }
    }
}
