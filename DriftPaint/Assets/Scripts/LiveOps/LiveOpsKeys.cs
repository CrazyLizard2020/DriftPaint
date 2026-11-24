namespace DriftPaint.LiveOperations
{
    /// <summary>Nombres de las claves en Remote Config. Tienen que coincidir con las del dashboard de Unity Cloud.</summary>
    public static class LiveOpsKeys
    {
        /// <summary>Mapas que el host puede elegir, separados por coma.</summary>
        public const string MapsEnabled = "maps_enabled";
        /// <summary>Autos que se pueden elegir, separados por coma.</summary>
        public const string CarsEnabled = "cars_enabled";
        /// <summary>JSON con las estadísticas que pisan las de la ficha de cada auto.</summary>
        public const string CarStats = "car_stats";
        /// <summary>JSON opcional con el tema que suena en cada pantalla o mapa.</summary>
        public const string MusicOverrides = "music_overrides";
    }
}
