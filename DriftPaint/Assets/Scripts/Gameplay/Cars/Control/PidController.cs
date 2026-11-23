using System;
using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Configuración serializable de un PID.
    /// En la locomoción del auto las unidades de OutputLimit e IntegralLimit son m/s² o rad/s².
    /// </summary>
    [Serializable]
    public class PidSettings
    {
        [Tooltip("Corrección proporcional al error actual. Más alto responde más rápido y agresivo.")]
        [SerializeField] float _proportional = 1f;

        [Tooltip("Corrección por error sostenido. Compensa rozamiento y pendientes; en exceso sobrecorrige.")]
        [SerializeField] float _integral;

        [Tooltip("Amortigua según qué tan rápido cambia la medición. Reduce el bamboleo.")]
        [SerializeField] float _derivative = 0.15f;

        [Tooltip("Límite de la acumulación integral (anti-windup).")]
        [SerializeField, Min(0f)] float _integralLimit = 1f;

        [Tooltip("Límite máximo de salida del PID.")]
        [SerializeField, Min(0f)] float _outputLimit = 1f;

        public PidSettings() { }

        public PidSettings(float proportional, float integral, float derivative, float integralLimit, float outputLimit)
        {
            _proportional = proportional;
            _integral = integral;
            _derivative = derivative;
            _integralLimit = integralLimit;
            _outputLimit = outputLimit;
        }

        public float Proportional => _proportional;
        public float Integral => _integral;
        public float Derivative => _derivative;
        public float IntegralLimit => _integralLimit;
        public float OutputLimit => _outputLimit;
    }

    /// <summary>
    /// PID discreto con integral limitada, salida limitada y Reset.
    /// u(t) = Kp·e + Ki·∫e + Kd·de/dt
    /// </summary>
    [Serializable]
    public class PidController
    {
        float _proportional = 1f;
        float _integral;
        float _derivative = 0.15f;
        float _integralLimit = 1f;
        float _outputLimit = 1f;

        float _integralState;
        float _previousError;
        bool _hasPreviousError;
        float _previousMeasurement;
        bool _hasPreviousMeasurement;

        public PidController() { }

        public PidController(PidSettings settings) => ApplySettings(settings);

        public void ApplySettings(PidSettings settings)
        {
            if (settings == null) return;

            _proportional = settings.Proportional;
            _integral = settings.Integral;
            _derivative = settings.Derivative;
            _integralLimit = settings.IntegralLimit;
            _outputLimit = settings.OutputLimit;
            Reset();
        }

        /// <summary>
        /// Forma clásica: deriva sobre el error. Útil cuando el setpoint cambia de forma continua.
        /// </summary>
        public float Evaluate(float error, float deltaTime)
        {
            if (deltaTime <= 0f)
                return Mathf.Clamp(error * _proportional, -_outputLimit, _outputLimit);

            _integralState = Mathf.Clamp(_integralState + error * deltaTime, -_integralLimit, _integralLimit);

            float derivative = _hasPreviousError ? (error - _previousError) / deltaTime : 0f;
            _previousError = error;
            _hasPreviousError = true;

            float output = error * _proportional + _integralState * _integral + derivative * _derivative;
            return Mathf.Clamp(output, -_outputLimit, _outputLimit);
        }

        /// <summary>
        /// Derivada sobre la medición: misma amortiguación, sin "derivative kick"
        /// cuando el setpoint salta de golpe (por ejemplo al pasar de A a D).
        /// </summary>
        public float Evaluate(float setpoint, float measurement, float deltaTime)
        {
            float error = setpoint - measurement;

            if (deltaTime <= 0f)
                return Mathf.Clamp(error * _proportional, -_outputLimit, _outputLimit);

            _integralState = Mathf.Clamp(_integralState + error * deltaTime, -_integralLimit, _integralLimit);

            float derivative = _hasPreviousMeasurement
                ? -(measurement - _previousMeasurement) / deltaTime
                : 0f;

            _previousMeasurement = measurement;
            _hasPreviousMeasurement = true;

            float output = error * _proportional + _integralState * _integral + derivative * _derivative;
            return Mathf.Clamp(output, -_outputLimit, _outputLimit);
        }

        public void Reset()
        {
            _integralState = 0f;
            _previousError = 0f;
            _hasPreviousError = false;
            _previousMeasurement = 0f;
            _hasPreviousMeasurement = false;
        }
    }
}
