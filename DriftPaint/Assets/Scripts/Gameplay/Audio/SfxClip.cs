using System;
using UnityEngine;

namespace DriftPaint.Audio
{
    /// <summary>
    /// Un efecto de sonido con sus variantes: cuál suena cada vez, su volumen y cuánto varían el tono y el
    /// volumen entre repeticiones. Se usa como campo en cualquier asset o componente (por ejemplo
    /// <see cref="SfxLibrarySO"/> o <see cref="SfxEmitter"/>) y se ajusta desde el inspector.
    /// </summary>
    [Serializable]
    public class SfxClip
    {
        [Tooltip("Variantes del efecto.")]
        [SerializeField] AudioClip[] _clips = new AudioClip[0];
        [Tooltip("Cómo se elige la variante que suena cada vez.")]
        [SerializeField] SfxSelection _selection = SfxSelection.RandomNoRepeat;
        [SerializeField, Range(0f, 1.5f)] float _volume = 0.8f;
        [Tooltip("Variación aleatoria del volumen (±, en proporción). 0 suena siempre igual de fuerte.")]
        [SerializeField, Range(0f, 0.5f)] float _volumeVariation;
        [Tooltip("Variación aleatoria del tono (±), para que repetirlo no suene a máquina.")]
        [SerializeField, Range(0f, 0.3f)] float _pitchVariation = 0.05f;

        // Estado de la elección: no se guarda en el asset.
        [NonSerialized] int _last = -1;
        [NonSerialized] int[] _bag;
        [NonSerialized] int _bagIndex;

        public float Volume => _volume;
        public float PitchVariation => _pitchVariation;
        public SfxSelection Selection => _selection;
        public bool HasClips => _clips != null && _clips.Length > 0;

        public SfxClip() { }

        public SfxClip(AudioClip[] clips, float volume, float pitchVariation, SfxSelection selection = SfxSelection.RandomNoRepeat,
            float volumeVariation = 0f)
        {
            _clips = clips;
            _volume = volume;
            _pitchVariation = pitchVariation;
            _selection = selection;
            _volumeVariation = volumeVariation;
        }

        /// <summary>La próxima variante según el modo de selección.</summary>
        public AudioClip Pick()
        {
            if (!HasClips) return null;
            _last = NextIndex(_clips.Length);
            return _clips[_last];
        }

        public float PickPitch() => 1f + UnityEngine.Random.Range(-_pitchVariation, _pitchVariation);

        /// <summary>Volumen de esta repetición, antes del volumen general de efectos.</summary>
        public float PickVolume() => _volume * (1f + UnityEngine.Random.Range(-_volumeVariation, _volumeVariation));

        /// <summary>Prepara la fuente con la próxima variante, su tono y su volumen (por gain) y la toca.</summary>
        public bool Play(AudioSource source, float gain = 1f)
        {
            if (source == null || !HasClips) return false;
            source.Stop();
            source.clip = Pick();
            source.pitch = PickPitch();
            source.volume = PickVolume() * gain;
            source.Play();
            return true;
        }

        int NextIndex(int count)
        {
            if (count == 1) return 0;
            switch (_selection)
            {
                case SfxSelection.Sequence:
                    return (_last + 1) % count;
                case SfxSelection.RandomNoRepeat:
                    if (_last < 0 || _last >= count) return UnityEngine.Random.Range(0, count);
                    // Se elige entre las demás: los índices desde la anterior se corren uno.
                    int pick = UnityEngine.Random.Range(0, count - 1);
                    return pick >= _last ? pick + 1 : pick;
                case SfxSelection.Shuffle:
                    if (_bag == null || _bag.Length != count || _bagIndex >= count) Refill(count);
                    return _bag[_bagIndex++];
                default:
                    return UnityEngine.Random.Range(0, count);
            }
        }

        /// <summary>Nueva mezcla de todas las variantes. La primera nunca es la que sonó al final de la anterior.</summary>
        void Refill(int count)
        {
            if (_bag == null || _bag.Length != count)
            {
                _bag = new int[count];
                for (int i = 0; i < count; i++) _bag[i] = i;
            }
            for (int i = count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
            }
            if (_bag[0] == _last)
            {
                int swap = UnityEngine.Random.Range(1, count);
                (_bag[0], _bag[swap]) = (_bag[swap], _bag[0]);
            }
            _bagIndex = 0;
        }
    }
}
