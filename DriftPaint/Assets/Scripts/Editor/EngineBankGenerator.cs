using System.Collections.Generic;
using System.Linq;
using DriftPaint.Cars;
using UnityEditor;
using UnityEngine;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Fabrica los loops de motor de cada familia (cuatro en línea, rotativo, V8 de cigüeñal cruzado y V10)
    /// y los sonidos sueltos que los acompañan. Cada loop es un tren de explosiones con el ritmo real de esa
    /// familia, pasado por las resonancias del escape, la admisión o el block, con un número entero de
    /// ciclos del motor para que empalme sin costura. Siempre sale igual: el ruido usa semillas fijas.
    /// </summary>
    public static class EngineBankGenerator
    {
        /// <summary>Subir este número cuando cambia una receta: los bancos viejos se regeneran solos.</summary>
        public const int Version = 1;

        public enum Source { Exhaust, Intake, Mechanical }

        const float LoopSeconds = 0.6f;
        const float LoopRms = 0.2f;
        const int ExhaustAccelerating = 11;
        const int ExhaustDecelerating = 6;
        const int IntakeClips = 7;
        const int MechanicalClips = 6;

        /// <summary>
        /// Cuatro en línea atmosférico de alta (4A-GE, VTEC): explosiones parejas cada media vuelta, sonido
        /// filoso y nasal que se vuelve un aullido arriba, con mucha admisión.
        /// </summary>
        public static readonly EngineRecipe InlineFour = new EngineRecipe
        {
            Id = "I4",
            CycleRevs = 2f,
            FiringAngles = new[] { 0f, 0.25f, 0.5f, 0.75f },
            FiringGains = new[] { 1f, 0.93f, 0.97f, 0.9f },
            PulseRise = 0.00045f,
            TimingJitter = 0.008f,
            GainJitter = 0.06f,
            IdleRoughness = 0.12f,
            Exhaust = new[]
            {
                new Resonance(95f, 1.6f, 0.7f), new Resonance(260f, 3f, 0.8f), new Resonance(620f, 4f, 0.45f),
                new Resonance(1800f, 2.5f, 0.35f), new Resonance(3400f, 3f, 0.15f)
            },
            ExhaustBody = 0.4f,
            ExhaustNoise = 0.25f,
            Drive = 2.2f,
            Intake = new[] { new Resonance(320f, 5f, 0.7f), new Resonance(900f, 4f, 0.6f), new Resonance(2200f, 3f, 0.35f) },
            IntakeNoise = 0.8f,
            ValveTicks = 0.6f,
            ValveTone = 5200f,
            WhineOrder = 2,
            WhineGain = 0.05f,
            MinRpm = 700f,
            MaxRpm = 8800f,
            PopThump = 115f
        };

        /// <summary>
        /// Rotativo de dos rotores con turbo (13B): dos explosiones por vuelta con lumbreras que abren de golpe,
        /// pulsos muy filosos y un ralentí irregular, el "brap", que se alisa en un zumbido agudo arriba.
        /// </summary>
        public static readonly EngineRecipe Rotary = new EngineRecipe
        {
            Id = "Rotary",
            CycleRevs = 1f,
            FiringAngles = new[] { 0f, 0.5f },
            FiringGains = new[] { 1f, 0.96f },
            PulseRise = 0.00022f,
            TimingJitter = 0.006f,
            GainJitter = 0.05f,
            IdleRoughness = 0.45f,
            Exhaust = new[]
            {
                new Resonance(120f, 1.5f, 0.6f), new Resonance(420f, 2.5f, 0.75f), new Resonance(1100f, 3f, 0.6f),
                new Resonance(2600f, 2.5f, 0.45f), new Resonance(4800f, 3f, 0.2f)
            },
            ExhaustBody = 0.3f,
            ExhaustNoise = 0.35f,
            Drive = 3f,
            Intake = new[] { new Resonance(250f, 3f, 0.6f), new Resonance(750f, 4f, 0.4f), new Resonance(3800f, 6f, 0.2f) },
            IntakeNoise = 0.5f,
            ValveTicks = 0f,
            WhineOrder = 3,
            WhineGain = 0.18f,
            MinRpm = 700f,
            MaxRpm = 8800f,
            PopThump = 140f,
            Turbo = true
        };

        /// <summary>
        /// V8 de cigüeñal cruzado (muscle car): cada bancada explota a intervalos desparejos, y la bancada más
        /// cercana se oye más fuerte. Ese desequilibrio es el borboteo grave, con traqueteo de levas en ralentí.
        /// </summary>
        public static readonly EngineRecipe CrossplaneV8 = new EngineRecipe
        {
            Id = "V8",
            CycleRevs = 2f,
            FiringAngles = new[] { 0f, 0.125f, 0.25f, 0.375f, 0.5f, 0.625f, 0.75f, 0.875f },
            // Orden 1-8-4-3-6-5-7-2: bancadas I D D I D I I D.
            FiringGains = new[] { 1f, 0.72f, 0.72f, 1f, 0.72f, 1f, 1f, 0.72f },
            PulseRise = 0.0007f,
            TimingJitter = 0.012f,
            GainJitter = 0.08f,
            IdleRoughness = 0.35f,
            Exhaust = new[]
            {
                new Resonance(70f, 1.4f, 0.9f), new Resonance(180f, 2.5f, 0.8f), new Resonance(420f, 3f, 0.5f),
                new Resonance(1100f, 2.5f, 0.3f), new Resonance(2500f, 2f, 0.12f)
            },
            ExhaustBody = 0.7f,
            ExhaustNoise = 0.2f,
            Drive = 1.8f,
            Intake = new[] { new Resonance(220f, 4f, 0.6f), new Resonance(600f, 4f, 0.4f) },
            IntakeNoise = 0.5f,
            ValveTicks = 0.35f,
            ValveTone = 3000f,
            MinRpm = 600f,
            MaxRpm = 7000f,
            PopThump = 70f
        };

        /// <summary>
        /// V10 atmosférico de alta (5.2 FSI): cinco explosiones por vuelta alternando bancadas, un alarido
        /// agudo y parejo con algo de aspereza, y mucha admisión a alta.
        /// </summary>
        public static readonly EngineRecipe V10 = new EngineRecipe
        {
            Id = "V10",
            CycleRevs = 2f,
            FiringAngles = Enumerable.Range(0, 10).Select(i => i / 10f).ToArray(),
            FiringGains = Enumerable.Range(0, 10).Select(i => i % 2 == 0 ? 1f : 0.82f).ToArray(),
            PulseRise = 0.0003f,
            TimingJitter = 0.006f,
            GainJitter = 0.04f,
            IdleRoughness = 0.12f,
            Exhaust = new[]
            {
                new Resonance(110f, 1.6f, 0.6f), new Resonance(330f, 3f, 0.75f), new Resonance(850f, 3.5f, 0.55f),
                new Resonance(2100f, 3f, 0.45f), new Resonance(4200f, 3f, 0.25f)
            },
            ExhaustBody = 0.35f,
            ExhaustNoise = 0.2f,
            Drive = 2.6f,
            Intake = new[] { new Resonance(380f, 5f, 0.7f), new Resonance(1200f, 4f, 0.55f), new Resonance(3000f, 3f, 0.3f) },
            IntakeNoise = 0.7f,
            ValveTicks = 0.45f,
            ValveTone = 6000f,
            MinRpm = 900f,
            MaxRpm = 9200f,
            PopThump = 120f
        };

        public static readonly EngineRecipe[] Recipes = { InlineFour, Rotary, CrossplaneV8, V10 };

        static readonly Source[] Sources = { Source.Exhaust, Source.Intake, Source.Mechanical };

        /// <summary>Nombres de los sonidos sueltos de cada familia.</summary>
        public static class OneShots
        {
            public static readonly string[] Pops = { "Pop_0", "Pop_1", "Pop_2" };
            public static readonly string[] Limiter = { "Limiter_0", "Limiter_1" };
            public const string TipIn = "TipIn";
            public const string TipOut = "TipOut";
            public const string Shift = "Shift";
        }

        public static string BankPath(EngineRecipe recipe, Source source) => ScriptableObjectPaths.EngineBank(recipe.Id, source.ToString());

        public static string OneShotPath(EngineRecipe recipe, string name) =>
            $"{GeneratedAudioImport.EnginesFolder}/{recipe.Id}/{GeneratedAudioImport.OneShotPrefix}{name}.wav";

        public static EngineSoundBankSO LoadBank(EngineRecipe recipe, Source source) =>
            AssetDatabase.LoadAssetAtPath<EngineSoundBankSO>(BankPath(recipe, source));

        public static AudioClip LoadOneShot(EngineRecipe recipe, string name) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>(OneShotPath(recipe, name));

        /// <summary>Falta algún banco o lo escribió una versión anterior de las recetas.</summary>
        public static bool Outdated()
        {
            foreach (EngineRecipe recipe in Recipes)
            foreach (Source source in Sources)
            {
                EngineSoundBankSO bank = LoadBank(recipe, source);
                if (bank == null || bank.GeneratorVersion != Version || bank.Accelerating.Any(c => c == null || c.Clip == null)) return true;
            }
            return false;
        }

        public static void GenerateAll()
        {
            var written = new Dictionary<(EngineRecipe, Source), (List<(string path, int rpm)> acc, List<(string path, int rpm)> dec)>();
            try
            {
                foreach (EngineRecipe recipe in Recipes)
                {
                    EditorUtility.DisplayProgressBar("Motores", $"Generando {recipe.Id}", (float)System.Array.IndexOf(Recipes, recipe) / Recipes.Length);
                    foreach (Source source in Sources)
                        written[(recipe, source)] = WriteBank(recipe, source);
                    WriteOneShots(recipe);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AudioSynthesis.ImportWritten();

            foreach (var entry in written)
            {
                (EngineRecipe recipe, Source source) = entry.Key;
                EngineSoundBankSO bank = EditorAssetUtility.LoadOrCreate(BankPath(recipe, source), ScriptableObject.CreateInstance<EngineSoundBankSO>);
                bank.SetClips(LoadClips(entry.Value.acc), LoadClips(entry.Value.dec), Version);
                EditorUtility.SetDirty(bank);
            }
            AssetDatabase.SaveAssets();
        }

        static EngineBankClip[] LoadClips(List<(string path, int rpm)> files) =>
            files.Select(f => new EngineBankClip(AssetDatabase.LoadAssetAtPath<AudioClip>(f.path), f.rpm)).ToArray();

        static (List<(string, int)>, List<(string, int)>) WriteBank(EngineRecipe recipe, Source source)
        {
            int accCount = source == Source.Exhaust ? ExhaustAccelerating : source == Source.Intake ? IntakeClips : MechanicalClips;
            int decCount = source == Source.Exhaust ? ExhaustDecelerating : 0;

            var acc = new List<(string, int)>();
            foreach (float rpm in RpmPoints(recipe.MinRpm, recipe.MaxRpm, accCount))
                acc.Add(WriteLoop(recipe, source, rpm, true));

            var dec = new List<(string, int)>();
            foreach (float rpm in RpmPoints(recipe.MinRpm * 1.4f, recipe.MaxRpm, decCount))
                dec.Add(WriteLoop(recipe, source, rpm, false));
            return (acc, dec);
        }

        /// <summary>RPM repartidas en proporción fija: el salto de tono entre loops vecinos es siempre el mismo.</summary>
        static IEnumerable<float> RpmPoints(float min, float max, int count)
        {
            for (int i = 0; i < count; i++)
                yield return count == 1 ? min : min * Mathf.Pow(max / min, i / (float)(count - 1));
        }

        static (string, int) WriteLoop(EngineRecipe recipe, Source source, float rpm, bool accelerating)
        {
            float[] samples = SynthesizeLoop(recipe, source, rpm, accelerating, out int actualRpm);
            string path = $"{GeneratedAudioImport.EnginesFolder}/{recipe.Id}/{source}_{(accelerating ? "Acc" : "Dec")}_{actualRpm:0000}.wav";
            AudioSynthesis.WriteWav(path, samples);
            return (path, actualRpm);
        }

        /// <summary>
        /// Arma un loop a unas RPM. El largo es un número entero de ciclos del motor; como las muestras también
        /// son enteras, las RPM reales salen apenas distintas de las pedidas y se devuelven en <paramref name="actualRpm"/>.
        /// </summary>
        public static float[] SynthesizeLoop(EngineRecipe recipe, Source source, float rpm, bool accelerating, out int actualRpm)
        {
            float cycleHz = rpm / 60f / recipe.CycleRevs;
            int cycles = Mathf.Max(2, Mathf.RoundToInt(LoopSeconds * cycleHz));
            int length = Mathf.RoundToInt(cycles * AudioSynthesis.SampleRate / cycleHz);
            actualRpm = Mathf.RoundToInt(cycles * AudioSynthesis.SampleRate / (float)length * 60f * recipe.CycleRevs);

            var random = new System.Random(Seed(recipe.Id, source.ToString(), actualRpm, accelerating));
            float progress = Mathf.InverseLerp(recipe.MinRpm, recipe.MaxRpm, rpm);
            // La irregularidad del ralentí se alisa al subir de vueltas.
            float roughness = recipe.IdleRoughness * (1f - Mathf.Clamp01(progress * 3f));

            BuildFirings(recipe, cycles, length, accelerating, roughness, random, out float[] pulses, out float[] envelope);
            float[] noise = AudioSynthesis.Noise(length, random);
            for (int i = 0; i < length; i++) noise[i] *= envelope[i];

            float[] output = source switch
            {
                Source.Exhaust => ExhaustLoop(recipe, pulses, noise, accelerating),
                Source.Intake => IntakeLoop(recipe, pulses, noise),
                _ => MechanicalLoop(recipe, pulses, cycles, length, progress, random)
            };

            output = AudioSynthesis.FilterLoop(output, AudioSynthesis.Biquad.HighPass(35f));
            AudioSynthesis.RemoveDc(output);
            AudioSynthesis.Normalize(output, LoopRms);
            return output;
        }

        /// <summary>
        /// Tren de explosiones. <paramref name="pulses"/> es la onda de presión de cada explosión y
        /// <paramref name="envelope"/> su envolvente más larga, para modular el ruido de los gases.
        /// </summary>
        static void BuildFirings(EngineRecipe recipe, int cycles, int length, bool accelerating, float roughness,
            System.Random random, out float[] pulses, out float[] envelope)
        {
            pulses = new float[length];
            envelope = new float[length];
            float interval = length / (float)(cycles * recipe.FiringsPerCycle);
            float rise = Mathf.Clamp(recipe.PulseRise * AudioSynthesis.SampleRate, 1.5f, interval * 0.25f);
            float envelopeRise = Mathf.Max(rise * 3f, interval * 0.3f);

            for (int cycle = 0; cycle < cycles; cycle++)
            {
                for (int j = 0; j < recipe.FiringsPerCycle; j++)
                {
                    float jitter = (recipe.TimingJitter + roughness * 0.08f) * AudioSynthesis.Range(random, -1f, 1f) * interval;
                    float position = (cycle + recipe.FiringAngles[j]) * length / cycles + jitter;
                    float gain = recipe.FiringGains[j] * (1f + (recipe.GainJitter + roughness * 0.6f) * AudioSynthesis.Range(random, -1f, 1f));

                    if (accelerating)
                    {
                        // Explosiones flojas sueltas: el traqueteo del ralentí irregular.
                        if (random.NextDouble() < roughness * 0.2f) gain *= 0.3f;
                    }
                    else
                    {
                        // Sin acelerar entra poca mezcla: explosiones débiles y algunas que casi no ocurren.
                        gain *= random.NextDouble() < 0.2 ? 0.08f : AudioSynthesis.Range(random, 0.25f, 0.45f);
                    }

                    AddPulse(pulses, position, rise, gain);
                    AddPulse(envelope, position, envelopeRise, Mathf.Abs(gain));
                }
            }
        }

        /// <summary>Suma un pulso con forma de subida rápida y caída exponencial (t·e^(1-t)), con pico 1 en t = rise.</summary>
        static void AddPulse(float[] buffer, float position, float rise, float gain)
        {
            int length = buffer.Length;
            int start = Mathf.CeilToInt(position);
            int span = Mathf.CeilToInt(rise * 10f);
            for (int n = 0; n < span; n++)
            {
                float t = (start + n - position) / rise;
                int index = ((start + n) % length + length) % length;
                buffer[index] += gain * t * Mathf.Exp(1f - t);
            }
        }

        static float[] ExhaustLoop(EngineRecipe recipe, float[] pulses, float[] noise, bool accelerating)
        {
            int length = pulses.Length;
            var excitation = new float[length];
            float noiseGain = recipe.ExhaustNoise * (accelerating ? 1f : 1.8f);
            for (int i = 0; i < length; i++) excitation[i] = pulses[i] + noise[i] * noiseGain;

            float[] output = AudioSynthesis.FilterLoop(pulses, AudioSynthesis.Biquad.LowPass(350f));
            for (int i = 0; i < length; i++) output[i] *= recipe.ExhaustBody;
            foreach (Resonance resonance in recipe.Exhaust)
                AudioSynthesis.Mix(output, AudioSynthesis.FilterLoop(excitation, AudioSynthesis.Biquad.BandPass(resonance.Frequency, resonance.Q)), resonance.Gain);

            // La saturación se aplica con un nivel fijo de entrada, así el carácter no depende del volumen de la receta.
            AudioSynthesis.Normalize(output, 0.25f, 10f);
            AudioSynthesis.Saturate(output, recipe.Drive * (accelerating ? 1f : 0.55f));
            return output;
        }

        static float[] IntakeLoop(EngineRecipe recipe, float[] pulses, float[] noise)
        {
            int length = pulses.Length;
            var excitation = new float[length];
            for (int i = 0; i < length; i++) excitation[i] = noise[i] * recipe.IntakeNoise + pulses[i] * 0.25f;

            var output = new float[length];
            foreach (Resonance resonance in recipe.Intake)
                AudioSynthesis.Mix(output, AudioSynthesis.FilterLoop(excitation, AudioSynthesis.Biquad.BandPass(resonance.Frequency, resonance.Q)), resonance.Gain);

            output = AudioSynthesis.FilterLoop(output, AudioSynthesis.Biquad.HighPass(120f));
            AudioSynthesis.Normalize(output, 0.25f, 10f);
            AudioSynthesis.Saturate(output, 1.3f);
            return output;
        }

        /// <summary>Zumbido del block al ritmo de las explosiones, golpeteo de válvulas y silbido de engranajes.</summary>
        static float[] MechanicalLoop(EngineRecipe recipe, float[] pulses, int cycles, int length, float progress, System.Random random)
        {
            float[] output = AudioSynthesis.FilterLoop(pulses, AudioSynthesis.Biquad.LowPass(180f));
            AudioSynthesis.Mix(output, AudioSynthesis.FilterLoop(pulses, AudioSynthesis.Biquad.LowPass(600f)), 0.3f);
            AudioSynthesis.Normalize(output, 0.2f, 10f);

            if (recipe.ValveTicks > 0f)
            {
                // Dos válvulas que asientan por explosión, golpes muy cortos y desparejos.
                var ticks = new float[length];
                int events = cycles * recipe.FiringsPerCycle * 2;
                float spacing = length / (float)events;
                for (int i = 0; i < events; i++)
                    AddPulse(ticks, i * spacing + AudioSynthesis.Range(random, -0.05f, 0.05f) * spacing, 2.5f, AudioSynthesis.Range(random, 0.5f, 1f));

                float[] ring = AudioSynthesis.FilterLoop(ticks, AudioSynthesis.Biquad.BandPass(recipe.ValveTone, 5f));
                AudioSynthesis.Mix(ring, AudioSynthesis.FilterLoop(ticks, AudioSynthesis.Biquad.BandPass(recipe.ValveTone * 0.55f, 4f)), 0.6f);
                AudioSynthesis.Normalize(ring, 0.2f, 10f);
                AudioSynthesis.Mix(output, ring, recipe.ValveTicks);
            }

            if (recipe.WhineOrder > 0 && recipe.WhineGain > 0f)
            {
                // Un múltiplo entero de las vueltas completa ciclos enteros en el loop: no hay costura.
                float revolutions = cycles * recipe.CycleRevs;
                float gain = recipe.WhineGain * Mathf.Lerp(0.5f, 1f, progress) * 0.28f;
                for (int i = 0; i < length; i++)
                {
                    float phase = 2f * Mathf.PI * recipe.WhineOrder * revolutions * i / length;
                    output[i] += (Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 2f)) * gain;
                }
            }

            AudioSynthesis.Saturate(output, 1.2f);
            return output;
        }

        static void WriteOneShots(EngineRecipe recipe)
        {
            var random = new System.Random(Seed(recipe.Id, "fx", 0, true));
            for (int i = 0; i < OneShots.Pops.Length; i++)
                AudioSynthesis.WriteWav(OneShotPath(recipe, OneShots.Pops[i]), Pops(recipe, random, 0.4f, random.Next(1, 4), 0.03f, 0.9f));
            for (int i = 0; i < OneShots.Limiter.Length; i++)
                AudioSynthesis.WriteWav(OneShotPath(recipe, OneShots.Limiter[i]), Pops(recipe, random, 0.3f, random.Next(7, 12), 0.012f, 0.75f));
            AudioSynthesis.WriteWav(OneShotPath(recipe, OneShots.Shift), ShiftBrap(recipe, random));
            AudioSynthesis.WriteWav(OneShotPath(recipe, OneShots.TipIn), recipe.Turbo ? TurboSpool(random) : IntakeGasp(recipe, random));
            AudioSynthesis.WriteWav(OneShotPath(recipe, OneShots.TipOut), recipe.Turbo ? BlowOff(random) : ThrottleSnap(random));
        }

        /// <summary>
        /// Petardeos: combustible que explota en el escape. Cada uno es un chasquido de ruido con un golpe grave,
        /// pasado por las resonancias del escape de la familia.
        /// </summary>
        static float[] Pops(EngineRecipe recipe, System.Random random, float seconds, int count, float thumpDecay, float peak)
        {
            int length = AudioSynthesis.Samples(seconds);
            var excitation = new float[length];
            for (int p = 0; p < count; p++)
            {
                int start = AudioSynthesis.Samples(AudioSynthesis.Range(random, 0.005f, seconds * 0.7f));
                float gain = AudioSynthesis.Range(random, 0.4f, 1f);
                float thump = recipe.PopThump * AudioSynthesis.Range(random, 0.85f, 1.2f);
                for (int i = start; i < length; i++)
                {
                    float t = (i - start) / (float)AudioSynthesis.SampleRate;
                    float crack = (float)(random.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t / 0.0015f);
                    float body = Mathf.Sin(2f * Mathf.PI * thump * t) * Mathf.Exp(-t / thumpDecay);
                    excitation[i] += gain * (crack * 0.8f + body);
                }
            }

            float[] output = AudioSynthesis.Filter(excitation, AudioSynthesis.Biquad.LowPass(300f));
            foreach (Resonance resonance in recipe.Exhaust)
                AudioSynthesis.Mix(output, AudioSynthesis.Filter(excitation, AudioSynthesis.Biquad.BandPass(resonance.Frequency, resonance.Q)), resonance.Gain);
            AudioSynthesis.NormalizePeak(output, 1f);
            AudioSynthesis.Saturate(output, 2.5f);
            AudioSynthesis.FadeEdges(output, 0.001f, 0.03f);
            AudioSynthesis.NormalizePeak(output, peak);
            return output;
        }

        /// <summary>El "brap" de un cambio de doble embrague: una ráfaga corta de explosiones mientras se corta el encendido.</summary>
        static float[] ShiftBrap(EngineRecipe recipe, System.Random random)
        {
            int length = AudioSynthesis.Samples(0.16f);
            var excitation = new float[length];
            for (int p = 0; p < 5; p++)
            {
                float start = 0.004f + p * AudioSynthesis.Range(random, 0.022f, 0.03f);
                float gain = Mathf.Lerp(1f, 0.45f, p / 4f);
                int first = AudioSynthesis.Samples(start);
                for (int i = first; i < length; i++)
                {
                    float t = (i - first) / (float)AudioSynthesis.SampleRate;
                    float crack = (float)(random.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t / 0.002f);
                    excitation[i] += gain * (crack * 0.6f + Mathf.Sin(2f * Mathf.PI * recipe.PopThump * 1.3f * t) * Mathf.Exp(-t / 0.015f));
                }
            }

            float[] output = AudioSynthesis.Filter(excitation, AudioSynthesis.Biquad.LowPass(400f));
            foreach (Resonance resonance in recipe.Exhaust)
                AudioSynthesis.Mix(output, AudioSynthesis.Filter(excitation, AudioSynthesis.Biquad.BandPass(resonance.Frequency, resonance.Q)), resonance.Gain);
            AudioSynthesis.NormalizePeak(output, 1f);
            AudioSynthesis.Saturate(output, 3f);
            AudioSynthesis.FadeEdges(output, 0.001f, 0.02f);
            AudioSynthesis.NormalizePeak(output, 0.9f);
            return output;
        }

        /// <summary>Bocanada de aire al abrir la mariposa de golpe en un motor atmosférico.</summary>
        static float[] IntakeGasp(EngineRecipe recipe, System.Random random)
        {
            int length = AudioSynthesis.Samples(0.45f);
            float[] noise = AudioSynthesis.Noise(length, random);
            var output = new float[length];
            foreach (Resonance resonance in recipe.Intake)
                AudioSynthesis.Mix(output, AudioSynthesis.Filter(noise, AudioSynthesis.Biquad.BandPass(resonance.Frequency * 1.3f, resonance.Q * 0.6f)), resonance.Gain);
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)AudioSynthesis.SampleRate;
                output[i] *= (1f - Mathf.Exp(-t / 0.02f)) * Mathf.Exp(-t / 0.14f);
            }
            AudioSynthesis.FadeEdges(output, 0.002f, 0.05f);
            AudioSynthesis.NormalizePeak(output, 0.7f);
            return output;
        }

        /// <summary>Chasquido de la mariposa al cerrarse y el aire que rebota en la admisión.</summary>
        static float[] ThrottleSnap(System.Random random)
        {
            int length = AudioSynthesis.Samples(0.25f);
            float[] noise = AudioSynthesis.Noise(length, random);
            float[] air = AudioSynthesis.Filter(noise, AudioSynthesis.Biquad.BandPass(1200f, 1f));
            float[] click = AudioSynthesis.Filter(noise, AudioSynthesis.Biquad.BandPass(2800f, 3f));
            var output = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)AudioSynthesis.SampleRate;
                output[i] = air[i] * Mathf.Exp(-t / 0.06f) * 0.7f + click[i] * Mathf.Exp(-t / 0.004f);
            }
            AudioSynthesis.FadeEdges(output, 0.001f, 0.04f);
            AudioSynthesis.NormalizePeak(output, 0.6f);
            return output;
        }

        /// <summary>El turbo cargando: un silbido que sube de tono con un soplido detrás.</summary>
        static float[] TurboSpool(System.Random random)
        {
            int length = AudioSynthesis.Samples(0.9f);
            float[] whoosh = AudioSynthesis.Filter(AudioSynthesis.Noise(length, random), AudioSynthesis.Biquad.BandPass(2500f, 1.2f));
            var output = new float[length];
            float phase = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)AudioSynthesis.SampleRate;
                float frequency = Mathf.Lerp(1800f, 5500f, 1f - Mathf.Exp(-t / 0.35f));
                phase += 2f * Mathf.PI * frequency / AudioSynthesis.SampleRate;
                float swell = (1f - Mathf.Exp(-t / 0.15f)) * Mathf.Exp(-Mathf.Max(0f, t - 0.5f) / 0.15f);
                output[i] = (Mathf.Sin(phase) * 0.35f + whoosh[i] * 0.6f) * swell;
            }
            AudioSynthesis.FadeEdges(output, 0.005f, 0.08f);
            AudioSynthesis.NormalizePeak(output, 0.6f);
            return output;
        }

        /// <summary>Válvula de alivio del turbo: un soplido agudo con el aleteo del compresor frenando.</summary>
        static float[] BlowOff(System.Random random)
        {
            int length = AudioSynthesis.Samples(0.7f);
            float[] noise = AudioSynthesis.Noise(length, random);
            float[] hiss = AudioSynthesis.Filter(noise, AudioSynthesis.Biquad.HighPass(1500f));
            AudioSynthesis.Mix(hiss, AudioSynthesis.Filter(noise, AudioSynthesis.Biquad.BandPass(3500f, 2f)), 0.8f);
            var output = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)AudioSynthesis.SampleRate;
                float flutter = 1f - 0.45f * Mathf.Exp(-t / 0.25f) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 17f * t));
                output[i] = hiss[i] * (1f - Mathf.Exp(-t / 0.008f)) * Mathf.Exp(-t / 0.18f) * flutter;
            }
            AudioSynthesis.FadeEdges(output, 0.002f, 0.08f);
            AudioSynthesis.NormalizePeak(output, 0.65f);
            return output;
        }

        /// <summary>Semilla estable entre corridas y máquinas (el hash de string de .NET puede cambiar por proceso).</summary>
        static int Seed(string family, string source, int rpm, bool accelerating)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in family + "/" + source) hash = (hash ^ c) * 16777619;
                hash = (hash ^ (uint)rpm) * 16777619;
                hash = (hash ^ (accelerating ? 1u : 2u)) * 16777619;
                return (int)(hash & 0x7fffffff);
            }
        }
    }
}
