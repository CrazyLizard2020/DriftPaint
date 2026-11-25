namespace DriftPaint.Match
{
    /// <summary>
    /// Mapas disponibles: identificador que viaja en la sala, nombre visible y escena.
    /// El identificador también es el contexto de música del mapa.
    /// </summary>
    public static class MapCatalog
    {
        public const string MenuScene = "Menu";
        public const string GraffithasiaId = "graffithasia";
        public const string RoyalDriftRoadId = "royal_drift_road";
        /// <summary>Mapa que se usa al crear una sala sin elegir otro y al jugar sin sala.</summary>
        public const string DefaultMapId = GraffithasiaId;

        static readonly string[] MapIds = { GraffithasiaId, RoyalDriftRoadId };

        /// <summary>Todos los mapas del juego, en el orden en que se ofrecen.</summary>
        public static System.Collections.Generic.IReadOnlyList<string> All => MapIds;

        public static bool IsKnown(string mapId) => System.Array.IndexOf(MapIds, mapId) >= 0;

        /// <summary>Mapa que se juega en la escena, o null si la escena no es de partida.</summary>
        public static string MapForScene(string sceneName)
        {
            foreach (string mapId in MapIds)
                if (SceneFor(mapId) == sceneName) return mapId;
            return null;
        }

        public static string SceneFor(string mapId)
        {
            switch (mapId)
            {
                case RoyalDriftRoadId: return "Match_RoyalDriftRoad";
                case GraffithasiaId:
                default:
                    return "Match_DriftArena";
            }
        }

        public static string DisplayName(string mapId)
        {
            switch (mapId)
            {
                case GraffithasiaId: return "Graffithasia";
                case RoyalDriftRoadId: return "Royal Drift Road";
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
