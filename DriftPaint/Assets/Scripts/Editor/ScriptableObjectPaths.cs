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

        public const string CarData = CarsFolder + "/CarData_Default.asset";
        public const string CarCatalog = CarsFolder + "/CarCatalog.asset";
        public const string TeamPalette = TeamsFolder + "/TeamPalette_Default.asset";
        public const string ProjectileSettings = CombatFolder + "/ProjectileSettings.asset";
        public const string BarrelSettings = CombatFolder + "/BarrelSettings.asset";
        public const string MusicLibrary = AudioFolder + "/MusicLibrary.asset";
    }
}
