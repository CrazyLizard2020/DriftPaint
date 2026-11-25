using DriftPaint.Cars;
using NUnit.Framework;
using UnityEngine;

namespace DriftPaint.Tests
{
    public class StabilityMathTests
    {
        #region Inclinación

        static float Settle(Quaternion start, Vector3 omega, float kp, float kd, float max, float dt, float limit)
        {
            Quaternion rotation = start;
            for (float t = 0f; t < limit; t += dt)
            {
                omega += StabilityMath.Tilt(rotation, Vector3.up, omega, kp, kd, kd, max, dt) * dt;
                rotation = Quaternion.AngleAxis(omega.magnitude * Mathf.Rad2Deg * dt, omega.sqrMagnitude > 0f ? omega.normalized : Vector3.up) * rotation;
                if (Vector3.Angle(rotation * Vector3.up, Vector3.up) < 3f && omega.magnitude < 0.3f) return t;
            }
            return float.PositiveInfinity;
        }

        [TestCase(0.02f)]
        [TestCase(0.0166667f)]
        [TestCase(0.01f)]
        [TestCase(0.0083333f)]
        public void Tilt_SettlesWithinItsResponse(float dt)
        {
            var settings = new StabilitySettings();
            float air = Settle(Quaternion.Euler(30f, 40f, 10f), Vector3.zero, settings.AirAlignKp, settings.AirPitchDamping, settings.MaxAngularAcceleration, dt, 3f);
            float landing = Settle(Quaternion.Euler(20f, 0f, 10f), Vector3.zero, settings.LandingAlignKp, settings.LandingAlignKd, settings.MaxAngularAcceleration, dt, 3f);
            Assert.That(air, Is.InRange(0.25f, 0.7f));
            Assert.LessOrEqual(landing, 0.4f);
        }

        [Test]
        public void Tilt_HandlesUpsideDown()
        {
            Vector3 torque = StabilityMath.Tilt(Quaternion.Euler(0f, 0f, 180f), Vector3.up, Vector3.zero, 64f, 16f, 16f, 60f, 0.02f);
            Assert.Greater(torque.magnitude, 1f);
            Assert.IsFalse(float.IsNaN(torque.x + torque.y + torque.z));
            Assert.Less(Settle(Quaternion.Euler(0f, 30f, 180f), Vector3.zero, 196f, 28f, 60f, 0.02f, 3f), 1.2f);
        }

        [Test]
        public void Tilt_NeverTouchesYaw_NorPassesItsLimit()
        {
            Quaternion rotation = Quaternion.Euler(25f, 70f, -15f);
            Vector3 torque = StabilityMath.Tilt(rotation, Vector3.up, new Vector3(1f, 5f, -2f), 600f, 50f, 50f, 60f, 0.02f);
            Assert.AreEqual(0f, Vector3.Dot(torque, rotation * Vector3.up), 1e-3f);
            Assert.LessOrEqual(torque.magnitude, 60.001f);
        }

        [Test]
        public void Tilt_IsZeroWhenAlignedAndStill() =>
            Assert.AreEqual(0f, StabilityMath.Tilt(Quaternion.Euler(0f, 123f, 0f), Vector3.up, Vector3.up * 3f, 196f, 28f, 28f, 60f, 0.02f).magnitude, 1e-2f);

        [Test]
        public void AngularLimit_OnlyActsAboveTheLimit_AndNeverInverts()
        {
            Assert.AreEqual(Vector3.zero, StabilityMath.AngularLimit(new Vector3(2.9f, 9f, -2.9f), 3f, 3f, 25f, 0.02f));
            foreach (float strength in new[] { 25f, 500f })
            {
                Vector3 limit = StabilityMath.AngularLimit(new Vector3(8f, 9f, -7f), 3f, 3f, strength, 0.02f);
                Assert.AreEqual(0f, limit.y);
                Assert.GreaterOrEqual(8f + limit.x * 0.02f, 3f - 1e-4f);
                Assert.LessOrEqual(-7f + limit.z * 0.02f, -3f + 1e-4f);
            }
        }

        #endregion

        #region Absorción y adherencia

        [Test]
        public void Absorption_OnlyActsOnVelocityLeavingTheGround()
        {
            Assert.AreEqual(0f, StabilityMath.Absorption(-5f, 0.5f, 60f, 0.02f));
            Assert.AreEqual(0f, StabilityMath.Absorption(0f, 1f, 60f, 0.02f));
            Assert.AreEqual(1f, StabilityMath.Absorption(1f, 1f, 1000f, 0.02f), 1e-4f);
            Assert.AreEqual(60f * 0.02f, StabilityMath.Absorption(50f, 1f, 60f, 0.02f), 1e-4f);
        }

