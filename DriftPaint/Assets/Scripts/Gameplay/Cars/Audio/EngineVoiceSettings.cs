using System;
using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Una de las fuentes que forman el sonido del motor (escape, admisión o mecánica): su banco de
    /// loops, cuánto pesa en la mezcla y el carácter que le dan los filtros del sintetizador.
    /// </summary>
    [Serializable]
    public class EngineVoiceSettings
    {
        [Tooltip("Loops de esta fuente. Vacío: la fuente no suena.")]
        [SerializeField] EngineSoundBankSO _bank;

        [Tooltip("Volumen de la fuente en la mezcla del auto propio.")]
        [SerializeField, Range(0f, 2f)] float _volume = 1f;

        [Tooltip("Multiplica el tono de todos los loops. Sirve para que dos autos con el mismo banco suenen distinto.")]
        [SerializeField, Range(0.7f, 1.4f)] float _pitch = 1f;

        [Tooltip("Volumen según las RPM, de ralentí (0) a la RPM más alta del banco (1).")]
        [SerializeField] AnimationCurve _volumeByRpm = AnimationCurve.Linear(0f, 0.55f, 1f, 1f);

        [Tooltip("Volumen en ralentí, relativo al de alta.")]
        [SerializeField, Range(0.05f, 1f)] float _idleVolume = 0.35f;

        [Tooltip("Volumen máximo de los loops sin acelerar, relativo a los de aceleración.")]
        [SerializeField, Range(0f, 2f)] float _decelerationVolume = 0.6f;

        [Header("Carácter")]
        [Tooltip("Apaga los agudos al soltar el acelerador. 0 = siempre abierto.")]
        [SerializeField, Range(0f, 1f)] float _lowPass = 0.2f;
        [Tooltip("Recorta graves para un sonido más áspero.")]
        [SerializeField, Range(0f, 1f)] float _highPass;
        [Tooltip("Resonancia del filtro: un tono más nasal o metálico.")]
        [SerializeField, Range(0f, 1f)] float _resonance;
        [Tooltip("Saturación con carga y RPM altas.")]
        [SerializeField, Range(0f, 1f)] float _distortion = 0.2f;
        [Tooltip("Desafinado entre copias: engrosa el sonido, como dos escapes que no suenan igual.")]
        [SerializeField, Range(0f, 1f)] float _chorus;

        public EngineSoundBankSO Bank => _bank;
        public float Volume => _volume;
        public float Pitch => _pitch;
        public AnimationCurve VolumeByRpm => _volumeByRpm;
        public float IdleVolume => _idleVolume;
        public float DecelerationVolume => _decelerationVolume;
        public float LowPass => _lowPass;
        public float HighPass => _highPass;
        public float Resonance => _resonance;
        public float Distortion => _distortion;
        public float Chorus => _chorus;
        public bool HasClips => _bank != null && _bank.Accelerating != null && _bank.Accelerating.Length > 0;

        public EngineVoiceSettings() { }

        public EngineVoiceSettings(EngineSoundBankSO bank, float volume, float pitch, AnimationCurve volumeByRpm,
            float idleVolume, float decelerationVolume, float lowPass, float highPass, float resonance, float distortion, float chorus)
        {
            _bank = bank;
            _volume = volume;
            _pitch = pitch;
            _volumeByRpm = volumeByRpm ?? AnimationCurve.Linear(0f, 0.55f, 1f, 1f);
            _idleVolume = idleVolume;
            _decelerationVolume = decelerationVolume;
            _lowPass = lowPass;
            _highPass = highPass;
            _resonance = resonance;
            _distortion = distortion;
            _chorus = chorus;
        }
    }
}
