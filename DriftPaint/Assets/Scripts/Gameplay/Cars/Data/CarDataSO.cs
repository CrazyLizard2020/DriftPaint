using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Valores base de un modelo de auto. CarStats los toma como punto de partida
    /// y puede pisar algunos con overrides recibidos al empezar la partida.
    /// </summary>
    [CreateAssetMenu(fileName = "CarData", menuName = "DriftPaint/Cars/Car Data")]
    public class CarDataSO : ScriptableObject
    {
        [Header("Velocidad")]
        [Tooltip("Velocidad máxima hacia adelante en m/s. 33,3 m/s = 120 km/h.")]
        [SerializeField, Min(1f)] float _maxSpeed = 33.333f;

        [Tooltip("Segundos de 0 a la velocidad máxima. CarStats escala la curva para cumplirlo.")]
        [SerializeField, Min(0.5f)] float _timeToMaxSpeed = 6f;

        [Tooltip("Forma de la aceleración según velocidad normalizada (0 = quieto, 1 = máxima). Más alta a baja velocidad simula los cambios automáticos.")]
        [SerializeField] AnimationCurve _accelerationBySpeed = new AnimationCurve(
            new Keyframe(0f, 1f, 0f, -0.3f),
            new Keyframe(0.35f, 0.85f),
            new Keyframe(0.7f, 0.55f),
            new Keyframe(1f, 0.32f));

        [Tooltip("Desaceleración al frenar con S yendo hacia adelante (m/s²). Con 16,7 o más frena de 120 km/h a 0 en 2 s.")]
        [SerializeField, Min(16.7f)] float _brakeDeceleration = 19f;

        [Tooltip("Velocidad máxima de marcha atrás (m/s). 11,1 m/s = 40 km/h.")]
        [SerializeField, Min(0f)] float _maxReverseSpeed = 11.11f;

        [Tooltip("Aceleración de marcha atrás (m/s²). 3,7 m/s² llega a 40 km/h en 3 s.")]
        [SerializeField, Min(0f)] float _reverseAcceleration = 3.7f;

        [Tooltip("Desaceleración sin input (m/s²): el auto rueda y se frena solo.")]
        [SerializeField, Min(0f)] float _coastDeceleration = 5f;

        [Tooltip("Cuánto se puede alejar la velocidad objetivo de la medida (m/s). Evita que el auto salga disparado al despegarse de una pared.")]
        [SerializeField, Min(0.1f)] float _setpointLead = 3f;

        [Header("Giro")]
        [Tooltip("Velocidad de giro máxima en rad/s.")]
        [SerializeField, Min(0f)] float _maxYawRate = 2.4f;

        [Tooltip("Multiplicador del giro según velocidad normalizada. Vale 0 con el auto quieto.")]
        [SerializeField] AnimationCurve _turnBySpeed = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 12f),
            new Keyframe(0.12f, 0.85f),
            new Keyframe(0.3f, 1f),
            new Keyframe(1f, 0.8f));

        [Header("Agarre")]
        [Tooltip("Aceleración lateral máxima que puede hacer el agarre (m/s²). Si la curva pide más, el auto derrapa.")]
        [SerializeField, Min(0f)] float _maxGrip = 30f;

        [Tooltip("Gravedad extra (m/s²) para que el auto se sienta pegado al piso.")]
        [SerializeField, Min(0f)] float _extraGravity = 10f;

        [Header("Suspensión")]
        [Tooltip("Rigidez de cada rueda: aceleración (m/s²) por metro de compresión. Más alta, auto más duro.")]
        [SerializeField, Min(1f)] float _suspensionStiffness = 70f;

        [Tooltip("Amortiguación de cada rueda: aceleración (m/s²) por m/s de velocidad vertical. Evita que rebote.")]
        [SerializeField, Min(0f)] float _suspensionDamping = 7f;

        [Tooltip("Multiplicador de la amortiguación cuando la rueda se estira después de comprimirse. " +
                 "Mayor que 1 frena el rebote al aterrizar sin endurecer la entrada a los pozos.")]
        [SerializeField, Min(1f)] float _suspensionReboundRatio = 2.5f;

        [Tooltip("Recorrido de la rueda por debajo de su altura normal antes de perder contacto (m).")]
        [SerializeField, Min(0.01f)] float _suspensionTravel = 0.3f;

        [Header("Auto dado vuelta")]
        [Tooltip("Ángulo entre el techo del auto y la vertical a partir del cual se considera dado vuelta.")]
        [SerializeField, Range(30f, 180f)] float _flippedAngle = 75f;

        [Tooltip("Segundos dado vuelta antes de empezar a titilar.")]
        [SerializeField, Min(0f)] float _flipDetectTime = 2f;

        [Tooltip("Segundos de titileo antes de enderezar el auto.")]
        [SerializeField, Min(0f)] float _flipBlinkTime = 2f;

        [Header("Estados")]
        [Tooltip("Multiplica la aceleración sobre pintura propia. MaxSpeed no cambia.")]
        [SerializeField, Min(1f)] float _paintBoostMultiplier = 1.5f;

        [Header("Drift")]
        [SerializeField, Min(0f)] float _driftMinSpeed = 11.11f;
        [SerializeField, Range(0f, 1f)] float _driftGripFactor = 0.3f;
        [SerializeField, Min(1f)] float _driftYawMultiplier = 1.5f;
        [Tooltip("Pérdida de velocidad durante el drift (m/s²).")]
        [SerializeField, Min(0f)] float _driftSpeedLoss = 3f;
        [Tooltip("Segundos de drift para carga nivel 1 y nivel 2.")]
        [SerializeField] Vector2 _driftChargeTiers = new Vector2(0.5f, 1.5f);
        [SerializeField, Min(1f)] float _driftTurboMultiplier = 2f;
        [Tooltip("Duración del turbo para nivel 1 y nivel 2 (s).")]
        [SerializeField] Vector2 _driftTurboDuration = new Vector2(0.5f, 1f);

        [Header("Controller")]
        [SerializeField, Min(0.1f)] float _respawnHoldTime = 1f;

        [Header("Motor")]
        [Tooltip("Revoluciones y caja automática del motor virtual. Solo afectan el sonido, no la física.")]
        [SerializeField] EngineSettings _engine = new EngineSettings();

        [Tooltip("Sonido del motor de este modelo. Vacío: el auto no suena.")]
        [SerializeField] EngineSoundProfileSO _engineSound;

        [Header("PID")]
        [Tooltip("PI: la I compensa rozamiento y pendientes. OutputLimit > 16,7 m/s² o no frena en 2 s.")]
        [SerializeField] PidSettings _speedPid = new PidSettings(8f, 2f, 0f, 4f, 40f);

        [Tooltip("PD: la D frena el bamboleo al soltar A o D. Unidades: rad/s².")]
        [SerializeField] PidSettings _yawRatePid = new PidSettings(14f, 0f, 0.4f, 1f, 45f);

        [Tooltip("P o PD sobre la velocidad lateral. La salida se limita además con MaxGrip.")]
        [SerializeField] PidSettings _gripPid = new PidSettings(12f, 0f, 0f, 1f, 200f);

        public float MaxSpeed => _maxSpeed;
        public float TimeToMaxSpeed => _timeToMaxSpeed;
        public AnimationCurve AccelerationBySpeed => _accelerationBySpeed;
        public float BrakeDeceleration => _brakeDeceleration;
        public float MaxReverseSpeed => _maxReverseSpeed;
        public float ReverseAcceleration => _reverseAcceleration;
        public float CoastDeceleration => _coastDeceleration;
        public float SetpointLead => _setpointLead;
        public float MaxYawRate => _maxYawRate;
        public AnimationCurve TurnBySpeed => _turnBySpeed;
        public float MaxGrip => _maxGrip;
        public float ExtraGravity => _extraGravity;
        public float SuspensionStiffness => _suspensionStiffness;
        public float SuspensionDamping => _suspensionDamping;
        public float SuspensionReboundRatio => _suspensionReboundRatio;
        public float SuspensionTravel => _suspensionTravel;
        public float FlippedAngle => _flippedAngle;
        public float FlipDetectTime => _flipDetectTime;
        public float FlipBlinkTime => _flipBlinkTime;
        public float PaintBoostMultiplier => _paintBoostMultiplier;
        public float DriftMinSpeed => _driftMinSpeed;
        public float DriftGripFactor => _driftGripFactor;
        public float DriftYawMultiplier => _driftYawMultiplier;
        public float DriftSpeedLoss => _driftSpeedLoss;
        public Vector2 DriftChargeTiers => _driftChargeTiers;
        public float DriftTurboMultiplier => _driftTurboMultiplier;
        public Vector2 DriftTurboDuration => _driftTurboDuration;
        public float RespawnHoldTime => _respawnHoldTime;
        public EngineSettings Engine => _engine;
        public EngineSoundProfileSO EngineSound => _engineSound;
        public PidSettings SpeedPid => _speedPid;
        public PidSettings YawRatePid => _yawRatePid;
        public PidSettings GripPid => _gripPid;

        /// <summary>Los generadores de editor asignan el motor de cada modelo con este método.</summary>
        public void SetEngine(EngineSettings engine, EngineSoundProfileSO sound)
        {
            _engine = engine;
            _engineSound = sound;
        }
    }
}
