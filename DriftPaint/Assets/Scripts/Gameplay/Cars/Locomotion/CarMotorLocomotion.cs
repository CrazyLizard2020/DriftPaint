using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Capa 5: convierte el FrameInput en fuerzas en cada FixedUpdate, siempre en cuatro pasos:
    /// 1. setpoints, 2. estados, 3. PID (velocidad, yaw rate y agarre), 4. fuerzas.
    /// Solo corre en el auto local. Todas las fuerzas usan ForceMode.Acceleration.
    /// </summary>
    [DefaultExecutionOrder(0)]
    public class CarMotorLocomotion : MonoBehaviour
    {
        CarStats _stats;
        CarMovementInput _movementInput;
        CarStatusEffects _statusEffects;
        CarGroundSensor _groundSensor;
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

        public void Initialize(CarStats stats, CarMovementInput input, CarStatusEffects effects, CarGroundSensor ground, Rigidbody rb)
        {
            if (_statusEffects != null) _statusEffects.StunChanged -= OnStunChanged;

            _stats = stats;
            _movementInput = input;
            _statusEffects = effects;
            _groundSensor = ground;
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

        void FixedUpdate()
        {
            if (_stats == null || _rb == null || _rb.isKinematic) return;

            float dt = Time.fixedDeltaTime;
            Vector3 localVelocity = transform.InverseTransformDirection(_rb.linearVelocity);
            ForwardSpeed = localVelocity.z;
            LateralSpeed = localVelocity.x;

            // Gravedad extra para que el auto se sienta pegado al piso.
            if (_stats.ExtraGravity > 0f)
                _rb.AddForce(Vector3.down * _stats.ExtraGravity, ForceMode.Acceleration);

            // Las ruedas sostienen el auto con resortes; la carrocería no toca el piso.
            ApplySuspension();

            // Pasos 1 y 2: setpoints ajustados por los estados.
            UpdateSetpoints(localVelocity.z, dt);

            if (_groundSensor != null && !_groundSensor.IsGrounded)
            {
                // En el aire no hay motor, giro ni agarre, y los PID no acumulan error hasta aterrizar.
                ResetControllers(keepTargetSpeed: true);
                return;
            }

            Vector3 groundNormal = _groundSensor != null ? _groundSensor.GroundNormal : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, groundNormal).normalized;

            // Paso 3 y 4: PID de velocidad (PI).
            float accel = _speedPid.Evaluate(_targetSpeed, localVelocity.z, dt);
            _rb.AddForce(forward * accel, ForceMode.Acceleration);

            // PID de yaw rate (PD).
            float yawRate = transform.InverseTransformDirection(_rb.angularVelocity).y;
            float yawAccel = _yawRatePid.Evaluate(_targetYawRate, yawRate, dt);
            _rb.AddRelativeTorque(0f, yawAccel, 0f, ForceMode.Acceleration);

            // PID de agarre lateral, limitado por MaxGrip (baja durante el drift).
            float lateralAccel = _gripPid.Evaluate(0f, localVelocity.x, dt);
            float grip = _stats.MaxGrip * (_statusEffects != null ? _statusEffects.GripMultiplier : 1f);
            lateralAccel = Mathf.Clamp(lateralAccel, -grip, grip);
            _rb.AddForce(right * lateralAccel, ForceMode.Acceleration);

            ApplySafetySpeedLimit();
        }

        /// <summary>
        /// Suspensión por rueda: cada rueda empuja hacia arriba del auto con un resorte amortiguado
        /// según qué tan comprimida está. La altura de reposo se calcula para que, con el peso
        /// repartido entre las ruedas, el centro de cada rueda quede a la altura de su radio.
        /// Así las rampas y los bordes los sube la rueda y no el canto del collider.
        /// </summary>
        /// <remarks>
        /// El resorte solo no alcanza a levantar el auto cuando entra rápido a una rampa: la rueda
        /// se hunde hasta el tope y la carrocería termina chocando. Para eso hay un tope de
        /// suspensión: si la rueda se hunde más que BumpStopRatio de su radio, se anula la parte
        /// de la velocidad de esa rueda que va contra la superficie. La rueda se comporta como un
        /// contacto rígido y la velocidad se redirige a lo largo de la rampa en lugar de perderse.
        /// </remarks>
        void ApplySuspension()
        {
            if (_groundSensor == null) return;

            CarGroundSensor.WheelContact[] contacts = _groundSensor.Contacts;
            Transform[] probes = _groundSensor.Probes;
            int wheelCount = contacts.Length;
            if (wheelCount == 0) return;

            float stiffness = _stats.SuspensionStiffness;
            float totalGravity = -Physics.gravity.y + _stats.ExtraGravity;
            float restHeight = _groundSensor.WheelRadius + totalGravity / (wheelCount * stiffness);
            float bumpStopHeight = _groundSensor.WheelRadius * BumpStopRatio;
            Vector3 up = transform.up;

            for (int i = 0; i < wheelCount; i++)
            {
                CarGroundSensor.WheelContact contact = contacts[i];
                if (!contact.HasHit || probes[i] == null) continue;

                float compression = restHeight - contact.Height;
                if (compression <= 0f) continue;

                Vector3 wheelPosition = probes[i].position;
                Vector3 pointVelocity = _rb.GetPointVelocity(wheelPosition);

                if (contact.Height < bumpStopHeight)
                {
                    float intoSurface = Vector3.Dot(pointVelocity, contact.Normal);
                    if (intoSurface < 0f)
                    {
                        // Se reparte entre las ruedas: el impulso en un punto también hace girar
                        // el auto, así que aplicarlo entero en cada rueda lo haría rebotar.
                        _rb.AddForceAtPosition(contact.Normal * (-intoSurface * BumpStopShare), wheelPosition, ForceMode.VelocityChange);
                    }
                }

                // Al estirarse (velocidad hacia arriba) amortigua más: el resorte devuelve la energía
                // del aterrizaje de a poco en lugar de lanzar el auto hacia arriba.
                float verticalSpeed = Vector3.Dot(pointVelocity, up);
                float damping = _stats.SuspensionDamping * (verticalSpeed > 0f ? _stats.SuspensionReboundRatio : 1f);
                float accel = stiffness * compression - damping * verticalSpeed;
                if (accel <= 0f) continue;

                _rb.AddForceAtPosition(up * accel, wheelPosition, ForceMode.Acceleration);
            }
        }

        /// <summary>Altura del centro de la rueda, como fracción del radio, a la que actúa el tope de suspensión.</summary>
        const float BumpStopRatio = 0.6f;

        /// <summary>Parte de la velocidad contra la superficie que anula cada rueda en el tope, por paso de física.</summary>
        const float BumpStopShare = 0.5f;

        void UpdateSetpoints(float forwardSpeed, float dt)
        {
            FrameInput input = _movementInput != null ? _movementInput.CurrentInput : default;
            bool canAccelerate = _statusEffects == null || _statusEffects.CanAccelerate;
            float throttle = canAccelerate ? input.Throttle : Mathf.Min(input.Throttle, 0f);
            float accelMultiplier = _statusEffects != null ? _statusEffects.AccelerationMultiplier : 1f;

            float goal;
            float rate;

            if (throttle > 0f && forwardSpeed < -0.1f)
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
            else if (throttle < 0f && forwardSpeed > 0.1f)
            {
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

        public void ResetControllers() => ResetControllers(keepTargetSpeed: false);

        void ResetControllers(bool keepTargetSpeed)
        {
            _speedPid.Reset();
            _yawRatePid.Reset();
            _gripPid.Reset();
            if (!keepTargetSpeed)
            {
                _targetSpeed = 0f;
                _targetYawRate = 0f;
            }
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
