namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Rutas de todos los ScriptableObjects del juego. Viven bajo una sola carpeta raíz, con una
    /// subcarpeta por categoría, y las herramientas de editor los buscan y crean solo acá.
    /// </summary>
    public static class ScriptableObjectPaths
    {
        public const string Root = "Assets/ScriptableObjects";

        public const string CarsFolder = Root + "/Cars";
        public const string TeamsFolder = Root + "/Teams";
        public const string CombatFolder = Root + "/Combat";
        public const string AudioFolder = Root + "/Audio";
        public const string MusicTracksFolder = AudioFolder + "/Music";
        public const string EngineBanksFolder = AudioFolder + "/Engines";

        public const string CarData = CarsFolder + "/CarData_Default.asset";
        public const string CarCatalog = CarsFolder + "/CarCatalog.asset";
        public const string TeamPalette = TeamsFolder + "/TeamPalette_Default.asset";
        public const string ProjectileSettings = CombatFolder + "/ProjectileSettings.asset";
        public const string BarrelSettings = CombatFolder + "/BarrelSettings.asset";
        public const string MusicLibrary = AudioFolder + "/MusicLibrary.asset";
        public const string SfxLibrary = AudioFolder + "/SfxLibrary.asset";
        public const string PaintRings = Root + "/Paint/PaintRingSettings.asset";

        /// <summary>Datos de un modelo de auto (física y motor). Cada modelo tiene el suyo.</summary>
        public static string CarDataFor(string model) => $"{CarsFolder}/CarData_{model}.asset";

        /// <summary>Sonido de motor de un modelo de auto.</summary>
        public static string EngineSoundFor(string model) => $"{CarsFolder}/EngineSound_{model}.asset";

        /// <summary>Banco de loops de una fuente (escape, admisión, mecánica) de una familia de motores.</summary>
        public static string EngineBank(string family, string source) => $"{EngineBanksFolder}/EngineBank_{family}_{source}.asset";
    }
}
