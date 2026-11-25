using System.Text.RegularExpressions;
using DriftPaint.UI.Menu;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Agrega las flechas para elegir mapa en la tarjeta del mapa del lobby: una a cada lado de la imagen,
    /// por encima de la barra con el nombre. Quedan como objetos de la escena, editables a mano.
    /// </summary>
    public static class LobbyMapSelectorBuilder
    {
        const string PreviousName = "Map Previous";
        const string NextName = "Map Next";
        static readonly Vector2 ArrowSize = new Vector2(48f, 64f);
        const float ArrowInset = 10f;
        // Sube las flechas para que queden sobre la imagen y no tapen el nombre del mapa.
        const float ArrowLift = 26f;

        /// <summary>El lobby muestra el mapa pero todavía no tiene las flechas conectadas.</summary>
        public static bool Pending(string sceneText) =>
            Regex.IsMatch(sceneText, @"_mapText: \{fileID: [1-9]") &&
            !Regex.IsMatch(sceneText, @"_mapNext: \{fileID: [1-9]");

        public static void Install(Scene scene)
        {
            MenuSceneBuilder.LoadAssets();
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (LobbyScreen lobby in root.GetComponentsInChildren<LobbyScreen>(true))
                Connect(lobby);
        }

        /// <summary>Se puede llamar varias veces: si las flechas ya existen, solo las conecta.</summary>
        public static void Connect(LobbyScreen lobby)
        {
            var data = new SerializedObject(lobby);
            if (data.FindProperty("_mapText").objectReferenceValue is not TMP_Text mapText) return;

            // La tarjeta es el abuelo del nombre: Map Preview / Name Bar / Map Name.
            Transform card = mapText.transform.parent != null ? mapText.transform.parent.parent : null;
            if (card == null) return;

            Button previous = FindOrCreate(card, PreviousName, "<", new Vector2(0f, 0.5f), new Vector2(ArrowInset, ArrowLift),
                new Vector2(0f, 0.5f));
            Button next = FindOrCreate(card, NextName, ">", new Vector2(1f, 0.5f), new Vector2(-ArrowInset, ArrowLift),
                new Vector2(1f, 0.5f));

            data.FindProperty("_mapPrevious").objectReferenceValue = previous;
            data.FindProperty("_mapNext").objectReferenceValue = next;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static Button FindOrCreate(Transform card, string name, string label, Vector2 anchor, Vector2 position, Vector2 pivot)
        {
            Transform existing = card.Find(name);
            if (existing != null && existing.TryGetComponent(out Button found)) return found;

            Button button = MenuSceneBuilder.CreateButton(card, name, label, MenuSceneBuilder.ButtonStyle.Dark, 40f, anchor, position,
                ArrowSize, pivot);
            // Solo las ve el host: el lobby las prende al refrescar la sala.
            button.gameObject.SetActive(false);
            EditorUtility.SetDirty(button.gameObject);
            return button;
        }
    }
}
