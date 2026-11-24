using System;
using System.IO;
using System.Linq;
using DriftPaint.Audio;
using DriftPaint.UI.Options;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Mantiene las escenas y los recursos del juego al día sin pasos manuales. Cada vez que Unity recarga
    /// los scripts revisa si a alguna escena le falta un componente que el código ya espera y, si falta,
    /// aplica el paso que lo agrega y guarda la escena. La revisión lee el archivo de la escena, así que
    /// solo se abre una escena cuando de verdad hay algo que agregar.
    /// <para>
    /// Para un cambio nuevo en una escena existente, se agrega un paso a <see cref="Steps"/> con los
    /// componentes que deben existir y la función que los crea. Para prefabs o recursos generados, se agrega
    /// un paso a <see cref="AssetSteps"/> con una revisión barata y la función que los actualiza. Las funciones
    /// tienen que poder correr más de una vez sin duplicar nada.
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class SceneAutoSetup
    {
        readonly struct Step
        {
            public readonly string ScenePath;
            public readonly Type[] Required;
            public readonly Action<Scene> Apply;
            /// <summary>Para cambios que no agregan componentes propios: decide leyendo el texto de la escena.</summary>
            public readonly Func<string, bool> Pending;
            public readonly string Label;

            public Step(string scenePath, Type[] required, Action<Scene> apply)
            {
                ScenePath = scenePath;
                Required = required;
                Apply = apply;
                Pending = null;
                Label = null;
            }

            public Step(string scenePath, string label, Func<string, bool> pending, Action<Scene> apply)
            {
                ScenePath = scenePath;
                Required = Array.Empty<Type>();
                Apply = apply;
                Pending = pending;
                Label = label;
            }
        }

        static readonly Step[] Steps =
        {
            new Step(MatchSceneBuilder.ScenePath, new[] { typeof(DriftPaint.Paint.PaintRingSystem) }, PaintRingBuilder.Install),
            new Step(MenuSceneBuilder.ScenePath, new[] { typeof(OptionsMenu), typeof(SceneMusic), typeof(DisplayOptions) }, OptionsMenuBuilder.Install),
            new Step(MatchSceneBuilder.ScenePath, new[] { typeof(OptionsMenu), typeof(SceneMusic), typeof(DisplayOptions) }, OptionsMenuBuilder.Install),
            new Step(MatchSceneBuilder.ScenePath, new[] { typeof(MatchSfx) }, SfxBuilder.InstallMatch),
            new Step(MatchSceneBuilder.ScenePath, new[] { typeof(DriftPaint.Combat.BarrelLook) }, BarrelArtBuilder.InstallPickups),
            new Step(RoyalDriftRoadMap.ScenePath, new[] { typeof(DriftPaint.Combat.BarrelLook) }, BarrelArtBuilder.InstallPickups),
            new Step(MatchSceneBuilder.ScenePath, "nombres centrados sobre los autos", NameplateBuilder.Pending, NameplateBuilder.Install),
            new Step(RoyalDriftRoadMap.ScenePath, "nombres centrados sobre los autos", NameplateBuilder.Pending, NameplateBuilder.Install),
            new Step(MenuSceneBuilder.ScenePath, new[] { typeof(LanguageOptions) }, LanguageMenuBuilder.Install),
            new Step(MatchSceneBuilder.ScenePath, new[] { typeof(LanguageOptions) }, LanguageMenuBuilder.Install),
            new Step(RoyalDriftRoadMap.ScenePath, new[] { typeof(LanguageOptions) }, LanguageMenuBuilder.Install),
            new Step(MatchSceneBuilder.ScenePath, "panel de habilidades que se adapta al idioma", AbilitiesLayoutBuilder.Pending, AbilitiesLayoutBuilder.Install),
            new Step(RoyalDriftRoadMap.ScenePath, "panel de habilidades que se adapta al idioma", AbilitiesLayoutBuilder.Pending, AbilitiesLayoutBuilder.Install),
            new Step(MenuSceneBuilder.ScenePath, "chat de sala desplazable", ChatScrollBuilder.Pending, ChatScrollBuilder.Install),
        };

        /// <summary>Recursos compartidos (prefabs, mallas generadas) que el código espera en una versión dada.</summary>
        readonly struct AssetStep
        {
            public readonly string Name;
            public readonly Func<bool> Outdated;
            public readonly Action Apply;

            public AssetStep(string name, Func<bool> outdated, Action apply)
            {
                Name = name;
                Outdated = outdated;
                Apply = apply;
            }
        }

        static readonly AssetStep[] AssetSteps =
        {
            new AssetStep("chorros de pintura de los autos", ArenaArtBuilder.ExhaustStreamsOutdated, ArenaArtBuilder.UpdateExhaustStreams),
            new AssetStep("motores de los autos", EngineAudioBuilder.Outdated, EngineAudioBuilder.Apply),
            new AssetStep("efectos de sonido", SfxBuilder.Outdated, SfxBuilder.Apply),
            new AssetStep("escena de Royal Drift Road", MatchSceneBuilder.RoyalDriftRoadMissing, MatchSceneBuilder.BuildRoyalDriftRoad),
            new AssetStep("modelo del barril", BarrelArtBuilder.Outdated, BarrelArtBuilder.Apply),
            new AssetStep("japonés y chino", CjkLanguageBuilder.Outdated, CjkLanguageBuilder.Apply),
        };

        static SceneAutoSetup()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += Run;
        }

        /// <summary>Entrada para Unity en modo batch (-executeMethod), con el editor cerrado.</summary>
        public static void RunFromCommandLine() => Run();

        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // En Play no se tocan escenas: se revisa al volver al modo edición.
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Run;
                return;
            }

            // Primero los recursos, así las escenas que se abran después ya los ven actualizados.
            foreach (AssetStep step in AssetSteps)
            {
                try
                {
                    if (!step.Outdated()) continue;
                    step.Apply();
                    Debug.Log($"Puesta al día aplicada: {step.Name}.");
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            foreach (Step step in Steps)
            {
                if (!File.Exists(step.ScenePath)) continue;
                string text = File.ReadAllText(step.ScenePath);
                if (step.Pending != null)
                {
                    // Estos pasos se pueden repetir sin duplicar: si la escena abierta ya lo tiene sin guardar, no cambia nada.
                    if (step.Pending(text)) Apply(step, step.Label);
                    continue;
                }
                Type[] missing = step.Required.Where(type => !SceneUses(text, type)).ToArray();
                if (missing.Length == 0 || AlreadyInOpenScene(step)) continue;
                Apply(step, string.Join(", ", missing.Select(type => type.Name)));
            }
        }

        /// <summary>La escena está abierta con los componentes ya agregados, pero todavía sin guardar.</summary>
        static bool AlreadyInOpenScene(Step step)
        {
            Scene scene = SceneManager.GetSceneByPath(step.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) return false;
            GameObject[] roots = scene.GetRootGameObjects();
            return step.Required.All(type => roots.Any(root => root.GetComponentInChildren(type, true) != null));
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Run();
        }

        /// <summary>La escena guardada tiene algún componente de ese tipo: su script aparece referenciado en el archivo.</summary>
        static bool SceneUses(string sceneText, Type type)
        {
            string guid = ScriptGuid(type);
            return guid != null && sceneText.Contains(guid);
        }

        static string ScriptGuid(Type type)
        {
            foreach (MonoScript script in MonoImporter.GetAllRuntimeMonoScripts())
                if (script != null && script.GetClass() == type)
                    return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script));
            return null;
        }

        static void Apply(Step step, string names)
        {
            Scene scene = SceneManager.GetSceneByPath(step.ScenePath);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            bool hadUnsavedChanges = wasOpen && scene.isDirty;
            Scene previousActive = SceneManager.GetActiveScene();

            if (!wasOpen) scene = EditorSceneManager.OpenScene(step.ScenePath, OpenSceneMode.Additive);
            // Los generadores crean los objetos en la escena activa.
            SceneManager.SetActiveScene(scene);
            try
            {
                step.Apply(scene);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            }

            if (hadUnsavedChanges)
            {
                // No se guardan cambios ajenos sin que el usuario los vea: la escena queda marcada para guardar.
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"Se agregó {names} a {step.ScenePath}. La escena tenía cambios sin guardar, así que quedó sin guardar.");
            }
            else
            {
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"Se agregó {names} a {step.ScenePath} y se guardó la escena.");
            }

            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
        }
    }
}
