using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Suma el japonés y el chino simplificado. Registra los dos idiomas, crea sus tablas con los textos del
    /// archivo de traducciones y arma Noto Sans JP y Noto Sans SC como fuentes de reserva de las fuentes del
    /// juego, para que esos caracteres se dibujen sin cambiar la tipografía del texto latino.
    /// <para>
    /// Una entrada que ya tiene texto en la tabla no se pisa: lo corregido a mano en Unity se conserva. Para
    /// rehacer una traducción desde el archivo, se borra su texto en la tabla.
    /// </para>
    /// </summary>
    public static class CjkLanguageBuilder
    {
        const string TranslationsPath = "Assets/Localization/Editor/DriftPaint_CJK.json";
        const string LocaleFolder = "Assets/Localization";
        const string FontFolder = "Assets/Fonts/CJK";

        /// <summary>Tamaño de muestreo y relleno del atlas. Menor que el de las fuentes latinas para que entren más ideogramas.</summary>
        const int SamplingSize = 72;
        const int AtlasPadding = 8;
        const int AtlasSize = 2048;

        /// <summary>
        /// Alto de línea en em. Las Noto CJK declaran 1,16 arriba y 0,29 abajo para dejar lugar a otros alfabetos;
        /// como reserva eso agranda cada renglón con ideogramas y TextMeshPro oculta el texto de las celdas justas.
        /// Se usa el cuadro real de los ideogramas (0,88 y -0,12) y el mismo interlineado que Barlow.
        /// </summary>
        const float Ascent = 0.88f;
        const float Descent = -0.12f;
        const float LineHeight = 1.2f;

        static readonly (string Code, string AssetName)[] Locales =
        {
            ("ja", "Japanese (ja)"),
            ("zh", "Chinese (Simplified) (zh)"),
        };

        [Serializable]
        class Entry
        {
            public string key;
            public string en;
            public string ja;
            public string zh;

            public string For(string code) => code == "ja" ? ja : zh;
        }

        [Serializable]
        class Translations
        {
            public Entry[] entries;
        }

        enum Weight { Regular, Bold }

        static string FontPath(string family, Weight weight) => $"{FontFolder}/NotoSans{family}-{weight}.otf";
        static string FontAssetPath(string family, Weight weight) => $"{FontFolder}/NotoSans{family}-{weight} SDF.asset";

        public static bool Outdated()
        {
            if (!File.Exists(TranslationsPath) || !File.Exists(FontPath("JP", Weight.Regular))) return false;
            if (FontAssetsMissing()) return true;
            if (ReserveFonts().Any(font => !MetricsFitted(font))) return true;
            if (FallbacksMissing()) return true;

            var collection = LocalizationEditorSettings.GetStringTableCollection(GameLanguage.Table);
            if (collection == null) return false;
            Entry[] entries = Load();
            foreach (var (code, _) in Locales)
            {
                if (LocalizationEditorSettings.GetLocale(new LocaleIdentifier(code)) == null) return true;
                if (collection.GetTable(new LocaleIdentifier(code)) is not StringTable table) return true;
                if (entries.Any(entry => Pending(collection, table, entry, code))) return true;
            }
            return false;
        }

        public static void Apply()
        {
            Entry[] entries = Load();
            CreateFontAssets(entries);
            FitMetrics();
            ConnectFallbacks();
            FillTables(entries);
            AssetDatabase.SaveAssets();
        }

        static Entry[] Load() => JsonUtility.FromJson<Translations>(File.ReadAllText(TranslationsPath)).entries ?? Array.Empty<Entry>();

        static bool Pending(StringTableCollection collection, StringTable table, Entry entry, string code)
        {
            if (string.IsNullOrEmpty(entry.For(code)) || collection.SharedData.GetEntry(entry.key) == null) return false;
            return string.IsNullOrEmpty(table.GetEntry(entry.key)?.Value);
        }

        static void FillTables(Entry[] entries)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(GameLanguage.Table) ??
                throw new InvalidOperationException("Falta la colección de textos de DriftPaint.");

            foreach (var (code, assetName) in Locales)
            {
                var id = new LocaleIdentifier(code);
                if (LocalizationEditorSettings.GetLocale(id) == null)
                {
                    // Se reutiliza el idioma si ya existe como asset aunque no esté registrado.
                    string path = $"{LocaleFolder}/{assetName}.asset";
                    var locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
                    if (locale == null)
                    {
                        locale = Locale.CreateLocale(id);
                        AssetDatabase.CreateAsset(locale, path);
                    }
                    LocalizationEditorSettings.AddLocale(locale);
                }

                var table = collection.GetTable(id) as StringTable ?? collection.AddNewTable(id) as StringTable;
                if (table == null) throw new InvalidOperationException($"No se pudo crear la tabla de textos para {code}.");

                int added = 0;
                foreach (Entry entry in entries)
                {
                    if (!Pending(collection, table, entry, code)) continue;
                    table.AddEntry(entry.key, entry.For(code));
                    added++;
                }
                if (added == 0) continue;
                EditorUtility.SetDirty(table);
                Debug.Log($"Tabla {table.name}: {added} textos cargados desde {TranslationsPath}.", table);
            }
        }

        static bool FontAssetsMissing()
        {
            foreach (string family in new[] { "JP", "SC" })
                foreach (Weight weight in new[] { Weight.Regular, Weight.Bold })
                    if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath(family, weight)) == null) return true;
            return false;
        }

        static void CreateFontAssets(Entry[] entries)
        {
            foreach (string family in new[] { "JP", "SC" })
            {
                // Los caracteres de las traducciones se agregan al atlas de antemano; el resto, como nombres o
                // chat, se agrega solo cuando aparece en pantalla.
                string code = family == "JP" ? "ja" : "zh";
                string characters = new string(entries.SelectMany(entry => entry.For(code) ?? "").Distinct().ToArray());

                foreach (Weight weight in new[] { Weight.Regular, Weight.Bold })
                {
                    string assetPath = FontAssetPath(family, weight);
                    if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null) continue;

                    var source = AssetDatabase.LoadAssetAtPath<Font>(FontPath(family, weight));
                    if (source == null) throw new FileNotFoundException("Falta la fuente de origen.", FontPath(family, weight));

                    var asset = TMP_FontAsset.CreateFontAsset(source, SamplingSize, AtlasPadding, GlyphRenderMode.SDFAA,
                        AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
                    if (asset == null) throw new InvalidOperationException($"No se pudo leer {source.name}. Revisá que tenga activado Include Font Data.");

                    string name = Path.GetFileNameWithoutExtension(assetPath);
                    asset.name = name;
                    AssetDatabase.CreateAsset(asset, assetPath);
                    asset.atlasTextures[0].name = name + " Atlas";
                    asset.material.name = name + " Material";
                    AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
                    AssetDatabase.AddObjectToAsset(asset.material, asset);

                    if (!asset.TryAddCharacters(characters, out string missing))
                        Debug.LogWarning($"{name} no tiene estos caracteres: {missing}", asset);
                    EditorUtility.SetDirty(asset);
                }
            }
            AssetDatabase.SaveAssets();
        }

        static IEnumerable<TMP_FontAsset> ReserveFonts() =>
            new[] { Weight.Regular, Weight.Bold }.SelectMany(ReservesFor).Where(font => font != null);

        static bool MetricsFitted(TMP_FontAsset font)
        {
            FaceInfo face = font.faceInfo;
            return Close(face.ascentLine, Ascent * face.pointSize) &&
                   Close(face.descentLine, Descent * face.pointSize) &&
                   Close(face.lineHeight, LineHeight * face.pointSize);
        }

        static bool Close(float a, float b) => Mathf.Abs(a - b) < 0.01f;

        static void FitMetrics()
        {
            foreach (TMP_FontAsset font in ReserveFonts())
            {
                if (MetricsFitted(font)) continue;
                FaceInfo face = font.faceInfo;
                face.ascentLine = Ascent * face.pointSize;
                face.descentLine = Descent * face.pointSize;
                face.lineHeight = LineHeight * face.pointSize;
                face.superscriptOffset = face.ascentLine;
                face.subscriptOffset = face.descentLine;
                font.faceInfo = face;
                EditorUtility.SetDirty(font);
            }
        }

        static IEnumerable<TMP_FontAsset> GameFonts()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Fonts", "Assets/TextMesh Pro/Resources" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(FontFolder + "/")) continue;
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font != null) yield return font;
            }
        }

        /// <summary>Las fuentes de títulos y botones toman la variante Bold; las de texto corrido, la Regular.</summary>
        static Weight WeightFor(TMP_FontAsset font)
        {
            string style = font.faceInfo.styleName ?? "";
            bool heavy = style.Contains("Bold") || style.Contains("Black") || font.faceInfo.familyName == "Racing Sans One";
            return heavy ? Weight.Bold : Weight.Regular;
        }

        static TMP_FontAsset[] ReservesFor(Weight weight) => new[]
        {
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath("JP", weight)),
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath("SC", weight)),
        };

        static bool IsReserve(TMP_FontAsset font) => font != null && AssetDatabase.GetAssetPath(font).StartsWith(FontFolder + "/");

        static bool FallbacksMissing()
        {
            if (FontAssetsMissing()) return true;
            foreach (TMP_FontAsset font in GameFonts())
            {
                var fallbacks = font.fallbackFontAssetTable;
                if (fallbacks == null || ReservesFor(WeightFor(font)).Any(reserve => !fallbacks.Contains(reserve))) return true;
            }
            var global = TMP_Settings.fallbackFontAssets;
            return global == null || ReservesFor(Weight.Regular).Any(reserve => !global.Contains(reserve));
        }

        static void ConnectFallbacks()
        {
            foreach (TMP_FontAsset font in GameFonts())
            {
                var fallbacks = font.fallbackFontAssetTable ?? new List<TMP_FontAsset>();
                fallbacks.RemoveAll(entry => entry == null || IsReserve(entry));
                fallbacks.AddRange(ReservesFor(WeightFor(font)));
                font.fallbackFontAssetTable = fallbacks;
                EditorUtility.SetDirty(font);
            }

            // Reserva general para textos con una fuente que no esté en la carpeta del juego.
            var settings = TMP_Settings.instance;
            var global = TMP_Settings.fallbackFontAssets;
            if (settings == null || global == null) return;
            global.RemoveAll(entry => entry == null || IsReserve(entry));
            global.AddRange(ReservesFor(Weight.Regular));
            EditorUtility.SetDirty(settings);
        }
    }
}
