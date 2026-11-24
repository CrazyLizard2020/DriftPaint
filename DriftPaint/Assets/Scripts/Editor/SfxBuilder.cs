using System;
using System.IO;
using System.Linq;
using DriftPaint.Audio;
using DriftPaint.Cars;
using DriftPaint.Combat;
using DriftPaint.Match;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Synth = DriftPaint.EditorTools.AudioSynthesis;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Fabrica los efectos de sonido de la partida, arma su biblioteca y los conecta: el reproductor de
    /// efectos en la escena de partida y el chirrido de neumÃ¡ticos en cada prefab de auto. Los sonidos
    /// existentes no se regeneran; para rehacer uno se borra su WAV.
    /// </summary>
    public static class SfxBuilder
    {
        const string Folder = GeneratedAudioImport.EffectsFolder;
        const string ObjectName = "Efectos de sonido";

        /// <summary>
        /// Versión de los sonidos generados. Al subirla se rehacen los que cambiaron, sobre el mismo archivo:
        /// el WAV conserva su .meta y las referencias de la biblioteca siguen apuntando a él.
        /// </summary>
        const int Version = 3;
        /// <summary>Desde esta versión el disparo tiene chasquido y golpe medio al arrancar.</summary>
        const int PunchierShotVersion = 2;
        /// <summary>Desde esta versión el disparo usa los clips grabados de la carpeta <see cref="RecordedShotsFolder"/>.</summary>
        const int RecordedShotsVersion = 3;
        const float PreviousShotVolume = 0.55f;
        const float ShotVolume = 0.65f;
        const float ShotPitchVariation = 0.05f;

        /// <summary>Disparos grabados a mano. Si la carpeta tiene clips, reemplazan a los generados.</summary>
        public const string RecordedShotsFolder = Folder + "/paint_shots";

        static string Wav(string name) => $"{Folder}/{name}.wav";

        static readonly string[] Shots = { "Shot_0", "Shot_1" };
        static readonly string[] Impacts = { "Impact_0", "Impact_1", "Impact_2" };
        static readonly string[] Explosions = { "Explosion_0", "Explosion_1" };
        const string BarrelDrop = "BarrelDrop";
        const string CountdownTick = "CountdownTick";
        const string CountdownGo = "CountdownGo";
        const string TireSqueal = "TireSqueal";
        const string DriftBoost = "DriftBoost";

        static string[] AllNames => Shots.Concat(Impacts).Concat(Explosions)
            .Concat(new[] { BarrelDrop, CountdownTick, CountdownGo, TireSqueal, DriftBoost }).ToArray();

        /// <summary>Falta algÃºn sonido, la biblioteca o el chirrido en algÃºn prefab de auto.</summary>
        public static bool Outdated()
        {
            if (AllNames.Any(name => !File.Exists(Wav(name)))) return true;
            var library = AssetDatabase.LoadAssetAtPath<SfxLibrarySO>(ScriptableObjectPaths.SfxLibrary);
            if (library == null || !library.Shot.HasClips || !library.RingCounter.HasClips ||
                !library.RingWin.HasClips || library.GeneratorVersion < Version) return true;
            foreach (CarPrefabBuilder.CarModelSpec spec in CarPrefabBuilder.Models)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CarPrefabBuilder.PrefabFolder}/Car_{spec.Name}.prefab");
                if (prefab != null && prefab.GetComponent<CarTireAudio>() == null) return true;
            }
            return false;
        }

        public static void Apply()
        {
            var existing = AssetDatabase.LoadAssetAtPath<SfxLibrarySO>(ScriptableObjectPaths.SfxLibrary);
            int generated = existing != null ? existing.GeneratorVersion : Version;
            GenerateMissing(remakeShots: generated < PunchierShotVersion);
            SfxLibrarySO library = EnsureLibrary();
            if (library != null && library.GeneratorVersion < Version)
            {
                if (generated < PunchierShotVersion) RaiseShotVolume(library);
                if (generated < RecordedShotsVersion) UseRecordedShots(library);
                library.MarkGenerated(Version);
                EditorUtility.SetDirty(library);
            }
            foreach (CarPrefabBuilder.CarModelSpec spec in CarPrefabBuilder.Models)
                ConnectCar($"{CarPrefabBuilder.PrefabFolder}/Car_{spec.Name}.prefab");
            AssetDatabase.SaveAssets();
            if (library == null) Debug.LogWarning("SfxBuilder: no se pudo crear la biblioteca de efectos.");
        }

        /// <summary>Agrega el reproductor de efectos a la escena de partida activa y lo conecta con sus sistemas.</summary>
        public static void InstallMatch(Scene scene)
        {
            if (Outdated()) Apply();

            GameObject[] roots = scene.GetRootGameObjects();
            MatchSfx sfx = roots.Select(r => r.GetComponentInChildren<MatchSfx>(true)).FirstOrDefault(c => c != null);
            if (sfx == null)
            {
                var host = new GameObject(ObjectName);
                SceneManager.MoveGameObjectToScene(host, scene);
                sfx = host.AddComponent<MatchSfx>();
            }

            var serialized = new SerializedObject(sfx);
            serialized.FindProperty("_library").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SfxLibrarySO>(ScriptableObjectPaths.SfxLibrary);
            serialized.FindProperty("_match").objectReferenceValue = Find<MatchController>(roots);
            serialized.FindProperty("_weapons").objectReferenceValue = Find<WeaponSystem>(roots);
            serialized.FindProperty("_barrels").objectReferenceValue = Find<BarrelSystem>(roots);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static T Find<T>(GameObject[] roots) where T : Component =>
            roots.Select(r => r.GetComponentInChildren<T>(true)).FirstOrDefault(c => c != null);

        /// <summary>Agrega el chirrido de neumÃ¡ticos al prefab de un auto, si le falta.</summary>
        public static void ConnectCar(string prefabPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) return;
            using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
            ConfigureTires(scope.prefabContentsRoot);
        }

        /// <summary>Deja el chirrido de neumÃ¡ticos configurado en un auto (prefab abierto o en construcciÃ³n).</summary>
        public static void ConfigureTires(GameObject car)
        {
            CarTireAudio tires = car.GetComponent<CarTireAudio>();
            if (tires == null) tires = car.AddComponent<CarTireAudio>();
            tires.SetClips(Load(TireSqueal), Load(DriftBoost));
        }

        static AudioClip Load(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Wav(name));

        static AudioClip[] LoadAll(string[] names) => names.Select(Load).Where(c => c != null).ToArray();

        static SfxLibrarySO EnsureLibrary()
        {
            SfxLibrarySO library = EditorAssetUtility.LoadOrCreate(ScriptableObjectPaths.SfxLibrary, ScriptableObject.CreateInstance<SfxLibrarySO>);
            if (!library.RingCounter.HasClips || !library.RingWin.HasClips)
            {
                library.FillRingClips(Load("ring/ring_counter_acquired"), Load("ring/ring_win"));
                EditorUtility.SetDirty(library);
            }
            if (library.Shot.HasClips) return library;

            AudioClip[] recorded = RecordedShots();
            library.Set(
                new SfxClip(recorded.Length > 0 ? recorded : LoadAll(Shots), ShotVolume, ShotPitchVariation, SfxSelection.Shuffle),
                new SfxClip(LoadAll(Impacts), 0.8f, 0.08f),
                new SfxClip(LoadAll(Explosions), 1f, 0.05f),
                new SfxClip(new[] { Load(BarrelDrop) }, 0.7f, 0.08f),
                new SfxClip(new[] { Load(CountdownTick) }, 0.6f, 0f),
                new SfxClip(new[] { Load(CountdownGo) }, 0.7f, 0f));
            EditorUtility.SetDirty(library);
            return library;
        }

        /// <summary>
        /// El disparo nuevo pierde algo de cuerpo al normalizar porque el golpe del arranque ocupa el pico:
        /// se compensa con el volumen, solo si todavía estaba en el valor original (no se pisa un ajuste a mano).
        /// </summary>
        static void RaiseShotVolume(SfxLibrarySO library)
        {
            var serialized = new SerializedObject(library);
            SerializedProperty volume = serialized.FindProperty("_shot._volume");
            if (volume == null || !Mathf.Approximately(volume.floatValue, PreviousShotVolume)) return;
            volume.floatValue = ShotVolume;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Clips de la carpeta de disparos grabados, ordenados por nombre.</summary>
        static AudioClip[] RecordedShots()
        {
            if (!AssetDatabase.IsValidFolder(RecordedShotsFolder)) return new AudioClip[0];
            return AssetDatabase.FindAssets("t:AudioClip", new[] { RecordedShotsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                .Where(clip => clip != null)
                .ToArray();
        }

        /// <summary>
        /// El disparo pasa a los clips grabados, mezclados para que no se repita uno seguido y con una leve
        /// variación de tono. Conserva el volumen que ya tenía la biblioteca.
        /// </summary>
        static void UseRecordedShots(SfxLibrarySO library)
        {
            AudioClip[] recorded = RecordedShots();
            if (recorded.Length == 0) return;

            var serialized = new SerializedObject(library);
            SerializedProperty clips = serialized.FindProperty("_shot._clips");
            clips.arraySize = recorded.Length;
            for (int i = 0; i < recorded.Length; i++)
                clips.GetArrayElementAtIndex(i).objectReferenceValue = recorded[i];
            serialized.FindProperty("_shot._selection").enumValueIndex = (int)SfxSelection.Shuffle;
            serialized.FindProperty("_shot._pitchVariation").floatValue = ShotPitchVariation;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void GenerateMissing(bool remakeShots)
        {
            var random = new System.Random(4242);
            Write(Shots[0], () => Shot(random, 1f), remakeShots);
            Write(Shots[1], () => Shot(random, 0.9f), remakeShots);
            Write(Impacts[0], () => Impact(random, 1f));
            Write(Impacts[1], () => Impact(random, 0.85f));
            Write(Impacts[2], () => Impact(random, 1.15f));
            Write(Explosions[0], () => Explosion(random, 1f));
            Write(Explosions[1], () => Explosion(random, 0.85f));
            Write(BarrelDrop, () => Clunk(random));
            Write(CountdownTick, () => Beep(880f, 0.14f, 0.15f));
            Write(CountdownGo, () => Beep(1320f, 0.5f, 0.45f));
            Write(TireSqueal, () => Squeal(random));
            Write(DriftBoost, () => Boost(random));
            Synth.ImportWritten();
        }

        static void Write(string name, Func<float[]> make, bool replace = false)
        {
            if (File.Exists(Wav(name)) && !replace) return;
            Synth.WriteWav(Wav(name), make());
        }

        static float Seconds(int i) => i / (float)Synth.SampleRate;

        /// <summary>
        /// Cañón de pintura. El cuerpo es un golpe grave que cae de tono con un sub que le da peso, más el aire
        /// y el "splurt" húmedo. Encima, al principio, van las capas que le dan impacto: un chasquido agudo de
        /// pocos milisegundos y un golpe medio que se oye también en parlantes chicos. El cuerpo se satura
        /// por separado y las capas de ataque se suman después, para que el arranque sobresalga del resto.
        /// </summary>
        static float[] Shot(System.Random random, float pitch)
        {
            int length = Synth.Samples(0.4f);
            float[] noise = Synth.Noise(length, random);
            float[] air = Synth.Filter(noise, Synth.Biquad.BandPass(900f * pitch, 1.2f));
            float[] wet = Synth.Filter(noise, Synth.Biquad.BandPass(420f * pitch, 3f));
            float[] crack = Synth.Filter(Synth.Filter(noise, Synth.Biquad.HighPass(2800f * pitch)), Synth.Biquad.HighPass(2800f * pitch));
            float[] knock = Synth.Filter(noise, Synth.Biquad.BandPass(650f * pitch, 1.4f));

            var body = new float[length];
            float kickPhase = 0f;
            float subPhase = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = Seconds(i);
                // Arranca agudo y cae enseguida: el barrido rápido es lo que se siente como "patada".
                float kickFrequency = Mathf.Lerp(58f, 320f, Mathf.Exp(-t / 0.012f)) * pitch;
                kickPhase += 2f * Mathf.PI * kickFrequency / Synth.SampleRate;
                // El golpe pega fuerte al principio y se apaga rápido: así el arranque se despega de la cola.
                float accent = 1f + 1.2f * Mathf.Exp(-t / 0.018f);
                float kick = Mathf.Sin(kickPhase) * Mathf.Exp(-t / 0.065f) * accent;
                subPhase += 2f * Mathf.PI * 46f * pitch / Synth.SampleRate;
                float sub = Mathf.Sin(subPhase) * (1f - Mathf.Exp(-t / 0.006f)) * Mathf.Exp(-t / 0.1f) * 0.6f;
                float wobble = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 38f * t);
                body[i] = kick + sub + air[i] * Mathf.Exp(-t / 0.025f) * 0.6f + wet[i] * Mathf.Exp(-t / 0.07f) * wobble * 0.45f;
            }
            Synth.Saturate(body, 1.5f);

            var output = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = Seconds(i);
                float snap = (1f - Mathf.Exp(-t / 0.0003f)) * Mathf.Exp(-t / 0.005f);
                float punch = (1f - Mathf.Exp(-t / 0.0006f)) * Mathf.Exp(-t / 0.015f);
                output[i] = body[i] * 0.6f + crack[i] * snap * 2.2f + knock[i] * punch * 1.8f;
            }
            Synth.Saturate(output, 1.1f);
            Synth.FadeEdges(output, 0.0003f, 0.06f);
            Synth.NormalizePeak(output, 0.95f);
            return output;
        }

        /// <summary>Pintura que revienta: golpe sordo, salpicÃ³n y gotas que caen alrededor.</summary>
        static float[] Impact(System.Random random, float pitch)
        {
            int length = Synth.Samples(0.6f);
            float[] noise = Synth.Noise(length, random);
            float[] splash = Synth.Filter(noise, Synth.Biquad.LowPass(2600f * pitch));
            var drops = new float[length];
            for (int d = 0; d < 14; d++)
            {
                int start = Synth.Samples(Synth.Range(random, 0.03f, 0.45f));
                float frequency = Synth.Range(random, 900f, 2400f) * pitch;
                float gain = Synth.Range(random, 0.15f, 0.45f);
                for (int i = start; i < Mathf.Min(length, start + Synth.Samples(0.03f)); i++)
                {
                    float t = Seconds(i - start);
                    drops[i] += Mathf.Sin(2f * Mathf.PI * frequency * (1f + t * 6f) * t) * Mathf.Exp(-t / 0.006f) * gain;
                }
            }
            var output = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = Seconds(i);
                float thud = Mathf.Sin(2f * Mathf.PI * 90f * pitch * t) * Mathf.Exp(-t / 0.06f);
                output[i] = thud + splash[i] * (1f - Mathf.Exp(-t / 0.002f)) * Mathf.Exp(-t / 0.08f) + drops[i];
            }
            Synth.Saturate(output, 1.4f);
            Synth.FadeEdges(output, 0.001f, 0.08f);
            Synth.NormalizePeak(output, 0.85f);
            return output;
        }

        /// <summary>Barril que explota: estampido grave largo, crepitar y una lluvia de pintura.</summary>
        static float[] Explosion(System.Random random, float pitch)
        {
            int length = Synth.Samples(1.5f);
            float[] noise = Synth.Noise(length, random);
            float[] rumble = Synth.Filter(Synth.Filter(noise, Synth.Biquad.LowPass(500f * pitch)), Synth.Biquad.LowPass(500f * pitch));
            float[] hiss = Synth.Filter(noise, Synth.Biquad.HighPass(2500f));
            var output = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = Seconds(i);
                float boom = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(38f, 75f, Mathf.Exp(-t / 0.08f)) * pitch * t) * Mathf.Exp(-t / 0.25f);
                float crackle = random.NextDouble() < 0.004 * Mathf.Exp(-t / 0.4f) ? Synth.Range(random, -1f, 1f) : 0f;
                output[i] = boom * 1.2f
                            + rumble[i] * 6f * (1f - Mathf.Exp(-t / 0.004f)) * Mathf.Exp(-t / 0.45f)
                            + hiss[i] * 0.25f * Mathf.Exp(-t / 0.12f)
                            + crackle;
            }
            Synth.Saturate(output, 2.2f);
            Synth.FadeEdges(output, 0.001f, 0.3f);
            Synth.NormalizePeak(output, 0.95f);
            return output;
        }

        /// <summary>Barril de metal que cae al piso: parciales inarmÃ³nicos de chapa y un golpe seco.</summary>
        static float[] Clunk(System.Random random)
        {
            int length = Synth.Samples(0.55f);
            float[] partials = { 210f, 547f, 884f, 1333f, 1910f };
            float[] noise = Synth.Filter(Synth.Noise(length, random), Synth.Biquad.BandPass(1800f, 1.5f));
            var output = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = Seconds(i);
                float metal = 0f;
                for (int p = 0; p < partials.Length; p++)
                    metal += Mathf.Sin(2f * Mathf.PI * partials[p] * t) * Mathf.Exp(-t / (0.18f / (1f + p * 0.6f))) / (1f + p * 0.5f);
                float thud = Mathf.Sin(2f * Mathf.PI * 110f * t) * Mathf.Exp(-t / 0.04f);
                output[i] = metal * 0.6f + thud + noise[i] * Mathf.Exp(-t / 0.01f) * 0.8f;
            }
            Synth.FadeEdges(output, 0.001f, 0.1f);
            Synth.NormalizePeak(output, 0.8f);
            return output;
        }

        /// <summary>Pitido de la cuenta regresiva: tono con un armÃ³nico, ataque rÃ¡pido y cola corta.</summary>
        static float[] Beep(float frequency, float seconds, float sustain)
        {
            int length = Synth.Samples(seconds);
            var output = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = Seconds(i);
                float envelope = Mathf.Min(1f, t / 0.004f) * (t < sustain * 0.6f ? 1f : Mathf.Exp(-(t - sustain * 0.6f) / (sustain * 0.25f)));
                output[i] = (Mathf.Sin(2f * Mathf.PI * frequency * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * frequency * t)) * envelope;
            }
            Synth.FadeEdges(output, 0.002f, 0.02f);
            Synth.NormalizePeak(output, 0.7f);
            return output;
        }

        /// <summary>
        /// Loop del chirrido de gomas: ruido por resonancias angostas (la goma vibrando contra el piso) con un
        /// temblor de agarre y suelta. Las modulaciones completan ciclos enteros y los filtros son circulares,
        /// asÃ­ el loop no tiene costura.
        /// </summary>
        static float[] Squeal(System.Random random)
        {
            int length = Synth.Samples(1f);
            float[] noise = Synth.Noise(length, random);
            var output = new float[length];
            Synth.Mix(output, Synth.FilterLoop(noise, Synth.Biquad.BandPass(1150f, 18f)), 1f);
            Synth.Mix(output, Synth.FilterLoop(noise, Synth.Biquad.BandPass(1720f, 16f)), 0.7f);
            Synth.Mix(output, Synth.FilterLoop(noise, Synth.Biquad.BandPass(2380f, 14f)), 0.45f);
            Synth.Mix(output, Synth.FilterLoop(noise, Synth.Biquad.BandPass(3300f, 4f)), 0.15f);
            for (int i = 0; i < length; i++)
            {
                float cycle = i / (float)length;
                float stickSlip = 0.7f + 0.2f * Mathf.Sin(2f * Mathf.PI * 47f * cycle) + 0.1f * Mathf.Sin(2f * Mathf.PI * 7f * cycle);
                output[i] *= stickSlip;
            }
            Synth.RemoveDc(output);
            Synth.Normalize(output, 0.2f);
            return output;
        }

        /// <summary>Turbo de drift: soplido que sube de tono con un golpe grave al salir.</summary>
        static float[] Boost(System.Random random)
        {
            int length = Synth.Samples(0.8f);
            float[] noise = Synth.Noise(length, random);
            var output = new float[length];
            var filter = new float[length];
            // El centro del filtro sube por tramos cortos: un barrido sin calcular el filtro en cada muestra.
            const int block = 128;
            float x1 = 0f, x2 = 0f, y1 = 0f, y2 = 0f;
            for (int start = 0; start < length; start += block)
            {
                float t = Seconds(start);
                Synth.Biquad f = Synth.Biquad.BandPass(Mathf.Lerp(500f, 3200f, Mathf.Sqrt(Mathf.Clamp01(t / 0.5f))), 2.5f);
                for (int i = start; i < Mathf.Min(length, start + block); i++)
                {
                    float x = noise[i];
                    float y = f.B0 * x + f.B1 * x1 + f.B2 * x2 - f.A1 * y1 - f.A2 * y2;
                    x2 = x1; x1 = x; y2 = y1; y1 = y;
                    filter[i] = y;
                }
            }
            for (int i = 0; i < length; i++)
            {
                float t = Seconds(i);
                float swell = (1f - Mathf.Exp(-t / 0.05f)) * Mathf.Exp(-t / 0.3f);
                float kick = Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t / 0.08f);
                output[i] = filter[i] * swell * 2f + kick * 0.8f;
            }
            Synth.FadeEdges(output, 0.002f, 0.12f);
            Synth.NormalizePeak(output, 0.8f);
            return output;
        }
    }
}
