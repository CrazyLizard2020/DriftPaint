using System;
using UnityEngine;

namespace DriftPaint.Audio
{
    /// <summary>Un efecto de sonido con su volumen y cuánto varía el tono entre repeticiones.</summary>
    [Serializable]
    public class SfxClip
    {
        [Tooltip("Variantes del efecto; cada vez suena una al azar.")]
        [SerializeField] AudioClip[] _clips = new AudioClip[0];
        [SerializeField, Range(0f, 1.5f)] float _volume = 0.8f;
        [Tooltip("Variación aleatoria del tono (±), para que repetirlo no suene a máquina.")]
        [SerializeField, Range(0f, 0.3f)] float _pitchVariation = 0.05f;

        public float Volume => _volume;
        public float PitchVariation => _pitchVariation;
        public bool HasClips => _clips != null && _clips.Length > 0;

        public AudioClip Pick() => HasClips ? _clips[UnityEngine.Random.Range(0, _clips.Length)] : null;

        public float PickPitch() => 1f + UnityEngine.Random.Range(-_pitchVariation, _pitchVariation);

        public SfxClip() { }

        public SfxClip(AudioClip[] clips, float volume, float pitchVariation)
        {
            _clips = clips;
            _volume = volume;
            _pitchVariation = pitchVariation;
        }
    }
}
