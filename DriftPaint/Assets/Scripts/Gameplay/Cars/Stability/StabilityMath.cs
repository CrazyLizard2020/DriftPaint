using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>Cuentas de la estabilidad, sin estado ni acceso a la escena.</summary>
    public static class StabilityMath
    {
        /// <summary>Seno del ángulo a un vuelco completo por debajo del cual se elige el eje largo (unos 20 grados).</summary>
        const float RollOverSine = 0.35f;

        /// <summary>
        /// Aceleración angular que lleva el techo del auto hacia target sin tocar el giro sobre su propio eje.
        /// Es un PD semi implícito: no oscila aunque la ganancia sea alta para el paso de física.
        /// </summary>
        public static Vector3 Tilt(Quaternion rotation, Vector3 target, Vector3 angularVelocity, float kp, float pitchKd, float rollKd, float maxAcceleration, float dt)
        {
            Vector3 up = rotation * Vector3.up;
            Vector3 axis = Vector3.Cross(up, target);
            float sin = axis.magnitude;
            float cos = Vector3.Dot(up, target);
            // Casi dado vuelta el eje es puro ruido: se rueda sobre el largo del auto, que no hace palanca con la trompa.
            Vector3 forward = rotation * Vector3.forward;
            if (cos < 0f && sin < RollOverSine) axis = Vector3.Dot(axis, forward) < 0f ? -forward : forward;
            else axis = sin > 0.001f ? axis / sin : forward;
            float angle = cos < 0f || sin > 0.001f ? Mathf.Atan2(sin, cos) : 0f;

            Quaternion inverse = Quaternion.Inverse(rotation);
            Vector3 error = inverse * (axis * angle);
            Vector3 rate = inverse * angularVelocity;

            var local = new Vector3(
                SemiImplicit(error.x, rate.x, kp, pitchKd, dt),
                0f,
                SemiImplicit(error.z, rate.z, kp, rollKd, dt));
            return rotation * Vector3.ClampMagnitude(local, maxAcceleration);
        }

        static float SemiImplicit(float error, float rate, float kp, float kd, float dt) =>
            (kp * error - (kd + kp * dt) * rate) / (1f + kd * dt + kp * dt * dt);

        /// <summary>
        /// Frena el cabeceo y el rolido que pasan su límite. Nunca los invierte: como mucho los lleva al límite.
        /// </summary>
        public static Vector3 AngularLimit(Vector3 localRate, float maxPitch, float maxRoll, float strength, float dt)
        {
            if (dt <= 0f) return Vector3.zero;
            float factor = Mathf.Clamp01(strength * dt) / dt;
            return new Vector3(Excess(localRate.x, maxPitch) * factor, 0f, Excess(localRate.z, maxRoll) * factor);
        }

        static float Excess(float rate, float limit) => rate > limit ? limit - rate : rate < -limit ? -limit - rate : 0f;

        /// <summary>Adherencia con el auto apoyado: base por pendiente, velocidad, input y ruedas en contacto.</summary>
        public static float Adhesion(StabilitySettings settings, float slope, float speed, float throttle, float contactFactor)
        {
            float low = Mathf.Abs(throttle) < 0.05f && speed < settings.AdhesionSpeedThreshold ? settings.LowSpeedAdhesionFactor : 1f;
            float value = settings.ExtraGravity * settings.AdhesionBySlope.Evaluate(slope) * settings.AdhesionBySpeed.Evaluate(speed) * low * Mathf.Clamp01(contactFactor);
            return Mathf.Clamp(value, 0f, settings.MaxAdhesionAcceleration);
        }

        /// <summary>
        /// Parte de la ventana de aterrizaje que sigue activa: 1 al tocar y baja a 0 en el último tramo, para
        /// que la suspensión no salte al terminar.
        /// </summary>
        public static float LandingWindow(float elapsed, float duration)
        {
            if (duration <= 0f || elapsed >= duration) return 0f;
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(duration * 0.5f, duration, elapsed));
        }

        /// <summary>Cambio de velocidad contra el rebote. Solo actúa sobre velocidad que se aleja del piso.</summary>
        public static float Absorption(float normalSpeed, float damping, float maxCorrection, float dt)
        {
            if (normalSpeed <= 0f || dt <= 0f) return 0f;
            return Mathf.Min(normalSpeed * PerStep(damping, dt), maxCorrection * dt);
        }

        /// <summary>Fracción por paso equivalente a una fracción dada cada 0,02 s.</summary>
        public static float PerStep(float damping, float dt) => 1f - Mathf.Pow(1f - Mathf.Clamp01(damping), dt / 0.02f);

        /// <summary>Empuje de una rueda (m/s²): resorte más amortiguación distinta al comprimir y al estirar.</summary>
        public static float Suspension(float compression, float verticalSpeed, float stiffness, float damping, float rebound, float max)
        {
            if (compression <= 0f) return 0f;
            float damp = damping * (verticalSpeed > 0f ? rebound : 1f);
            return Mathf.Clamp(stiffness * compression - damp * verticalSpeed, 0f, max);
        }

        /// <summary>Altura de reposo del centro de la rueda sobre el piso.</summary>
        public static float RestHeight(float wheelRadius, float totalGravity, int wheels, float stiffness) =>
            wheelRadius + totalGravity / (Mathf.Max(1, wheels) * Mathf.Max(1f, stiffness));

        /// <summary>Un tope de suspensión en contacto, listo para resolverse junto con los demás.</summary>
        public struct Stop
        {
            public int Wheel;
            public Vector3 Arm;
            public Vector3 Normal;
            /// <summary>Velocidad sobre la normal producida por un impulso unitario.</summary>
            public float InverseMass;
            public Vector3 AngularResponse;
            public float Impulse;
            public float MaxImpulse;
        }

        /// <summary>
        /// Resuelve los topes juntos, como contactos rígidos sin rebote: cada uno solo frena lo que empuja contra
        /// la superficie, teniendo en cuenta cómo el impulso de los demás ya mueve y gira el auto.
        /// </summary>
        public static void SolveStops(Stop[] stops, int count, Vector3 velocity, Vector3 angularVelocity, float mass, int iterations)
        {
            float inverseMass = 1f / Mathf.Max(0.001f, mass);
            for (int pass = 0; pass < iterations; pass++)
            {
                for (int i = 0; i < count; i++)
                {
                    float speed = Vector3.Dot(velocity + Vector3.Cross(angularVelocity, stops[i].Arm), stops[i].Normal);
                    float impulse = Mathf.Clamp(stops[i].Impulse - speed / stops[i].InverseMass, 0f, stops[i].MaxImpulse);
                    float change = impulse - stops[i].Impulse;
                    stops[i].Impulse = impulse;
                    velocity += stops[i].Normal * (change * inverseMass);
                    angularVelocity += stops[i].AngularResponse * change;
                }
            }
        }

        /// <summary>Respuesta angular del cuerpo a un impulso unitario en un brazo dado.</summary>
        public static Vector3 AngularResponse(Vector3 arm, Vector3 normal, Vector3 inertia, Quaternion inertiaRotation)
        {
            Vector3 local = Quaternion.Inverse(inertiaRotation) * Vector3.Cross(arm, normal);
            local.x = inertia.x > 0f ? local.x / inertia.x : 0f;
            local.y = inertia.y > 0f ? local.y / inertia.y : 0f;
            local.z = inertia.z > 0f ? local.z / inertia.z : 0f;
            return inertiaRotation * local;
        }
    }
}