        [Test]
        public void Absorption_DoesNotDependOnTheStep()
        {
            float At(float dt)
            {
                float speed = 2f;
                for (float t = 0f; t < 0.2f - 1e-5f; t += dt) speed -= StabilityMath.Absorption(speed, 0.2f, 1000f, dt);
                return speed;
            }
            Assert.AreEqual(At(0.02f), At(0.01f), 0.01f);
            Assert.AreEqual(At(0.02f), At(0.02f / 2.4f), 0.05f);
        }

        [Test]
        public void Adhesion_ScalesAndCaps()
        {
            var settings = new StabilitySettings();
            Assert.AreEqual(settings.ExtraGravity, StabilityMath.Adhesion(settings, 0f, 20f, 1f, 1f), 1e-3f);
            Assert.AreEqual(settings.ExtraGravity * 1.4f, StabilityMath.Adhesion(settings, 55f, 20f, 1f, 1f), 0.01f);
            Assert.AreEqual(settings.ExtraGravity * 0.5f, StabilityMath.Adhesion(settings, 0f, 20f, 1f, 0.5f), 1e-3f);
            Assert.AreEqual(0f, StabilityMath.Adhesion(settings, 0f, 20f, 1f, 0f));
            Assert.LessOrEqual(StabilityMath.Adhesion(settings, 60f, 60f, 1f, 1f), settings.MaxAdhesionAcceleration);
        }

        [Test]
        public void LandingWindow_FadesToZero()
        {
            Assert.AreEqual(1f, StabilityMath.LandingWindow(0f, 0.25f));
            Assert.AreEqual(1f, StabilityMath.LandingWindow(0.1f, 0.25f), 1e-3f);
            Assert.That(StabilityMath.LandingWindow(0.2f, 0.25f), Is.InRange(0.01f, 0.99f));
            Assert.AreEqual(0f, StabilityMath.LandingWindow(0.25f, 0.25f));
        }

        #endregion

        #region Suspensión y topes

        [Test]
        public void Suspension_NeverPulls_AndCaps()
        {
            Assert.AreEqual(0f, StabilityMath.Suspension(0f, -10f, 70f, 7f, 2.5f, 120f));
            Assert.AreEqual(0f, StabilityMath.Suspension(0.1f, 5f, 70f, 7f, 2.5f, 120f));
            Assert.AreEqual(120f, StabilityMath.Suspension(0.3f, -30f, 70f, 7f, 2.5f, 120f));
            Assert.AreEqual(7f, StabilityMath.Suspension(0.1f, 0f, 70f, 7f, 2.5f, 120f), 1e-4f);
        }

        [Test]
        public void Suspension_HoldsTheCarAtRestHeight()
        {
            float rest = StabilityMath.RestHeight(0.25f, 19.81f, 4, 70f);
            float compression = rest - 0.25f;
            Assert.AreEqual(19.81f, 4f * StabilityMath.Suspension(compression, 0f, 70f, 7f, 2.5f, 120f), 1e-3f);
        }

        static StabilityMath.Stop[] FourStops(float mass, Vector3 inertia)
        {
            var stops = new StabilityMath.Stop[4];
            Vector3[] arms = { new Vector3(-0.7f, -0.1f, 1.2f), new Vector3(0.7f, -0.1f, 1.2f), new Vector3(-0.7f, -0.1f, -1.2f), new Vector3(0.7f, -0.1f, -1.2f) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 response = StabilityMath.AngularResponse(arms[i], Vector3.up, inertia, Quaternion.identity);
                stops[i] = new StabilityMath.Stop
                {
                    Wheel = i,
                    Arm = arms[i],
                    Normal = Vector3.up,
                    AngularResponse = response,
                    InverseMass = 1f / mass + Vector3.Dot(Vector3.Cross(response, arms[i]), Vector3.up),
                    MaxImpulse = 1e6f
                };
            }
            return stops;
        }

        static float Energy(Vector3 velocity, Vector3 omega, float mass, Vector3 inertia) =>
            0.5f * mass * velocity.sqrMagnitude + 0.5f * Vector3.Dot(omega, Vector3.Scale(inertia, omega));

        [TestCase(-12f, 0f, 0f)]
        [TestCase(-8f, 2f, 0f)]
        [TestCase(-8f, -2f, 1.5f)]
        [TestCase(-3f, 0f, 3f)]
        public void Stops_NeverAddEnergyNorBounce(float vertical, float pitch, float roll)
        {
            const float mass = 1200f;
            var inertia = new Vector3(1500f, 1800f, 500f);
            var velocity = new Vector3(0f, vertical, 25f);
            var omega = new Vector3(pitch, 0f, roll);
            float before = Energy(velocity, omega, mass, inertia);

            StabilityMath.Stop[] stops = FourStops(mass, inertia);
            StabilityMath.SolveStops(stops, 4, velocity, omega, mass, 8);
            foreach (StabilityMath.Stop stop in stops)
            {
                Assert.GreaterOrEqual(stop.Impulse, 0f);
                velocity += stop.Normal * (stop.Impulse / mass);
                omega += stop.AngularResponse * stop.Impulse;
            }

            Assert.LessOrEqual(Energy(velocity, omega, mass, inertia), before + 1f);
            Assert.LessOrEqual(velocity.y, 0.5f);
            Assert.AreEqual(25f, velocity.z, 1e-3f, "La velocidad a lo largo del piso no cambia.");
        }

