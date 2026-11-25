using System;
using System.Globalization;
using System.IO;
using System.Text;
using DriftPaint.Cars;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DriftPaint.Sandbox
{
    /// <summary>
    /// Panel de desarrollo de la escena de prueba: estado, apoyo, velocidades, fuerzas y efectos del auto, con el
    /// historial de transiciones. F7 lo muestra u oculta y F8 guarda en disco lo grabado mientras estuvo abierto.
    /// En la build final no hace nada.
    /// </summary>
    public class VehicleStabilityDebugger : MonoBehaviour
    {
        [SerializeField] SandboxBootstrap _sandbox;
        [SerializeField] GameObject _panel;
        [SerializeField] TextMeshProUGUI _text;

        // En la build final estos campos quedan guardados pero nadie los lee.
#pragma warning disable CS0414
        [Tooltip("Actualizaciones del texto por segundo.")]
        [SerializeField, Range(1f, 30f)] float _refreshRate = 10f;

        [Tooltip("Transiciones que se muestran.")]
        [SerializeField, Range(1, 16)] int _visibleTransitions = 8;

        [Tooltip("Dibuja apoyos, normal y predicción en la vista de escena.")]
        [SerializeField] bool _gizmos = true;

        [Tooltip("Pasos de física que guarda la grabación. Al llenarse pisa los más viejos.")]
        [SerializeField, Range(100, 20000)] int _recordCapacity = 3000;
#pragma warning restore CS0414

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        struct Record
        {
            public float Time;
            public StabilityFrame Frame;
            public StabilityTelemetry Telemetry;
            public Vector3 Position, Velocity, Angular;
            public int Wheels;
            public float Tilt;
        }

        readonly StringBuilder _builder = new StringBuilder(2048);
        Record[] _records;
        int _next, _count;
        float _refreshTimer;

        void Awake()
        {
            _records = new Record[_recordCapacity];
            if (_panel != null) _panel.SetActive(false);
        }

        bool IsOpen => _panel != null && _panel.activeSelf;

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && _panel != null)
            {
                if (keyboard.f7Key.wasPressedThisFrame) _panel.SetActive(!_panel.activeSelf);
                if (keyboard.f8Key.wasPressedThisFrame) Export();
            }

            CarController car = _sandbox != null ? _sandbox.Car : null;
            if (!IsOpen || _text == null || car == null || car.Stats == null) return;

            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f) return;
            _refreshTimer = 1f / _refreshRate;
            Write(car);
            _text.SetText(_builder);
        }

        void FixedUpdate()
        {
            CarController car = _sandbox != null ? _sandbox.Car : null;
            if (!IsOpen || car == null || car.Stats == null || _records.Length == 0) return;

            _records[_next] = new Record
            {
                Time = Time.fixedTime,
                Frame = car.Stability.Frame,
                Telemetry = car.Stability.Telemetry,
                Position = car.Body.position,
                Velocity = car.Body.linearVelocity,
                Angular = car.Body.angularVelocity,
                Wheels = car.GroundSensor.GroundedWheels,
                Tilt = Vector3.Angle(car.Body.rotation * Vector3.up, Vector3.up)
            };
            _next = (_next + 1) % _records.Length;
            _count = Mathf.Min(_count + 1, _records.Length);
        }

        void Write(CarController car)
        {
            ref readonly StabilityFrame frame = ref car.Stability.Frame;
            ref readonly StabilityTelemetry applied = ref car.Stability.Telemetry;
            ref readonly GroundSample ground = ref car.GroundSensor.Sample;
            Vector3 velocity = car.Velocity;

            _builder.Clear();
            _builder.Append("<b>").Append(car.name).Append("</b>  ").Append(car.Mode).Append("  paso ").Append(Time.fixedDeltaTime.ToString("0.0000")).Append(" s\n");
            _builder.Append("<b>").Append(frame.State).Append("</b>  ").Append(frame.StateTime.ToString("0.00")).Append(" s   aterrizaje ").Append(frame.LandingEpisode).Append('\n');
            _builder.Append("Ruedas ").Append(ground.WheelContactCount).Append("  confianza ").Append(ground.Confidence.ToString("0.00"))
                .Append("  pendiente ").Append(ground.SlopeAngle.ToString("0.0")).Append("°  casco ").Append(ground.HasBodyGroundContact ? "sí" : "no").Append('\n');
            _builder.Append("Predicción ").Append(ground.HasPrediction ? ground.PredictedTime.ToString("0.00") + " s" : "-")
                .Append("  consultas ").Append(car.GroundSensor.LastCastCount).Append('\n');
            _builder.Append("Velocidad ").Append((velocity.magnitude * 3.6f).ToString("0")).Append(" km/h  avance ").Append(car.Locomotion.ForwardSpeed.ToString("0.0"))
                .Append("  lateral ").Append(car.Locomotion.LateralSpeed.ToString("0.0")).Append("  normal ").Append(ground.NormalSpeed.ToString("0.00")).Append('\n');
            _builder.Append("Manejo ").Append(frame.DriveBlend.ToString("0.00")).Append("  ventana ").Append(frame.LandingWindow.ToString("0.00")).Append('\n');

            _builder.Append("\n<b>Permitido / aplicado</b>\n");
            Line("Suspensión", frame.ApplySuspension, applied.Suspension, "m/s²");
            Line("Topes", frame.ApplySuspension, applied.StopImpulse, "m/s");
            Line("Adherencia", frame.AdhesionAcceleration > 0f, applied.Adhesion, "m/s²");
            Line("Peso en vuelo", frame.ApplyFlightGravity, applied.FlightGravity, "m/s²");
            Line("Alineación " + Alignment(frame), frame.ApplyAirAlignment || frame.ApplyAutoRoll || frame.ApplyLandingAssist || frame.ApplySupportAlignment || frame.ApplyRecovery, applied.Tilt, "rad/s²");
            Line("Absorción", frame.ApplyLandingAbsorption, applied.Absorption, "m/s");
            Line("Límite de giro", frame.ApplyAngularLimit, applied.AngularLimit, "rad/s²");
            if (frame.RecoveryWarning) _builder.Append("Recuperación, quedan ").Append(frame.WarningTimeLeft.ToString("0.0")).Append(" s\n");

            CarStatusEffects effects = car.StatusEffects;
            _builder.Append("\nEfectos: ").Append(effects.IsDrifting ? "drift " : "").Append(effects.IsBoosting ? "boost " : "").Append(effects.IsTurboActive ? "turbo " : "")
                .Append(effects.IsStunned ? "stun " : "").Append(effects.IsSlowed ? "slow" : "").Append("\n\n<b>Transiciones</b>\n");

            StabilityHistory history = car.Stability.History;
            for (int i = 0; i < Mathf.Min(_visibleTransitions, history.Count); i++)
            {
                StabilityTransition transition = history.Recent(i);
                _builder.Append(transition.Time.ToString("0.00")).Append("  ").Append(transition.From).Append(" → ").Append(transition.To)
                    .Append("  (").Append(transition.Duration.ToString("0.00")).Append(" s, ").Append(transition.Wheels).Append(" ruedas)\n");
            }
        }

        static string Alignment(in StabilityFrame frame) =>
            frame.ApplyRecovery ? "(recuperación)" : frame.ApplyAutoRoll ? "(enderezado)" : frame.ApplyLandingAssist ? "(aterrizaje)" : frame.ApplySupportAlignment ? "(apoyo)" : frame.ApplyAirAlignment ? "(aire)" : "";

        void Line(string label, bool allowed, float value, string unit)
        {
            _builder.Append(allowed ? "<color=#57D93B>●</color> " : "<color=#7A8699>○</color> ").Append(label).Append(": ")
                .Append(value.ToString("0.00")).Append(' ').Append(unit).Append('\n');
        }

        void Export()
        {
            if (_count == 0) return;
            string folder = Path.Combine(Application.persistentDataPath, "Stability");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"stability_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            var csv = new StringBuilder("t;state;wheels;blend;tilt;px;py;pz;vx;vy;vz;wx;wy;wz;suspension;stops;adhesion;flight;tilt_acc;absorption;limit\n");
            for (int i = 0; i < _count; i++)
            {
                Record r = _records[((_next - _count + i) % _records.Length + _records.Length) % _records.Length];
                csv.AppendFormat(CultureInfo.InvariantCulture, "{0:0.000};{1};{2};{3:0.00};{4:0.0};{5:0.00};{6:0.00};{7:0.00};{8:0.00};{9:0.00};{10:0.00};{11:0.00};{12:0.00};{13:0.00};",
                    r.Time, r.Frame.State, r.Wheels, r.Frame.DriveBlend, r.Tilt, r.Position.x, r.Position.y, r.Position.z, r.Velocity.x, r.Velocity.y, r.Velocity.z, r.Angular.x, r.Angular.y, r.Angular.z);
                csv.AppendFormat(CultureInfo.InvariantCulture, "{0:0.00};{1:0.00};{2:0.00};{3:0.00};{4:0.00};{5:0.00};{6:0.00}\n",
                    r.Telemetry.Suspension, r.Telemetry.StopImpulse, r.Telemetry.Adhesion, r.Telemetry.FlightGravity, r.Telemetry.Tilt, r.Telemetry.Absorption, r.Telemetry.AngularLimit);
            }
            File.WriteAllText(path, csv.ToString());
            Debug.Log($"Grabación de estabilidad guardada en {path}.");
        }

        void OnDrawGizmos()
        {
            CarController car = _sandbox != null ? _sandbox.Car : null;
            if (!_gizmos || !Application.isPlaying || car == null || car.GroundSensor == null) return;

            foreach (WheelSample wheel in car.GroundSensor.Wheels)
            {
                if (!wheel.HasHit) continue;
                Gizmos.color = wheel.IsSupporting ? Color.green : Color.yellow;
                Gizmos.DrawLine(wheel.Position, wheel.Point);
                Gizmos.DrawRay(wheel.Point, wheel.Normal * 0.4f);
            }
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(car.Position, car.Stability.Frame.SupportNormal * 1.5f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(car.Position, car.Velocity * 0.1f);
        }
#else
        void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
            enabled = false;
        }
#endif
    }
}
