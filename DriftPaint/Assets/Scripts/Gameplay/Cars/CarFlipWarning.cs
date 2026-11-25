using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Hace titilar el auto mientras se recupera de un vuelco o está por reubicarse. Solo presenta: no decide
    /// nada ni mueve el auto.
    /// </summary>
    /// <remarks>
    /// El auto propio muestra lo que decide CarStabilityController. En un auto ajeno se calcula con su rotación,
    /// que es lo único que llega por red.
    /// </remarks>
    [RequireComponent(typeof(CarController))]
    public class CarFlipWarning : MonoBehaviour
    {
        CarController _car;
        CarBlinkEffect _blink;
        float _flippedTime;

        /// <summary>El auto se está recuperando o por reubicarse.</summary>
        public bool IsWarning { get; private set; }

        /// <summary>El auto está dado vuelta.</summary>
        public bool IsFlipped { get; private set; }

        /// <summary>Segundos que faltan para reubicarlo. 0 si se está enderezando solo o no se sabe.</summary>
        public float TimeLeft { get; private set; }

        void Awake()
        {
            _car = GetComponent<CarController>();
            _blink = GetComponent<CarBlinkEffect>();
        }

        void OnEnable() => _car.Teleported += OnTeleported;

        void OnDisable()
        {
            _car.Teleported -= OnTeleported;
            Show(false, false, 0f);
        }

        void OnTeleported(Vector3 position, Quaternion rotation)
        {
            _flippedTime = 0f;
            Show(false, false, 0f);
        }

        void Update()
        {
            if (_car.Stats == null) return;
            StabilitySettings settings = _car.Stats.Stability;
            bool flipped = Vector3.Angle(_car.Rotation * Vector3.up, Vector3.up) > settings.FlippedAngle;

            if (_car.Mode == CarSimulationMode.Local)
            {
                ref readonly StabilityFrame frame = ref _car.Stability.Frame;
                Show(flipped, frame.RecoveryWarning, frame.WarningTimeLeft);
                return;
            }

            _flippedTime = flipped ? _flippedTime + Time.deltaTime : 0f;
            Show(flipped, _flippedTime >= settings.FlipDetectTime, 0f);
        }

        void Show(bool flipped, bool warning, float timeLeft)
        {
            IsFlipped = flipped;
            TimeLeft = timeLeft;
            IsWarning = warning;
            if (_blink != null) _blink.SetBlinking(warning);
        }
    }
}
