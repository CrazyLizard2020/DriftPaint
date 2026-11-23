using UnityEngine;

namespace DriftPaint.Combat
{
    /// <summary>
    /// Valores y fórmulas de disparos y barriles. Las fórmulas son puras, así el tirador, el Master y el
    /// resto de los clientes calculan el mismo vuelo del proyectil con los mismos datos.
    /// </summary>
    public static class CombatRules
    {
        // Disparo
        public const float FireCooldown = 5f;
        /// <summary>Margen que acepta el Master al validar el cooldown, por diferencias de reloj.</summary>
        public const float FireCooldownTolerance = 0.25f;
        /// <summary>Lado en metros del cuadrado que pinta un proyectil al impactar.</summary>
        public const int ProjectileChunkSize = 5;
        public const float ProjectileForwardSpeed = 32f;
        public const float ProjectileLiftSpeed = 7f;
        public const float ProjectileGravity = 22f;
        /// <summary>Segundos máximos de vuelo antes de descartar el proyectil.</summary>
        public const float ProjectileLifetime = 3f;
        /// <summary>Distancia al centro del auto para contar un impacto directo.</summary>
        public const float DirectHitRadius = 1.6f;

        // Barril
        public const int StartBarrels = 3;
        public const int MaxBarrels = 4;
        public const float BarrelLifetime = 15f;
        /// <summary>Velocidad con la que sale el barril por el baúl (5 km/h).</summary>
        public const float BarrelDropSpeed = 5f / 3.6f;
        /// <summary>Segundos que tarda el barril en frenar después de soltarlo.</summary>
        public const float BarrelSlideTime = 0.6f;
        /// <summary>Lado en metros del cuadrado que pinta y alcanza la explosión de un barril.</summary>
        public const int BarrelBlastSize = 8;
        /// <summary>Distancia horizontal al barril para considerar que un auto lo pisó.</summary>
        public const float BarrelTriggerRadius = 1.6f;
        /// <summary>Segundos después de soltarlo en los que el barril todavía no explota, para que nadie lo pise al salir.</summary>
        public const float BarrelArmDelay = 0.4f;

        // Pickups
        public const float PickupRadius = 2.2f;
        public const float PickupRespawnSeconds = 20f;

        /// <summary>Margen alrededor de la zona de explosión: alcanza al auto aunque solo la toque con la carrocería.</summary>
        public const float CarHalfWidth = 1f;

        /// <summary>Velocidad inicial del proyectil: hacia adelante y hacia arriba, sumada a la del auto en el plano.</summary>
        public static Vector3 ProjectileVelocity(Vector3 forward, Vector3 carVelocity)
        {
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            carVelocity.y = 0f;
            return carVelocity + forward * ProjectileForwardSpeed + Vector3.up * ProjectileLiftSpeed;
        }

        /// <summary>Posición del proyectil t segundos después del disparo (tiro parabólico).</summary>
        public static Vector3 ProjectilePosition(Vector3 origin, Vector3 velocity, float t)
        {
            return origin + velocity * t + Vector3.down * (0.5f * ProjectileGravity * t * t);
        }

        /// <summary>El Master acepta un disparo si pasó el cooldown desde el anterior del mismo jugador.</summary>
        public static bool IsFireAllowed(double lastFireTime, double fireTime)
        {
            return double.IsNegativeInfinity(lastFireTime) || fireTime - lastFireTime >= FireCooldown - FireCooldownTolerance;
        }

        /// <summary>True si el punto (en el plano) está dentro del cuadrado de lado size centrado en center, más el margen del auto.</summary>
        public static bool IsInsideSquare(Vector3 center, int size, Vector3 point)
        {
            float half = size * 0.5f + CarHalfWidth;
            return Mathf.Abs(point.x - center.x) <= half && Mathf.Abs(point.z - center.z) <= half;
        }

        /// <summary>Distancia recorrida por un barril t segundos después de soltarlo: sale a 5 km/h y frena hasta quedar quieto.</summary>
        public static float BarrelSlideDistance(float t)
        {
            t = Mathf.Clamp(t, 0f, BarrelSlideTime);
            // Desaceleración constante: v(t) = v0 (1 - t / T).
            return BarrelDropSpeed * (t - t * t / (2f * BarrelSlideTime));
        }

        /// <summary>Barriles después de recolectar uno, sin pasar del máximo.</summary>
        public static int AddBarrel(int current) => Mathf.Min(MaxBarrels, Mathf.Max(0, current) + 1);
    }
}
