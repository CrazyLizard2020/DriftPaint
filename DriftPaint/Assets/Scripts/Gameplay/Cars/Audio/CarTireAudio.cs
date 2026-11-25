using DriftPaint.Audio;
using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Chirrido de neumáticos al derrapar y soplido del turbo de drift. El chirrido sale del deslizamiento que
    /// ya calcula <see cref="CarVisuals"/> con la velocidad, así suena igual en el auto propio (estéreo) y en
    /// los rivales (3D) sin datos de red extra. El turbo solo lo conoce el auto propio.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class CarTireAudio : MonoBehaviour
    {
        [Tooltip("Loop del chirrido de las gomas.")]
        [SerializeField] AudioClip _squeal;
        [SerializeField, Range(0f, 1.5f)] float _squealVolume = 0.55f;
        [Tooltip("Ángulo de deslizamiento (grados) con el que el chirrido llega a volumen pleno.")]
        [SerializeField, Range(5f, 60f)] float _fullSlipAngle = 30f;
        [Tooltip("Velocidad (m/s) desde la que el chirrido suena pleno; más despacio se apaga.")]
        [SerializeField, Min(1f)] float _fullSpeed = 14f;

        [Tooltip("Soplido al soltar un drift cargado (turbo).")]
        [SerializeField] AudioClip _driftBoost;
        [SerializeField, Range(0f, 1.5f)] float _driftBoostVolume = 0.6f;

        const float Response = 0.08f;
        const int LocalPriority = 60;
        const int RemotePriority = 120;

        CarController _car;
        CarVisuals _visuals;
        AudioSource _squealSource;
        AudioSource _boostSource;
        CarSimulationMode _configuredMode;
        CarStatusEffects _subscribed;
        float _level;

        void Awake()
        {
            _car = GetComponent<CarController>();
            _visuals = GetComponent<CarVisuals>();
        }

        void Start()
        {
            var host = new GameObject("Neumaticos");
            host.transform.SetParent(transform, false);
            _squealSource = host.AddComponent<AudioSource>();
            _squealSource.clip = _squeal;
            _squealSource.loop = true;
            _squealSource.playOnAwake = false;
            _squealSource.rolloffMode = AudioRolloffMode.Logarithmic;
            _squealSource.minDistance = 6f;
            _squealSource.maxDistance = 100f;
            _squealSource.dopplerLevel = 0.3f;

            _boostSource = host.AddComponent<AudioSource>();
            _boostSource.playOnAwake = false;
            _boostSource.spatialBlend = 0f;
            _boostSource.priority = LocalPriority;

            ConfigureForMode();
        }

        void OnDisable() => Unsubscribe();

        void Update()
        {
            if (_squealSource == null) return;
            if (_car.Mode != _configuredMode) ConfigureForMode();

            float target = 0f;
            if (_visuals != null && _visuals.IsSliding)
            {
                float slip = Mathf.Clamp01(_visuals.SlipAngle / _fullSlipAngle);
                float speed = Mathf.Clamp01(_car.Speed / _fullSpeed);
                target = slip * speed;
            }
            _level = Mathf.Lerp(_level, target, 1f - Mathf.Exp(-Time.deltaTime / Response));

            // Sin chirrido la fuente se detiene, así no ocupa una voz.
            bool audible = _level > 0.01f && _squeal != null;
            _squealSource.volume = _level * _squealVolume * VolumeSettings.EffectsGain;
            _squealSource.pitch = Mathf.Lerp(0.9f, 1.1f, _level);
            if (audible && !_squealSource.isPlaying) _squealSource.Play();
            else if (!audible && _squealSource.isPlaying) _squealSource.Stop();
        }

        void ConfigureForMode()
        {
            _configuredMode = _car.Mode;
            bool local = _configuredMode == CarSimulationMode.Local;
            _squealSource.spatialBlend = local ? 0f : 1f;
            _squealSource.priority = local ? LocalPriority : RemotePriority;

            Unsubscribe();
            if (local && _car.StatusEffects != null)
            {
                _subscribed = _car.StatusEffects;
                _subscribed.DriftEnded += OnDriftEnded;
            }
        }

        void Unsubscribe()
        {
            if (_subscribed != null) _subscribed.DriftEnded -= OnDriftEnded;
            _subscribed = null;
        }

        /// <summary>Un drift que terminó con carga suelta el turbo.</summary>
        void OnDriftEnded(int level)
        {
            if (level <= 0 || _driftBoost == null) return;
            _boostSource.pitch = level >= 2 ? 1.12f : 1f;
            _boostSource.PlayOneShot(_driftBoost, _driftBoostVolume * VolumeSettings.EffectsGain);
        }

        /// <summary>El generador de editor asigna los sonidos con este método.</summary>
        public void SetClips(AudioClip squeal, AudioClip driftBoost)
        {
            _squeal = squeal;
            _driftBoost = driftBoost;
        }
    }
}
