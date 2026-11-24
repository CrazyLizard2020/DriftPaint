using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace DriftPaint.UI.Options
{
    /// <summary>
    /// Desplegable de idioma del menú de opciones. Cada idioma figura escrito en su propia lengua, así se
    /// reconoce aunque la interfaz esté en uno que no se lee. Solo se listan los que tienen traducción cargada.
    /// </summary>
    public sealed class LanguageOptions : MonoBehaviour
    {
        [Serializable]
        public struct Choice
        {
            [Tooltip("Código del idioma en Localization, como \"es\" o \"ja\".")]
            public string Code;
            [Tooltip("Nombre que se ve en la lista, escrito en ese mismo idioma.")]
            public string Name;

            public Choice(string code, string name)
            {
                Code = code;
                Name = name;
            }
        }

        [SerializeField] TMP_Dropdown _dropdown;
        [Tooltip("Idiomas en el orden en que aparecen en la lista.")]
        [SerializeField] Choice[] _choices =
        {
            new Choice("es", "Español"),
            new Choice("en", "English"),
            new Choice("ja", "日本語"),
            new Choice("zh", "简体中文"),
        };

        readonly List<string> _codes = new List<string>();

        void Awake() => _dropdown.onValueChanged.AddListener(Choose);

        // El panel se habilita al abrir el menú, cuando los idiomas ya están cargados.
        void OnEnable()
        {
            GameLanguage.Changed += ShowCurrent;
            Populate();
        }

        void OnDisable() => GameLanguage.Changed -= ShowCurrent;

        void Populate()
        {
            _codes.Clear();
            _dropdown.ClearOptions();
            foreach (Choice choice in _choices)
            {
                if (LocalizationSettings.AvailableLocales.GetLocale(choice.Code) == null) continue;
                _codes.Add(choice.Code);
                _dropdown.options.Add(new TMP_Dropdown.OptionData(choice.Name));
            }
            ShowCurrent();
        }

        /// <summary>Marca el idioma elegido, también cuando cambia desde Restablecer opciones.</summary>
        void ShowCurrent()
        {
            int index = _codes.IndexOf(LocalizationSettings.SelectedLocale?.Identifier.Code);
            _dropdown.SetValueWithoutNotify(Mathf.Max(0, index));
            _dropdown.RefreshShownValue();
        }

        void Choose(int index)
        {
            if (index >= 0 && index < _codes.Count) GameLanguage.Select(_codes[index]);
        }
    }
}