        [Test]
        public void Stops_FlatFallStopsWithoutTurning()
        {
            const float mass = 1200f;
            var inertia = new Vector3(1500f, 1800f, 500f);
            StabilityMath.Stop[] stops = FourStops(mass, inertia);
            StabilityMath.SolveStops(stops, 4, Vector3.down * 10f, Vector3.zero, mass, 8);
            float total = 0f;
            Vector3 omega = Vector3.zero;
            foreach (StabilityMath.Stop stop in stops)
            {
                total += stop.Impulse;
                omega += stop.AngularResponse * stop.Impulse;
            }
            Assert.AreEqual(10f, total / mass, 0.3f);
            Assert.Less(omega.magnitude, 0.3f);
        }

        [Test]
        public void Stops_LeaveSeparatingWheelsAlone()
        {
            StabilityMath.Stop[] stops = FourStops(1200f, new Vector3(1500f, 1800f, 500f));
            StabilityMath.SolveStops(stops, 4, Vector3.up * 2f, Vector3.zero, 1200f, 8);
            foreach (StabilityMath.Stop stop in stops) Assert.AreEqual(0f, stop.Impulse);
        }

        #endregion

        #region Base y PID

        [Test]
        public void SupportBasis_IsOrthonormal()
        {
            Vector3 normal = new Vector3(0.2f, 0.95f, -0.1f).normalized;
            SupportBasis basis = SupportBasis.From(normal, new Vector3(0.3f, 0.1f, 1f));
            Assert.IsTrue(basis.Valid);
            Assert.AreEqual(0f, Vector3.Dot(basis.Forward, normal), 1e-4f);
            Assert.AreEqual(0f, Vector3.Dot(basis.Right, normal), 1e-4f);
            Assert.AreEqual(0f, Vector3.Dot(basis.Right, basis.Forward), 1e-4f);
            Assert.AreEqual(1f, basis.Right.magnitude, 1e-4f);
            Assert.Greater(SupportBasis.From(Vector3.up, Vector3.forward).Right.x, 0.99f);
            Assert.IsFalse(SupportBasis.From(Vector3.up, Vector3.up).Valid);
        }

        [Test]
        public void Pid_DoesNotIntegrateWhileOutputIsReduced()
        {
            var pid = new PidController(new PidSettings(1f, 1f, 0f, 10f, 100f));
            for (int i = 0; i < 100; i++) pid.Evaluate(5f, 0f, 0.02f, integrate: false);
            Assert.AreEqual(5f, pid.Evaluate(5f, 0f, 0.02f, integrate: false), 1e-4f);
        }

        [Test]
        public void Pid_DoesNotWindUpWhileSaturated()
        {
            var pid = new PidController(new PidSettings(10f, 5f, 0f, 100f, 20f));
            for (int i = 0; i < 500; i++) pid.Evaluate(50f, 0f, 0.02f);
            Assert.AreEqual(0f, pid.Evaluate(0f, 0f, 0.02f), 0.01f);
        }

        [Test]
        public void Pid_ResetFromMeasurement_HasNoDerivativeKick()
        {
            var pid = new PidController(new PidSettings(0f, 0f, 1f, 1f, 1000f));
            pid.ResetFromMeasurement(30f);
            Assert.AreEqual(0f, pid.Evaluate(30f, 30f, 0.02f), 1e-4f);
        }

        #endregion

        #region Aviso de aterrizaje

        [Test]
        public void LandingDetector_FiresOncePerLanding()
        {
            var detector = new LandingDetector();
            int landings = 0;
            float strength = 0f;
            void Feed(float speed, float seconds)
            {
                for (float t = 0f; t < seconds; t += 1f / 60f)
                    if (detector.Update(speed, 1f / 60f, 3f, 0.4f, out float fall)) { landings++; strength = fall; }
            }

            Feed(0f, 0.5f);
            for (float v = 4f; v > -9f; v -= 0.33f) Feed(v, 0.016f);
            Feed(-1f, 0.03f);
            Feed(0.5f, 0.05f);
            Feed(-0.5f, 0.05f);
            Feed(0f, 1f);
            Assert.AreEqual(1, landings);
            Assert.Greater(strength, 7f);
        }

        [Test]
        public void LandingDetector_IgnoresDrivingOverBumps()
        {
            var detector = new LandingDetector();
            for (int i = 0; i < 600; i++)
                Assert.IsFalse(detector.Update(Mathf.Sin(i * 0.4f) * 2f, 1f / 60f, 3f, 0.4f, out _));
        }

        #endregion
    }
}
