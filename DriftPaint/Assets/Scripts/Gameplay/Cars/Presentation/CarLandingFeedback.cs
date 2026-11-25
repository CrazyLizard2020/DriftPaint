using System;
using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Muestra el golpe de un aterrizaje: la carrocería se hunde y vuelve sobre sus ruedas. Sale del cambio de
    /// velocidad vertical, así que se ve igual en el auto propio y en los ajenos y no envía nada por red.
    /// Solo mueve el modelo: no toca la física ni las ruedas.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class CarLandingFeedback : MonoBehaviour
    {
        [Tooltip("Carrocería que se hunde. No incluye las ruedas. Si está vacío se busca el hijo Visual/Model.")]
        [SerializeField] Transform _body;

        [Tooltip("Velocidad de caída (m/s) a partir de la cual se nota el golpe.")]
        [SerializeField, Min(0f)] float _minFallSpeed = 3f;

        [Tooltip("Velocidad de caída (m/s) del golpe más fuerte.")]
        [SerializeField, Min(0.1f)] float _maxFallSpeed = 12f;

        [Tooltip("Hundimiento de la carrocería con el golpe más fuerte (m).")]
        [SerializeField, Range(0f, 0.15f)] float _maxDip = 0.06f;

        [Tooltip("Rapidez con la que vuelve a su lugar.")]
        [SerializeField, Min(1f)] float _frequency = 16f;

        [Tooltip("Tiempo mínimo entre dos golpes (s).")]
        [SerializeField, Min(0f)] float _debounce = 0.4f;

        CarController _car;
        LandingDetector _detector;
        Vector3 _restPosition;
        float _offset;
        float _speed;

        /// <summary>Fuerza del golpe de 0 a 1, una vez por aterrizaje.</summary>
        public event Action<float> Landed;

        void Awake()
        {
            _car = GetComponent<CarController>();
            if (_body == null) _body = transform.Find("Visual/Model");
            if (_body != null) _restPosition = _body.localPosition;
        }

        void OnEnable()
        {
            _car.Teleported += OnTeleported;
            ResetPose();
        }

        void OnDisable()
        {
            _car.Teleported -= OnTeleported;
            ResetPose();
        }

        void OnTeleported(Vector3 position, Quaternion rotation) => ResetPose();

        void ResetPose()
        {
            _detector = default;
            _offset = 0f;
            _speed = 0f;
            if (_body != null) _body.localPosition = _restPosition;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (_body == null || dt <= 0f) return;

            if (_detector.Update(_car.Velocity.y, dt, _minFallSpeed, _debounce, out float fall))
            {
                float strength = Mathf.InverseLerp(_minFallSpeed * 0.5f, _maxFallSpeed, fall);
                // Velocidad inicial con la que el resorte llega justo al hundimiento pedido.
                _speed -= _maxDip * strength * _frequency * Mathf.Exp(1f);
                Landed?.Invoke(strength);
            }

            if (_offset == 0f && _speed == 0f) return;

            // Resorte con amortiguación crítica, resuelto exacto: no depende de los cuadros por segundo.
            float decay = Mathf.Exp(-_frequency * dt);
            float drive = _speed + _frequency * _offset;
            float offset = (_offset + drive * dt) * decay;
            _speed = (_speed - _frequency * drive * dt) * decay;
            _offset = Mathf.Clamp(offset, -_maxDip, _maxDip);
            if (Mathf.Abs(_offset) < 0.0002f && Mathf.Abs(_speed) < 0.002f)
            {
                _offset = 0f;
                _speed = 0f;
            }
            _body.localPosition = _restPosition + Vector3.up * _offset;
        }
    }

    /// <summary>Reconoce un aterrizaje por cómo cambia la velocidad vertical.</summary>
    public struct LandingDetector
    {
        /// <summary>Cuánto tiempo (s) se recuerda la caída más rápida.</summary>
        const float Memory = 0.15f;

        float _fall;
        float _fallAge;
        float _sinceLanding;
        bool _started;

        /// <summary>Devuelve true una vez por aterrizaje; fall es la velocidad de caída que se frenó.</summary>
        public bool Update(float verticalSpeed, float dt, float minFall, float debounce, out float fall)
        {
            if (!_started)
            {
                _started = true;
                _sinceLanding = debounce;
            }
            fall = 0f;
            _sinceLanding += dt;
            _fallAge += dt;

            float falling = Mathf.Max(0f, -verticalSpeed);
            if (falling >= _fall || _fallAge > Memory)
            {
                _fall = falling;
                _fallAge = 0f;
                return false;
            }

            if (_fall < minFall || falling > _fall * 0.4f) return false;
            float stopped = _fall;
            _fall = falling;
            _fallAge = 0f;
            if (_sinceLanding < debounce) return false;

            _sinceLanding = 0f;
            fall = stopped;
            return true;
        }
    }
}
