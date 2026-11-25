using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Mueve las ruedas a partir de ICarState: giro de dirección en las delanteras y rotación
    /// por velocidad en las cuatro. Funciona igual en autos locales y remotos, porque solo lee
    /// velocidad y giro. También calcula el ángulo de derrape para efectos visuales.
    /// </summary>
    /// <remarks>
    /// Cada rueda es un pivote ubicado en el centro de la rueda y alineado con el auto
    /// (X = eje de la rueda). El mesh va como hijo del pivote con la rotación que necesite.
    /// </remarks>
    public class CarVisuals : MonoBehaviour
    {
        [SerializeField] Transform _wheelFrontLeft;
        [SerializeField] Transform _wheelFrontRight;
        [SerializeField] Transform _wheelRearLeft;
        [SerializeField] Transform _wheelRearRight;

        [Tooltip("Radio de la rueda en metros. Define cuánto gira por metro recorrido.")]
        [SerializeField, Min(0.05f)] float _wheelRadius = 0.25f;

        [Tooltip("Ángulo máximo de dirección de las ruedas delanteras.")]
        [SerializeField, Range(0f, 45f)] float _maxSteerAngle = 28f;

        [Tooltip("Velocidad con la que las ruedas delanteras siguen el input de giro.")]
        [SerializeField, Min(0.1f)] float _steerResponse = 10f;

        [Tooltip("Ángulo entre velocidad y orientación a partir del cual se considera derrape.")]
        [SerializeField, Range(0f, 90f)] float _slipAngleThreshold = 12f;

        [Tooltip("Velocidad mínima (m/s) para evaluar el derrape.")]
        [SerializeField, Min(0f)] float _slipMinSpeed = 8f;

        ICarState _state;
        float _spinAngle;
        float _steerAngle;

        /// <summary>Ángulo en grados entre la velocidad horizontal y el frente del auto.</summary>
        public float SlipAngle { get; private set; }
        public bool IsSliding { get; private set; }
        public float WheelRadius => _wheelRadius;

        void Awake()
        {
            _state = GetComponentInParent<ICarState>();
            if (_wheelFrontLeft == null) _wheelFrontLeft = FindChild("Wheel_FL");
            if (_wheelFrontRight == null) _wheelFrontRight = FindChild("Wheel_FR");
            if (_wheelRearLeft == null) _wheelRearLeft = FindChild("Wheel_RL");
            if (_wheelRearRight == null) _wheelRearRight = FindChild("Wheel_RR");
        }

        Transform FindChild(string childName)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                if (child.name == childName) return child;
            return null;
        }

        void LateUpdate()
        {
            if (_state == null) return;
            float dt = Time.deltaTime;

            Vector3 velocity = _state.Velocity;
            Vector3 forward = _state.Rotation * Vector3.forward;
            float forwardSpeed = Vector3.Dot(velocity, forward);

            _spinAngle = Mathf.Repeat(_spinAngle + forwardSpeed / _wheelRadius * Mathf.Rad2Deg * dt, 360f);
            float targetSteer = _state.SteerInput * _maxSteerAngle;
            _steerAngle = Mathf.Lerp(_steerAngle, targetSteer, 1f - Mathf.Exp(-_steerResponse * dt));

            Quaternion spin = Quaternion.Euler(_spinAngle, 0f, 0f);
            Quaternion steer = Quaternion.Euler(0f, _steerAngle, 0f);

            SetWheel(_wheelFrontLeft, steer * spin);
            SetWheel(_wheelFrontRight, steer * spin);
            SetWheel(_wheelRearLeft, spin);
            SetWheel(_wheelRearRight, spin);

            UpdateSlip(velocity, forward);
        }

        static void SetWheel(Transform wheel, Quaternion rotation)
        {
            if (wheel != null) wheel.localRotation = rotation;
        }

        void UpdateSlip(Vector3 velocity, Vector3 forward)
        {
            Vector3 horizontal = Vector3.ProjectOnPlane(velocity, Vector3.up);
            if (horizontal.magnitude < _slipMinSpeed)
            {
                SlipAngle = 0f;
                IsSliding = false;
                return;
            }

            Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            SlipAngle = Vector3.Angle(flatForward, horizontal);
            // En marcha atrás la velocidad apunta hacia atrás: se mide contra el frente invertido.
            if (SlipAngle > 90f) SlipAngle = 180f - SlipAngle;
            IsSliding = SlipAngle >= _slipAngleThreshold;
        }
    }
}
