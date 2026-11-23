using System;
using UnityEngine;

namespace DriftPaint.Audio
{
    public enum VolumeChannel
    {
        Master,
        Music,
        Effects
    }

    /// <summary>
    /// Volúmenes que elige el jugador, de 0 a 1, guardados en esta máquina. El general se aplica al
    /// AudioListener; la música y los efectos los aplica cada fuente de sonido multiplicando por su ganancia.
    /// </summary>
    public static class VolumeSettings
    {
        const string KeyPrefix = "DriftPaint.Volume.";
        static readonly float[] Defaults = { 1f, 0.7f, 0.8f };

        static float[] _values;

        /// <summary>Se dispara cuando cambia cualquier volumen.</summary>
        public static event Action Changed;

        static float[] Values
        {
            get
            {
                if (_values == null) Load();
                return _values;
            }
        }

        /// <summary>Valor del control deslizante, de 0 a 1.</summary>
        public static float Get(VolumeChannel channel) => Values[(int)channel];

        /// <summary>
        /// Ganancia a aplicar al sonido. El oído percibe el volumen en escala logarítmica: elevar al
        /// cuadrado hace que la mitad del control suene cerca de la mitad del volumen.
        /// </summary>
        public static float Gain(VolumeChannel channel)
        {
            float value = Get(channel);
            return value * value;
        }

        public static float MusicGain => Gain(VolumeChannel.Music);
        public static float EffectsGain => Gain(VolumeChannel.Effects);

        public static void Set(VolumeChannel channel, float value)
        {
            int index = (int)channel;
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(Values[index], value)) return;

            _values[index] = value;
            PlayerPrefs.SetFloat(KeyPrefix + channel, value);
            Apply();
            Changed?.Invoke();
        }

        /// <summary>Escribe los valores en disco. Se llama al cerrar las opciones, no en cada movimiento del control.</summary>
        public static void Save() => PlayerPrefs.Save();

        static void Load()
        {
            _values = new float[Defaults.Length];
            for (int i = 0; i < Defaults.Length; i++)
                _values[i] = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyPrefix + (VolumeChannel)i, Defaults[i]));
        }

        static void Apply() => AudioListener.volume = Gain(VolumeChannel.Master);

        // Con la recarga de dominio apagada, los valores estáticos sobreviven entre ejecuciones del editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _values = null;
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyOnStart() => Apply();
    }
}
