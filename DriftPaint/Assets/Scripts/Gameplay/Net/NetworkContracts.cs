using System;
using System.Collections.Generic;
using DriftPaint.Paint;
using UnityEngine;

namespace DriftPaint.Net
{
    /// <summary>En qué punto de la conexión está el jugador.</summary>
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        InLobby,
        JoiningRoom,
        InRoom,
        LeavingRoom
    }

    /// <summary>Estado de la partida guardado en la sala. El valor numérico es el que viaja por red.</summary>
    public enum MatchPhase : byte
    {
        Lobby = 0,
        Countdown = 1,
        Playing = 2,
        Tiebreak = 3,
        Results = 4
    }

    public static class MatchPhaseExtensions
    {
        /// <summary>Entre partidas: en el lobby o mostrando resultados. El host puede arrancar otra.</summary>
        public static bool IsBetweenMatches(this MatchPhase phase) => phase == MatchPhase.Lobby || phase == MatchPhase.Results;

        /// <summary>Se está jugando una ronda (la normal o un desempate).</summary>
        public static bool IsRound(this MatchPhase phase) => phase == MatchPhase.Playing || phase == MatchPhase.Tiebreak;
    }

    /// <summary>Configuración que elige el creador de la sala.</summary>
    [Serializable]
    public struct RoomSettings
    {
        public string Title;
        public int MaxPlayers;
        public int DurationSeconds;
        public string MapId;
    }

    /// <summary>Datos de una sala tal como los ve el juego.</summary>
    public readonly struct RoomSummary
    {
        public readonly string Id;
        public readonly string Title;
        public readonly int PlayerCount;
        public readonly int MaxPlayers;
        public readonly bool IsOpen;
        public readonly string MapId;
        public readonly int DurationSeconds;
        public readonly MatchPhase Phase;

        public RoomSummary(string id, string title, int playerCount, int maxPlayers, bool isOpen,
            string mapId, int durationSeconds, MatchPhase phase)
        {
            Id = id;
            Title = title;
            PlayerCount = playerCount;
            MaxPlayers = maxPlayers;
            IsOpen = isOpen;
            MapId = mapId;
            DurationSeconds = durationSeconds;
            Phase = phase;
        }

        public bool IsValid => !string.IsNullOrEmpty(Id);
        public bool IsFull => MaxPlayers > 0 && PlayerCount >= MaxPlayers;
    }

    /// <summary>Datos de un jugador de la sala actual.</summary>
    public readonly struct PlayerSummary
    {
        /// <summary>Identificador del jugador en la sala. Más bajo es quien entró antes.</summary>
        public readonly int ActorId;
        public readonly string Nickname;
        public readonly byte Team;
        public readonly bool IsMaster;
        public readonly bool IsLocal;
        /// <summary>Desconectado, pero con el lugar reservado hasta que venza el tiempo de reconexión.</summary>
        public readonly bool IsInactive;
        /// <summary>Puntaje publicado por el Master: celdas en pie más robos.</summary>
        public readonly int Points;
        /// <summary>Celdas en pie publicadas por el Master, para desempatar el orden de la tabla.</summary>
        public readonly int Standing;

        public PlayerSummary(int actorId, string nickname, byte team, bool isMaster, bool isLocal, bool isInactive,
            int points = 0, int standing = 0)
        {
            ActorId = actorId;
            Nickname = nickname;
            Team = team;
            IsMaster = isMaster;
            IsLocal = isLocal;
            IsInactive = isInactive;
            Points = points;
            Standing = standing;
        }
    }

    /// <summary>Mensaje del chat de sala. Los mensajes de sistema no tienen jugador.</summary>
    public readonly struct ChatMessage
    {
        public readonly bool IsSystem;
        public readonly string SenderName;
        public readonly byte SenderTeam;
        public readonly string Text;

        public ChatMessage(bool isSystem, string senderName, byte senderTeam, string text)
        {
            IsSystem = isSystem;
            SenderName = senderName;
            SenderTeam = senderTeam;
            Text = text;
        }

        public static ChatMessage System(string text) => new ChatMessage(true, "", 0, text);
    }

    /// <summary>Disparo recibido: todos los clientes simulan el proyectil desde FireTime.</summary>
    public readonly struct FireInfo
    {
        public readonly int ShooterActorId;
        public readonly Vector3 Origin;
        public readonly Vector3 Direction;
        /// <summary>Tiempo del servidor en el que se disparó.</summary>
        public readonly double FireTime;

        public FireInfo(int shooterActorId, Vector3 origin, Vector3 direction, double fireTime)
        {
            ShooterActorId = shooterActorId;
            Origin = origin;
            Direction = direction;
            FireTime = fireTime;
        }
    }

    public enum ImpactKind : byte
    {
        /// <summary>Impacto directo de un proyectil: stun.</summary>
        Direct = 0,
        /// <summary>Splash de un proyectil: slow.</summary>
        Splash = 1,
        /// <summary>Explosión de un barril: mismos efectos que el disparo.</summary>
        Barrel = 2
    }

    /// <summary>Impacto decidido por el Master.</summary>
    public readonly struct ImpactInfo
    {
        public readonly Vector3 Position;
        /// <summary>Jugador alcanzado, o -1 si no le pegó a nadie.</summary>
        public readonly int VictimActorId;
        public readonly ImpactKind Kind;
        public readonly int SourceActorId;

        public ImpactInfo(Vector3 position, int victimActorId, ImpactKind kind, int sourceActorId)
        {
            Position = position;
            VictimActorId = victimActorId;
            Kind = kind;
            SourceActorId = sourceActorId;
        }
    }

    /// <summary>Objeto creado en red por un jugador, por ejemplo su auto.</summary>
    public readonly struct NetworkObjectInfo
    {
        public readonly GameObject Instance;
        public readonly int OwnerActorId;
        /// <summary>True si lo controla este cliente.</summary>
        public readonly bool IsLocal;
        public readonly string PrefabId;

        public NetworkObjectInfo(GameObject instance, int ownerActorId, bool isLocal, string prefabId)
        {
            Instance = instance;
            OwnerActorId = ownerActorId;
            IsLocal = isLocal;
            PrefabId = prefabId;
        }
    }

    /// <summary>
    /// Contrato de red en el lenguaje del juego. Lo define Gameplay y lo implementa la capa de Red,
    /// así ningún sistema de juego conoce Photon, códigos de evento ni serialización.
    /// </summary>
    public interface INetworkMediator
    {
        // Conexión
        ConnectionState State { get; }
        string LocalNickname { get; }
        int LocalActorId { get; }
        bool IsMaster { get; }
        /// <summary>Reloj del servidor en segundos, igual en todos los clientes.</summary>
        double ServerTime { get; }
        /// <summary>Latencia al servidor en milisegundos.</summary>
        int Ping { get; }
        event Action StateChanged;
        /// <summary>Error para mostrar al jugador (conexión perdida, sala llena, etc.).</summary>
        event Action<string> ErrorRaised;

        void Connect(string nickname);
        void Disconnect();
        void SetNickname(string nickname);

        // Salas
        IReadOnlyList<RoomSummary> Rooms { get; }
        event Action RoomListChanged;
        bool CreateRoom(RoomSettings settings);
        bool JoinRoom(string roomId);
        void LeaveRoom();
        /// <summary>Deja la sala y después se desconecta, para volver al menú principal.</summary>
        void LeaveRoomAndDisconnect();

        // Sala actual
        RoomSummary CurrentRoom { get; }
        /// <summary>Jugadores ordenados por orden de entrada.</summary>
        IReadOnlyList<PlayerSummary> Players { get; }
        MatchPhase Phase { get; }
        /// <summary>Tiempo del servidor en el que arranca la partida (fin de la cuenta regresiva).</summary>
        double MatchStartTime { get; }
        event Action RoomChanged;
        event Action PlayersChanged;
        event Action<MatchPhase> PhaseChanged;
        event Action<ChatMessage> ChatReceived;

        /// <summary>Solo el Master y en el lobby: cambia la duración de la partida.</summary>
        void SetRoomDuration(int seconds);
        void SendChat(string text);
        /// <summary>Solo el Master, en el lobby o en resultados: pasa la sala a la cuenta regresiva y limpia los puntajes.</summary>
        bool RequestStartMatch();
        /// <summary>Solo el Master: cambia el estado de la partida. Volver a Lobby limpia el reloj y la ronda.</summary>
        bool SetMatchPhase(MatchPhase phase);
        /// <summary>Celdas pintadas por cada equipo, publicadas por el Master.</summary>
        int ScoreA { get; }
        int ScoreB { get; }
        /// <summary>Ronda actual: 1 la partida normal, 2 en adelante los desempates. 0 fuera de partida.</summary>
        int Round { get; }
        /// <summary>Equipo ganador publicado al cerrar la partida, o ninguno.</summary>
        byte Winner { get; }

        /// <summary>Solo el Master: publica las celdas de cada equipo si cambiaron desde el último envío.</summary>
        void PublishTeamScores(int cellsA, int cellsB);
        /// <summary>Solo el Master: publica el puntaje y las celdas en pie de un jugador si cambiaron.</summary>
        void PublishPlayerScore(int actorId, int points, int standing);
        /// <summary>
        /// Solo el Master: cierra la partida publicando en una sola actualización las celdas finales,
        /// el ganador y el estado de resultados.
        /// </summary>
        bool FinishMatch(int cellsA, int cellsB, byte winner);
        /// <summary>Solo el Master: programa una ronda de desempate sobre la misma grilla, que arranca en startTime (reloj del servidor).</summary>
        bool StartTiebreak(double startTime);

        /// <summary>
        /// Frena la entrega de mensajes mientras se carga una escena, para que nada llegue
        /// a una escena que está por destruirse. Los mensajes quedan en cola.
        /// </summary>
        void SetIncomingPaused(bool paused);

        // Objetos de red
        /// <summary>Registra un prefab que se puede crear en red con ese identificador.</summary>
        void RegisterPrefab(string prefabId, GameObject prefab);
        /// <summary>Crea un objeto del jugador local en todos los clientes. Sin sala, lo crea solo localmente.</summary>
        GameObject SpawnOwned(string prefabId, Vector3 position, Quaternion rotation);
        /// <summary>Destruye un objeto propio en todos los clientes.</summary>
        void Despawn(GameObject instance);
        /// <summary>Objetos de red que existen ahora, propios y ajenos.</summary>
        IReadOnlyList<NetworkObjectInfo> Objects { get; }
        event Action<NetworkObjectInfo> ObjectSpawned;
        event Action<GameObject> ObjectDespawned;

        // Pintura
        /// <summary>Solo el Master: cambios de la grilla oficial para los demás clientes.</summary>
        void SendPaintDelta(PaintDelta delta);
        /// <summary>Pide al Master la grilla completa (al entrar a una partida empezada).</summary>
        void RequestPaintSnapshot();
        /// <summary>Solo el Master: grilla completa para un jugador.</summary>
        void SendPaintSnapshot(int actorId, PaintSnapshot snapshot);
        event Action<PaintDelta> PaintDeltaReceived;
        /// <summary>Un jugador pidió la grilla completa. Solo le llega al Master.</summary>
        event Action<int> PaintSnapshotRequested;
        event Action<PaintSnapshot> PaintSnapshotReceived;

        // Partida
        void RequestFire(Vector3 origin, Vector3 direction);
        void RequestDropBarrel(Vector3 position, Vector3 direction);
        void RequestPickup(int pickupId);
        event Action<FireInfo> FireReceived;
        event Action<ImpactInfo> ImpactReceived;
    }

    /// <summary>
    /// Punto de acceso al mediator para los sistemas de juego. Lo registra el arranque de la aplicación.
    /// </summary>
    public static class NetworkServices
    {
        public static INetworkMediator Mediator { get; private set; }

        public static void Register(INetworkMediator mediator) => Mediator = mediator;
    }
}
