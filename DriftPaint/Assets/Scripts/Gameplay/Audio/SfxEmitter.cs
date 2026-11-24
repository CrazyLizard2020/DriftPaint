using UnityEngine;

namespace DriftPaint.Audio
{
    /// <summary>
    /// Toca un efecto con variantes desde cualquier objeto: se agrega el componente, se cargan los clips y
    /// se elige el modo (al azar, sin repetir, mezclado o en orden) y la variación de tono y volumen. Se dispara
    /// llamando a <see cref="Play"/> (por código o desde un UnityEvent, como el OnClick de un botón) o solo,
    /// al activarse el objeto. Respeta el volumen de Efectos del menú de opciones.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SfxEmitter : MonoBehaviour
    {
        [SerializeField] SfxClip _sfx = new SfxClip();
        [Tooltip("Suena solo cada vez que el objeto se activa.")]
        [SerializeField] bool _playOnEnable;
        [Tooltip("Segundos mínimos entre dos reproducciones. Evita que se amontone si se pide muchas veces seguidas.")]
        [SerializeField, Min(0f)] float _minInterval;

        AudioSource _source;
        float _lastPlay = float.NegativeInfinity;

        public SfxClip Sfx => _sfx;

        void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
        }

        void OnEnable()
        {
            if (_playOnEnable) Play();
        }

        /// <summary>Toca la próxima variante del efecto.</summary>
        public void Play()
        {
            if (Time.unscaledTime - _lastPlay < _minInterval) return;
            if (_sfx.Play(_source, VolumeSettings.EffectsGain)) _lastPlay = Time.unscaledTime;
        }

        public void Stop()
        {
            if (_source != null) _source.Stop();
        }
    }
}
