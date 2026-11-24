using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Herramientas para fabricar sonidos por código: filtros, saturación, normalización y escritura de WAV.
    /// Los filtros tienen una versión circular para loops: recorren la señal dos veces y se quedan con la
    /// segunda vuelta, así el final empalma con el principio sin clic.
    /// </summary>
    public static class AudioSynthesis
    {
        public const int SampleRate = 32000;

        /// <summary>Filtro de segundo orden (fórmulas de R. Bristow-Johnson).</summary>
        public struct Biquad
        {
            public float B0, B1, B2, A1, A2;

            public static Biquad BandPass(float frequency, float q)
            {
                Prepare(frequency, q, out float cos, out float alpha);
                return Normalize(alpha, 0f, -alpha, 1f + alpha, -2f * cos, 1f - alpha);
            }

            public static Biquad LowPass(float frequency, float q = 0.707f)
            {
                Prepare(frequency, q, out float cos, out float alpha);
                return Normalize((1f - cos) * 0.5f, 1f - cos, (1f - cos) * 0.5f, 1f + alpha, -2f * cos, 1f - alpha);
            }

            public static Biquad HighPass(float frequency, float q = 0.707f)
            {
                Prepare(frequency, q, out float cos, out float alpha);
                return Normalize((1f + cos) * 0.5f, -(1f + cos), (1f + cos) * 0.5f, 1f + alpha, -2f * cos, 1f - alpha);
            }

            static void Prepare(float frequency, float q, out float cos, out float alpha)
            {
                float w0 = 2f * Mathf.PI * Mathf.Clamp(frequency, 10f, SampleRate * 0.45f) / SampleRate;
                cos = Mathf.Cos(w0);
                alpha = Mathf.Sin(w0) / (2f * Mathf.Max(0.1f, q));
            }

            static Biquad Normalize(float b0, float b1, float b2, float a0, float a1, float a2) => new Biquad
            {
                B0 = b0 / a0, B1 = b1 / a0, B2 = b2 / a0, A1 = a1 / a0, A2 = a2 / a0
            };
        }

        /// <summary>Filtra un loop sin costura: el estado del filtro al final alimenta el principio.</summary>
        public static float[] FilterLoop(float[] input, Biquad f)
        {
            int length = input.Length;
            var output = new float[length];
            float x1 = 0f, x2 = 0f, y1 = 0f, y2 = 0f;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < length; i++)
                {
                    float x = input[i];
                    float y = f.B0 * x + f.B1 * x1 + f.B2 * x2 - f.A1 * y1 - f.A2 * y2;
                    x2 = x1; x1 = x; y2 = y1; y1 = y;
                    if (pass == 1) output[i] = y;
                }
            }
            return output;
        }

        /// <summary>Filtra un sonido suelto, que empieza y termina en silencio.</summary>
        public static float[] Filter(float[] input, Biquad f)
        {
            var output = new float[input.Length];
            float x1 = 0f, x2 = 0f, y1 = 0f, y2 = 0f;
            for (int i = 0; i < input.Length; i++)
            {
                float x = input[i];
                float y = f.B0 * x + f.B1 * x1 + f.B2 * x2 - f.A1 * y1 - f.A2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                output[i] = y;
            }
            return output;
        }

        /// <summary>Suma <paramref name="source"/> por <paramref name="gain"/> sobre <paramref name="target"/>.</summary>
        public static void Mix(float[] target, float[] source, float gain)
        {
            int count = Mathf.Min(target.Length, source.Length);
            for (int i = 0; i < count; i++) target[i] += source[i] * gain;
        }

        /// <summary>Saturación suave: redondea los picos como un parlante o un escape exigido.</summary>
        public static void Saturate(float[] samples, float drive)
        {
            if (drive <= 0f) return;
            float scale = 1f / (float)Math.Tanh(drive);
            for (int i = 0; i < samples.Length; i++)
                samples[i] = (float)Math.Tanh(samples[i] * drive) * scale;
        }

        /// <summary>Saca el promedio: un loop con continua mueve el parlante y suena a golpe al empezar.</summary>
        public static void RemoveDc(float[] samples)
        {
            double sum = 0;
            foreach (float s in samples) sum += s;
            float mean = (float)(sum / Mathf.Max(1, samples.Length));
            for (int i = 0; i < samples.Length; i++) samples[i] -= mean;
        }

        public static float Rms(float[] samples)
        {
            double sum = 0;
            foreach (float s in samples) sum += s * s;
            return (float)Math.Sqrt(sum / Mathf.Max(1, samples.Length));
        }

        public static float Peak(float[] samples)
        {
            float peak = 0f;
            foreach (float s in samples) peak = Mathf.Max(peak, Mathf.Abs(s));
            return peak;
        }

        /// <summary>Lleva el sonido a un volumen promedio dado, sin pasar del pico permitido.</summary>
        public static void Normalize(float[] samples, float targetRms, float maxPeak = 0.97f)
        {
            float rms = Rms(samples);
            if (rms <= 1e-6f) return;
            float gain = targetRms / rms;
            float peak = Peak(samples) * gain;
            if (peak > maxPeak) gain *= maxPeak / peak;
            for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
        }

        /// <summary>Lleva el pico a un valor dado. Para sonidos sueltos, cortos y con mucho silencio.</summary>
        public static void NormalizePeak(float[] samples, float targetPeak)
        {
            float peak = Peak(samples);
            if (peak <= 1e-6f) return;
            float gain = targetPeak / peak;
            for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
        }

        /// <summary>Rampas cortas al principio y al final para que un sonido suelto no haga clic.</summary>
        public static void FadeEdges(float[] samples, float fadeInSeconds, float fadeOutSeconds)
        {
            int fadeIn = Mathf.Min(samples.Length, Mathf.RoundToInt(fadeInSeconds * SampleRate));
            int fadeOut = Mathf.Min(samples.Length, Mathf.RoundToInt(fadeOutSeconds * SampleRate));
            for (int i = 0; i < fadeIn; i++) samples[i] *= (float)i / fadeIn;
            for (int i = 0; i < fadeOut; i++) samples[samples.Length - 1 - i] *= (float)i / fadeOut;
        }

        /// <summary>Ruido blanco de -1 a 1, siempre igual para la misma semilla.</summary>
        public static float[] Noise(int length, System.Random random)
        {
            var noise = new float[length];
            for (int i = 0; i < length; i++) noise[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            return noise;
        }

        public static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        public static int Samples(float seconds) => Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));

        /// <summary>Escribe un WAV mono de 16 bits. La ruta es relativa al proyecto (Assets/...).</summary>
        public static void WriteWav(string assetPath, float[] samples)
        {
            string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);
            int dataBytes = samples.Length * 2;
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);
            foreach (float s in samples)
                writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
        }

        /// <summary>Importa de una vez todos los WAV escritos. La configuración la pone <see cref="GeneratedAudioImport"/>.</summary>
        public static void ImportWritten() => AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }
}
