using System;
using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Paso 2 de la locomoción: guarda los efectos activos (boost, stun, slow, drift y turbo)
    /// con sus timers y expone multiplicadores que CarMotorLocomotion lee.
    /// Los aplica el dueño del auto, porque es el dueño de su física. CarController llama a Tick una vez por paso.
    /// </summary>
    public class CarStatusEffects : MonoBehaviour
    {
        [Tooltip("Tiempo con la dirección derecha antes de cortar el drift. Evita cortarlo al contravolantear.")]
        [SerializeField, Min(0f)] float _driftStraightGrace = 0.2f;

        CarStats _stats;
        CarMovementInput _input;
        CarGroundSensor _ground;
        CarStabilityController _stability;
        Rigidbody _rb;

        float _stunTimer;
        float _stunSpeedCap;
        float _slowTimer;
        float _slowDeceleration;
        float _driftStraightTimer;
        float _turboTimer;

        public bool IsStunned => _stunTimer > 0f;
        public bool IsSlowed => _slowTimer > 0f;
        public bool IsDrifting { get; private set; }
        public float DriftTime { get; private set; }
        public bool IsBoosting => _stability != null && _stability.IsGrounded && _ground != null && _ground.OnOwnPaint;
        public bool IsTurboActive => _turboTimer > 0f;
        public float StunTimeLeft => _stunTimer;
        public float SlowTimeLeft => _slowTimer;
        public float TurboTimeLeft => _turboTimer;

        /// <summary>0 sin carga, 1 o 2 según el tiempo de drift.</summary>
        public int DriftChargeLevel
        {
            get
            {
                if (!IsDrifting || _stats == null) return 0;
                if (DriftTime >= _stats.DriftChargeTiers.y) return 2;
                if (DriftTime >= _stats.DriftChargeTiers.x) return 1;
                return 0;
            }
        }

        public bool CanAccelerate => !IsStunned;
        public bool CanPaint => !IsStunned;

        /// <summary>Gana el más fuerte entre boost de pintura y turbo de drift.</summary>
        public float AccelerationMultiplier
        {
            get
            {
                if (_stats == null) return 1f;
                float boost = IsBoosting ? _stats.PaintBoostMultiplier : 1f;
                float turbo = IsTurboActive ? _stats.DriftTurboMultiplier : 1f;
                return Mathf.Max(boost, turbo);
            }
        }

        public float GripMultiplier => IsDrifting && _stats != null ? _stats.DriftGripFactor : 1f;
        public float YawMultiplier => IsDrifting && _stats != null ? _stats.DriftYawMultiplier : 1f;

        /// <summary>Al empezar y terminar un stun la locomoción resetea el PID de velocidad.</summary>
        public event Action StunChanged;
        public event Action DriftStarted;
        /// <summary>Nivel de carga con el que terminó el drift (0 si no dio turbo).</summary>
        public event Action<int> DriftEnded;

        public void Initialize(CarStats stats, CarMovementInput input, CarGroundSensor ground, CarStabilityController stability, Rigidbody rb)
        {
            _stats = stats;
            _input = input;
            _ground = ground;
            _stability = stability;
            _rb = rb;
        }

        float ForwardSpeed => _rb != null ? Vector3.Dot(_rb.linearVelocity, transform.forward) : 0f;

        /// <summary>Avanza los efectos un paso de física.</summary>
        public void Tick(float dt)
        {
            if (_stats == null) return;

            if (_stunTimer > 0f)
            {
                _stunTimer -= dt;
                if (_stunTimer <= 0f)
                {
                    _stunTimer = 0f;
                    StunChanged?.Invoke();
                }
            }

            if (_slowTimer > 0f) _slowTimer = Mathf.Max(0f, _slowTimer - dt);
            if (_turboTimer > 0f) _turboTimer = Mathf.Max(0f, _turboTimer - dt);

            UpdateDrift(dt);
        }

        void UpdateDrift(float dt)
        {
            FrameInput input = _input != null ? _input.CurrentInput : default;
            bool grounded = _stability == null || _stability.IsGrounded;
            bool fastEnough = ForwardSpeed >= _stats.DriftMinSpeed;
            bool steering = Mathf.Abs(input.Steer) > 0.15f;

            if (!IsDrifting)
            {
                if (input.Drift && steering && fastEnough && grounded && !IsStunned)
                {
                    IsDrifting = true;
                    DriftTime = 0f;
                    _driftStraightTimer = 0f;
                    DriftStarted?.Invoke();
                }
                return;
            }

            _driftStraightTimer = steering ? 0f : _driftStraightTimer + dt;

            bool keep = input.Drift && fastEnough && grounded && !IsStunned && _driftStraightTimer <= _driftStraightGrace;
            if (keep)
            {
                DriftTime += dt;
                return;
            }

            EndDrift(grantTurbo: !IsStunned);
        }

        void EndDrift(bool grantTurbo)
        {
            int level = DriftChargeLevel;
            IsDrifting = false;
            DriftTime = 0f;

            if (grantTurbo && level > 0)
            {
                _turboTimer = level >= 2 ? _stats.DriftTurboDuration.y : _stats.DriftTurboDuration.x;
                DriftEnded?.Invoke(level);
            }
            else
            {
                DriftEnded?.Invoke(0);
            }
        }

        /// <summary>
        /// Ajusta la velocidad objetivo (stun, slow y drift). Se llama una vez por FixedUpdate.
        /// </summary>
        public float ApplyToSpeed(float targetSpeed, float dt)
        {
            if (_stats == null) return targetSpeed;

            if (IsStunned && targetSpeed > _stunSpeedCap)
                targetSpeed = _stunSpeedCap;

            if (IsSlowed && targetSpeed > 0f)
                targetSpeed = Mathf.Max(0f, targetSpeed - _slowDeceleration * dt);

            if (IsDrifting && targetSpeed > 0f)
                targetSpeed = Mathf.Max(0f, targetSpeed - _stats.DriftSpeedLoss * dt);

            return targetSpeed;
        }

        /// <summary>
        /// Impacto directo: la velocidad máxima baja a speedFactor de la actual y no acelera ni pinta
        /// durante duration segundos.
        /// </summary>
        public void ApplyStun(float duration, float speedFactor)
        {
            if (_stats == null || duration <= 0f) return;

            bool wasStunned = IsStunned;
            _stunSpeedCap = Mathf.Max(0f, ForwardSpeed) * Mathf.Clamp01(speedFactor);
            _stunTimer = Mathf.Max(_stunTimer, duration);

            // Un stun corta el drift y borra la carga.
            if (IsDrifting) EndDrift(grantTurbo: false);
            _turboTimer = 0f;

            if (!wasStunned) StunChanged?.Invoke();
        }

        /// <summary>
        /// Splash: pierde deceleration m/s por segundo durante duration segundos. Sigue pintando. Si ya
        /// estaba lento, queda el slow más fuerte y el que más dura.
        /// </summary>
        public void ApplySlow(float duration, float deceleration)
        {
            if (_stats == null || duration <= 0f) return;
            _slowDeceleration = IsSlowed ? Mathf.Max(_slowDeceleration, deceleration) : deceleration;
            _slowTimer = Mathf.Max(_slowTimer, duration);
        }

        public void ClearAll()
        {
            bool wasStunned = IsStunned;
            _stunTimer = 0f;
            _slowTimer = 0f;
            _turboTimer = 0f;
            IsDrifting = false;
            DriftTime = 0f;
            if (wasStunned) StunChanged?.Invoke();
        }
    }
}
