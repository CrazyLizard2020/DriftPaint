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
        const float ForwardSpeed = 32f;
        const float LiftSpeed = 7f;
        const float Gravity = 22f;
        const float Cooldown = 5f;
        const float CooldownTolerance = 0.25f;
        const float DropSpeed = 5f / 3.6f;
        const float SlideTime = 0.6f;

        [Test]
        public void BarrelOpacity_StaysSolidBeforeTheWarning()
        {
            Assert.AreEqual(1f, CombatRules.BarrelOpacity(10f, 2f, 0.15f));
            Assert.AreEqual(1f, CombatRules.BarrelOpacity(2f, 2f, 0.15f));
        }

        [Test]
        public void BarrelOpacity_BlinksBetweenMinimumAndSolidDuringTheWarning()
        {
            float lowest = 1f;
            float highest = 0f;
            for (float remaining = 1.99f; remaining > 0f; remaining -= 0.01f)
            {
                float opacity = CombatRules.BarrelOpacity(remaining, 2f, 0.15f);
                Assert.GreaterOrEqual(opacity, 0.15f - 0.0001f);
                Assert.LessOrEqual(opacity, 1f);
                lowest = Mathf.Min(lowest, opacity);
                highest = Mathf.Max(highest, opacity);
            }
            Assert.Less(lowest, 0.2f, "Tiene que llegar casi a transparente.");
            Assert.Greater(highest, 0.95f, "Tiene que volver a verse opaco entre parpadeos.");
        }

        [Test]
        public void Projectile_StartsAtOrigin()
        {
            var origin = new Vector3(3f, 1.2f, -7f);
            Vector3 velocity = CombatRules.ProjectileVelocity(Vector3.forward, Vector3.zero, ForwardSpeed, LiftSpeed);
            Assert.That(Vector3.Distance(origin, CombatRules.ProjectilePosition(origin, velocity, Gravity, 0f)), Is.LessThan(0.0001f));
        }

        [Test]
        public void Projectile_ReachesItsApexWhenVerticalSpeedIsZero()
        {
            Vector3 velocity = CombatRules.ProjectileVelocity(Vector3.forward, Vector3.zero, ForwardSpeed, LiftSpeed);
            float apexTime = LiftSpeed / Gravity;
            float expected = LiftSpeed * LiftSpeed / (2f * Gravity);

            Vector3 apex = CombatRules.ProjectilePosition(Vector3.zero, velocity, Gravity, apexTime);
            Assert.AreEqual(expected, apex.y, 0.001f);
            Assert.AreEqual(ForwardSpeed * apexTime, apex.z, 0.001f);
        }

        [Test]
        public void ProjectileVelocity_AddsCarSpeedOnThePlaneOnly()
        {
            Vector3 velocity = CombatRules.ProjectileVelocity(new Vector3(0f, 0.5f, 2f), new Vector3(1f, -4f, 10f), ForwardSpeed, LiftSpeed);
            Assert.AreEqual(1f, velocity.x, 0.0001f);
            Assert.AreEqual(LiftSpeed, velocity.y, 0.0001f);
            Assert.AreEqual(10f + ForwardSpeed, velocity.z, 0.0001f);
        }

        [TestCase(double.NegativeInfinity, 0.0, true)]
        [TestCase(10.0, 11.0, false)]
        [TestCase(10.0, 14.7, false)]
        [TestCase(10.0, 14.8, true)]
        [TestCase(10.0, 15.0, true)]
        public void FireCooldown_IsValidatedWithASmallTolerance(double last, double fire, bool allowed)
        {
            Assert.AreEqual(allowed, CombatRules.IsFireAllowed(last, fire, Cooldown, CooldownTolerance));
        }

        [TestCase(-1, 4, 1)]
        [TestCase(0, 4, 1)]
        [TestCase(3, 4, 4)]
        [TestCase(4, 4, 4)]
        [TestCase(5, 6, 6)]
        public void AddBarrel_NeverPassesTheMaximum(int current, int max, int expected)
        {
            Assert.AreEqual(expected, CombatRules.AddBarrel(current, max));
        }

        [Test]
        public void BarrelSlide_StopsAfterTheSlideTime()
        {
            float full = DropSpeed * SlideTime * 0.5f;
            Assert.AreEqual(0f, CombatRules.BarrelSlideDistance(0f, DropSpeed, SlideTime), 0.0001f);
            Assert.AreEqual(full, CombatRules.BarrelSlideDistance(SlideTime, DropSpeed, SlideTime), 0.0001f);
            Assert.AreEqual(full, CombatRules.BarrelSlideDistance(5f, DropSpeed, SlideTime), 0.0001f);
            Assert.Less(CombatRules.BarrelSlideDistance(SlideTime * 0.5f, DropSpeed, SlideTime), full);
        }

        [TestCase(2.5f, 5)]
        [TestCase(4f, 8)]
        [TestCase(3.2f, 6)]
        [TestCase(0.1f, 1)]
        public void BlastSize_IsTwiceTheRadius(float radius, int expected)
        {
            Assert.AreEqual(expected, CombatRules.BlastSize(radius));
        }

        [Test]
        public void InsideSquare_IncludesTheCarMargin()
        {
            const int size = 8;
            float edge = size * 0.5f + CombatRules.CarHalfWidth;
            Assert.IsTrue(CombatRules.IsInsideSquare(Vector3.zero, size, new Vector3(edge, 0f, -edge)));
            Assert.IsFalse(CombatRules.IsInsideSquare(Vector3.zero, size, new Vector3(edge + 0.1f, 0f, 0f)));
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
            List<int> cells = Square(center, CombatRules.BlastSize(2.5f));

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
            Assert.AreEqual(64, Square(new Vector3(10.2f, 0f, -4.7f), CombatRules.BlastSize(4f)).Count);
        }

        [Test]
        public void Square_IsClippedAtTheGridEdge()
        {
            Vector3 corner = new Vector3(PaintGrid.Origin + 0.5f, 0f, PaintGrid.Origin + 0.5f);
            Assert.AreEqual(9, Square(corner, 5).Count);
        }
    }
}
