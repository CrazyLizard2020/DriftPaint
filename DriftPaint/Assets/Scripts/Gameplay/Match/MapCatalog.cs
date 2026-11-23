namespace DriftPaint.Match
{
    /// <summary>Mapas disponibles: identificador que viaja en la sala, nombre visible y escena.</summary>
    public static class MapCatalog
    {
        public const string MenuScene = "Menu";
        public const string DriftArenaId = "drift_arena";

        public static string SceneFor(string mapId)
        {
            switch (mapId)
            {
                case DriftArenaId:
                default:
                    return "Match_DriftArena";
            }
        }

        public static string DisplayName(string mapId)
        {
            switch (mapId)
            {
                case DriftArenaId: return "Drift Arena";
                default: return mapId;
            }
        }
    }

    /// <summary>Valores fijos de la partida.</summary>
    public static class MatchRules
    {
        /// <summary>Lado de la arena en metros. Cada celda de la grilla mide 1 x 1 m.</summary>
        public const int ArenaSize = 128;
        public const int TotalCells = ArenaSize * ArenaSize;
    }
}
