using UnityEngine;

namespace DriftPaint.Audio
{
    /// <summary>
    /// Efectos de sonido de la partida. Los dispara cada cliente con los mismos eventos que muestran los
    /// efectos visuales, así que no viaja nada extra por red.
    /// </summary>
    [CreateAssetMenu(fileName = "SfxLibrary", menuName = "DriftPaint/Audio/Sfx Library")]
    public class SfxLibrarySO : ScriptableObject
    {
        [Header("Combate")]
        [Tooltip("Sale un proyectil de pintura.")]
        [SerializeField] SfxClip _shot = new SfxClip();
        [Tooltip("Un proyectil revienta contra un auto o el escenario.")]
        [SerializeField] SfxClip _projectileImpact = new SfxClip();
        [Tooltip("Un barril explota.")]
        [SerializeField] SfxClip _barrelExplosion = new SfxClip();
        [Tooltip("Un auto suelta un barril.")]
        [SerializeField] SfxClip _barrelDrop = new SfxClip();

        [Header("Cuenta regresiva")]
        [SerializeField] SfxClip _countdownTick = new SfxClip();
        [Tooltip("La largada, cuando la cuenta llega a cero.")]
        [SerializeField] SfxClip _countdownGo = new SfxClip();

        [Header("Aros de pintura")]
        [SerializeField] SfxClip _ringCounter = new SfxClip();
        [SerializeField] SfxClip _ringWin = new SfxClip();

        [Header("Distancia")]
        [Tooltip("Hasta esta distancia (m) los efectos se oyen a volumen pleno.")]
        [SerializeField, Min(0.5f)] float _minDistance = 8f;
        [Tooltip("Desde esta distancia (m) los efectos dejan de bajar de volumen.")]
        [SerializeField, Min(1f)] float _maxDistance = 140f;

        [Tooltip("Versión del generador con la que se hicieron los sonidos. Si el generador cambia, rehace los que cambiaron.")]
        [SerializeField, HideInInspector] int _generatorVersion;

        public int GeneratorVersion => _generatorVersion;
        public void MarkGenerated(int version) => _generatorVersion = version;

        public SfxClip Shot => _shot;
        public SfxClip ProjectileImpact => _projectileImpact;
        public SfxClip BarrelExplosion => _barrelExplosion;
        public SfxClip BarrelDrop => _barrelDrop;
        public SfxClip CountdownTick => _countdownTick;
        public SfxClip CountdownGo => _countdownGo;
        /// <summary>Un equipo suma una carga intermedia.</summary>
        public SfxClip RingCounter => _ringCounter;
        /// <summary>La última carga activa el aro.</summary>
        public SfxClip RingWin => _ringWin;
        public float MinDistance => _minDistance;
        public float MaxDistance => Mathf.Max(_maxDistance, _minDistance + 1f);

        /// <summary>Conecta las grabaciones del aro conservando las selecciones del inspector.</summary>
        public void FillRingClips(AudioClip counter, AudioClip win)
        {
            if (!_ringCounter.HasClips && counter != null) _ringCounter = new SfxClip(new[] { counter }, 0.8f, 0f);
            if (!_ringWin.HasClips && win != null) _ringWin = new SfxClip(new[] { win }, 1f, 0f);
        }

        /// <summary>El generador de editor arma la biblioteca con este método.</summary>
        public void Set(SfxClip shot, SfxClip projectileImpact, SfxClip barrelExplosion, SfxClip barrelDrop,
            SfxClip countdownTick, SfxClip countdownGo)
        {
            _shot = shot;
            _projectileImpact = projectileImpact;
            _barrelExplosion = barrelExplosion;
            _barrelDrop = barrelDrop;
            _countdownTick = countdownTick;
            _countdownGo = countdownGo;
        }
    }
}
