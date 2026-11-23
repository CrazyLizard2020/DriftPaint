using DriftPaint.Cars;
using UnityEngine;

namespace DriftPaint.Sandbox
{
    /// <summary>
    /// Mide los tiempos de aceleración (0 a velocidad máxima) y frenado (velocidad máxima a 0)
    /// del auto observado, para validar el tuning contra los valores esperados.
    /// </summary>
    public class CarTelemetry : MonoBehaviour
    {
        [Tooltip("Margen en m/s para considerar alcanzada la velocidad máxima.")]
        [SerializeField, Min(0f)] float _maxSpeedTolerance = 0.35f;

        CarController _car;

        bool _measuringAcceleration;
        float _accelerationStart;
        bool _measuringBraking;
        float _brakingStart;

        public float LastAccelerationTime { get; private set; } = -1f;
        public float LastBrakingTime { get; private set; } = -1f;
        public float TopSpeedReached { get; private set; }

        public void SetTarget(CarController car)
        {
            _car = car;
            _measuringAcceleration = false;
            _measuringBraking = false;
            TopSpeedReached = 0f;
        }

        void FixedUpdate()
        {
            if (_car == null || _car.Stats == null) return;

            float speed = _car.ForwardSpeed;
            float maxSpeed = _car.Stats.MaxSpeed;
            FrameInput input = _car.MovementInput.CurrentInput;
            float now = Time.time;

            TopSpeedReached = Mathf.Max(TopSpeedReached, speed);

            // Aceleración: arranca con el auto casi quieto y W apretado; termina al llegar a la máxima.
            if (!_measuringAcceleration && speed < 0.3f && input.Throttle > 0.9f)
            {
                _measuringAcceleration = true;
                _accelerationStart = now;
            }
            else if (_measuringAcceleration)
            {
                if (input.Throttle < 0.9f) _measuringAcceleration = false;
                else if (speed >= maxSpeed - _maxSpeedTolerance)
                {
                    LastAccelerationTime = now - _accelerationStart;
                    _measuringAcceleration = false;
                }
            }

            // Frenado: arranca a velocidad máxima con S apretado; termina al llegar a 0.
            if (!_measuringBraking && speed >= maxSpeed - _maxSpeedTolerance && input.Throttle < -0.9f)
            {
                _measuringBraking = true;
                _brakingStart = now;
            }
            else if (_measuringBraking)
            {
                if (input.Throttle > -0.9f) _measuringBraking = false;
                else if (speed <= 0.05f)
                {
                    LastBrakingTime = now - _brakingStart;
                    _measuringBraking = false;
                }
            }
        }
    }
}
