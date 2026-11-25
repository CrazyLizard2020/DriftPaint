using System;
using DriftPaint.LiveOperations;
using Unity.Profiling;
using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Raíz del prefab del auto. Arma CarStats, conecta las piezas, implementa ICarState
    /// e ICarSimulation, y en SetMode prende o apaga input, locomoción y física. Su FixedUpdate es el único
    /// que mueve el auto: lee el piso, decide el estado, aplica las asistencias y después la conducción.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CarMovementInput))]
    [RequireComponent(typeof(CarAbilityInput))]
    [RequireComponent(typeof(CarGroundSensor))]
    [RequireComponent(typeof(CarStatusEffects))]
    [RequireComponent(typeof(CarStabilityController))]
    [RequireComponent(typeof(CarMotorLocomotion))]
    public class CarController : MonoBehaviour, ICarState, ICarSimulation
    {
        [Tooltip("Valores base del modelo de auto.")]
        [SerializeField] CarDataSO _carData;

        [Tooltip("Altura del centro de masa respecto de la raíz. Bajo para que el auto no vuelque fácil.")]
        [SerializeField] float _centerOfMassHeight = 0.25f;

        [Tooltip("Altura extra al reubicar el auto.")]
        [SerializeField, Min(0f)] float _respawnLift = 0.6f;

        [Tooltip("Cada cuántos segundos se guarda una posición segura para reaparecer después de caerse del mapa.")]
        [SerializeField, Min(0.1f)] float _safePoseInterval = 0.5f;

        /// <summary>Cantidad de posiciones seguras guardadas. Al caerse se usa la más vieja, lejos del borde.</summary>
        const int SafePoseHistory = 4;
        /// <summary>Inclinación máxima del auto (grados) para guardar una posición segura.</summary>
        const float SafeMaxTilt = 20f;
        /// <summary>Pendiente máxima del piso (grados) para guardar una posición segura.</summary>
        const float SafeMaxSlope = 8f;

        readonly Pose[] _safePoses = new Pose[SafePoseHistory];
        int _safePoseCount;
        int _safePoseNext;
        float _safePoseTimer;
        Pose _initialPose;

        static readonly ProfilerMarker SenseMarker = new ProfilerMarker("Car.Sense");
        static readonly ProfilerMarker StateMarker = new ProfilerMarker("Car.Stability");
        static readonly ProfilerMarker AssistMarker = new ProfilerMarker("Car.Assist");
        static readonly ProfilerMarker DriveMarker = new ProfilerMarker("Car.Drive");

        Rigidbody _rb;
        PhysicsMaterial _bodyMaterial;
        MeshCollider _hull;
        bool _inertiaScaled;
        CarMovementInput _movementInput;
        CarAbilityInput _abilityInput;
        CarGroundSensor _groundSensor;
        CarStatusEffects _statusEffects;
        CarStabilityController _stability;
        CarMotorLocomotion _locomotion;
        float _remoteSteer;

        public CarStats Stats { get; private set; }
        public CarDataSO CarData => _carData;
        public CarSimulationMode Mode { get; private set; } = CarSimulationMode.Local;

        public CarMovementInput MovementInput => _movementInput;
        public CarAbilityInput AbilityInput => _abilityInput;
        public CarGroundSensor GroundSensor => _groundSensor;
        public CarStatusEffects StatusEffects => _statusEffects;
        public CarStabilityController Stability => _stability;
        public CarMotorLocomotion Locomotion => _locomotion;
        public Rigidbody Body => _rb;

        /// <summary>
        /// Devuelve la pose donde reubicar el auto. Si es null, se endereza en el lugar.
        /// </summary>
        public Func<CarController, Pose> RespawnPoseProvider { get; set; }

        /// <summary>
        /// Devuelve la pose donde reaparecer después de caerse del mapa. Si es null, se usa la
        /// última posición segura guardada o, si no hay ninguna, la posición inicial.
        /// </summary>
        public Func<CarController, Pose> FallRespawnPoseProvider { get; set; }

        /// <summary>Se dispara después de un teletransporte (reubicar auto o spawn).</summary>
        public event Action<Vector3, Quaternion> Teleported;

        /// <summary>Se dispara cuando el auto reaparece después de caerse del mapa.</summary>
        public event Action FellOutOfMap;

        // ICarState
        public Vector3 Position => _rb != null ? _rb.position : transform.position;
        public Quaternion Rotation => _rb != null ? _rb.rotation : transform.rotation;
        public Vector3 Velocity => _rb != null && !_rb.isKinematic ? _rb.linearVelocity : _remoteVelocity;
        public float Speed
        {
            get
            {
                Vector3 v = Velocity;
                return new Vector2(v.x, v.z).magnitude;
            }
        }
        public float ForwardSpeed => Vector3.Dot(Velocity, transform.forward);
        public float SteerInput => Mode == CarSimulationMode.Local && _movementInput != null
            ? _movementInput.CurrentInput.Steer
            : _remoteSteer;

        Vector3 _remoteVelocity;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _movementInput = GetComponent<CarMovementInput>();
            _abilityInput = GetComponent<CarAbilityInput>();
            _groundSensor = GetComponent<CarGroundSensor>();
            _statusEffects = GetComponent<CarStatusEffects>();
            _stability = GetComponent<CarStabilityController>();
            _locomotion = GetComponent<CarMotorLocomotion>();
            _hull = GetComponent<MeshCollider>();

            ConfigureRigidbody();
            ApplyFrictionlessMaterial();
            WarnIfIncomplete();

            if (_carData == null)
            {
                Debug.LogError($"{name}: CarController necesita un CarDataSO.", this);
                enabled = false;
                return;
            }

            var stats = new CarStats(_carData);
            // Los valores de LiveOps de la sala pisan los de la ficha. Todos los jugadores usan la misma copia.
            string carId = CarChoice.IdFromObjectName(name);
            stats.ApplyOverrides(LiveOps.Room.StatsFor(carId));
            stats.ApplyOverrides(LiveOps.Room.StabilityFor(carId), message => Debug.LogWarning($"{carId}: {message}.", this));
            Initialize(stats);
            _movementInput.RespawnRequested += Respawn;
        }

        void Start()
        {
            _initialPose = GetUprightPoseInPlace();
        }

        void OnDestroy()
        {
            if (_movementInput != null) _movementInput.RespawnRequested -= Respawn;
            if (_bodyMaterial != null) Destroy(_bodyMaterial);
        }

        void FixedUpdate()
        {
            if (Mode == CarSimulationMode.Local && Stats != null && !_rb.isKinematic) Simulate(Time.fixedDeltaTime);
        }

        /// <summary>Un paso del auto local, siempre en el mismo orden.</summary>
        void Simulate(float dt)
        {
            using (SenseMarker.Auto()) _groundSensor.Sense(dt);
            _statusEffects.Tick(dt);
            using (StateMarker.Auto()) _stability.Evaluate(dt);

            if (_stability.Frame.RequestRespawn)
            {
                RespawnStuck();
                return;
            }

            using (AssistMarker.Auto()) _stability.ApplyAssistForces(dt);
            using (DriveMarker.Auto()) _locomotion.Tick(_stability.Frame, dt);
            TrackSafePose(dt);
        }

        /// <summary>
        /// Reubica un auto que no pudo recuperarse. Si está derecho pero trabado, volver al mismo lugar no sirve:
        /// va a la última posición segura.
        /// </summary>
        void RespawnStuck()
        {
            bool upright = Vector3.Angle(_rb.rotation * Vector3.up, Vector3.up) < Stats.Stability.AutoRollMaxAngle;
            if (!upright || _safePoseCount == 0)
            {
                Respawn();
                return;
            }
            Pose pose = GetFallRespawnPose();
            Teleport(pose.position, pose.rotation);
        }

        /// <summary>
        /// Guarda periódicamente dónde estuvo el auto bien apoyado: todas las ruedas en el piso,
        /// derecho y sobre una superficie casi plana. Las rampas no cuentan, porque reaparecer
        /// derecho sobre una pendiente dejaría el auto metido en ella.
        /// </summary>
        void TrackSafePose(float dt)
        {
            _safePoseTimer += dt;
            if (_safePoseTimer < _safePoseInterval) return;

            int wheelCount = _groundSensor.Probes != null ? _groundSensor.Probes.Length : 0;
            bool allWheelsDown = wheelCount > 0 && _groundSensor.GroundedWheels >= wheelCount;
            bool upright = Vector3.Angle(transform.up, Vector3.up) < SafeMaxTilt;
            bool flatGround = _stability.IsGrounded && _groundSensor.Sample.SlopeAngle < SafeMaxSlope;
            if (!allWheelsDown || !upright || !flatGround) return;

            _safePoseTimer = 0f;
            PushSafePose(GetUprightPoseInPlace());
        }

        void PushSafePose(Pose pose)
        {
            _safePoses[_safePoseNext] = pose;
            _safePoseNext = (_safePoseNext + 1) % SafePoseHistory;
            _safePoseCount = Mathf.Min(_safePoseCount + 1, SafePoseHistory);
        }

        /// <summary>Pose para reaparecer después de una caída.</summary>
        public Pose GetFallRespawnPose()
        {
            if (FallRespawnPoseProvider != null) return FallRespawnPoseProvider(this);
            if (_safePoseCount == 0) return _initialPose;

            // La más vieja del historial: unos segundos antes de la caída, lejos del borde.
            int oldest = _safePoseCount < SafePoseHistory ? 0 : _safePoseNext;
            return _safePoses[oldest];
        }

        /// <summary>
        /// Reaparece en el mapa jugable después de caerse. El historial queda solo con la pose usada,
        /// así una nueva caída inmediata vuelve al mismo lugar seguro.
        /// </summary>
        public void RespawnAfterFall()
        {
            Pose pose = GetFallRespawnPose();
            _safePoseCount = 0;
            _safePoseNext = 0;
            _safePoseTimer = 0f;
            PushSafePose(pose);

            Teleport(pose.position, pose.rotation);
            FellOutOfMap?.Invoke();
        }

        /// <summary>
        /// Avisa si al auto le faltan piezas que arma el generador de prefabs (aviso de vuelco, estabilidad,
        /// color de equipo o casco de colisión). Pasa cuando el prefab se generó con una versión
        /// anterior del generador y hay que regenerarlo.
        /// </summary>
        void WarnIfIncomplete()
        {
            bool hasHull = TryGetComponent(out MeshCollider hull) && hull.convex;
            if (TryGetComponent(out CarFlipWarning _) && TryGetComponent(out CarTeamColor _) && hasHull) return;

            Debug.LogWarning($"{name}: el prefab está desactualizado (le falta aviso de vuelco, color de equipo o casco de colisión). " +
                             "Regeneralo con DriftPaint > Regenerar prefabs de autos.", this);
        }

        /// <summary>
        /// Reinicializa con otros stats (por ejemplo con overrides aplicados).
        /// </summary>
        public void Initialize(CarStats stats)
        {
            Stats = stats;
            _movementInput.SetRespawnHoldTime(stats.RespawnHoldTime);
            _groundSensor.Configure(stats.Stability);
            _stability.Initialize(stats, _groundSensor, _movementInput, _rb);
            _statusEffects.Initialize(stats, _movementInput, _groundSensor, _stability, _rb);
            _locomotion.Initialize(stats, _movementInput, _statusEffects, _rb);
            ApplyMass(stats.Stability);
            _bodyMaterial.bounciness = stats.Stability.LandingBounciness;
        }

        /// <summary>Centro de masa, inercia y tope de giro según la ficha. Siempre parte de los valores originales.</summary>
        void ApplyMass(StabilitySettings settings)
        {
            float height = _centerOfMassHeight + settings.CenterOfMassOffsetY;
            if (_hull != null && _hull.sharedMesh != null)
            {
                Bounds bounds = _hull.sharedMesh.bounds;
                float margin = bounds.size.y * 0.02f;
                height = Mathf.Clamp(height, Mathf.Min(_centerOfMassHeight, bounds.min.y + margin), Mathf.Max(_centerOfMassHeight, bounds.max.y - margin));
            }

            _rb.automaticCenterOfMass = false;
            if (_inertiaScaled)
            {
                _rb.ResetInertiaTensor();
                _inertiaScaled = false;
            }
            _rb.centerOfMass = new Vector3(0f, height, 0f);
            _rb.maxAngularVelocity = settings.MaxAngularVelocity;

            if (settings.InertiaTensorScale == Vector3.one) return;
            Quaternion axes = _rb.inertiaTensorRotation;
            _rb.inertiaTensor = Vector3.Scale(_rb.inertiaTensor, settings.InertiaTensorScale);
            _rb.inertiaTensorRotation = axes;
            _inertiaScaled = true;
        }

        void ConfigureRigidbody()
        {
            _rb.useGravity = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _rb.linearDamping = 0f;
            _rb.angularDamping = 0.5f;
        }

        void ApplyFrictionlessMaterial()
        {
            // Sin fricción física: el agarre y el frenado los controlan los PID.
            _bodyMaterial = new PhysicsMaterial("CarBody")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounciness = 0f,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };

            foreach (Collider col in GetComponentsInChildren<Collider>())
            {
                if (!col.isTrigger) col.sharedMaterial = _bodyMaterial;
            }
        }

        public void SetMode(CarSimulationMode mode)
        {
            Mode = mode;
            bool local = mode == CarSimulationMode.Local;

            _movementInput.enabled = local;
            _abilityInput.enabled = local;
            _groundSensor.enabled = local;
            _statusEffects.enabled = local;
            _stability.enabled = local;
            _locomotion.enabled = local;

            _rb.isKinematic = !local;
            _rb.interpolation = local ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;

            _stability.ResetState();
            if (local)
            {
                _locomotion.ResetControllers();
                _statusEffects.ClearAll();
            }
        }

        public void SetRemoteSteer(float steer) => _remoteSteer = Mathf.Clamp(steer, -1f, 1f);

        /// <summary>Velocidad recibida por red, para visuales y pintura de autos remotos.</summary>
        public void SetRemoteVelocity(Vector3 velocity) => _remoteVelocity = velocity;

        public void SetDrivingGate(IDrivingGate gate) => _movementInput.SetDrivingGate(gate);

        public void Respawn()
        {
            Pose pose = RespawnPoseProvider != null ? RespawnPoseProvider(this) : GetUprightPoseInPlace();
            Teleport(pose.position, pose.rotation);
        }

        /// <summary>Pose derecha en la posición actual, conservando hacia dónde mira el auto.</summary>
        public Pose GetUprightPoseInPlace()
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(transform.up, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            return new Pose(transform.position + Vector3.up * _respawnLift, Quaternion.LookRotation(forward.normalized, Vector3.up));
        }

        /// <summary>Teletransporta el auto: velocidad en 0, PID y estados limpios.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _statusEffects.ClearAll();
            _locomotion.Teleport(position, rotation);
            _stability.ResetState();
            Teleported?.Invoke(position, rotation);
        }

        /// <summary>
        /// Un auto ajeno saltó de golpe a otra posición (teletransporte o corte de red). Avisa igual que
        /// un teletransporte propio, para que nadie una la posición vieja con la nueva.
        /// </summary>
        public void NotifyRemoteTeleport()
        {
            if (Mode == CarSimulationMode.Local) return;
            Teleported?.Invoke(Position, Rotation);
        }
    }
}
