using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

namespace DriftPaint.UI
{
    /// <summary>
    /// El japonés y el chino comparten muchos ideogramas, pero cada idioma los dibuja distinto. Las fuentes del
    /// juego tienen de reserva Noto Sans JP y Noto Sans SC; TextMeshPro usa la primera de la lista que tenga el
    /// carácter, así que según el idioma elegido la fuente de ese idioma pasa adelante.
    /// </summary>
    public static class HanFontOrder
    {
        const string JapaneseFamily = "Noto Sans JP";
        const string ChineseFamily = "Noto Sans SC";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            LocalizationSettings.SelectedLocaleChanged -= Apply;
            LocalizationSettings.SelectedLocaleChanged += Apply;
            // Las fuentes de una escena nueva se cargan con ella y traen el orden guardado en el asset.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply(LocalizationSettings.SelectedLocale);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply(LocalizationSettings.SelectedLocale);

        /// <summary>Ordena las reservas de todas las fuentes cargadas para el idioma dado.</summary>
        public static void Apply(Locale locale)
        {
            bool chinese = locale != null && locale.Identifier.Code.StartsWith("zh");
            string first = chinese ? ChineseFamily : JapaneseFamily;
            if (TMP_Settings.instance != null) Reorder(TMP_Settings.fallbackFontAssets, first);
            foreach (TMP_FontAsset font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                Reorder(font.fallbackFontAssetTable, first);
        }

        static void Reorder(List<TMP_FontAsset> fonts, string first)
        {
            if (fonts == null) return;
            int japanese = fonts.FindIndex(font => font != null && font.faceInfo.familyName == JapaneseFamily);
            int chinese = fonts.FindIndex(font => font != null && font.faceInfo.familyName == ChineseFamily);
            if (japanese < 0 || chinese < 0) return;
            bool japaneseFirst = japanese < chinese;
            if (japaneseFirst == (first == JapaneseFamily)) return;
            (fonts[japanese], fonts[chinese]) = (fonts[chinese], fonts[japanese]);
        }
    }
}
