using DriftPaint.Combat;
using DriftPaint.Match;
using DriftPaint.Net;
using UnityEngine;

namespace DriftPaint.Audio
{
    /// <summary>
    /// Efectos de sonido de la partida: disparos, impactos, barriles y cuenta regresiva. Escucha los mismos
    /// eventos que muestran los efectos visuales en cada cliente y toca el sonido en el lugar del hecho con
    /// un grupo fijo de fuentes, respetando el volumen de Efectos.
    /// </summary>
    public class MatchSfx : MonoBehaviour
    {
        [SerializeField] SfxLibrarySO _library;
        [SerializeField] MatchController _match;
        [SerializeField] WeaponSystem _weapons;
        [SerializeField] BarrelSystem _barrels;

        [Tooltip("Efectos que pueden sonar a la vez. Si se llenan, el más viejo deja lugar al nuevo.")]
        [SerializeField, Range(2, 32)] int _voices = 12;

        /// <summary>Prioridad de voz: por debajo del motor propio, por encima de los motores rivales.</summary>
        const int Priority = 80;

        AudioSource[] _sources;
        float[] _startTimes;
        int _lastSecond = -1;
        bool _counting;

        void Awake()
        {
            _sources = new AudioSource[_voices];
            _startTimes = new float[_voices];
            for (int i = 0; i < _voices; i++)
            {
                var host = new GameObject($"Efecto {i:00}");
                host.transform.SetParent(transform, false);
                AudioSource source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.priority = Priority;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.dopplerLevel = 0f;
                _sources[i] = source;
            }
        }

        void OnEnable()
        {
            if (_weapons != null)
            {
                _weapons.ShotFired += OnShot;
                _weapons.ImpactShown += OnImpact;
            }
            if (_barrels != null) _barrels.BarrelDropped += OnBarrelDropped;
        }

        void OnDisable()
        {
            if (_weapons != null)
            {
                _weapons.ShotFired -= OnShot;
                _weapons.ImpactShown -= OnImpact;
            }
            if (_barrels != null) _barrels.BarrelDropped -= OnBarrelDropped;
        }

        void Update()
        {
            if (_match == null || _library == null) return;

            // Un pitido por segundo en los últimos tres y uno más agudo en la largada, también en el desempate.
            bool counting = _match.Phase == MatchPhase.Countdown || _match.TiebreakPending;
            if (counting)
            {
                int second = Mathf.CeilToInt(_match.CountdownRemaining);
                if (second != _lastSecond && second >= 1 && second <= 3) Play(_library.CountdownTick, null);
                _lastSecond = second;
                _counting = true;
            }
            else if (_counting)
            {
                if (_match.AllowsDriving) Play(_library.CountdownGo, null);
                _counting = false;
                _lastSecond = -1;
            }
        }

        void OnShot(Vector3 position) => Play(_library != null ? _library.Shot : null, position);

        void OnImpact(Vector3 position, ImpactKind kind)
        {
            if (_library == null) return;
            Play(kind == ImpactKind.Barrel ? _library.BarrelExplosion : _library.ProjectileImpact, position);
        }

        void OnBarrelDropped(Vector3 position) => Play(_library != null ? _library.BarrelDrop : null, position);

        /// <summary>Toca un efecto en una posición del mundo, o en estéreo sin posición si es null.</summary>
        void Play(SfxClip sfx, Vector3? position)
        {
            if (sfx == null || !sfx.HasClips || _sources == null) return;

            int index = FreeSource();
            AudioSource source = _sources[index];
            source.spatialBlend = position.HasValue ? 1f : 0f;
            source.minDistance = _library.MinDistance;
            source.maxDistance = _library.MaxDistance;
            if (position.HasValue) source.transform.position = position.Value;
            else source.transform.localPosition = Vector3.zero;
            // La variante, el tono y el volumen los elige el efecto según cómo está configurado.
            sfx.Play(source, VolumeSettings.EffectsGain);
            _startTimes[index] = Time.unscaledTime;
        }

        int FreeSource()
        {
            int oldest = 0;
            for (int i = 0; i < _sources.Length; i++)
            {
                if (!_sources[i].isPlaying) return i;
                if (_startTimes[i] < _startTimes[oldest]) oldest = i;
            }
            return oldest;
        }
    }
}
