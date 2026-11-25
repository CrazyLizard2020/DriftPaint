using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>Lo que el motor virtual necesita saber del auto en cada paso de física.</summary>
    public struct EngineInput
    {
        /// <summary>Velocidad hacia adelante en m/s. Negativa yendo marcha atrás.</summary>
        public float ForwardSpeed;
        /// <summary>Acelerador de -1 a 1: positivo acelera, negativo frena o va marcha atrás.</summary>
        public float Throttle;
        public float MaxSpeed;
        public float MaxReverseSpeed;
        /// <summary>Las ruedas tocan el piso. En el aire el motor gira libre.</summary>
        public bool Grounded;
        /// <summary>Las ruedas traseras patinan (derrape acelerando): el motor sube de vueltas.</summary>
        public bool Wheelspin;
        /// <summary>Turbo del drift activo: el motor va exigido, cerca del corte.</summary>
        public bool Boost;
    }

    /// <summary>
    /// Motor con caja automática para el sonido. Traduce velocidad y acelerador en RPM, carga y marcha,
    /// sin tocar la física: el auto se mueve igual, el motor solo acompaña. Es una clase común para
    /// poder probarla sin escena.
    /// </summary>
    public class EngineSimulation
    {
        /// <summary>Carga en ralentí con el auto quieto. Por encima del cruce del sintetizador para que suene el banco de aceleración, bajito.</summary>
        public const float IdleLoad = 0.3f;
        /// <summary>Tiempo de respuesta de las RPM cuando el motor está acoplado a las ruedas.</summary>
        const float CoupledResponse = 0.06f;
        /// <summary>Duración de cada corte del limitador.</summary>
        const float LimiterCutTime = 0.07f;
        /// <summary>RPM que cae el motor en cada corte del limitador.</summary>
        const float LimiterDip = 350f;
        /// <summary>Debajo de esta velocidad (m/s) el auto se considera quieto.</summary>
        const float StandstillSpeed = 0.4f;
        /// <summary>Con el turbo del drift el motor sube al menos a esta fracción de la línea roja.</summary>
        const float BoostRedlineFraction = 0.95f;

        readonly EngineSettings _settings;
        float _shiftTimer;
        float _cutTimer;

        public float Rpm { get; private set; }
        public float Load { get; private set; }
        /// <summary>Marcha actual: 1 en adelante hacia adelante, -1 marcha atrás.</summary>
        public int Gear { get; private set; } = 1;
        public bool IsShifting => _shiftTimer > 0f;
        /// <summary>Arrancando con el embrague patinando: las RPM van por encima de lo que piden las ruedas.</summary>
        public bool IsLaunching { get; private set; }
        public bool IsAtLimiter { get; private set; }
        /// <summary>Hubo un cambio de marcha en el último paso.</summary>
        public bool ShiftedThisStep { get; private set; }
        public EngineSettings Settings => _settings;

        /// <summary>RPM entre ralentí (0) y corte (1).</summary>
        public float NormalizedRpm => Mathf.InverseLerp(_settings.IdleRpm, _settings.LimiterRpm, Rpm);

        public EngineSimulation(EngineSettings settings)
        {
            _settings = settings ?? new EngineSettings();
            Reset();
        }

        /// <summary>Motor en ralentí y primera, como recién encendido.</summary>
        public void Reset()
        {
            Rpm = _settings.IdleRpm;
            Load = IdleLoad;
            Gear = 1;
            _shiftTimer = 0f;
            _cutTimer = 0f;
            IsLaunching = false;
            IsAtLimiter = false;
            ShiftedThisStep = false;
        }

        public void Step(in EngineInput input, float dt)
        {
            if (dt <= 0f) return;
            ShiftedThisStep = false;

            float speed = Mathf.Abs(input.ForwardSpeed);
            bool reversing = input.ForwardSpeed < -StandstillSpeed || (speed < StandstillSpeed && input.Throttle < -0.05f);
            bool accelerating = reversing ? input.Throttle < -0.05f : input.Throttle > 0.05f;
            float pedal = accelerating ? Mathf.Abs(input.Throttle) : 0f;

            if (reversing && Gear != -1) SetGear(-1);
            else if (!reversing && Gear == -1) SetGear(1);

            float wheelRpm = WheelRpm(speed, input);
            if (Gear >= 1 && !IsShifting && input.Grounded && ChooseGear(wheelRpm, pedal, speed, input))
                wheelRpm = WheelRpm(speed, input);

            float idle = _settings.IdleRpm;
            float limiter = _settings.LimiterRpm;
            float target;
            bool free;
            IsLaunching = false;

            if (IsShifting)
            {
                // Embrague abierto: las RPM caen (o suben, al reducir) hacia lo que pide la marcha nueva.
                _shiftTimer -= dt;
                target = Mathf.Max(wheelRpm, idle);
                Load = 0f;
                free = true;
            }
            else if (!input.Grounded)
            {
                target = pedal > 0f ? Mathf.Lerp(idle, limiter, pedal) : idle;
                Load = pedal;
                free = true;
            }
            else if (pedal > 0f)
            {
                float launch = Mathf.Abs(Gear) == 1 ? Mathf.Lerp(idle, _settings.LaunchRpm, pedal) : idle;
                IsLaunching = wheelRpm < launch && speed < input.MaxSpeed * 0.2f;
                target = Mathf.Max(wheelRpm, launch);
                if (input.Wheelspin) target *= 1f + _settings.WheelspinBoost;
                if (input.Boost) target = Mathf.Max(target, _settings.RedlineRpm * BoostRedlineFraction);
                Load = pedal;
                free = IsLaunching || input.Wheelspin || input.Boost;
            }
            else
            {
                // Sin acelerar el motor sigue a las ruedas (freno motor) y nunca baja del ralentí.
                target = Mathf.Max(wheelRpm, idle);
                Load = speed < StandstillSpeed ? IdleLoad : 0f;
                free = false;
            }

            target = ApplyLimiter(target, pedal, dt);

            if (free)
            {
                float rate = target > Rpm ? _settings.RevRiseRate : _settings.RevDropRate;
                Rpm = Mathf.MoveTowards(Rpm, target, rate * dt);
            }
            else
            {
                Rpm = Mathf.Lerp(Rpm, target, 1f - Mathf.Exp(-dt / CoupledResponse));
            }
            Rpm = Mathf.Clamp(Rpm, idle * 0.9f, limiter);
        }

        float WheelRpm(float speed, in EngineInput input)
        {
            float top = Gear == -1 ? _settings.ReverseTopSpeed : _settings.GearTopSpeed(Gear);
            float reference = Gear == -1 ? input.MaxReverseSpeed : input.MaxSpeed;
            float fraction = speed / Mathf.Max(0.1f, reference);
            return _settings.RedlineRpm * fraction / top;
        }

        /// <summary>Cambia de marcha según las RPM de las ruedas. Devuelve true si hubo cambio.</summary>
        bool ChooseGear(float wheelRpm, float pedal, float speed, in EngineInput input)
        {
            float redline = _settings.RedlineRpm;
            bool canUp = Gear < _settings.GearCount;

            // Acelerando se estira hasta la línea roja; sin acelerar se pasa antes del corte para no sobrerrevolucionar.
            if (canUp && (pedal > 0.1f ? wheelRpm >= redline : wheelRpm >= _settings.LimiterRpm))
            {
                SetGear(Gear + 1);
                return true;
            }

            if (Gear > 1)
            {
                float fraction = speed / Mathf.Max(0.1f, input.MaxSpeed);
                float lowerRpm = redline * fraction / _settings.GearTopSpeed(Gear - 1);
                if (wheelRpm < redline * _settings.DownshiftFraction && lowerRpm < redline * 0.9f)
                {
                    SetGear(Gear - 1);
                    return true;
                }
            }
            return false;
        }

        void SetGear(int gear)
        {
            if (gear == Gear) return;
            Gear = gear;
            _shiftTimer = _settings.ShiftTime;
            ShiftedThisStep = true;
        }

        /// <summary>Al llegar al corte la inyección se corta a intervalos: las RPM rebotan contra el límite.</summary>
        float ApplyLimiter(float target, float pedal, float dt)
        {
            float limiter = _settings.LimiterRpm;
            if (_cutTimer > 0f)
            {
                _cutTimer -= dt;
                Load = Mathf.Min(Load, 0.1f);
                return limiter - LimiterDip;
            }

            IsAtLimiter = pedal > 0f && target >= limiter && Rpm >= limiter - 25f;
            if (IsAtLimiter)
            {
                _cutTimer = LimiterCutTime;
                return limiter - LimiterDip;
            }
            return Mathf.Min(target, limiter);
        }
    }
}
