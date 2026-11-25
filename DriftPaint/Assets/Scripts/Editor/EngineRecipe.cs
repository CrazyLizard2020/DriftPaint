namespace DriftPaint.EditorTools
{
    /// <summary>Resonancia de un caño, una caja de aire o el block: realza una banda de frecuencias.</summary>
    public readonly struct Resonance
    {
        public readonly float Frequency;
        public readonly float Q;
        public readonly float Gain;

        public Resonance(float frequency, float q, float gain)
        {
            Frequency = frequency;
            Q = q;
            Gain = gain;
        }
    }

    /// <summary>
    /// Receta de una familia de motores para <see cref="EngineBankGenerator"/>: cómo se reparten las explosiones
    /// en el ciclo y qué resonancias tienen el escape, la admisión y la mecánica.
    /// </summary>
    public class EngineRecipe
    {
        /// <summary>Nombre de la familia; se usa en carpetas y nombres de archivo.</summary>
        public string Id;
        /// <summary>Vueltas del cigüeñal en un ciclo completo de explosiones: 2 en cuatro tiempos, 1 en rotativos.</summary>
        public float CycleRevs = 2f;
        /// <summary>Momento de cada explosión como fracción del ciclo.</summary>
        public float[] FiringAngles;
        /// <summary>Fuerza de cada explosión tal como llega al oído (un banco del motor queda más cerca que el otro).</summary>
        public float[] FiringGains;

        /// <summary>Tiempo de subida del pulso de escape (s). Más corto, más brillante y áspero.</summary>
        public float PulseRise = 0.0005f;
        /// <summary>Corrimiento aleatorio de cada explosión, como fracción del intervalo entre explosiones.</summary>
        public float TimingJitter = 0.01f;
        /// <summary>Variación aleatoria de fuerza entre explosiones.</summary>
        public float GainJitter = 0.05f;
        /// <summary>Irregularidad extra en ralentí: el traqueteo de un árbol de levas cruzado o el "brap" de un rotativo.</summary>
        public float IdleRoughness = 0.1f;

        public Resonance[] Exhaust;
        /// <summary>Cuánto del pulso pasa directo, filtrado en graves: el cuerpo del sonido.</summary>
        public float ExhaustBody = 0.4f;
        /// <summary>Soplido de gases que acompaña cada explosión.</summary>
        public float ExhaustNoise = 0.25f;
        /// <summary>Saturación acelerando. Sin acelerar se usa menos.</summary>
        public float Drive = 2f;

        public Resonance[] Intake;
        public float IntakeNoise = 0.7f;

        /// <summary>Golpeteo de válvulas. 0 en motores sin válvulas.</summary>
        public float ValveTicks = 0.5f;
        /// <summary>Frecuencia central del golpeteo de válvulas.</summary>
        public float ValveTone = 4500f;
        /// <summary>Silbido de engranajes como múltiplo de las vueltas del motor. 0 lo apaga.</summary>
        public int WhineOrder;
        public float WhineGain;

        /// <summary>Rango de RPM que cubren los bancos.</summary>
        public float MinRpm = 800f;
        public float MaxRpm = 8800f;

        /// <summary>Frecuencia del golpe grave de los petardeos.</summary>
        public float PopThump = 110f;
        /// <summary>El motor tiene turbo: la mariposa suena a turbo cargando y a válvula de alivio.</summary>
        public bool Turbo;

        public int FiringsPerCycle => FiringAngles.Length;
    }
}
