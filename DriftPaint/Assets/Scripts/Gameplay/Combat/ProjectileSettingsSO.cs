using UnityEngine;

namespace DriftPaint.Combat
{
    /// <summary>
    /// Valores del disparo de pintura: cooldown, vuelo del proyectil y efectos del impacto. Todos los
    /// clientes simulan el vuelo con estos valores, así que tienen que ser iguales en todas las builds.
    /// </summary>
    [CreateAssetMenu(fileName = "ProjectileSettings", menuName = "DriftPaint/Combat/Projectile Settings")]
    public class ProjectileSettingsSO : ScriptableObject
    {
        [Header("Cooldown")]
        [Tooltip("Segundos entre dos disparos del mismo jugador.")]
        [SerializeField, Min(0.1f)] float _cooldown = 5f;
        [Tooltip("Margen que acepta el Master al validar el cooldown, por diferencias de reloj (s).")]
        [SerializeField, Min(0f)] float _cooldownTolerance = 0.25f;

        [Header("Vuelo")]
        [Tooltip("Velocidad hacia adelante, sumada a la del auto (m/s).")]
        [SerializeField, Min(0f)] float _forwardSpeed = 32f;
        [Tooltip("Velocidad inicial hacia arriba (m/s).")]
        [SerializeField, Min(0f)] float _liftSpeed = 7f;
        [Tooltip("Gravedad del proyectil (m/s²).")]
        [SerializeField, Min(0.1f)] float _gravity = 22f;
        [Tooltip("Segundos máximos de vuelo antes de descartar el proyectil.")]
        [SerializeField, Min(0.1f)] float _lifetime = 3f;
        [Tooltip("Distancia al centro de un auto rival para contar impacto directo (m).")]
        [SerializeField, Min(0.1f)] float _directHitRadius = 1.6f;

        [Header("Impacto")]
        [SerializeField] ImpactEffectSettings _impact = new ImpactEffectSettings(2.5f);

        public float Cooldown => _cooldown;
        public float CooldownTolerance => _cooldownTolerance;
        public float ForwardSpeed => _forwardSpeed;
        public float LiftSpeed => _liftSpeed;
        public float Gravity => _gravity;
        public float Lifetime => _lifetime;
        public float DirectHitRadius => _directHitRadius;
        public ImpactEffectSettings Impact => _impact;

        public Vector3 Velocity(Vector3 forward, Vector3 carVelocity) =>
            CombatRules.ProjectileVelocity(forward, carVelocity, _forwardSpeed, _liftSpeed);

        public Vector3 Position(Vector3 origin, Vector3 velocity, float t) =>
            CombatRules.ProjectilePosition(origin, velocity, _gravity, t);

        public bool IsFireAllowed(double lastFireTime, double fireTime) =>
            CombatRules.IsFireAllowed(lastFireTime, fireTime, _cooldown, _cooldownTolerance);
    }
}
