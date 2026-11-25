using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>Lo que la máquina de estados necesita saber de un paso de física.</summary>
    public struct StabilityInput
    {
        public GroundSample Ground;
        /// <summary>Velocidad del centro de masa (m/s).</summary>
        public Vector3 Velocity;
        /// <summary>Ángulo entre el techo del auto y la vertical (grados).</summary>
        public float Tilt;
        /// <summary>Ángulo entre el techo del auto y la normal del piso que toca (grados).</summary>
        public float SupportTilt;
        /// <summary>Hay lugar para dar vuelta el auto.</summary>
        public bool HasRecoverySpace;
    }

    /// <summary>
    /// Máquina de estados del apoyo del auto. No conoce la escena ni el Rigidbody: recibe una lectura del piso y
    /// devuelve permisos. Hace como mucho una transición por paso y los permisos ya son los del estado nuevo.
    /// </summary>
    public sealed class VehicleStabilityFSM
    {
        /// <summary>Velocidad de separación (m/s) a partir de la cual perder el piso es un despegue.</summary>
        public const float TakeoffSpeed = 0.5f;
        /// <summary>Tiempo en el aire (s) para que el contacto siguiente cuente como un aterrizaje nuevo.</summary>
        public const float MinFlightTime = 0.15f;
        /// <summary>Duración máxima de Takeoff (s).</summary>
        public const float TakeoffTime = 0.12f;
        /// <summary>Confianza mínima de la lectura para confirmar apoyo.</summary>
        public const float MinConfidence = 0.5f;
        /// <summary>Inclinación (grados) por debajo de la cual la recuperación terminó bien.</summary>
        public const float RecoveredAngle = 40f;
        /// <summary>Inclinación (grados) con la que un auto en el aire ya no necesita la recuperación para caer bien.</summary>
        public const float UprightAngle = 15f;
        /// <summary>Tiempo (s) sin contacto que se tolera sin volver a contar el vuelco desde cero.</summary>
        public const float FlipGraceTime = 0.12f;
        /// <summary>Velocidad (m/s) por debajo de la cual un auto apoyado a medias se considera varado.</summary>
        public const float StrandedSpeed = 2f;

        bool _started;
        int _episode;
        float _airTime;
        float _landingElapsed;
        float _confirmTimer;
        float _partialTimer;
        float _blend;
        bool _confirmed;
        bool _wasDriving;
        float _flipTimer;
        float _flipLostTimer;
        float _blinkTimer;
        bool _blinking;
        float _cooldown;
        bool _impulsePending;
        bool _respawnRequested;

        public VehicleStabilityState State { get; private set; } = VehicleStabilityState.Airborne;
        /// <summary>Estado anterior a la última transición.</summary>
        public VehicleStabilityState Previous { get; private set; } = VehicleStabilityState.Airborne;
        public float StateTime { get; private set; }
        /// <summary>Transiciones hechas desde el último reinicio.</summary>
        public int Transitions { get; private set; }
        public float FlipTimer => _flipTimer;

        public VehicleStabilityFSM() => Reset();

        /// <summary>Olvida todo: cambio de modo, teletransporte o reconexión.</summary>
        public void Reset()
        {
            _started = false;
            _episode = 0;
            State = VehicleStabilityState.Airborne;
            Previous = VehicleStabilityState.Airborne;
            StateTime = 0f;
            Transitions = 0;
            _airTime = 0f;
            _landingElapsed = float.MaxValue;
            _cooldown = 0f;
            _wasDriving = false;
            _respawnRequested = false;
            ResetConfirmation();
            ResetFlip();
            _impulsePending = false;
        }

        public StabilityFrame Evaluate(in StabilityInput input, StabilitySettings settings, float dt)
        {
            if (!_started) Begin(input, settings);

            StateTime += dt;
            _cooldown = Mathf.Max(0f, _cooldown - dt);
            if (_landingElapsed < float.MaxValue) _landingElapsed += dt;
            if (IsFlying(State)) _airTime += dt;

            UpdateFlip(input, settings, dt);
            UpdateSupport(input, settings, dt);

            VehicleStabilityState next = Next(input, settings);
            if (next != State) Enter(next, settings);

            return Build(input, settings);
        }

        void Begin(in StabilityInput input, StabilitySettings settings)
        {
            _started = true;
            bool supported = input.Ground.HasValidWheelSupport;
            State = supported ? VehicleStabilityState.Landing : VehicleStabilityState.Airborne;
            Previous = State;
            StateTime = 0f;
        }

        static bool IsFlying(VehicleStabilityState state) =>
            state == VehicleStabilityState.Takeoff || state == VehicleStabilityState.Airborne || state == VehicleStabilityState.PreLanding;

        static bool IsConfirmable(in GroundSample ground, StabilitySettings settings) =>
            ground.HasValidWheelSupport && ground.Basis.Valid && ground.WheelContactCount >= settings.MinGroundedWheels && ground.Confidence >= MinConfidence;

        #region Temporizadores

        void UpdateSupport(in StabilityInput input, StabilitySettings settings, float dt)
        {
            if (State != VehicleStabilityState.Grounded && State != VehicleStabilityState.Landing) return;

            if (!input.Ground.HasValidWheelSupport)
            {
                ResetConfirmation();
                return;
            }

            if (IsConfirmable(input.Ground, settings))
            {
                _partialTimer = 0f;
                if (!_confirmed)
                {
                    _confirmTimer += dt;
                    if (_confirmTimer >= settings.GroundedConfirmationTime - 1e-5f) _confirmed = true;
                }
                if (_confirmed)
                    _blend = settings.LandingGripBlendTime <= 0f ? 1f : Mathf.Min(1f, _blend + dt / settings.LandingGripBlendTime);
                return;
            }

            // Apoyo real pero incompleto: se tolera un momento antes de volver a confirmar.
            _partialTimer += dt;
            if (!_confirmed || _partialTimer > settings.ContactGraceTime + 1e-5f) ResetConfirmation(keepPartial: true);
        }

        void ResetConfirmation(bool keepPartial = false)
        {
            _confirmTimer = 0f;
            _confirmed = false;
            _blend = 0f;
            if (!keepPartial) _partialTimer = 0f;
        }

        void UpdateFlip(in StabilityInput input, StabilitySettings settings, float dt)
        {
            if (State == VehicleStabilityState.SelfRecovery) return;

            bool flipped = input.Tilt > settings.FlippedAngle && input.Ground.HasAnyGroundContact;
            bool stranded = State == VehicleStabilityState.Landing && !_confirmed && input.Ground.HasAnyGroundContact && input.Velocity.sqrMagnitude < StrandedSpeed * StrandedSpeed;
            if (!flipped && !stranded)
            {
                // Rebotar sobre el techo no reinicia la cuenta; quedar derecho sí.
                _flipLostTimer += dt;
                if (input.Tilt <= settings.FlippedAngle || _flipLostTimer > FlipGraceTime) ResetFlip();
                return;
            }

            _flipLostTimer = 0f;
            _flipTimer += dt;
            if (_blinking) _blinkTimer += dt;
        }

        void ResetFlip()
        {
            _flipTimer = 0f;
            _flipLostTimer = 0f;
            _blinkTimer = 0f;
            _blinking = false;
        }

        #endregion

        #region Transiciones

        VehicleStabilityState Next(in StabilityInput input, StabilitySettings settings)
        {
            GroundSample ground = input.Ground;

            if (State == VehicleStabilityState.SelfRecovery)
            {
                bool recovered = input.Tilt < RecoveredAngle && ground.WheelContactCount > 0;
                if (recovered) return VehicleStabilityState.Landing;
                // Ya giró y solo le falta caer: sigue como cualquier vuelo.
                if (input.Tilt < UprightAngle && !ground.HasAnyGroundContact)
                    return ground.HasPrediction ? VehicleStabilityState.PreLanding : VehicleStabilityState.Airborne;
                if (StateTime >= settings.SelfRecoveryMaxTime) _respawnRequested = true;
                return State;
            }

            if (_flipTimer >= settings.FlipDetectTime && !_blinking)
            {
                if (input.HasRecoverySpace && _cooldown <= 0f) return VehicleStabilityState.SelfRecovery;
                _blinking = true;
                _blinkTimer = 0f;
            }
            if (_blinking && _blinkTimer >= settings.FlipBlinkTime) _respawnRequested = true;

            switch (State)
            {
                case VehicleStabilityState.Grounded:
                    if (ground.HasValidWheelSupport) return _confirmed ? State : VehicleStabilityState.Landing;
                    return LeaveGround(input);

                case VehicleStabilityState.Landing:
                    if (ground.HasValidWheelSupport) return _confirmed && _blend >= 1f ? VehicleStabilityState.Grounded : State;
                    if (ground.HasBodyGroundContact) return State;
                    return LeaveGround(input);

                case VehicleStabilityState.Takeoff:
                    if (TouchesGround(ground)) return VehicleStabilityState.Landing;
                    if (input.Velocity.y <= 0f) return ground.HasPrediction ? VehicleStabilityState.PreLanding : VehicleStabilityState.Airborne;
                    return StateTime >= TakeoffTime ? VehicleStabilityState.Airborne : State;

                case VehicleStabilityState.Airborne:
                    if (TouchesGround(ground)) return VehicleStabilityState.Landing;
                    return ground.HasPrediction ? VehicleStabilityState.PreLanding : State;

                case VehicleStabilityState.PreLanding:
                    if (TouchesGround(ground)) return VehicleStabilityState.Landing;
                    return ground.HasPrediction ? State : VehicleStabilityState.Airborne;

                default:
                    return State;
            }
        }

        static bool TouchesGround(in GroundSample ground) => ground.HasValidWheelSupport || ground.HasBodyGroundContact;

        static VehicleStabilityState LeaveGround(in StabilityInput input)
        {
            if (input.Ground.NormalSpeed > TakeoffSpeed && input.Velocity.y > 0f) return VehicleStabilityState.Takeoff;
            return input.Ground.HasPrediction ? VehicleStabilityState.PreLanding : VehicleStabilityState.Airborne;
        }

        void Enter(VehicleStabilityState next, StabilitySettings settings)
        {
            Exit(State, settings);
            Previous = State;
            State = next;
            StateTime = 0f;
            Transitions++;

            switch (next)
            {
                case VehicleStabilityState.Landing:
                    // Un toque corto entre dos contactos sigue siendo el mismo aterrizaje.
                    if (_airTime >= MinFlightTime)
                    {
                        _episode++;
                        _landingElapsed = 0f;
                    }
                    _airTime = 0f;
                    break;

                case VehicleStabilityState.Takeoff:
                case VehicleStabilityState.Airborne:
                case VehicleStabilityState.PreLanding:
                    ResetConfirmation();
                    break;

                case VehicleStabilityState.SelfRecovery:
                    ResetConfirmation();
                    ResetFlip();
                    _impulsePending = true;
                    break;
            }
        }

        void Exit(VehicleStabilityState state, StabilitySettings settings)
        {
            if (state != VehicleStabilityState.SelfRecovery) return;
            _cooldown = settings.RecoveryCooldown;
            _impulsePending = false;
            _airTime = 0f;
        }

        #endregion

        StabilityFrame Build(in StabilityInput input, StabilitySettings settings)
        {
            GroundSample ground = input.Ground;
            bool landing = State == VehicleStabilityState.Landing;
            bool grounded = State == VehicleStabilityState.Grounded;
            bool flying = IsFlying(State);
            bool recovering = State == VehicleStabilityState.SelfRecovery;
            bool wheels = ground.HasValidWheelSupport;

            float contactFactor = wheels ? Mathf.Clamp01(ground.WheelContactCount / (float)settings.MinGroundedWheels) : 0f;
            float blend = (grounded || landing) && _confirmed ? _blend * contactFactor : 0f;
            bool partial = ground.HasAnyGroundContact && !IsConfirmable(ground, settings);
            bool rollable = input.SupportTilt > 1f && input.Tilt < settings.AutoRollMaxAngle;

            var frame = new StabilityFrame
            {
                State = State,
                StateTime = StateTime,
                DriveBlend = blend,
                CanDrive = blend > 0f,
                CanApplySteering = blend > 0f,
                CanApplyGrip = blend > 0f,
                JustLostSupport = _wasDriving && blend <= 0f,
                JustRegainedSupport = !_wasDriving && blend > 0f,

                ApplySuspension = (grounded || landing || (recovering && input.Tilt < RecoveredAngle)) && wheels,
                ApplyAngularLimit = true,
                ApplyAirAlignment = flying,
                ApplyFlightGravity = flying,
                ApplyAutoRoll = (grounded || landing) && partial && rollable,
                ApplyLandingAssist = landing && wheels && !partial,
                ApplySupportAlignment = grounded && !partial,
                ApplyLandingAbsorption = landing && wheels,
                ApplyRecovery = recovering,

                SupportNormal = ground.SupportNormal,
                Basis = ground.Basis,
                LandingEpisode = _episode,
                LandingWindow = landing ? StabilityMath.LandingWindow(_landingElapsed, settings.LandingAdhesionDuration) : 0f,
                RecoveryWarning = recovering || _blinking,
                WarningTimeLeft = _blinking ? Mathf.Max(0f, settings.FlipBlinkTime - _blinkTimer) : 0f,
                RequestRecoveryImpulse = recovering && _impulsePending,
            };

            if (_respawnRequested)
            {
                frame.RequestRespawn = true;
                frame.RespawnReason = recovering ? RespawnReason.RecoveryTimeout : RespawnReason.NotRecoverable;
                _respawnRequested = false;
                ResetFlip();
            }

            _impulsePending = false;
            _wasDriving = blend > 0f;
            return frame;
        }
    }
}
