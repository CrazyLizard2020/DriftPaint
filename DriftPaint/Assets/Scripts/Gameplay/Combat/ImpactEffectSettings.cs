using System;
using UnityEngine;

namespace DriftPaint.Combat
{
    /// <summary>
    /// Lo que hace una explosión al impactar: el tamaño de la zona que pinta, cuánto tiempo esa pintura
    /// le gana a los rastros de los autos, el stun del auto alcanzado de lleno y el slow de los rivales que
    /// quedan dentro de la zona. Lo usan los disparos y los barriles, cada uno con sus propios valores.
    /// </summary>
    [Serializable]
    public class ImpactEffectSettings
    {
        [Tooltip("Mitad del lado del cuadrado que pinta la explosión (m). 2.5 pinta 5 x 5 m.")]
        [SerializeField, Min(0.5f)] float _explosionRadius = 2.5f;
        [Tooltip("Segundos en los que la zona recién pintada no la tapan los rastros de los autos. Otra explosión sí la pisa.")]
        [SerializeField, Min(0f)] float _paintPrioritySeconds = 1.5f;

        [Header("Stun (auto alcanzado de lleno)")]
        [Tooltip("Segundos sin acelerar ni pintar. 0 desactiva el stun.")]
        [SerializeField, Min(0f)] float _stunDuration = 2f;
        [Tooltip("La velocidad máxima baja a este factor de la velocidad que tenía al recibir el impacto.")]
        [SerializeField, Range(0f, 1f)] float _stunSpeedFactor = 0.2f;

        [Header("Slow (rivales dentro de la zona)")]
        [Tooltip("Segundos de slow. Sigue pintando. 0 desactiva el slow.")]
        [SerializeField, Min(0f)] float _slowDuration = 2f;
        [Tooltip("Velocidad que pierde por segundo mientras dura el slow (m/s²).")]
        [SerializeField, Min(0f)] float _slowDeceleration = 8f;

        public ImpactEffectSettings() { }

        public ImpactEffectSettings(float explosionRadius)
        {
            _explosionRadius = explosionRadius;
        }

        public float ExplosionRadius => _explosionRadius;
        /// <summary>Lado en celdas (1 m) del cuadrado que pinta y alcanza la explosión.</summary>
        public int BlastSize => CombatRules.BlastSize(_explosionRadius);
        public float PaintPrioritySeconds => _paintPrioritySeconds;
        public float StunDuration => _stunDuration;
        public float StunSpeedFactor => _stunSpeedFactor;
        public float SlowDuration => _slowDuration;
        public float SlowDeceleration => _slowDeceleration;
    }
}
