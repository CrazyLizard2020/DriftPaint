using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Vaivén corto del tono del motor después de un cambio o de un salto brusco de RPM, como el tirón
    /// de la transmisión al acoplar. Devuelve un desvío de tono que se suma al del sintetizador y se apaga solo.
    /// </summary>
    public class EnginePitchOscillator
    {
        const float RiseThreshold = 150f;
        const float DropThreshold = 80f;
        const float Damping = 5f;
        const float LoadInfluence = 0.3f;
        const float BaseSpeed = 8f;
        const float HarmonicFrequency = 2f;
        const float HarmonicAmplitude = 0.3f;
        const float ArmDelay = 0.05f;

        readonly float _depth;
        float _lastRpm;
        bool _hasLastRpm;
        float _intensity;
        float _delayTimer;
        float _phase;
        float _harmonicPhase;

        /// <param name="depth">Desvío máximo de tono. 0 lo apaga.</param>
        public EnginePitchOscillator(float depth)
        {
            _depth = Mathf.Max(0f, depth);
        }

        public void Reset()
        {
            _hasLastRpm = false;
            _intensity = 0f;
            _delayTimer = 0f;
            _phase = 0f;
            _harmonicPhase = 0f;
        }

        public void OnGearChange() => _intensity = 1f;

        public float Process(float rpm, float load, float dt)
        {
            if (_depth <= 0f || dt <= 0f) return 0f;

            // La primera muestra solo fija la referencia: sin esto el arranque se ve como un salto de RPM.
            if (!_hasLastRpm)
            {
                _lastRpm = rpm;
                _hasLastRpm = true;
                return 0f;
            }

            float delta = rpm - _lastRpm;
            _lastRpm = rpm;
            if (Mathf.Abs(delta) > (delta > 0f ? RiseThreshold : DropThreshold))
            {
                _delayTimer += dt;
                if (_delayTimer >= ArmDelay)
                {
                    _intensity = 1f;
                    _delayTimer = 0f;
                }
            }
            else
            {
                _delayTimer = 0f;
            }

            if (_intensity > 0f)
            {
                _intensity *= Mathf.Exp(-Damping * dt);
                if (_intensity < 0.001f) _intensity = 0f;
            }
            if (_intensity <= 0f) return 0f;

            float speed = BaseSpeed * (1f + rpm / 1000f);
            _phase = Mathf.Repeat(_phase + speed * dt, 2f * Mathf.PI);
            _harmonicPhase = Mathf.Repeat(_harmonicPhase + speed * HarmonicFrequency * dt, 2f * Mathf.PI);

            float wave = (Mathf.Sin(_phase) + Mathf.Sin(_harmonicPhase) * HarmonicAmplitude) / (1f + HarmonicAmplitude);
            return wave * _depth * _intensity * (1f + load * LoadInfluence);
        }
    }
}
