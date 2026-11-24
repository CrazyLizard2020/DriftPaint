using UnityEditor;
using UnityEngine;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Configuración de importación de los sonidos generados por código. Los loops del motor van sin comprimir
    /// y descomprimidos en memoria: son cortos y así empalman exacto. Los sonidos sueltos (petardeos, efectos)
    /// usan ADPCM, que pesa la cuarta parte y se decodifica casi gratis.
    /// </summary>
    public class GeneratedAudioImport : AssetPostprocessor
    {
        public const string EnginesFolder = "Assets/Audio/Engines";
        public const string EffectsFolder = "Assets/Audio/Sfx";
        public const string CrowdFolder = EffectsFolder + "/Crowd";
        /// <summary>Prefijo de los sonidos sueltos dentro de las carpetas de motores.</summary>
        public const string OneShotPrefix = "FX_";

        void OnPreprocessAudio()
        {
            bool engine = assetPath.StartsWith(EnginesFolder + "/");
            bool effect = assetPath.StartsWith(EffectsFolder + "/");
            if (!engine && !effect) return;

            bool loop = engine && !System.IO.Path.GetFileName(assetPath).StartsWith(OneShotPrefix);
            var importer = (AudioImporter)assetImporter;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            if (assetPath.StartsWith(CrowdFolder + "/"))
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.preloadAudioData = false;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.loadInBackground = true;
                return;
            }
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = loop ? AudioCompressionFormat.PCM : AudioCompressionFormat.ADPCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.loadInBackground = false;
        }
    }
}
