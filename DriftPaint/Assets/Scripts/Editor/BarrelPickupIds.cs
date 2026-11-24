using System.Collections.Generic;
using DriftPaint.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Mantiene únicos los ids de los pickups de barril. El id es lo que viaja por red, así que dos pickups con
    /// el mismo (por ejemplo al duplicar uno con Ctrl+D) hacen que el Master no reconozca el nuevo. Al guardar
    /// una escena y al entrar en Play, el primero en la jerarquía conserva su id y las copias reciben el
    /// siguiente libre. Los ids quedan guardados en la escena, iguales para todos los clientes.
    /// </summary>
    [InitializeOnLoad]
    public static class BarrelPickupIds
    {
        static BarrelPickupIds()
        {
            // Al guardar, el cambio entra en el archivo que se está escribiendo: no hace falta marcar la escena.
            EditorSceneManager.sceneSaving += (scene, _) => Fix(scene, markDirty: false);
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingEditMode) return;
                for (int i = 0; i < SceneManager.sceneCount; i++) Fix(SceneManager.GetSceneAt(i), markDirty: true);
            };
        }

        /// <summary>Renumera los ids repetidos de una escena. Devuelve cuántos cambió.</summary>
        public static int Fix(Scene scene, bool markDirty)
        {
            if (!scene.IsValid() || !scene.isLoaded) return 0;

            var pickups = new List<BarrelPickup>();
            foreach (GameObject root in scene.GetRootGameObjects())
                pickups.AddRange(root.GetComponentsInChildren<BarrelPickup>(true));

            int highest = 0;
            foreach (BarrelPickup pickup in pickups) highest = Mathf.Max(highest, pickup.Id);

            var used = new HashSet<int>();
            int changed = 0;
            foreach (BarrelPickup pickup in pickups)
            {
                if (pickup.Id > 0 && used.Add(pickup.Id)) continue;

                int id = ++highest;
                used.Add(id);
                var serialized = new SerializedObject(pickup);
                serialized.FindProperty("_id").intValue = id;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (markDirty) EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"El pickup {pickup.name} tenía un id repetido; ahora es {id}.", pickup);
                changed++;
            }
            return changed;
        }
    }
}
