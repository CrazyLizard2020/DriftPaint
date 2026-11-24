using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace DriftPaint
{
    /// <summary>Los textos de interfaz se resuelven en el idioma de este cliente. La sala conserva los datos de los jugadores.</summary>
    public static class GameLanguage
    {
        public const string Table = "DriftPaint";
        const string Preference = "DriftPaint.Language";
        static Locale _locale;
        static StringTable _table;

        /// <summary>Actualiza las vistas abiertas una vez que la tabla del nuevo idioma está disponible.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            _locale = null;
            _table = null;
            Changed = null;
        }

        /// <summary>Busca el mensaje en la tabla y conserva el texto de autor si falta la entrada.</summary>
        public static string Text(string key)
        {
            if (string.IsNullOrEmpty(key) || !Application.isPlaying) return key;
            var locale = LocalizationSettings.SelectedLocale;
            if (locale == null) return key;
            if (_locale != locale || _table == null)
            {
                _table = LocalizationSettings.StringDatabase.GetTable(Table, locale);
                _locale = locale;
            }
            return _table?.GetEntry(key)?.LocalizedValue ?? key;
        }

        /// <summary>Aplica y guarda el idioma sin interrumpir la partida.</summary>
        public static void Select(string code)
        {
            var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            if (locale == null) return;
            LocalizationSettings.SelectedLocale = locale;
            PlayerPrefs.SetString(Preference, code);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>El idioma de fábrica sigue al sistema cuando hay una traducción disponible.</summary>
        public static void RestoreDefaults()
        {
            var system = new SystemLocaleSelector().GetStartupLocale(LocalizationSettings.AvailableLocales);
            Select(system?.Identifier.Code ?? "es");
            PlayerPrefs.DeleteKey(Preference);
            PlayerPrefs.Save();
        }

        /// <summary>Permite alternar los dos idiomas desde un botón de teclado, mouse o joystick.</summary>
        public static void Toggle() => Select(LocalizationSettings.SelectedLocale?.Identifier.Code == "en" ? "es" : "en");
    }
}
