#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DriftPaint.Cars;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DriftPaint.Tests
{
    /// <summary>
    /// Corre la batería completa de aterrizajes y conducción y la deja en CSV, para comparar revisiones,
    /// modelos y frecuencias de física. Es explícita: no forma parte de la corrida normal de tests.
    /// Variables de entorno: DRIFTPAINT_LAB_OUT (carpeta), DRIFTPAINT_LAB_DT (paso fijo),
    /// DRIFTPAINT_LAB_MODELS (lista separada por comas), DRIFTPAINT_LAB_TRACE (1 para guardar la traza) y DRIFTPAINT_LAB_CFG
    /// (copia de la sala con cambios de Live Ops).
    /// </summary>
    [Explicit, Category("Lab")]
    public sealed class VehicleLabReport
    {
        static float Dt => float.TryParse(Environment.GetEnvironmentVariable("DRIFTPAINT_LAB_DT"), NumberStyles.Float, CultureInfo.InvariantCulture, out float dt) ? dt : 0f;

        static string[] Models
        {
            get
            {
                string text = Environment.GetEnvironmentVariable("DRIFTPAINT_LAB_MODELS");
                return string.IsNullOrEmpty(text) ? VehicleLab.Models : text.Split(',');
            }
        }

        [UnityTest, Timeout(3600000)]
        public IEnumerator Landings()
        {
            LogAssert.ignoreFailingMessages = true;
            var rows = new List<string> { VehicleLab.Header };
            StringBuilder trace = Environment.GetEnvironmentVariable("DRIFTPAINT_LAB_TRACE") == "1" ? new StringBuilder("model;case;t;state;y;z;vy;speed;tilt;clearance;blend\n") : null;
            using var lab = new VehicleLab(Dt);

            foreach (string model in Models)
            {
                foreach (LandingCase landing in LandingCases.All)
                {
                    var result = new VehicleLab.Landing { Model = model, Case = landing.Name, Normal = landing.Normal };
                    yield return landing.Run(lab, result, trace);
                    rows.Add(VehicleLab.Row(result));
                }
            }

            VehicleLab.Write("landings.csv", rows);
            if (trace != null) VehicleLab.Write("trace.csv", new[] { trace.ToString() });
        }

        [UnityTest, Timeout(3600000)]
        public IEnumerator Locomotion()
        {
            LogAssert.ignoreFailingMessages = true;
            var rows = new List<string> { "model;t_0_120_s;t_120_0_s;t_0_rev40_s;top_kmh;yaw_rate_20ms;yaw_rate_30ms;drift_yaw_20ms;drift_slip_deg;drift_speed_loss;turbo_level;ramp_entry_loss_pct;straight_dev_m;rest_height_m;rest_tilt_deg" };
            using var lab = new VehicleLab(Dt);

            foreach (string model in Models)
            {
                lab.Clear();
                lab.Floor();
                lab.Ramp(new Vector3(60f, 0f, 60f), 0f, 14f, 8f, 13f);
                CarController car = lab.Spawn(model, new Vector3(0f, 0.3f, -780f), Quaternion.identity);
                yield return lab.Settle(1.5f);
                float restHeight = lab.LocalPosition.y, restTilt = Vector3.Angle(car.Body.rotation * Vector3.up, Vector3.up);

                // Aceleración 0 a máxima.
                lab.Drive(1f);
                float t = 0f, top = 0f;
                float max = car.Stats.MaxSpeed;
                while (car.ForwardSpeed < max - 0.35f && t < 12f) { yield return VehicleLab.Step; t += Time.fixedDeltaTime; }
                float accelTime = t;
                for (float e = 0f; e < 1.5f; e += Time.fixedDeltaTime) { yield return VehicleLab.Step; top = Mathf.Max(top, car.ForwardSpeed); }
                float straight = Mathf.Abs(lab.LocalPosition.x);

                // Frenado y marcha atrás.
                lab.Drive(-1f);
                t = 0f;
                while (car.ForwardSpeed > 0.05f && t < 8f) { yield return VehicleLab.Step; t += Time.fixedDeltaTime; }
                float brakeTime = t;
                t = 0f;
                while (car.ForwardSpeed > -car.Stats.MaxReverseSpeed + 0.3f && t < 8f) { yield return VehicleLab.Step; t += Time.fixedDeltaTime; }
                float reverseTime = t;

                // Giro estable a 20 y 30 m/s.
                var yaws = new float[2];
                for (int i = 0; i < 2; i++)
                {
                    float speed = i == 0 ? 20f : 30f;
                    lab.Spawn(model, new Vector3(-150f, 0.05f, 0f), Quaternion.identity);
                    car = lab.Car;
                    yield return lab.Settle(0.5f);
                    lab.Launch(Vector3.forward * speed);
                    lab.Drive(speed / max, 1f);
                    yield return lab.Settle(0.8f);
                    float sum = 0f;
                    int count = 0;
                    for (float e = 0f; e < 0.6f; e += Time.fixedDeltaTime) { yield return VehicleLab.Step; sum += car.Body.angularVelocity.y; count++; }
                    yaws[i] = sum / count;
                }

                // Drift sostenido a 20 m/s.
                lab.Spawn(model, new Vector3(-150f, 0.05f, 0f), Quaternion.identity);
                car = lab.Car;
                yield return lab.Settle(0.5f);
                lab.Launch(Vector3.forward * 22f);
                lab.Drive(1f, 1f, true);
                yield return lab.Settle(0.1f);
                float driftStart = car.Body.linearVelocity.magnitude, driftYaw = 0f, slip = 0f;
                for (float e = 0f; e < 1f; e += Time.fixedDeltaTime)
                {
                    yield return VehicleLab.Step;
                    driftYaw = Mathf.Max(driftYaw, car.Body.angularVelocity.y);
                    slip = Mathf.Max(slip, Vector3.Angle(car.Body.linearVelocity, car.Body.rotation * Vector3.forward));
                }
                int level = car.StatusEffects.DriftChargeLevel;
                float driftLoss = driftStart - car.Body.linearVelocity.magnitude;

                // Entrada a rampa a 28 m/s: velocidad perdida al subir, sin contar la gravedad.
                lab.Spawn(model, new Vector3(60f, 0.05f, 40f), Quaternion.identity);
                car = lab.Car;
                yield return lab.Settle(0.5f);
                lab.Launch(Vector3.forward * 28f);
                lab.Drive(28f / max);
                while (lab.LocalPosition.z < 58f) yield return VehicleLab.Step;
                float entry = car.Body.linearVelocity.magnitude;
                while (lab.LocalPosition.z < 66f && car.Body.linearVelocity.magnitude > 1f) yield return VehicleLab.Step;
                float loss = (entry - car.Body.linearVelocity.magnitude) / entry * 100f;

                string F(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);
                rows.Add(string.Join(";", model, F(accelTime), F(brakeTime), F(reverseTime), F(top * 3.6f), F(yaws[0]), F(yaws[1]), F(driftYaw), F(slip), F(driftLoss),
                    level.ToString(), F(loss), F(straight), F(restHeight), F(restTilt)));
            }

            VehicleLab.Write("locomotion.csv", rows);
        }
    }

    /// <summary>Un caso de aterrizaje: cómo se prepara el auto y contra qué plano se mide.</summary>
    public sealed class LandingCase
    {
        public string Name;
        /// <summary>Cuenta como aterrizaje normal: despegue derecho o con inclinación moderada, sin choques.</summary>
        public bool Normal;
        public Func<VehicleLab, VehicleLab.Landing, StringBuilder, IEnumerator> Execute;

        public IEnumerator Run(VehicleLab lab, VehicleLab.Landing result, StringBuilder trace)
        {
            lab.Clear();
            yield return null;
            yield return Execute(lab, result, trace);
        }
    }

    public static class LandingCases
    {
        /// <summary>
        /// Casos fijados antes de ajustar valores. Normales: rampas de 14° y 20° de 12 m/s a la máxima, cresta,
        /// caídas de hasta 2 m con hasta 20° de inclinación y caída sobre pendiente. El resto se informa aparte.
        /// </summary>
        public static readonly LandingCase[] All =
            RampCases(14f, 13f, 12f, 20f, 28f, 33f)
            .Concat(RampCases(20f, 9f, 12f, 20f, 28f))
            .Concat(new[]
            {
                RampOneSide(14f, 13f, 20f), RampTurning(14f, 13f, 22f), RampDrift(14f, 13f, 22f),
                Ledge("ledge1.5_20", 1.5f, 20f), Ledge("ledge0.5_28", 0.5f, 28f),
                Drop("drop1_flat", true, 1f, 15f, Vector3.zero, Vector3.zero),
                Drop("drop2_flat", true, 2f, 15f, Vector3.zero, Vector3.zero),
                Drop("drop2_flat_still", true, 2f, 0f, Vector3.zero, Vector3.zero),
                Drop("drop2_pitch15", true, 2f, 15f, new Vector3(15f, 0f, 0f), Vector3.zero),
                Drop("drop2_pitch-15", true, 2f, 15f, new Vector3(-15f, 0f, 0f), Vector3.zero),
                Drop("drop2_roll20", true, 2f, 15f, new Vector3(0f, 0f, 20f), Vector3.zero),
                Drop("drop2_diag15", true, 2f, 15f, new Vector3(12f, 0f, 12f), Vector3.zero),
                Slope("slope14_down_20", 14f, 20f),
                Drop("x_drop4_flat", false, 4f, 20f, Vector3.zero, Vector3.zero),
                Drop("x_drop2_roll45", false, 2f, 15f, new Vector3(0f, 0f, 45f), Vector3.zero),
                Drop("x_drop3_pitch40", false, 3f, 20f, new Vector3(40f, 0f, 0f), Vector3.zero),
                Drop("x_drop2_roll70", false, 2f, 12f, new Vector3(0f, 0f, 70f), Vector3.zero),
                Drop("x_drop3_rollspin", false, 3f, 18f, Vector3.zero, new Vector3(0f, 0f, 4f)),
                Drop("x_drop3_pitchspin", false, 3f, 18f, Vector3.zero, new Vector3(-3f, 0f, 0f)),
                Drop("x_drop3_yaw60", false, 3f, 18f, new Vector3(0f, 60f, 0f), Vector3.zero),
                Drop("x_side90_still", false, 1.2f, 0f, new Vector3(0f, 0f, 90f), Vector3.zero, 6f),
                Drop("x_roof180_still", false, 1f, 0f, new Vector3(0f, 0f, 180f), Vector3.zero, 6f),
                Drop("x_roof180_slide", false, 1f, 15f, new Vector3(0f, 0f, 178f), Vector3.zero, 6f),
                Drop("x_nose90", false, 2.2f, 5f, new Vector3(89f, 0f, 0f), Vector3.zero, 6f),
            }).ToArray();

        static IEnumerable<LandingCase> RampCases(float angle, float length, params float[] speeds) =>
            speeds.Select(speed => Ramp($"ramp{angle:0}_{speed:0}", angle, length, speed, 1f, 0f, false, 0f));

        static LandingCase RampOneSide(float angle, float length, float speed) => Ramp($"ramp{angle:0}_{speed:0}_edge", angle, length, speed, 1f, 0f, false, 3.6f, false, "x_");
        static LandingCase RampTurning(float angle, float length, float speed) => Ramp($"ramp{angle:0}_{speed:0}_turn", angle, length, speed, 1f, 0.35f, false, -1f);
        static LandingCase RampDrift(float angle, float length, float speed) => Ramp($"ramp{angle:0}_{speed:0}_drift", angle, length, speed, 1f, 0.5f, true, -1.5f, false, "x_");

        static LandingCase Ramp(string name, float angle, float length, float speed, float throttle, float steer, bool drift, float offsetX, bool normal = true, string prefix = "")
        {
            return new LandingCase
            {
                Name = prefix + name,
                Normal = normal,
                Execute = (lab, result, trace) => RunRamp(lab, result, trace, angle, length, speed, throttle, steer, drift, offsetX)
            };
        }

        static IEnumerator RunRamp(VehicleLab lab, VehicleLab.Landing result, StringBuilder trace, float angle, float length, float speed, float throttle, float steer, bool drift, float offsetX)
        {
            lab.Floor();
            lab.Ramp(Vector3.zero, 0f, angle, 8f, length);
            CarController car = lab.Spawn(result.Model, new Vector3(offsetX, 0.05f, -14f), Quaternion.identity);
            yield return lab.Settle(0.6f);
            lab.Launch(Vector3.forward * speed);
            // Acelerador proporcional hasta el borde para llegar a la velocidad pedida.
            float lip = length * Mathf.Cos(angle * Mathf.Deg2Rad);
            while (lab.LocalPosition.z < 1f)
            {
                lab.Drive(Mathf.Clamp(speed / car.Stats.MaxSpeed + (speed - car.ForwardSpeed) * 0.5f, 0f, 1f));
                yield return VehicleLab.Step;
            }
            lab.Drive(throttle, steer, drift);
            float start = lab.LocalPosition.z;
            yield return lab.Measure(result, Vector3.zero, Vector3.up, () => lab.LocalPosition.z > lip - 0.5f, 10f, 2.5f, trace);
        }

        /// <summary>Sale a nivel de un bloque elevado.</summary>
        static LandingCase Ledge(string name, float height, float speed) => new LandingCase
        {
            Name = name,
            Normal = true,
            Execute = (lab, result, trace) => RunLedge(lab, result, trace, height, speed)
        };

        static IEnumerator RunLedge(VehicleLab lab, VehicleLab.Landing result, StringBuilder trace, float height, float speed)
        {
            lab.Floor();
            lab.Box("Ledge", new Vector3(0f, height * 0.5f, -20f), new Vector3(12f, height, 40f), Quaternion.identity);
            lab.Spawn(result.Model, new Vector3(0f, height + 0.05f, -16f), Quaternion.identity);
            yield return lab.Settle(0.6f);
            lab.Launch(Vector3.forward * speed);
            lab.Drive(0.6f);
            yield return lab.Measure(result, Vector3.zero, Vector3.up, () => lab.LocalPosition.z > -0.5f, 8f, 2.5f, trace);
        }

        static LandingCase Drop(string name, bool normal, float height, float speed, Vector3 euler, Vector3 spin, float window = 2.5f) => new LandingCase
        {
            Name = name,
            Normal = normal,
            Execute = (lab, result, trace) => RunDrop(lab, result, trace, height, speed, euler, spin, window)
        };

        static IEnumerator RunDrop(VehicleLab lab, VehicleLab.Landing result, StringBuilder trace, float height, float speed, Vector3 euler, Vector3 spin, float window)
        {
            lab.Floor();
            Quaternion rotation = Quaternion.Euler(euler);
            lab.Spawn(result.Model, new Vector3(0f, height, 0f), rotation);
            lab.Drive(0.6f);
            lab.Launch(Vector3.forward * speed, spin);
            yield return lab.Measure(result, Vector3.zero, Vector3.up, () => true, 5f + window, window, trace);
        }

        /// <summary>Cae sobre una bajada, como la salida de una cresta.</summary>
        static LandingCase Slope(string name, float angle, float speed) => new LandingCase
        {
            Name = name,
            Normal = true,
            Execute = (lab, result, trace) => RunSlope(lab, result, trace, angle, speed)
        };

        static IEnumerator RunSlope(VehicleLab lab, VehicleLab.Landing result, StringBuilder trace, float angle, float speed)
        {
            Quaternion rotation = Quaternion.Euler(angle, 0f, 0f);
            lab.Box("Slope", rotation * new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 400f), rotation);
            lab.Spawn(result.Model, rotation * new Vector3(0f, 0f, -150f) + Vector3.up * 2f, Quaternion.identity);
            lab.Drive(0.6f);
            lab.Launch(Vector3.forward * speed);
            yield return lab.Measure(result, Vector3.zero, rotation * Vector3.up, () => true, 8f, 2.5f, trace);
        }
    }
}
#endif
