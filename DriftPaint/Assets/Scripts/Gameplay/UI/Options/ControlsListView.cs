using System;
using System.Collections.Generic;
using DriftPaint.Input;
using TMPro;
using UnityEngine;

namespace DriftPaint.UI.Options
{
    /// <summary>
    /// Lista de controles del menú de opciones. Las filas viven en la escena con el nombre de cada acción
    /// escrito a mano; este componente completa las teclas y botones leyendo las asignaciones reales,
    /// así la lista nunca queda desactualizada si cambian.
    /// </summary>
    public class ControlsListView : MonoBehaviour
    {
        [Serializable]
        public struct Row
        {
            [Tooltip("Acción de teclado y mouse, como \"Driving/Fire\". Vacío: se deja el texto escrito en la escena.")]
            public string KeyboardAction;
            [Tooltip("Acción de joystick. Vacío: se usa la misma que la de teclado.")]
            public string GamepadAction;
            public TMP_Text KeyboardText;
            public TMP_Text GamepadText;
        }

        [SerializeField] List<Row> _rows = new List<Row>();

        void OnEnable() { GameLanguage.Changed += Refresh; Refresh(); }
        void OnDisable() => GameLanguage.Changed -= Refresh;

        public void Refresh()
        {
            foreach (Row row in _rows)
            {
                if (row.KeyboardText != null && !string.IsNullOrEmpty(row.KeyboardAction))
                    row.KeyboardText.text = DriftPaintInputMaps.BindingsText(DriftPaintInputMaps.Find(row.KeyboardAction), false);

                string gamepadAction = string.IsNullOrEmpty(row.GamepadAction) ? row.KeyboardAction : row.GamepadAction;
                if (row.GamepadText != null && !string.IsNullOrEmpty(gamepadAction))
                    row.GamepadText.text = DriftPaintInputMaps.BindingsText(DriftPaintInputMaps.Find(gamepadAction), true);
            }
        }
    }
}
