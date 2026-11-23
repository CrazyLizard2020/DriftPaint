using UnityEngine;

namespace DriftPaint.Combat
{
    /// <summary>
    /// Fórmulas de disparos y barriles. Son puras y reciben los valores de ProjectileSettingsSO y
    /// BarrelSettingsSO, así el tirador, el Master y el resto de los clientes calculan lo mismo con los
    /// mismos datos.
    /// </summary>
    public static class CombatRules
    {
        /// <summary>Margen alrededor de la zona de explosión: alcanza al auto aunque solo la toque con la carrocería.</summary>
        public const float CarHalfWidth = 1f;

        /// <summary>Lado en celdas del cuadrado de una explosión con el radio dado (la mitad del lado), al menos 1.</summary>
        public static int BlastSize(float radius) => Mathf.Max(1, Mathf.RoundToInt(radius * 2f));

        /// <summary>Velocidad inicial del proyectil: hacia adelante y hacia arriba, sumada a la del auto en el plano.</summary>
        public static Vector3 ProjectileVelocity(Vector3 forward, Vector3 carVelocity, float forwardSpeed, float liftSpeed)
        {
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            carVelocity.y = 0f;
            return carVelocity + forward * forwardSpeed + Vector3.up * liftSpeed;
        }

        /// <summary>Posición del proyectil t segundos después del disparo (tiro parabólico).</summary>
        public static Vector3 ProjectilePosition(Vector3 origin, Vector3 velocity, float gravity, float t)
        {
            return origin + velocity * t + Vector3.down * (0.5f * gravity * t * t);
        }

        /// <summary>El Master acepta un disparo si pasó el cooldown (menos el margen) desde el anterior del mismo jugador.</summary>
        public static bool IsFireAllowed(double lastFireTime, double fireTime, float cooldown, float tolerance)
        {
            return double.IsNegativeInfinity(lastFireTime) || fireTime - lastFireTime >= cooldown - tolerance;
        }

        /// <summary>True si el punto (en el plano) está dentro del cuadrado de lado size centrado en center, más el margen del auto.</summary>
        public static bool IsInsideSquare(Vector3 center, int size, Vector3 point)
        {
            float half = size * 0.5f + CarHalfWidth;
            return Mathf.Abs(point.x - center.x) <= half && Mathf.Abs(point.z - center.z) <= half;
        }

        /// <summary>Distancia recorrida por un barril t segundos después de soltarlo: sale a dropSpeed y frena hasta quedar quieto en slideTime.</summary>
        public static float BarrelSlideDistance(float t, float dropSpeed, float slideTime)
        {
            if (slideTime <= 0f) return 0f;
            t = Mathf.Clamp(t, 0f, slideTime);
            // Desaceleración constante: v(t) = v0 (1 - t / T).
            return dropSpeed * (t - t * t / (2f * slideTime));
        }

        /// <summary>Barriles después de recolectar uno, sin pasar del máximo.</summary>
        public static int AddBarrel(int current, int max) => Mathf.Min(max, Mathf.Max(0, current) + 1);
    }
}
