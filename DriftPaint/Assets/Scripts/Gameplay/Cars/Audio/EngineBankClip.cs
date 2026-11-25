using System;
using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>Un loop de motor grabado (o generado) a unas RPM fijas.</summary>
    [Serializable]
    public class EngineBankClip
    {
        [SerializeField] AudioClip _clip;
        [Tooltip("RPM a las que suena el loop a velocidad normal.")]
        [SerializeField, Min(1)] int _rpm = 1000;

        public AudioClip Clip => _clip;
        public int Rpm => _rpm;

        public EngineBankClip(AudioClip clip, int rpm)
        {
            _clip = clip;
            _rpm = rpm;
        }
    }
}
