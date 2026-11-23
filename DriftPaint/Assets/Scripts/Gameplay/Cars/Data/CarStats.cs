using System.Collections.Generic;
using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Valores efectivos del auto en la partida: los de CarDataSO más los overrides aplicados.
    /// Calcula la escala de la curva de aceleración a partir de TimeToMaxSpeed.
    /// </summary>
    public class CarStats
    {
        /// <summary>Keys que acepta ApplyOverrides.</summary>
        public const string KeyTimeToMaxSpeed = "accel";
        public const string KeyBrakeDeceleration = "brake";
        public const string KeyMaxYawRate = "turn";

        readonly CarDataSO _data;
        float _timeToMaxSpeed;
        float _accelerationScale;

        public CarStats(CarDataSO data)
        {
            _data = data;
            MaxSpeed = data.MaxSpeed;
            BrakeDeceleration = data.BrakeDeceleration;
            MaxYawRate = data.MaxYawRate;
            SetTimeToMaxSpeed(data.TimeToMaxSpeed);
        }

        public CarDataSO Data => _data;

        public float MaxSpeed { get; private set; }
        public float TimeToMaxSpeed => _timeToMaxSpeed;
        public float BrakeDeceleration { get; private set; }
        public float MaxYawRate { get; private set; }

        public float MaxReverseSpeed => _data.MaxReverseSpeed;
        public float ReverseAcceleration => _data.ReverseAcceleration;
        public float CoastDeceleration => _data.CoastDeceleration;
        public float SetpointLead => _data.SetpointLead;
        public float MaxGrip => _data.MaxGrip;
        public float ExtraGravity => _data.ExtraGravity;
        public float SuspensionStiffness => _data.SuspensionStiffness;
        public float SuspensionDamping => _data.SuspensionDamping;
        public float SuspensionReboundRatio => _data.SuspensionReboundRatio;
        public float SuspensionTravel => _data.SuspensionTravel;
        public float FlippedAngle => _data.FlippedAngle;
        public float FlipDetectTime => _data.FlipDetectTime;
        public float FlipBlinkTime => _data.FlipBlinkTime;
        public float PaintBoostMultiplier => _data.PaintBoostMultiplier;
        public float StunSpeedFactor => _data.StunSpeedFactor;
        public float StatusDuration => _data.StatusDuration;
        public float SlowDeceleration => _data.SlowDeceleration;
        public float DriftMinSpeed => _data.DriftMinSpeed;
        public float DriftGripFactor => _data.DriftGripFactor;
        public float DriftYawMultiplier => _data.DriftYawMultiplier;
        public float DriftSpeedLoss => _data.DriftSpeedLoss;
        public Vector2 DriftChargeTiers => _data.DriftChargeTiers;
        public float DriftTurboMultiplier => _data.DriftTurboMultiplier;
        public Vector2 DriftTurboDuration => _data.DriftTurboDuration;
        public float RespawnHoldTime => _data.RespawnHoldTime;
        public PidSettings SpeedPid => _data.SpeedPid;
        public PidSettings YawRatePid => _data.YawRatePid;
        public PidSettings GripPid => _data.GripPid;

        /// <summary>
        /// Aceleración de la rampa (m/s²) a esta velocidad hacia adelante.
        /// Más alta a baja velocidad y más baja cerca de 120 km/h (cambios automáticos).
        /// </summary>
        public float AccelerationAt(float forwardSpeed)
        {
            float normalized = Mathf.Clamp01(forwardSpeed / MaxSpeed);
            return _accelerationScale * SampleAccelerationShape(normalized);
        }

        /// <summary>Multiplicador de giro: 0 quieto y crece con la velocidad.</summary>
        public float TurnBySpeed(float absoluteSpeed)
        {
            float normalized = Mathf.Clamp01(absoluteSpeed / MaxSpeed);
            return Mathf.Max(0f, _data.TurnBySpeed.Evaluate(normalized));
        }

        /// <summary>Pisa valores con los de car_stats. Keys desconocidas se ignoran.</summary>
        public void ApplyOverrides(IReadOnlyDictionary<string, float> overrides)
        {
            if (overrides == null) return;

            if (overrides.TryGetValue(KeyTimeToMaxSpeed, out float time) && time > 0f)
                SetTimeToMaxSpeed(time);
            if (overrides.TryGetValue(KeyBrakeDeceleration, out float brake) && brake > 0f)
                BrakeDeceleration = brake;
            if (overrides.TryGetValue(KeyMaxYawRate, out float yaw) && yaw > 0f)
                MaxYawRate = yaw;
        }

        void SetTimeToMaxSpeed(float seconds)
        {
            _timeToMaxSpeed = Mathf.Max(0.5f, seconds);
            _accelerationScale = ComputeAccelerationScale();
        }

        float SampleAccelerationShape(float normalized)
        {
            // Nunca 0: si la curva llega a 0 el auto no alcanzaría nunca la velocidad máxima.
            return Mathf.Max(0.05f, _data.AccelerationBySpeed.Evaluate(normalized));
        }

        /// <summary>
        /// Escala la curva para que de 0 a MaxSpeed tarde TimeToMaxSpeed.
        /// T = ∫ dv / (k·c(v/Vmax)) = (Vmax/k)·∫₀¹ dx/c(x)  →  k = Vmax·I / T
        /// </summary>
        float ComputeAccelerationScale()
        {
            const int samples = 200;
            float integral = 0f;
            for (int i = 0; i < samples; i++)
            {
                float x = (i + 0.5f) / samples;
                integral += 1f / SampleAccelerationShape(x);
            }
            integral /= samples;
            return MaxSpeed * integral / _timeToMaxSpeed;
        }
    }
}
