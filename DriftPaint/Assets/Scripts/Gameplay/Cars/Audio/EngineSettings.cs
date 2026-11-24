using System;
using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Motor virtual de un modelo: revoluciones y caja automática. No cambia cómo se maneja el auto;
    /// traduce la velocidad y el acelerador en RPM y marchas para el sonido.
    /// </summary>
    [Serializable]
    public class EngineSettings
    {
        [Tooltip("RPM en ralentí, con el auto quieto y sin acelerar.")]
        [SerializeField, Min(300f)] float _idleRpm = 850f;

        [Tooltip("RPM a la que la caja pasa a la marcha siguiente acelerando a fondo.")]
        [SerializeField, Min(1000f)] float _redlineRpm = 7000f;

        [Tooltip("RPM del corte de inyección. Arriba de la línea roja; ahí aparece el petardeo del corte.")]
        [SerializeField, Min(1000f)] float _limiterRpm = 7200f;

        [Tooltip("Velocidad, como fracción de la máxima del auto, a la que cada marcha llega a la línea roja. " +
                 "La última pasa de 1 para que a velocidad máxima el motor no quede pegado al corte.")]
        [SerializeField] float[] _gearTopSpeeds = { 0.2f, 0.36f, 0.54f, 0.76f, 1.08f };

        [Tooltip("Velocidad, como fracción de la máxima de marcha atrás, a la que la reversa llega a la línea roja.")]
        [SerializeField, Min(0.1f)] float _reverseTopSpeed = 1.3f;

        [Tooltip("Segundos que tarda un cambio. Durante el cambio se corta la carga y las RPM caen a la marcha nueva.")]
        [SerializeField, Range(0.03f, 0.6f)] float _shiftTime = 0.18f;

        [Tooltip("Fracción de la línea roja debajo de la cual la caja baja una marcha.")]
        [SerializeField, Range(0.2f, 0.8f)] float _downshiftFraction = 0.45f;

        [Tooltip("RPM que el motor sostiene al arrancar con el auto casi quieto, mientras el embrague patina.")]
        [SerializeField, Min(500f)] float _launchRpm = 3000f;

        [Tooltip("RPM por segundo que sube el motor sin carga (en el aire o con las ruedas patinando). Motores livianos suben más rápido.")]
        [SerializeField, Min(1000f)] float _revRiseRate = 14000f;

        [Tooltip("RPM por segundo que cae el motor al soltar el acelerador sin carga. Un volante pesado cae más lento.")]
        [SerializeField, Min(500f)] float _revDropRate = 6000f;

        [Tooltip("Fracción de RPM extra cuando las ruedas patinan acelerando en un derrape.")]
        [SerializeField, Range(0f, 0.5f)] float _wheelspinBoost = 0.15f;

        public float IdleRpm => _idleRpm;
        public float RedlineRpm => Mathf.Max(_redlineRpm, _idleRpm + 500f);
        public float LimiterRpm => Mathf.Max(_limiterRpm, RedlineRpm);
        public int GearCount => _gearTopSpeeds != null ? _gearTopSpeeds.Length : 0;
        public float ReverseTopSpeed => _reverseTopSpeed;
        public float ShiftTime => _shiftTime;
        public float DownshiftFraction => _downshiftFraction;
        public float LaunchRpm => Mathf.Clamp(_launchRpm, _idleRpm, RedlineRpm);
        public float RevRiseRate => _revRiseRate;
        public float RevDropRate => _revDropRate;
        public float WheelspinBoost => _wheelspinBoost;

        /// <summary>Velocidad, como fracción de la máxima, a la que la marcha llega a la línea roja. Las marchas empiezan en 1.</summary>
        public float GearTopSpeed(int gear)
        {
            if (GearCount == 0) return 1f;
            int index = Mathf.Clamp(gear - 1, 0, GearCount - 1);
            return Mathf.Max(0.01f, _gearTopSpeeds[index]);
        }

        public EngineSettings() { }

        public EngineSettings(float idleRpm, float redlineRpm, float limiterRpm, float[] gearTopSpeeds,
            float shiftTime, float launchRpm, float revRiseRate, float revDropRate)
        {
            _idleRpm = idleRpm;
            _redlineRpm = redlineRpm;
            _limiterRpm = limiterRpm;
            _gearTopSpeeds = gearTopSpeeds;
            _shiftTime = shiftTime;
            _launchRpm = launchRpm;
            _revRiseRate = revRiseRate;
            _revDropRate = revDropRate;
        }
    }
}
