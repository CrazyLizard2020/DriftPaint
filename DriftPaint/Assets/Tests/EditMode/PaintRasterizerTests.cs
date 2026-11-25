using System.Collections.Generic;
using DriftPaint.Paint;
using NUnit.Framework;
using UnityEngine;

namespace DriftPaint.Tests
{
    public class PaintRasterizerTests
    {
        static List<int> Rasterize(Vector3 from, Vector3 to, float width)
        {
            var cells = new List<int>();
            PaintRasterizer.Rasterize(from, to, width, cells);
            return cells;
        }

        [Test]
        public void StoppedCar_PaintsTheCellBelow()
        {
            var position = new Vector3(0.5f, 0f, 0.5f);
            List<int> cells = Rasterize(position, position, PaintRules.SmallWidth);

            PaintGrid.TryGetIndex(position, out int expected);
            CollectionAssert.AreEqual(new[] { expected }, cells);
        }

        [Test]
        public void StraightSegment_SmallTierIsOneCellWide()
        {
            List<int> cells = Rasterize(new Vector3(0.5f, 0f, 0.5f), new Vector3(9.5f, 0f, 0.5f), PaintRules.SmallWidth);
            Assert.AreEqual(10, cells.Count);
        }

        [Test]
        public void WiderTiers_PaintMoreCells()
        {
            var from = new Vector3(0.5f, 0f, 0.5f);
            var to = new Vector3(9.5f, 0f, 0.5f);
            int small = Rasterize(from, to, PaintRules.SmallWidth).Count;
            int medium = Rasterize(from, to, PaintRules.MediumWidth).Count;
            int large = Rasterize(from, to, PaintRules.LargeWidth).Count;

            Assert.Less(small, medium);
            Assert.Less(medium, large);
            Assert.AreEqual(36, large); // Tres filas de diez más una columna de tres en cada punta redondeada.
        }

        [Test]
        public void SegmentOutsideArena_IsClipped()
        {
            List<int> cells = Rasterize(new Vector3(60.2f, 0f, 0.5f), new Vector3(80f, 0f, 0.5f), PaintRules.SmallWidth);
            foreach (int cell in cells)
                Assert.That(cell, Is.InRange(0, PaintGrid.CellCount - 1));
            Assert.AreEqual(4, cells.Count);
        }

        [Test]
        public void SameInput_GivesSameCells()
        {
            var from = new Vector3(-12.37f, 0f, 33.91f);
            var to = new Vector3(-9.02f, 0f, 35.44f);
            CollectionAssert.AreEqual(Rasterize(from, to, 3f), Rasterize(from, to, 3f));
        }

        [TestCase(0f, false, false, 0f)]
        [TestCase(10f, false, false, 1f)]
        [TestCase(41f, false, false, 2f)]
        [TestCase(91f, false, false, 3f)]
        [TestCase(10f, true, false, 3f)]
        [TestCase(100f, false, true, 0f)]
        public void Tiers_FollowSpeedDriftAndStun(float kmh, bool drifting, bool stunned, float expected)
        {
            Assert.AreEqual(expected, PaintRules.WidthFor(kmh, drifting, stunned));
        }

        [Test]
        public void DriftIsDetectedFromSlipAngle()
        {
            Vector3 forward = Vector3.forward;
            Assert.IsFalse(PaintRules.LooksLikeDrift(new Vector3(0f, 0f, 20f), forward, 5f));
            Assert.IsTrue(PaintRules.LooksLikeDrift(new Vector3(10f, 0f, 20f), forward, 5f));
            Assert.IsFalse(PaintRules.LooksLikeDrift(new Vector3(1f, 0f, 2f), forward, 5f));
        }
    }
}
