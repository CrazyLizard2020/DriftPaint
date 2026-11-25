#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using DriftPaint.Cars;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace DriftPaint.Tests
{
    /// <summary>Contratos físicos de la estabilidad con los prefabs reales.</summary>
    public sealed class VehicleStabilityPlayTests
    {
        const float MaxRebound = 0.15f;
        const float MinRetention = 0.9f;
        const float MaxTimeToDrive = 0.3f;

        [SetUp]
        public void SetUp() => LogAssert.ignoreFailingMessages = true;

        static string Describe(VehicleLab.Landing l) =>
            $"{l.Model} {l.Case}: rebote {l.Rebound:0.000} m, retención {l.Retention:0.000}, manejo {l.TimeToDrive:0.000} s, restitución {l.Restitution:0.000}, reubicaciones {l.Teleports}";

        static string History(CarController car)
        {
            StabilityHistory history = car.Stability.History;
            return string.Join("; ", Enumerable.Range(0, history.Count).Select(i => history.Recent(history.Count - 1 - i)).Select(t => $"{t.From}>{t.To} tras {t.Duration:0.00} s, {t.Wheels} ruedas, {t.Tilt:0}°"))
                   + $" | ruedas {car.GroundSensor.GroundedWheels}, confianza {car.GroundSensor.Sample.Confidence:0.00}, altura {car.Body.position.y - 500f:0.000}";
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator NormalLandings_MeetTheTargets([ValueSource(typeof(VehicleLab), nameof(VehicleLab.Models))] string model)
        {
            using var lab = new VehicleLab();
            foreach (LandingCase landing in LandingCases.All.Where(c => c.Normal))
            {
                var result = new VehicleLab.Landing { Model = model, Case = landing.Name, Normal = true };
                yield return landing.Run(lab, result, null);
                string text = Describe(result);
                Assert.IsTrue(result.Contacted, text);
                Assert.LessOrEqual(result.Rebound, MaxRebound, text);
                Assert.GreaterOrEqual(result.Retention, MinRetention, text);
                Assert.That(result.TimeToDrive, Is.InRange(0f, MaxTimeToDrive), text);
                Assert.AreEqual(0, result.Teleports, text);
                Assert.Less(result.FinalTilt, 5f, text);
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator FlippedCar_RecoversByItself([ValueSource(typeof(VehicleLab), nameof(VehicleLab.Models))] string model)
        {
            using var lab = new VehicleLab();
            lab.Floor();
            CarController car = lab.Spawn(model, Vector3.up, Quaternion.Euler(0f, 0f, 180f));
            int teleports = 0;
            car.Teleported += (p, r) => teleports++;

            float active = 0f;
            for (float t = 0f; t < 5f; t += Time.fixedDeltaTime)
            {
                yield return VehicleLab.Step;
                if (car.Stability.State == VehicleStabilityState.SelfRecovery) active += Time.fixedDeltaTime;
            }
            string history = History(car);
            Assert.AreEqual(VehicleStabilityState.Grounded, car.Stability.State, history);
            Assert.That(active, Is.InRange(0.1f, 1f), history);
            Assert.AreEqual(0, teleports, history);
            Assert.Less(Vector3.Angle(car.Body.rotation * Vector3.up, Vector3.up), 5f);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator FlippedUnderACeiling_WarnsAndRespawnsOnce()
        {
            using var lab = new VehicleLab();
            lab.Floor();
            lab.Box("Ceiling", new Vector3(0f, 2.3f, 0f), new Vector3(12f, 0.2f, 12f), Quaternion.identity);
            CarController car = lab.Spawn("Car_RX7", Vector3.up * 1.2f, Quaternion.Euler(0f, 0f, 180f));
            int teleports = 0;
            float at = 0f, time = 0f;
            car.Teleported += (p, r) => { teleports++; at = time; };
            bool warned = false, recovering = false;
            for (; time < 4f && teleports == 0; time += Time.fixedDeltaTime)
            {
                yield return VehicleLab.Step;
                warned |= car.Stability.Frame.RecoveryWarning;
                recovering |= car.Stability.State == VehicleStabilityState.SelfRecovery;
            }
            Assert.AreEqual(1, teleports);
            Assert.IsTrue(warned);
            Assert.IsFalse(recovering);
            Assert.That(at, Is.InRange(2.9f, 3.6f));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator RemoteCar_IsNeverSimulated()
        {
            using var lab = new VehicleLab();
            lab.Floor();
            CarController car = lab.Spawn("Car_RX7", Vector3.up * 2f, Quaternion.Euler(0f, 0f, 160f));
            car.SetMode(CarSimulationMode.Remote);
            Vector3 position = car.Body.position;
            yield return lab.Settle(3f);
            Assert.IsTrue(car.Body.isKinematic);
            Assert.AreEqual(position, car.Body.position);
            Assert.AreEqual(0, car.Stability.History.Count);
            Assert.AreEqual(VehicleStabilityState.Airborne, car.Stability.State);
            Assert.IsFalse(car.Stability.IsGrounded);
            Assert.IsTrue(car.GetComponent<CarFlipWarning>().IsWarning, "En los ajenos el aviso sale de la rotación.");

            car.SetMode(CarSimulationMode.Local);
            yield return lab.Settle(6f);
            Assert.AreEqual(VehicleStabilityState.Grounded, car.Stability.State);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Teleport_ForgetsTheLanding()
        {
            using var lab = new VehicleLab();
            lab.Floor();
            CarController car = lab.Spawn("Car_RX7", Vector3.up * 3f, Quaternion.identity);
            yield return lab.Settle(0.7f);
            Assert.AreEqual(1, car.Stability.Frame.LandingEpisode);
            car.Teleport(lab.WorldOf(Vector3.up * 2f), Quaternion.identity);
            Assert.AreEqual(Vector3.zero, car.Body.linearVelocity);
            Assert.IsFalse(car.Stability.IsGrounded);
            yield return VehicleLab.Step;
            yield return VehicleLab.Step;
            Assert.AreEqual(0, car.Stability.Frame.LandingEpisode);
            Assert.AreEqual(0f, car.Stability.Frame.AdhesionAcceleration);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Flight_HasNoSupportForces_AndKeepsThePlannedArc()
        {
            using var lab = new VehicleLab();
            CarController car = lab.Spawn("Car_RX7", Vector3.up * 30f, Quaternion.identity);
            lab.Launch(new Vector3(0f, 8f, 20f));
            lab.Drive(1f, 1f);
            yield return lab.Settle(0.1f);
            Vector3 position = car.Body.position, velocity = car.Body.linearVelocity;
            const float seconds = 0.8f;
            int steps = Mathf.RoundToInt(seconds / Time.fixedDeltaTime);
            for (int i = 0; i < steps; i++)
            {
                yield return VehicleLab.Step;
                StabilityTelemetry applied = car.Stability.Telemetry;
                Assert.AreEqual(0f, applied.Adhesion + applied.Suspension + applied.StopImpulse + applied.Absorption + applied.RecoveryImpulse);
                Assert.IsFalse(car.Stability.Frame.CanDrive);
            }
            float g = -Physics.gravity.y + car.Stats.Stability.FlightGravity;
            Vector3 expected = position + velocity * seconds + Vector3.down * (0.5f * g * seconds * seconds);
            Assert.Less(Vector3.Distance(expected, car.Body.position), 0.3f);
            Assert.AreEqual(velocity.z, car.Body.linearVelocity.z, 0.05f, "En el aire no hay motor.");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Walls_AreNotGround()
        {
            using var lab = new VehicleLab();
            lab.Floor();
            lab.Box("Wall", new Vector3(0f, 5f, 12f), new Vector3(40f, 10f, 1f), Quaternion.identity);
            CarController car = lab.Spawn("Car_RX7", Vector3.up * 0.1f, Quaternion.identity);
            yield return lab.Settle(0.5f);
            lab.Launch(Vector3.forward * 25f);
            float maxHeight = 0f;
            for (float t = 0f; t < 2f; t += Time.fixedDeltaTime)
            {
                lab.Drive(1f);
                yield return VehicleLab.Step;
                maxHeight = Mathf.Max(maxHeight, lab.LocalPosition.y);
                Assert.Less(Vector3.Angle(car.Stability.Frame.SupportNormal, Vector3.up), 1f);
            }
            Assert.Less(maxHeight, 0.6f);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator OtherCars_AreNotGround()
        {
            using var lab = new VehicleLab();
            CarController car = lab.Spawn("Car_RX7", Vector3.up * 1.5f, Quaternion.identity);
            GameObject platform = lab.Box("Car", Vector3.zero, new Vector3(20f, 1f, 20f), Quaternion.identity);
            var body = platform.AddComponent<Rigidbody>();
            body.isKinematic = true;
            yield return lab.Settle(1f);
            Assert.AreEqual(0, car.GroundSensor.GroundedWheels);
            Assert.IsFalse(car.Stability.IsGrounded);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SteadySimulation_DoesNotAllocate()
        {
            using var lab = new VehicleLab();
            lab.Floor();
            CarController car = lab.Spawn("Car_RX7", Vector3.up * 0.1f, Quaternion.identity);
            lab.Drive(0.6f, 0.2f);
            yield return lab.Settle(2f);

            float dt = Time.fixedDeltaTime;
            void Tick()
            {
                car.GroundSensor.Sense(dt);
                car.StatusEffects.Tick(dt);
                car.Stability.Evaluate(dt);
                car.Stability.ApplyAssistForces(dt);
                car.Locomotion.Tick(car.Stability.Frame, dt);
            }
            TestDelegate run = () => { for (int i = 0; i < 100; i++) Tick(); };
            run();
            Assert.That(run, Is.Not.AllocatingGCMemory());

            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++) Tick();
            watch.Stop();
            Assert.LessOrEqual(car.GroundSensor.LastCastCount, 7);
            TestContext.WriteLine($"Paso de simulación: {watch.Elapsed.TotalMilliseconds:0.000} µs de CPU en promedio (1000 pasos apoyado)");

            // También en vuelo, con predicción.
            car.Teleport(lab.WorldOf(Vector3.up * 3f), Quaternion.identity);
            lab.Launch(new Vector3(0f, -6f, 10f));
            yield return VehicleLab.Step;
            run();
            Assert.That(run, Is.Not.AllocatingGCMemory());
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator BodyMaterial_IsPerInstance_AndIsReleased()
        {
            using var lab = new VehicleLab();
            CarController car = lab.Spawn("Car_RX7", Vector3.up, Quaternion.identity);
            yield return null;
            PhysicsMaterial material = car.GetComponent<MeshCollider>().sharedMaterial;
            Assert.AreEqual(0f, material.bounciness);
            lab.Spawn("Car_RX7", Vector3.up, Quaternion.identity);
            yield return null;
            yield return null;
            Assert.IsTrue(material == null);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator LandingFeedback_MovesOnlyTheModel_OncePerLanding()
        {
            using var lab = new VehicleLab();
            Time.timeScale = 1f;
            lab.Floor();
            CarController car = lab.Spawn("Car_RX7", Vector3.up * 2f, Quaternion.identity);
            var feedback = car.GetComponent<CarLandingFeedback>();
            Transform model = car.transform.Find("Visual/Model");
            Transform wheel = car.GroundSensor.Probes[0];
            Vector3 rest = model.localPosition, wheelRest = wheel.localPosition;
            int landings = 0;
            float lowest = 0f;
            feedback.Landed += _ => landings++;
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                yield return null;
                lowest = Mathf.Min(lowest, model.localPosition.y - rest.y);
                Assert.AreEqual(wheelRest, wheel.localPosition);
            }
            Assert.AreEqual(1, landings);
            Assert.Less(lowest, -0.005f);
            Assert.AreEqual(rest.y, model.localPosition.y, 1e-3f);
        }
    }
}
#endif
