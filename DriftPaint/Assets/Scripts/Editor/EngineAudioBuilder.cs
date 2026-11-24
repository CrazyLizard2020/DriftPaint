using System.Linq;
using AroundTheGroundSimulator;
using DriftPaint.Cars;
using UnityEditor;
using UnityEngine;
using Source = DriftPaint.EditorTools.EngineBankGenerator.Source;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Arma el motor de cada modelo de auto: copia los datos base en un CarData propio del modelo, le carga
    /// la sección Motor (RPM y caja), crea su perfil de sonido con los bancos de su familia y conecta todo
    /// al prefab. Lo existente no se pisa: si alguien ya ajustó un perfil o un CarData, queda como está.
    /// </summary>
    public static class EngineAudioBuilder
    {
        /// <summary>Cómo suena y gira el motor de un modelo, a partir de cómo suena el auto real en que se inspira.</summary>
        class ModelEngine
        {
            public string Model;
            public EngineRecipe Recipe;
            public EngineSettings Settings;
            public int Cylinders = 4;
            public VehicleNoiseSynthesizer.CombustionCycleMode Cycle = VehicleNoiseSynthesizer.CombustionCycleMode.FourStroke;
            public System.Func<EngineSoundBankSO, EngineVoiceSettings> Exhaust;
            public System.Func<EngineSoundBankSO, EngineVoiceSettings> Intake;
            public System.Func<EngineSoundBankSO, EngineVoiceSettings> Mechanical;
            public float PopVolume, PopProbability, PopMinRpm;
            public float TipInVolume, TipOutVolume, LimiterVolume;
            public bool DctShifts;
            public float ShiftWobble;
            public bool AutoBlip, LaunchControl;
            public float RemoteMinDistance = 6f;
        }

        static AnimationCurve Linear(params float[] pairs)
        {
            var keys = new Keyframe[pairs.Length / 2];
            for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(pairs[i * 2], pairs[i * 2 + 1]);
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            return curve;
        }

        static readonly ModelEngine[] Models =
        {
            // Toyota AE86 (4A-GE 1.6, 16 válvulas, cuerpos de mariposa individuales): ralentí parejo, sube rápido
            // hasta 7600 y lo que más se oye arriba es la admisión aspirando.
            new ModelEngine
            {
                Model = "AE86",
                Recipe = EngineBankGenerator.InlineFour,
                Settings = new EngineSettings(900f, 7600f, 7800f, new[] { 0.2f, 0.36f, 0.54f, 0.76f, 1.08f }, 0.22f, 3500f, 16000f, 7000f),
                Exhaust = bank => new EngineVoiceSettings(bank, 0.8f, 1f, Linear(0f, 0.5f, 1f, 1f), 0.35f, 0.55f, 0.25f, 0.15f, 0.2f, 0.25f, 0f),
                Intake = bank => new EngineVoiceSettings(bank, 0.9f, 1f, Linear(0f, 0.15f, 0.5f, 0.45f, 1f, 1f), 0.2f, 0.5f, 0.1f, 0f, 0f, 0.1f, 0f),
                Mechanical = bank => new EngineVoiceSettings(bank, 0.35f, 1f, Linear(0f, 0.6f, 1f, 1f), 0.5f, 0.8f, 0f, 0f, 0f, 0f, 0f),
                PopVolume = 0.4f, PopProbability = 0.35f, PopMinRpm = 3800f,
                TipInVolume = 0.45f, TipOutVolume = 0.35f, LimiterVolume = 0.45f,
                ShiftWobble = 0.06f, AutoBlip = true
            },
            // Mazda RX-7 FD (13B-REW, dos rotores, biturbo): "brap" irregular en ralentí, zumbido agudo y liso
            // arriba, petardeos frecuentes, turbo silbando al cargar y válvula de alivio al soltar.
            new ModelEngine
            {
                Model = "RX7",
                Recipe = EngineBankGenerator.Rotary,
                Settings = new EngineSettings(850f, 8000f, 8200f, new[] { 0.19f, 0.35f, 0.53f, 0.75f, 1.07f }, 0.2f, 3800f, 18000f, 8000f),
                Cylinders = 2,
                Cycle = VehicleNoiseSynthesizer.CombustionCycleMode.TwoStroke,
                Exhaust = bank => new EngineVoiceSettings(bank, 0.9f, 1f, Linear(0f, 0.55f, 1f, 1f), 0.45f, 0.7f, 0.3f, 0.1f, 0.15f, 0.35f, 0.25f),
                Intake = bank => new EngineVoiceSettings(bank, 0.45f, 1f, Linear(0f, 0.2f, 1f, 0.8f), 0.3f, 0.5f, 0.3f, 0f, 0f, 0f, 0f),
                Mechanical = bank => new EngineVoiceSettings(bank, 0.35f, 1f, Linear(0f, 0.5f, 1f, 1f), 0.5f, 0.8f, 0f, 0f, 0f, 0f, 0f),
                PopVolume = 0.55f, PopProbability = 0.9f, PopMinRpm = 3000f,
                TipInVolume = 0.5f, TipOutVolume = 0.55f, LimiterVolume = 0.5f,
                ShiftWobble = 0.07f
            },
            // GC14 (cupé tipo Civic con VTEC): ralentí bajo, marchas cortas y, pasando las 5500, el cambio de
            // levas que dispara la admisión y vuelve el sonido más agudo y crudo hasta 8200.
            new ModelEngine
            {
                Model = "Gc14",
                Recipe = EngineBankGenerator.InlineFour,
                Settings = new EngineSettings(750f, 8200f, 8400f, new[] { 0.17f, 0.31f, 0.47f, 0.68f, 1.04f }, 0.18f, 4000f, 16000f, 7000f),
                Exhaust = bank => new EngineVoiceSettings(bank, 0.75f, 1.06f, Linear(0f, 0.5f, 1f, 1f), 0.3f, 0.5f, 0.2f, 0.25f, 0.3f, 0.2f, 0f),
                Intake = bank => new EngineVoiceSettings(bank, 1f, 1.06f, VtecCurve(bank, 750f, 8400f, 5500f), 0.2f, 0.5f, 0.1f, 0.1f, 0f, 0.15f, 0f),
                Mechanical = bank => new EngineVoiceSettings(bank, 0.3f, 1.06f, Linear(0f, 0.6f, 1f, 1f), 0.5f, 0.8f, 0f, 0f, 0f, 0f, 0f),
                PopVolume = 0.3f, PopProbability = 0.25f, PopMinRpm = 4500f,
                TipInVolume = 0.5f, TipOutVolume = 0.35f, LimiterVolume = 0.4f,
                ShiftWobble = 0.05f, AutoBlip = true
            },
            // CC96 (muscle car con V8 de cigüeñal cruzado): ralentí bajo y traqueteante, borboteo grave, pocas
            // marchas largas, corte temprano y mucho cuerpo; petardea grueso al soltar.
            new ModelEngine
            {
                Model = "CC96",
                Recipe = EngineBankGenerator.CrossplaneV8,
                Settings = new EngineSettings(700f, 6200f, 6400f, new[] { 0.3f, 0.52f, 0.76f, 1.1f }, 0.3f, 2400f, 9000f, 4500f),
                Cylinders = 8,
                Exhaust = bank => new EngineVoiceSettings(bank, 1f, 1f, Linear(0f, 0.6f, 1f, 1f), 0.55f, 0.65f, 0.4f, 0f, 0.5f, 0.45f, 0.1f),
                Intake = bank => new EngineVoiceSettings(bank, 0.35f, 1f, Linear(0f, 0.2f, 1f, 0.9f), 0.3f, 0.5f, 0.3f, 0f, 0f, 0f, 0f),
                Mechanical = bank => new EngineVoiceSettings(bank, 0.3f, 1f, Linear(0f, 0.6f, 1f, 1f), 0.5f, 0.8f, 0f, 0f, 0f, 0f, 0f),
                PopVolume = 0.6f, PopProbability = 0.6f, PopMinRpm = 2800f,
                TipInVolume = 0.35f, TipOutVolume = 0.25f, LimiterVolume = 0.5f,
                ShiftWobble = 0.1f, RemoteMinDistance = 8f
            },
            // A-R7 (deportivo tipo R8 con V10 5.2 atmosférico y doble embrague de 7): alarido agudo hasta 8500,
            // cambios rapidísimos con golpe de escape, golpe de gas al reducir y control de largada.
            new ModelEngine
            {
                Model = "A_R7",
                Recipe = EngineBankGenerator.V10,
                Settings = new EngineSettings(1000f, 8500f, 8700f, new[] { 0.13f, 0.22f, 0.33f, 0.46f, 0.61f, 0.8f, 1.06f }, 0.08f, 4500f, 15000f, 7500f),
                Cylinders = 10,
                Exhaust = bank => new EngineVoiceSettings(bank, 0.9f, 1f, Linear(0f, 0.45f, 1f, 1f), 0.35f, 0.6f, 0.2f, 0.2f, 0.25f, 0.3f, 0.1f),
                Intake = bank => new EngineVoiceSettings(bank, 0.7f, 1f, Linear(0f, 0.15f, 1f, 0.9f), 0.2f, 0.5f, 0.1f, 0f, 0f, 0.1f, 0f),
                Mechanical = bank => new EngineVoiceSettings(bank, 0.3f, 1f, Linear(0f, 0.6f, 1f, 1f), 0.5f, 0.8f, 0f, 0f, 0f, 0f, 0f),
                PopVolume = 0.45f, PopProbability = 0.55f, PopMinRpm = 3500f,
                TipInVolume = 0.4f, TipOutVolume = 0.3f, LimiterVolume = 0.5f,
                DctShifts = true, ShiftWobble = 0.05f, AutoBlip = true, LaunchControl = true
            }
        };

        /// <summary>
        /// Volumen de la admisión en un VTEC: discreta con las levas suaves y, al pasar el punto de cambio,
        /// el salto a las levas bravas. La curva del sintetizador va de ralentí a la RPM más alta entre el corte y el banco.
        /// </summary>
        static AnimationCurve VtecCurve(EngineSoundBankSO bank, float idle, float limiter, float vtecRpm)
        {
            float top = Mathf.Max(limiter, bank != null ? bank.HighestRpm : limiter);
            float point = Mathf.InverseLerp(idle, top, vtecRpm);
            return Linear(0f, 0.18f, point - 0.03f, 0.35f, point + 0.03f, 1f, 1f, 1f);
        }

        /// <summary>Falta algo: bancos, datos o perfil de algún modelo, o un prefab sin conectar.</summary>
        public static bool Outdated()
        {
            if (EngineBankGenerator.Outdated()) return true;
            foreach (ModelEngine model in Models)
            {
                var data = AssetDatabase.LoadAssetAtPath<CarDataSO>(ScriptableObjectPaths.CarDataFor(model.Model));
                if (data == null || data.EngineSound == null) return true;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(model.Model));
                if (prefab == null) continue;
                if (prefab.GetComponent<CarEngineAudio>() == null) return true;
                if (prefab.TryGetComponent(out CarController controller) && controller.CarData != data) return true;
            }
            return false;
        }

        public static void Apply()
        {
            if (EngineBankGenerator.Outdated()) EngineBankGenerator.GenerateAll();

            foreach (ModelEngine model in Models)
            {
                CarDataSO data = EnsureCarData(model);
                ConnectPrefab(model.Model, data);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>CarData del modelo. Si no existe, se copia del base para no perder el ajuste de la física.</summary>
        public static CarDataSO CarDataFor(string model, CarDataSO fallback)
        {
            var data = AssetDatabase.LoadAssetAtPath<CarDataSO>(ScriptableObjectPaths.CarDataFor(model));
            return data != null ? data : fallback;
        }

        static CarDataSO EnsureCarData(ModelEngine model)
        {
            string path = ScriptableObjectPaths.CarDataFor(model.Model);
            var data = AssetDatabase.LoadAssetAtPath<CarDataSO>(path);
            if (data == null)
            {
                EditorAssetUtility.EnsureFolder(ScriptableObjectPaths.CarsFolder);
                if (!AssetDatabase.CopyAsset(ScriptableObjectPaths.CarData, path))
                    AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<CarDataSO>(), path);
                data = AssetDatabase.LoadAssetAtPath<CarDataSO>(path);
                data.SetEngine(model.Settings, null);
            }

            if (data.EngineSound == null)
            {
                // Sin perfil, la sección Motor nunca se configuró: se carga la del modelo.
                data.SetEngine(model.Settings, EnsureProfile(model));
                EditorUtility.SetDirty(data);
            }
            return data;
        }

        static EngineSoundProfileSO EnsureProfile(ModelEngine model)
        {
            string path = ScriptableObjectPaths.EngineSoundFor(model.Model);
            var profile = AssetDatabase.LoadAssetAtPath<EngineSoundProfileSO>(path);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<EngineSoundProfileSO>();
            EngineRecipe recipe = model.Recipe;
            profile.SetVoices(
                model.Exhaust(EngineBankGenerator.LoadBank(recipe, Source.Exhaust)),
                model.Intake(EngineBankGenerator.LoadBank(recipe, Source.Intake)),
                model.Mechanical(EngineBankGenerator.LoadBank(recipe, Source.Mechanical)));
            profile.SetCombustion(model.Cylinders, model.Cycle);
            profile.SetPops(Clips(recipe, EngineBankGenerator.OneShots.Pops), model.PopVolume, model.PopProbability, model.PopMinRpm);
            profile.SetThrottleBody(
                Clips(recipe, EngineBankGenerator.OneShots.TipIn), model.TipInVolume,
                Clips(recipe, EngineBankGenerator.OneShots.TipOut), model.TipOutVolume);
            profile.SetLimiter(Clips(recipe, EngineBankGenerator.OneShots.Limiter), model.LimiterVolume);
            AudioClip shift = model.DctShifts ? EngineBankGenerator.LoadOneShot(recipe, EngineBankGenerator.OneShots.Shift) : null;
            profile.SetShifts(shift, 0.65f, model.ShiftWobble, model.AutoBlip, model.LaunchControl);
            profile.SetRemote(0.9f, model.RemoteMinDistance, 120f, 0.4f);

            EditorAssetUtility.EnsureFolder(ScriptableObjectPaths.CarsFolder);
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        static AudioClip[] Clips(EngineRecipe recipe, params string[] names) =>
            names.Select(name => EngineBankGenerator.LoadOneShot(recipe, name)).Where(clip => clip != null).ToArray();

        static string PrefabPath(string model) => $"{CarPrefabBuilder.PrefabFolder}/Car_{model}.prefab";

        static void ConnectPrefab(string model, CarDataSO data)
        {
            string path = PrefabPath(model);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;

            using var scope = new PrefabUtility.EditPrefabContentsScope(path);
            GameObject root = scope.prefabContentsRoot;
            if (root.TryGetComponent(out CarController controller))
            {
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("_carData").objectReferenceValue = data;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            if (root.GetComponent<CarEngineAudio>() == null) root.AddComponent<CarEngineAudio>();
        }
    }
}
