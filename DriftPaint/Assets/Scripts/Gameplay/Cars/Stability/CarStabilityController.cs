using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Dueño de la máquina de estados del apoyo y único que aplica fuerzas de estabilidad: suspensión, adherencia,
    /// alineación, absorción de rebote, límites de giro y recuperación. No corre solo: CarController llama a
    /// Evaluate y después a ApplyAssistForces una vez por paso de física, y solo en el auto local.
    /// </summary>
    public class CarStabilityController : MonoBehaviour
    {
        /// <summary>Altura del centro de la rueda, como fracción del radio, a la que actúa el tope de suspensión.</summary>
        const float StopRatio = 0.6f;
        const int StopIterations = 8;
        /// <summary>Inclinación (grados) a partir de la cual se corrige el auto ya apoyado.</summary>
        const float SupportAngleStart = 4f;
        /// <summary>Inclinación (grados) con la corrección completa.</summary>
        const float SupportAngleFull = 12f;
        /// <summary>Fuerza del freno de cabeceo y rolido (1/s).</summary>
        const float AngularLimitStrength = 25f;
        const float SpaceRadius = 0.55f;
        const float SpaceHeight = 1.1f;
        const int HistorySize = 64;

        readonly VehicleStabilityFSM _fsm = new VehicleStabilityFSM();
        readonly StabilityHistory _history = new StabilityHistory(HistorySize);
        readonly StabilityMath.Stop[] _stops = new StabilityMath.Stop[8];
        readonly Collider[] _overlaps = new Collider[16];

        CarStats _stats;
        StabilitySettings _settings;
        CarGroundSensor _ground;
        CarMovementInput _movementInput;
        Rigidbody _rb;
        StabilityFrame _frame;
        StabilityTelemetry _telemetry;
        int _transitions;

        /// <summary>Permisos y salidas del último paso.</summary>
        public ref readonly StabilityFrame Frame => ref _frame;
        public ref readonly StabilityTelemetry Telemetry => ref _telemetry;
        public VehicleStabilityState State => _fsm.State;
        /// <summary>Apoyo confirmado. Es el único dato de "está en el piso" que usa el resto del juego.</summary>
        public bool IsGrounded => _frame.CanDrive;
        public StabilityHistory History => _history;
        public float FlipTimer => _fsm.FlipTimer;

        public void Initialize(CarStats stats, CarGroundSensor ground, CarMovementInput input, Rigidbody rb)
        {
            _stats = stats;
            _settings = stats.Stability;
            _ground = ground;
            _movementInput = input;
            _rb = rb;
            ResetState();
        }

        /// <summary>Olvida el apoyo, los temporizadores y la recuperación en curso.</summary>
        public void ResetState()
        {
            _fsm.Reset();
            _transitions = 0;
            _frame = default;
            _frame.State = _fsm.State;
            _frame.SupportNormal = Vector3.up;
            _telemetry = default;
            if (_ground != null) _ground.ResetSample();
        }

        /// <summary>Decide el estado y los permisos de este paso. No aplica nada.</summary>
        public ref readonly StabilityFrame Evaluate(float dt)
        {
            ref readonly GroundSample ground = ref _ground.Sample;
            Vector3 up = _rb.rotation * Vector3.up;
            float tilt = Vector3.Angle(up, Vector3.up);
            Vector3 contactNormal = ground.HasValidWheelSupport ? ground.SupportNormal : ground.HasBodyGroundContact ? ground.BodyContactNormal : Vector3.up;

            var input = new StabilityInput
            {
                Ground = ground,
                Velocity = _rb.linearVelocity,
                Tilt = tilt,
                SupportTilt = Vector3.Angle(up, contactNormal),
                // Buscar lugar cuesta una consulta: solo hace falta cuando ya se está por recuperar.
                HasRecoverySpace = _fsm.FlipTimer + dt >= _settings.FlipDetectTime && HasRecoverySpace(contactNormal)
            };

            VehicleStabilityState before = _fsm.State;
            float duration = _fsm.StateTime;
            _frame = _fsm.Evaluate(input, _settings, dt);
            _frame.SupportNormal = ground.HasAnyGroundContact ? contactNormal : ground.HasPrediction ? ground.PredictedNormal : Vector3.up;
            _frame.AdhesionAcceleration = ResolveAdhesion(ground);

            if (_fsm.Transitions != _transitions)
            {
                _transitions = _fsm.Transitions;
                _history.Add(new StabilityTransition
                {
                    Time = Time.fixedTime,
                    From = before,
                    To = _frame.State,
                    Duration = duration,
                    Wheels = ground.WheelContactCount,
                    Velocity = _rb.linearVelocity,
                    Tilt = tilt
                });
            }
            return ref _frame;
        }

        /// <summary>La adherencia es una sola: gana la más fuerte de las que corresponden y no se suman.</summary>
        float ResolveAdhesion(in GroundSample ground)
        {
            bool landing = _frame.State == VehicleStabilityState.Landing;
            if (_frame.State != VehicleStabilityState.Grounded && !landing) return 0f;

            float adhesion = 0f;
            if (ground.HasValidWheelSupport)
            {
                float contact = ground.WheelContactCount / (float)Mathf.Max(1, _ground.Wheels.Length);
                Vector3 velocity = _rb.linearVelocity;
                float throttle = _movementInput != null ? _movementInput.CurrentInput.Throttle : 0f;
                adhesion = StabilityMath.Adhesion(_settings, ground.SlopeAngle, velocity.magnitude, throttle, contact);
                if (landing) adhesion = Mathf.Max(adhesion, _settings.LandingAdhesionAcceleration * _frame.LandingWindow * contact);
            }
            if (_frame.ApplyAutoRoll) adhesion = Mathf.Max(adhesion, _settings.AutoRollAcceleration);
            return Mathf.Min(adhesion, _settings.MaxAdhesionAcceleration);
        }

        /// <summary>Aplica las fuerzas que permite el último Evaluate.</summary>
        public void ApplyAssistForces(float dt)
        {
            _telemetry = default;

            if (_frame.ApplyFlightGravity && _settings.FlightGravity > 0f)
            {
                _telemetry.FlightGravity = _settings.FlightGravity;
                _rb.AddForce(Vector3.down * _settings.FlightGravity, ForceMode.Acceleration);
            }
            if (_frame.AdhesionAcceleration > 0f)
            {
                _telemetry.Adhesion = _frame.AdhesionAcceleration;
                _rb.AddForce(_frame.SupportNormal * -_frame.AdhesionAcceleration, ForceMode.Acceleration);
            }
            if (_frame.ApplySuspension) ApplySuspension(dt);
            if (_frame.ApplyLandingAbsorption) ApplyAbsorption(dt);
            if (_frame.RequestRecoveryImpulse) ApplyRecoveryImpulse();
            ApplyTilt(dt);
            if (_frame.ApplyAngularLimit) ApplyAngularLimit(dt);
        }

        #region Suspensión

        /// <summary>
        /// Cada rueda empuja hacia arriba del auto con un resorte amortiguado según qué tan comprimida está. Así las
        /// rampas y los bordes los sube la rueda y no el canto del collider.
        /// </summary>
        void ApplySuspension(float dt)
        {
            WheelSample[] wheels = _ground.Wheels;
            Vector3 up = _rb.rotation * Vector3.up;
            Vector3 center = _rb.worldCenterOfMass;
            Vector3 inertia = _rb.inertiaTensor;
            Quaternion inertiaRotation = _rb.rotation * _rb.inertiaTensorRotation;
            float mass = _rb.mass;

            // Velocidad que va a tener el auto con los resortes y el peso de este paso: los topes solo frenan lo que falta.
            Vector3 velocity = _rb.linearVelocity + (Physics.gravity - _frame.SupportNormal * _frame.AdhesionAcceleration) * dt;
            Vector3 angularVelocity = _rb.angularVelocity;
            for (int i = 0; i < wheels.Length; i++)
            {
                if (!wheels[i].HasHit) continue;
                float accel = StabilityMath.Suspension(wheels[i].Compression, Vector3.Dot(wheels[i].Velocity, up),
                    _settings.SuspensionStiffness, _settings.SuspensionDamping, _settings.SuspensionRebound, _settings.MaxSuspensionAcceleration);
                if (accel <= 0f) continue;

                _telemetry.Suspension += accel;
                _rb.AddForceAtPosition(up * accel, wheels[i].Position, ForceMode.Acceleration);
                velocity += up * (accel * dt);
                angularVelocity += StabilityMath.AngularResponse(wheels[i].Position - center, up, inertia, inertiaRotation) * (accel * mass * dt);
            }

            ApplyStops(velocity, angularVelocity, center, inertia, inertiaRotation, mass, dt);
        }

        /// <summary>
        /// Topes de suspensión. El resorte solo no alcanza cuando el auto entra rápido a una rampa o cae fuerte: si una
        /// rueda se hunde demasiado se comporta como un contacto rígido sin rebote y la velocidad sigue por la superficie.
        /// </summary>
        void ApplyStops(Vector3 velocity, Vector3 angularVelocity, Vector3 center, Vector3 inertia, Quaternion inertiaRotation, float mass, float dt)
        {
            WheelSample[] wheels = _ground.Wheels;
            float stopHeight = _ground.WheelRadius * StopRatio;
            float maxImpulse = _settings.MaxSuspensionAcceleration * dt * mass;
            int count = 0;

            for (int i = 0; i < wheels.Length && count < _stops.Length; i++)
            {
                if (!wheels[i].HasHit || wheels[i].Height >= stopHeight) continue;
                Vector3 arm = wheels[i].Position - center;
                Vector3 response = StabilityMath.AngularResponse(arm, wheels[i].Normal, inertia, inertiaRotation);
                _stops[count++] = new StabilityMath.Stop
                {
                    Wheel = i,
                    Arm = arm,
                    Normal = wheels[i].Normal,
                    AngularResponse = response,
                    InverseMass = 1f / mass + Vector3.Dot(Vector3.Cross(response, arm), wheels[i].Normal),
                    MaxImpulse = maxImpulse
                };
            }
            if (count == 0) return;

            StabilityMath.SolveStops(_stops, count, velocity, angularVelocity, mass, StopIterations);
            for (int i = 0; i < count; i++)
            {
                if (_stops[i].Impulse <= 0f) continue;
                _telemetry.StopImpulse += _stops[i].Impulse / mass;
                _rb.AddForceAtPosition(_stops[i].Normal * _stops[i].Impulse, wheels[_stops[i].Wheel].Position, ForceMode.Impulse);
            }
        }

        #endregion

        /// <summary>Quita solo la velocidad que se aleja del piso; la que va a lo largo queda igual.</summary>
        void ApplyAbsorption(float dt)
        {
            if (_frame.LandingWindow <= 0f) return;
            Vector3 normal = _frame.SupportNormal;
            float change = StabilityMath.Absorption(Vector3.Dot(_rb.linearVelocity, normal), _settings.LandingReboundDamping, _settings.MaxLandingCorrection, dt);
            if (change <= 0f) return;
            _telemetry.Absorption = change;
            _rb.AddForce(normal * -change, ForceMode.VelocityChange);
        }

        #region Giro

        /// <summary>Hay una sola corrección de inclinación por paso, según el estado. El giro lo maneja la locomoción.</summary>
        void ApplyTilt(float dt)
        {
            Vector3 target = _frame.SupportNormal;
            float kp, pitchKd, rollKd, weight = 1f;

            if (_frame.ApplyRecovery)
            {
                kp = _settings.LandingAlignKp;
                pitchKd = rollKd = _settings.LandingAlignKd;
            }
            else if (_frame.ApplyAutoRoll)
            {
                kp = _settings.AutoRollKp;
                pitchKd = rollKd = _settings.AutoRollKd;
            }
            else if (_frame.ApplyLandingAssist)
            {
                kp = _settings.LandingAlignKp;
                pitchKd = rollKd = _settings.LandingAlignKd;
            }
            else if (_frame.ApplySupportAlignment)
            {
                // Con las cuatro ruedas apoyadas alcanza la suspensión: solo se corrige una inclinación grande.
                weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SupportAngleStart, SupportAngleFull, Vector3.Angle(_rb.rotation * Vector3.up, target)));
                if (weight <= 0f) return;
                kp = _settings.LandingAlignKp;
                pitchKd = rollKd = _settings.LandingAlignKd;
            }
            else if (_frame.ApplyAirAlignment)
            {
                kp = _settings.AirAlignKp;
                pitchKd = _settings.AirPitchDamping;
                rollKd = _settings.AirRollDamping;
            }
            else return;

            Vector3 torque = StabilityMath.Tilt(_rb.rotation, target, _rb.angularVelocity, kp, pitchKd, rollKd, _settings.MaxAngularAcceleration, dt) * weight;
            if (_frame.ApplyAirAlignment && _settings.AirYawDamping > 0f)
            {
                Vector3 up = _rb.rotation * Vector3.up;
                torque -= up * (Vector3.Dot(_rb.angularVelocity, up) * _settings.AirYawDamping / (1f + _settings.AirYawDamping * dt));
            }
            _telemetry.Tilt = torque.magnitude;
            _rb.AddTorque(torque, ForceMode.Acceleration);
        }

        void ApplyAngularLimit(float dt)
        {
            bool recovering = _frame.ApplyRecovery;
            float pitch = recovering ? _settings.MaxAngularVelocity : _settings.MaxPitchRate;
            float roll = recovering ? _settings.MaxAngularVelocity : _settings.MaxRollRate;
            Quaternion rotation = _rb.rotation;
            Vector3 local = Quaternion.Inverse(rotation) * _rb.angularVelocity;
            Vector3 limit = StabilityMath.AngularLimit(local, pitch, roll, AngularLimitStrength, dt);
            if (limit == Vector3.zero) return;
            _telemetry.AngularLimit = limit.magnitude;
            _rb.AddTorque(rotation * limit, ForceMode.Acceleration);
        }

        #endregion

        #region Recuperación

        void ApplyRecoveryImpulse()
        {
            if (_settings.RecoveryImpulse <= 0f) return;
            // Cuanto más dado vuelta está, más alto tiene que saltar para girar.
            float tilt = Vector3.Angle(_rb.rotation * Vector3.up, Vector3.up);
            float impulse = _settings.RecoveryImpulse * Mathf.Lerp(0.6f, 1f, Mathf.InverseLerp(60f, 160f, tilt));
            _telemetry.RecoveryImpulse = impulse;
            _rb.AddForce(_frame.SupportNormal * impulse, ForceMode.VelocityChange);
        }

        /// <summary>No hay paredes ni techo en el espacio que el auto necesita para girar.</summary>
        bool HasRecoverySpace(Vector3 normal)
        {
            int count = Physics.OverlapSphereNonAlloc(_rb.worldCenterOfMass + normal * SpaceHeight, SpaceRadius, _overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (_overlaps[i].attachedRigidbody != _rb) return false;
            return true;
        }

        #endregion
    }
}
