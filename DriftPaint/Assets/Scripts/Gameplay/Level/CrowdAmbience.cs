using DriftPaint.Audio;
using UnityEngine;

namespace DriftPaint.Level
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class CrowdAmbience : MonoBehaviour
    {
        public CrowdStand stand;
        [Min(0)] public int clipIndex;
        [Range(0, 1)] public float sectorVolume = 1;
        [Range(0, .99f)] public float startFraction;
        [Tooltip("Recorrido horizontal de la fuente, como fracción del ancho de la tribuna.")]
        [Range(0, 1)] public float sourceSpan = .90f;
        AudioSource _source;
        Transform _listener;
        Vector3 _anchor;
        bool _started;

        void Start()
        {
            _source = GetComponent<AudioSource>();
            _anchor = transform.localPosition;
            _started = true;
            RefreshSettings();
        }

        void OnEnable()
        {
            VolumeSettings.Changed += ApplyVolume;
            if (_started) RefreshSettings();
        }

        public void RefreshSettings()
        {
            if (!_source || !stand || !stand.profile) return;
            var p = stand.profile;
            var clip = p.cheers == null || p.cheers.Length == 0 ? null :
                p.cheers[Mathf.Clamp(clipIndex, 0, p.cheers.Length - 1)];
            if (!clip) { _source.Stop(); return; }
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 1;
            _source.dopplerLevel = 0;
            _source.priority = 185;
            _source.minDistance = Mathf.Max(1, p.nearDistance);
            _source.maxDistance = Mathf.Max(_source.minDistance + 1, p.farDistance);
            var keys = p.rolloff.keys;
            float near = _source.minDistance / _source.maxDistance;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].time = Mathf.Lerp(near, 1, keys[i].time);
                keys[i].inTangent /= 1 - near;
                keys[i].outTangent /= 1 - near;
            }
            _source.rolloffMode = AudioRolloffMode.Custom;
            _source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(keys));
            var listener = FindFirstObjectByType<AudioListener>();
            _listener = listener ? listener.transform : null;
            PositionSource();
            ApplyVolume();
            if (_source.clip == clip && _source.isPlaying) return;
            _source.clip = clip;
            _source.time = clip.length * startFraction;
            _source.Play();
        }

        // Proyectar el listener sobre la grada evita picos de volumen al pasar frente al
        // centro de un lado de 126 m. La distancia y el paneo los resuelve el motor de audio.
        void LateUpdate() => PositionSource();

        void PositionSource()
        {
            if (!_listener || !stand) return;
            float half = stand.bayPitch * stand.bays * sourceSpan * .5f;
            float x = stand.transform.InverseTransformPoint(_listener.position).x;
            var point = _anchor;
            point.x += Mathf.Clamp(x - _anchor.x, -half, half);
            if ((point - transform.localPosition).sqrMagnitude > .0001f) transform.localPosition = point;
        }

        void ApplyVolume()
        {
            if (_source && stand && stand.profile)
                _source.volume = stand.profile.volume * sectorVolume * VolumeSettings.EffectsGain;
        }

        void OnDisable()
        {
            VolumeSettings.Changed -= ApplyVolume;
            if (_source) _source.Stop();
        }
    }
}
