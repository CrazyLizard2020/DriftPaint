using System.Collections.Generic;
using System.Linq;
using DriftPaint.Combat;
using DriftPaint.Paint;
using NUnit.Framework;
using UnityEngine;

namespace DriftPaint.Tests
{
    public class CombatRulesTests
    {
        [Test]
        public void Projectile_StartsAtOrigin()
        {
            var origin = new Vector3(3f, 1.2f, -7f);
            Vector3 velocity = CombatRules.ProjectileVelocity(Vector3.forward, Vector3.zero);
            Assert.That(Vector3.Distance(origin, CombatRules.ProjectilePosition(origin, velocity, 0f)), Is.LessThan(0.0001f));
        }

        [Test]
        public void Projectile_ReachesItsApexWhenVerticalSpeedIsZero()
        {
            Vector3 velocity = CombatRules.ProjectileVelocity(Vector3.forward, Vector3.zero);
            float apexTime = CombatRules.ProjectileLiftSpeed / CombatRules.ProjectileGravity;
            float expected = CombatRules.ProjectileLiftSpeed * CombatRules.ProjectileLiftSpeed / (2f * CombatRules.ProjectileGravity);

            Vector3 apex = CombatRules.ProjectilePosition(Vector3.zero, velocity, apexTime);
            Assert.AreEqual(expected, apex.y, 0.001f);
            Assert.AreEqual(CombatRules.ProjectileForwardSpeed * apexTime, apex.z, 0.001f);
        }

        [Test]
        public void ProjectileVelocity_AddsCarSpeedOnThePlaneOnly()
        {
            Vector3 velocity = CombatRules.ProjectileVelocity(new Vector3(0f, 0.5f, 2f), new Vector3(1f, -4f, 10f));
            Assert.AreEqual(1f, velocity.x, 0.0001f);
            Assert.AreEqual(CombatRules.ProjectileLiftSpeed, velocity.y, 0.0001f);
            Assert.AreEqual(10f + CombatRules.ProjectileForwardSpeed, velocity.z, 0.0001f);
        }

        [TestCase(double.NegativeInfinity, 0.0, true)]
        [TestCase(10.0, 11.0, false)]
        [TestCase(10.0, 14.7, false)]
        [TestCase(10.0, 14.8, true)]
        [TestCase(10.0, 15.0, true)]
        public void FireCooldown_IsValidatedWithASmallTolerance(double last, double fire, bool allowed)
        {
            Assert.AreEqual(allowed, CombatRules.IsFireAllowed(last, fire));
        }

        [TestCase(-1, 1)]
        [TestCase(0, 1)]
        [TestCase(3, 4)]
        [TestCase(4, 4)]
        public void AddBarrel_NeverPassesTheMaximum(int current, int expected)
        {
            Assert.AreEqual(expected, CombatRules.AddBarrel(current));
        }

        [Test]
        public void BarrelSlide_StopsAfterTheSlideTime()
        {
            float full = CombatRules.BarrelDropSpeed * CombatRules.BarrelSlideTime * 0.5f;
            Assert.AreEqual(0f, CombatRules.BarrelSlideDistance(0f), 0.0001f);
            Assert.AreEqual(full, CombatRules.BarrelSlideDistance(CombatRules.BarrelSlideTime), 0.0001f);
            Assert.AreEqual(full, CombatRules.BarrelSlideDistance(5f), 0.0001f);
            Assert.Less(CombatRules.BarrelSlideDistance(CombatRules.BarrelSlideTime * 0.5f), full);
        }

        [Test]
        public void InsideSquare_IncludesTheCarMargin()
        {
            float edge = CombatRules.BarrelBlastSize * 0.5f + CombatRules.CarHalfWidth;
            Assert.IsTrue(CombatRules.IsInsideSquare(Vector3.zero, CombatRules.BarrelBlastSize, new Vector3(edge, 0f, -edge)));
            Assert.IsFalse(CombatRules.IsInsideSquare(Vector3.zero, CombatRules.BarrelBlastSize, new Vector3(edge + 0.1f, 0f, 0f)));
        }

        static List<int> Square(Vector3 center, int size)
        {
            var cells = new List<int>();
            PaintRasterizer.RasterizeSquare(center, size, cells);
            return cells;
        }

        [Test]
        public void ProjectileSquare_Is5x5AroundTheImpactCell()
        {
            var center = new Vector3(0.5f, 0f, 0.5f);
            List<int> cells = Square(center, CombatRules.ProjectileChunkSize);

            Assert.AreEqual(25, cells.Count);
            Assert.AreEqual(25, cells.Distinct().Count());
            PaintGrid.TryGetIndex(center, out int middle);
            Assert.Contains(middle, cells);
            PaintGrid.TryGetIndex(center + new Vector3(2f, 0f, -2f), out int corner);
            Assert.Contains(corner, cells);
            PaintGrid.TryGetIndex(center + new Vector3(3f, 0f, 0f), out int outside);
            Assert.IsFalse(cells.Contains(outside));
        }

        [Test]
        public void BarrelSquare_Is8x8()
        {
            Assert.AreEqual(64, Square(new Vector3(10.2f, 0f, -4.7f), CombatRules.BarrelBlastSize).Count);
        }

        [Test]
        public void Square_IsClippedAtTheGridEdge()
        {
            Vector3 corner = new Vector3(PaintGrid.Origin + 0.5f, 0f, PaintGrid.Origin + 0.5f);
            Assert.AreEqual(9, Square(corner, CombatRules.ProjectileChunkSize).Count);
        }
    }
}
