namespace DriftPaint.Networking
{
    /// <summary>Códigos de los eventos de red. Todos se envían como confiables.</summary>
    public static class NetEventCodes
    {
        public const byte FireRequest = 1;
        public const byte ProjectileImpact = 2;
        public const byte DropBarrelRequest = 3;
        public const byte BarrelExplode = 4;
        public const byte PickupRequest = 5;
        public const byte PickupResult = 6;
        public const byte PaintDelta = 7;
        public const byte PaintSnapshot = 8;
        public const byte PaintSnapshotRequest = 9;
        public const byte TeamChangeRequest = 10;
        public const byte Chat = 20;
    }

    /// <summary>Códigos de los tipos serializados a mano. No pueden chocar con los que registra Photon (W, V, Q, P).</summary>
    public static class NetTypeCodes
    {
        public const byte PaintDelta = (byte)'d';
        public const byte PaintSnapshot = (byte)'s';
    }

    /// <summary>Claves de las propiedades de la sala.</summary>
    public static class RoomKeys
    {
        public const string Title = "title";
        public const string State = "state";
        public const string StartTime = "startTime";
        public const string Duration = "duration";
        public const string Round = "round";
        /// <summary>Identifica una partida de la sala y se conserva durante los desempates.</summary>
        public const string MatchId = "matchId";
        public const string Map = "map";
        public const string ScoreA = "scoreA";
        public const string ScoreB = "scoreB";
        public const string Winner = "winner";
        public const string Config = "cfg";

        /// <summary>Propiedades que se ven en la lista de salas.</summary>
        public static readonly string[] Lobby = { Title, State, Duration, Map };
    }

    /// <summary>Claves de las propiedades de cada jugador.</summary>
    public static class PlayerKeys
    {
        public const string Team = "team";
        public const string Car = "car";
        public const string Barrels = "brl";
        public const string LastPosition = "lastPos";
        public const string Points = "pts";

        /// <summary>Propiedades que valen solo para una sala. El auto elegido sí se conserva entre salas.</summary>
        public static readonly string[] RoomScoped = { Team, Barrels, LastPosition, Points };
    }

    /// <summary>Valores fijos de las salas.</summary>
    public static class RoomRules
    {
        public const int MaxPlayers = 8;
        public const int MinDurationSeconds = 120;
        public const int MaxDurationSeconds = 300;
        public const int DurationStepSeconds = 60;
        public const int DefaultDurationSeconds = 120;
        public const int PlayerTtlMs = 30000;
        public const double CountdownSeconds = 3.0;
        public const int MaxChatLength = 160;
        public const string DefaultMapId = "drift_arena";
    }
}
