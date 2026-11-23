using UnityEngine;

namespace DriftPaint.Combat
{
    /// <summary>
    /// Valores de los barriles: inventario, cómo salen por el baúl, cuándo explotan, los efectos de la
    /// explosión y los pickups. Las decisiones las toma el Master; la animación del barril la hace cada
    /// cliente, así que conviene que los valores sean iguales en todas las builds.
    /// </summary>
    [CreateAssetMenu(fileName = "BarrelSettings", menuName = "DriftPaint/Combat/Barrel Settings")]
    public class BarrelSettingsSO : ScriptableObject
    {
        [Header("Inventario")]
        [Tooltip("Barriles con los que arranca cada jugador la partida.")]
        [SerializeField, Min(0)] int _startBarrels = 3;
        [Tooltip("Barriles máximos en el baúl.")]
        [SerializeField, Min(1)] int _maxBarrels = 4;

        [Header("Barril soltado")]
        [Tooltip("Segundos que dura un barril en la pista si nadie lo pisa.")]
        [SerializeField, Min(1f)] float _lifetime = 15f;
        [Tooltip("Velocidad con la que sale por el baúl (km/h).")]
        [SerializeField, Min(0f)] float _dropSpeedKmh = 5f;
        [Tooltip("Segundos que tarda en frenar después de soltarlo.")]
        [SerializeField, Min(0.01f)] float _slideTime = 0.6f;
        [Tooltip("Distancia horizontal al barril para considerar que un rival lo pisó (m).")]
        [SerializeField, Min(0.1f)] float _triggerRadius = 1.6f;
        [Tooltip("Segundos después de soltarlo en los que todavía no explota.")]
        [SerializeField, Min(0f)] float _armDelay = 0.4f;

        [Header("Explosión")]
        [SerializeField] ImpactEffectSettings _impact = new ImpactEffectSettings(4f);

        [Header("Pickups")]
        [Tooltip("Distancia horizontal al pickup para recogerlo (m).")]
        [SerializeField, Min(0.1f)] float _pickupRadius = 2.2f;
        [Tooltip("Segundos hasta que el pickup vuelve a aparecer.")]
        [SerializeField, Min(0f)] float _pickupRespawnSeconds = 20f;

        public int StartBarrels => Mathf.Min(_startBarrels, _maxBarrels);
        public int MaxBarrels => _maxBarrels;
        public float Lifetime => _lifetime;
        public float DropSpeed => _dropSpeedKmh / 3.6f;
        public float SlideTime => _slideTime;
        public float TriggerRadius => _triggerRadius;
        public float ArmDelay => _armDelay;
        public ImpactEffectSettings Impact => _impact;
        public float PickupRadius => _pickupRadius;
        public float PickupRespawnSeconds => _pickupRespawnSeconds;

        public float SlideDistance(float t) => CombatRules.BarrelSlideDistance(t, DropSpeed, _slideTime);

        public int AddBarrel(int current) => CombatRules.AddBarrel(current, _maxBarrels);
    }
}
