using DriftPaint.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Crea los assets de ajustes de disparos y barriles (si no existen) y los conecta en la escena abierta
    /// y en el prefab del barril, sin regenerar la escena. Los valores ya editados se conservan.
    /// </summary>
    public static class CombatSettingsBuilder
    {
        const string ProjectileSettingsPath = ScriptableObjectPaths.ProjectileSettings;
        const string BarrelSettingsPath = ScriptableObjectPaths.BarrelSettings;
        const string BarrelPrefabPath = "Assets/Prefabs/Combat/" + BarrelSystem.PrefabId + ".prefab";

        public static ProjectileSettingsSO LoadOrCreateProjectileSettings() =>
            EditorAssetUtility.LoadOrCreate(ProjectileSettingsPath, ScriptableObject.CreateInstance<ProjectileSettingsSO>);

        public static BarrelSettingsSO LoadOrCreateBarrelSettings() =>
            EditorAssetUtility.LoadOrCreate(BarrelSettingsPath, ScriptableObject.CreateInstance<BarrelSettingsSO>);

        [MenuItem("DriftPaint/Conectar ajustes de combate")]
        public static void ConnectInOpenScene()
        {
            ProjectileSettingsSO projectile = LoadOrCreateProjectileSettings();
            BarrelSettingsSO barrel = LoadOrCreateBarrelSettings();

            int bound = 0;
            foreach (WeaponSystem weapons in Object.FindObjectsByType<WeaponSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                MenuSceneBuilder.Bind(weapons, ("_settings", projectile));
                bound++;
            }
            foreach (BarrelSystem barrels in Object.FindObjectsByType<BarrelSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                MenuSceneBuilder.Bind(barrels, ("_settings", barrel));
                bound++;
            }
            if (bound > 0) EditorSceneManager.MarkAllScenesDirty();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BarrelPrefabPath);
            if (prefab != null && prefab.TryGetComponent(out BarrelView view))
            {
                MenuSceneBuilder.Bind(view, ("_settings", barrel));
                PrefabUtility.SavePrefabAsset(prefab);
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = projectile;
            Debug.Log(bound > 0
                ? $"Ajustes de combate conectados en {bound} sistemas. Guardá la escena para conservarlos."
                : "Ajustes de combate creados. Abrí la escena de partida y volvé a usar el menú para conectarlos.");
        }
    }
}
