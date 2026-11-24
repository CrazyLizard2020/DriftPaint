using DriftPaint.Cars;
using NUnit.Framework;

namespace DriftPaint.Tests
{
    public class EngineSimulationTests
    {
        const float Dt = 0.02f;
        const float MaxSpeed = 33.333f;
        const float MaxReverse = 11.11f;

        static EngineSettings Settings() =>
            new EngineSettings(900f, 7600f, 7800f, new[] { 0.2f, 0.36f, 0.54f, 0.76f, 1.08f }, 0.2f, 3500f, 16000f, 7000f);

        static EngineInput Input(float forwardSpeed, float throttle, bool grounded = true) => new EngineInput
        {
            ForwardSpeed = forwardSpeed,
            Throttle = throttle,
            MaxSpeed = MaxSpeed,
            MaxReverseSpeed = MaxReverse,
            Grounded = grounded
        };

        static void Run(EngineSimulation engine, EngineInput input, float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt) engine.Step(input, Dt);
        }

        [Test]
        public void Standing_IdlesInFirstWithIdleLoad()
        {
            var engine = new EngineSimulation(Settings());
            Run(engine, Input(0f, 0f), 1f);

            Assert.AreEqual(900f, engine.Rpm, 30f);
            Assert.AreEqual(1, engine.Gear);
            Assert.AreEqual(EngineSimulation.IdleLoad, engine.Load);
        }

        [Test]
        public void FullThrottleRun_ShiftsUpThroughEveryGearWithoutPassingTheLimiter()
        {
            var engine = new EngineSimulation(Settings());
            int previousGear = 1;
            for (float t = 0f; t < 8f; t += Dt)
            {
                float speed = MaxSpeed * UnityEngine.Mathf.Clamp01(t / 6f);
                engine.Step(Input(speed, 1f), Dt);
                Assert.GreaterOrEqual(engine.Gear, previousGear, "La caja no debería reducir acelerando a fondo.");
                Assert.LessOrEqual(engine.Rpm, 7800f + 0.01f);
                previousGear = engine.Gear;
            }
            Assert.AreEqual(5, engine.Gear);
            Assert.Greater(engine.Rpm, 6000f, "A velocidad máxima en quinta el motor va alto pero sin cortar.");
        }

        [Test]
        public void Launching_HoldsRpmAboveWheelSpeed()
        {
            var engine = new EngineSimulation(Settings());
            Run(engine, Input(0.5f, 1f), 0.5f);

            Assert.IsTrue(engine.IsLaunching);
            Assert.Greater(engine.Rpm, 3000f);
            Assert.AreEqual(1f, engine.Load);
        }

        [Test]
        public void Coasting_FollowsWheelsWithoutLoad()
        {
            var engine = new EngineSimulation(Settings());
            Run(engine, Input(20f, 1f), 2f);
            Run(engine, Input(20f, 0f), 0.5f);

            Assert.AreEqual(0f, engine.Load);
            Assert.Greater(engine.Rpm, 900f);
        }

        [Test]
        public void SlowingDown_ShiftsBackDown()
        {
            var engine = new EngineSimulation(Settings());
            Run(engine, Input(MaxSpeed, 1f), 3f);
            int topGear = engine.Gear;
            for (float speed = MaxSpeed; speed > 2f; speed -= 0.3f) engine.Step(Input(speed, 0f), Dt);

            Assert.Less(engine.Gear, topGear);
        }

        [Test]
        public void HoldingReverse_EngagesReverseGear()
        {
            var engine = new EngineSimulation(Settings());
            Run(engine, Input(-3f, -1f), 0.5f);

            Assert.AreEqual(-1, engine.Gear);
            Assert.AreEqual(1f, engine.Load);
        }

        [Test]
        public void AirborneFullThrottle_RevsToTheLimiter()
        {
            var engine = new EngineSimulation(Settings());
            bool hitLimiter = false;
            for (float t = 0f; t < 2f; t += Dt)
            {
                engine.Step(Input(10f, 1f, grounded: false), Dt);
                hitLimiter |= engine.IsAtLimiter;
            }

            Assert.IsTrue(hitLimiter);
            Assert.LessOrEqual(engine.Rpm, 7800f);
        }

        [Test]
        public void DriftTurbo_RevsNearTheRedline()
        {
            var engine = new EngineSimulation(Settings());
            Run(engine, Input(20f, 1f), 2f);
            EngineInput boosted = Input(20f, 1f);
            boosted.Boost = true;
            Run(engine, boosted, 0.5f);

            Assert.Greater(engine.Rpm, 7600f * 0.9f);
            Assert.LessOrEqual(engine.Rpm, 7800f);
        }

        [Test]
        public void Reset_ReturnsToIdleInFirst()
        {
            var engine = new EngineSimulation(Settings());
            Run(engine, Input(MaxSpeed, 1f), 3f);
            engine.Reset();

            Assert.AreEqual(1, engine.Gear);
            Assert.AreEqual(900f, engine.Rpm);
        }
    }
}
