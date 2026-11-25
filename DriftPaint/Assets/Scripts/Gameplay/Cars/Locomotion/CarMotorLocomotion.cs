using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Capa 5: convierte el FrameInput en fuerzas en cada paso de física, siempre en cuatro pasos:
    /// 1. setpoints, 2. estados, 3. PID (velocidad, yaw rate y agarre), 4. fuerzas.
    /// CarController llama a Tick solo en el auto local. La suspensión, la adherencia y la orientación son de
    /// CarStabilityController. Todas las fuerzas usan ForceMode.Acceleration.
    /// </summary>
    public class CarMotorLocomotion : MonoBehaviour
    {
        CarStats _stats;
        CarMovementInput _movementInput;
        CarStatusEffects _statusEffects;
        Rigidbody _rb;

        readonly PidController _speedPid = new PidController();
        readonly PidController _yawRatePid = new PidController();
        readonly PidController _gripPid = new PidController();

        float _targetSpeed;
        float _targetYawRate;

        public float TargetSpeed => _targetSpeed;
        public float TargetYawRate => _targetYawRate;
        public float ForwardSpeed { get; private set; }
        public float LateralSpeed { get; private set; }
        public bool IsInitialized => _stats != null;

        public void Initialize(CarStats stats, CarMovementInput input, CarStatusEffects effects, Rigidbody rb)
        {
            if (_statusEffects != null) _statusEffects.StunChanged -= OnStunChanged;

            _stats = stats;
            _movementInput = input;
            _statusEffects = effects;
            _rb = rb;

            _speedPid.ApplySettings(stats.SpeedPid);
            _yawRatePid.ApplySettings(stats.YawRatePid);
            _gripPid.ApplySettings(stats.GripPid);

            if (_statusEffects != null) _statusEffects.StunChanged += OnStunChanged;
            ResetControllers();
        }

        void OnDestroy()
        {
            if (_statusEffects != null) _statusEffects.StunChanged -= OnStunChanged;
        }

        void OnStunChanged()
        {
            // La integral acumulada no tiene que empujar el auto de golpe al empezar o terminar el stun.
            _speedPid.Reset();
        }

        /// <summary>
        /// Un paso de conducción. Todo se mide y se aplica sobre la base del piso; los permisos y la mezcla vienen
        /// del estado de estabilidad de este mismo paso.
        /// </summary>
        public void Tick(in StabilityFrame frame, float dt)
        {
            if (_stats == null || _rb == null || _rb.isKinematic) return;

            SupportBasis basis = frame.CanDrive && frame.Basis.Valid ? frame.Basis : BodyBasis();
            Vector3 velocity = _rb.linearVelocity;
            ForwardSpeed = Vector3.Dot(velocity, basis.Forward);
            LateralSpeed = Vector3.Dot(velocity, basis.Right);
            float yawRate = Vector3.Dot(_rb.angularVelocity, basis.Normal);

            if (frame.JustLostSupport || !frame.CanDrive) ResetPids();
            if (frame.JustRegainedSupport)
            {
                // Vuelve a manejar con la velocidad que trae: sin tirones por lo que se pidió en el aire.
                _targetSpeed = ForwardSpeed;
                _speedPid.ResetFromMeasurement(ForwardSpeed);
                _yawRatePid.ResetFromMeasurement(yawRate);
                _gripPid.ResetFromMeasurement(LateralSpeed);
            }

            // Pasos 1 y 2: setpoints ajustados por los estados.
            UpdateSetpoints(ForwardSpeed, dt);
            if (!frame.CanDrive) return;

            float blend = frame.DriveBlend;
            bool integrate = blend >= 1f;

            // Paso 3 y 4: PID de velocidad (PI).
            float accel = _speedPid.Evaluate(_targetSpeed, ForwardSpeed, dt, integrate);
            _rb.AddForce(basis.Forward * (accel * blend), ForceMode.Acceleration);

            // PID de yaw rate (PD).
            if (frame.CanApplySteering)
            {
                float yawAccel = _yawRatePid.Evaluate(_targetYawRate, yawRate, dt, integrate);
                _rb.AddTorque(basis.Normal * (yawAccel * blend), ForceMode.Acceleration);
            }

            // PID de agarre lateral, limitado por MaxGrip (baja durante el drift).
            if (frame.CanApplyGrip)
            {
                float lateralAccel = _gripPid.Evaluate(0f, LateralSpeed, dt, integrate);
                float grip = _stats.MaxGrip * (_statusEffects != null ? _statusEffects.GripMultiplier : 1f);
                lateralAccel = Mathf.Clamp(lateralAccel, -grip, grip);
                _rb.AddForce(basis.Right * (lateralAccel * blend), ForceMode.Acceleration);
            }

            ApplySafetySpeedLimit();
        }

        SupportBasis BodyBasis()
        {
            Quaternion rotation = _rb.rotation;
            return new SupportBasis { Valid = true, Normal = rotation * Vector3.up, Forward = rotation * Vector3.forward, Right = rotation * Vector3.right };
        }

        void UpdateSetpoints(float forwardSpeed, float dt)
        {
            FrameInput input = _movementInput != null ? _movementInput.CurrentInput : default;
            bool canAccelerate = _statusEffects == null || _statusEffects.CanAccelerate;
            float throttle = canAccelerate ? input.Throttle : Mathf.Min(input.Throttle, 0f);
            float accelMultiplier = _statusEffects != null ? _statusEffects.AccelerationMultiplier : 1f;

            float goal;
            float rate;

            // Frenar o cambiar de sentido se decide con la velocidad pedida, no con la medida: el PID
            // acerca la velocidad real a 0 de a poco y puede quedar apenas por encima de cualquier
            // umbral, lo que obligaba a soltar y volver a apretar. La pedida llega a 0 exacto y, con
            // la tecla sostenida, sigue de largo hacia el otro sentido. El límite contra la velocidad
            // real de más abajo la mantiene positiva mientras el auto todavía avanza rápido.
            if (throttle > 0f && _targetSpeed < 0f)
            {
                // W yendo marcha atrás: primero frena.
                goal = 0f;
                rate = _stats.BrakeDeceleration;
            }
            else if (throttle > 0f)
            {
                goal = throttle * _stats.MaxSpeed;
                rate = _stats.AccelerationAt(forwardSpeed) * accelMultiplier;
            }
            else if (throttle < 0f && _targetSpeed > 0f)
            {
                // S yendo hacia adelante: primero frena.
                goal = 0f;
                rate = _stats.BrakeDeceleration;
            }
            else if (throttle < 0f)
            {
                goal = throttle * _stats.MaxReverseSpeed;
                rate = _stats.ReverseAcceleration;
            }
            else
            {
                goal = 0f;
                rate = _stats.CoastDeceleration;
            }

            _targetSpeed = Mathf.MoveTowards(_targetSpeed, goal, rate * dt);
            if (_statusEffects != null)
                _targetSpeed = _statusEffects.ApplyToSpeed(_targetSpeed, dt);
            _targetSpeed = Mathf.Clamp(_targetSpeed, forwardSpeed - _stats.SetpointLead, forwardSpeed + _stats.SetpointLead);

            // En marcha atrás se invierte el giro.
            float gear = forwardSpeed >= 0f ? 1f : -1f;
            float yawMultiplier = _statusEffects != null ? _statusEffects.YawMultiplier : 1f;
            _targetYawRate = input.Steer * _stats.MaxYawRate * yawMultiplier
                             * _stats.TurnBySpeed(Mathf.Abs(forwardSpeed)) * gear;
        }

        void ApplySafetySpeedLimit()
        {
            // Red de seguridad: si un choque empuja el auto por encima de MaxSpeed se limita.
            Vector3 velocity = _rb.linearVelocity;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            float limit = _stats.MaxSpeed * 1.15f;
            if (horizontal.sqrMagnitude > limit * limit)
            {
                horizontal = horizontal.normalized * limit;
                _rb.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
            }
        }

        /// <summary>PID y velocidades pedidas en cero.</summary>
        public void ResetControllers()
        {
            ResetPids();
            _targetSpeed = 0f;
            _targetYawRate = 0f;
        }

        void ResetPids()
        {
            _speedPid.Reset();
            _yawRatePid.Reset();
            _gripPid.Reset();
        }

        /// <summary>Reubicar auto: velocidad en 0 y PID limpios.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (_rb != null)
            {
                bool wasKinematic = _rb.isKinematic;
                if (!wasKinematic)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }
                _rb.position = position;
                _rb.rotation = rotation;
            }
            transform.SetPositionAndRotation(position, rotation);
            ResetControllers();
        }
    }
}
