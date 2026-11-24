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
        LeavingRoom,
        /// <summary>Se cortó la conexión estando en una sala y se intenta volver como el mismo jugador.</summary>
        Reconnecting
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
        /// <summary>Copia de la configuración de LiveOps que usará toda la sala.</summary>
        public string Config;
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
        /// <summary>Copia de LiveOps que dejó el host al crear la sala. Solo se conoce estando adentro.</summary>
        public readonly string Config;

        public RoomSummary(string id, string title, int playerCount, int maxPlayers, bool isOpen,
            string mapId, int durationSeconds, MatchPhase phase, string config = null)
        {
            Config = config;
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
        public readonly bool IsReady;
        /// <summary>Puntaje publicado por el Master: celdas en pie más robos.</summary>
        public readonly int Points;
        /// <summary>Celdas en pie publicadas por el Master, para desempatar el orden de la tabla.</summary>
        public readonly int Standing;
        /// <summary>Barriles en el baúl según el Master, o -1 si todavía no se asignaron en esta partida.</summary>
        public readonly int Barrels;
        /// <summary>Auto elegido (identificador del prefab), o null si todavía no eligió.</summary>
        public readonly string CarId;

        public PlayerSummary(int actorId, string nickname, byte team, bool isMaster, bool isLocal, bool isInactive,
            int points = 0, int standing = 0, int barrels = -1, string carId = null, bool isReady = false)
        {
            IsReady = isReady;
            Barrels = barrels;
            CarId = carId;
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

        public readonly string Argument;

        public ChatMessage(bool isSystem, string senderName, byte senderTeam, string text, string argument = null)
        {
            Argument = argument;
            IsSystem = isSystem;
            SenderName = senderName;
            SenderTeam = senderTeam;
            Text = text;
        }

        public static ChatMessage System(string key, string argument = null) => new ChatMessage(true, "", 0, key, argument);
    }

    /// <summary>Disparo recibido: todos los clientes simulan el proyectil desde FireTime.</summary>
    public readonly struct FireInfo
    {
        public readonly int ShooterActorId;
        /// <summary>Número de disparo del tirador. Con el tirador identifica al proyectil.</summary>
        public readonly int ShotId;
        public readonly Vector3 Origin;
        /// <summary>Velocidad inicial del proyectil en m/s.</summary>
        public readonly Vector3 Velocity;
        /// <summary>Tiempo del servidor en el que se disparó.</summary>
        public readonly double FireTime;

        public FireInfo(int shooterActorId, int shotId, Vector3 origin, Vector3 velocity, double fireTime)
        {
            ShooterActorId = shooterActorId;
            ShotId = shotId;
            Origin = origin;
            Velocity = velocity;
            FireTime = fireTime;
        }
    }

    /// <summary>Qué provocó un impacto.</summary>
    public enum ImpactKind : byte
    {
        Projectile = 0,
        Barrel = 1
    }

    /// <summary>
    /// Impacto decidido por el Master: dónde fue, quién lo causó, a quién aturde (impacto directo o
    /// barril pisado) y a quiénes frena (alcanzados por la zona de la explosión).
    /// </summary>
    public readonly struct ImpactInfo
    {
        public readonly Vector3 Position;
        public readonly ImpactKind Kind;
        public readonly int SourceActorId;
        /// <summary>Disparo que impactó (ShotId del tirador), o -1 si fue un barril.</summary>
        public readonly int ShotId;
        /// <summary>Jugador aturdido, o -1 si nadie recibió el golpe directo.</summary>
        public readonly int StunnedActorId;
        /// <summary>Jugadores frenados por la zona de la explosión.</summary>
        public readonly int[] SlowedActorIds;

        public ImpactInfo(Vector3 position, ImpactKind kind, int sourceActorId, int shotId, int stunnedActorId, int[] slowedActorIds)
        {
            Position = position;
            Kind = kind;
            SourceActorId = sourceActorId;
            ShotId = shotId;
            StunnedActorId = stunnedActorId;
            SlowedActorIds = slowedActorIds ?? Array.Empty<int>();
        }

        public bool Slows(int actorId) => Array.IndexOf(SlowedActorIds, actorId) >= 0;
    }

    /// <summary>Respuesta del Master a un pedido de pickup.</summary>
    public readonly struct PickupResult
    {
        public readonly int PickupId;
        /// <summary>Jugador que se llevó el barril, o -1 si el pedido fue rechazado.</summary>
        public readonly int ActorId;
        /// <summary>Tiempo del servidor en el que el pickup vuelve a aparecer.</summary>
        public readonly double RespawnTime;

        public PickupResult(int pickupId, int actorId, double respawnTime)
        {
            PickupId = pickupId;
            ActorId = actorId;
            RespawnTime = respawnTime;
        }

        public bool Accepted => ActorId >= 0;
    }

    /// <summary>Objeto creado en red por un jugador, por ejemplo su auto.</summary>
    public readonly struct NetworkObjectInfo
    {
        public readonly GameObject Instance;
        public readonly int OwnerActorId;
        /// <summary>True si lo controla este cliente.</summary>
        public readonly bool IsLocal;
        public readonly string PrefabId;
        /// <summary>Datos que pasó quien lo creó, iguales en todos los clientes. Nunca es null.</summary>
        public readonly object[] Data;

        public NetworkObjectInfo(GameObject instance, int ownerActorId, bool isLocal, string prefabId, object[] data = null)
        {
            Instance = instance;
            OwnerActorId = ownerActorId;
            IsLocal = isLocal;
            PrefabId = prefabId;
            Data = data ?? Array.Empty<object>();
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
        /// <summary>
        /// Volvió a la sala después de un corte, como el mismo jugador. Los objetos de red se vuelven a
        /// crear y la grilla hay que pedirla de nuevo al Master.
        /// </summary>
        event Action Rejoined;
        /// <summary>Segundos que quedan para volver a la sala mientras se reconecta.</summary>
        float ReconnectTimeRemaining { get; }
        /// <summary>Hay una reserva vigente que se puede solicitar desde el menú principal.</summary>
        bool CanRejoinLastMatch { get; }
        /// <summary>La escena de partida debe recuperar los objetos del actor que volvió desde el menú.</summary>
        bool ReturningFromMenu { get; }
        /// <summary>Pose anterior del auto cuando se recupera desde otra escena.</summary>
        Pose? ReturningCarPose { get; }
        /// <summary>Vuelve a conectar con la sala y solicita el mismo actor.</summary>
        void RejoinLastMatch();

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
        /// <summary>Solo el Master y entre partidas: cambia el mapa de la próxima partida.</summary>
        void SetRoomMap(string mapId);
        /// <summary>
        /// Pide pasar al jugador local al otro equipo. El Master lo acepta solo entre partidas y si los
        /// equipos no quedan desparejos.
        /// </summary>
        void RequestTeamChange();
        /// <summary>Guarda el auto elegido por el jugador local. Se conserva al cambiar de sala.</summary>
        void SetLocalCar(string carId);
        bool LocalReady { get; }
        (int ready, int required) ClientReadiness { get; }
        void SetLocalReady(bool ready);
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
        /// <summary>Identidad de la partida y cantidades de jugadores al comenzar la ronda.</summary>
        string MatchId { get; }
        int RoundTeamSize(byte team);
        /// <summary>Último estado confirmado de los objetivos de pintura de la sala.</summary>
        string RingSnapshot { get; }
        bool PublishRingSnapshot(string snapshot);
        /// <summary>Dupla cromática que publicó la autoridad al iniciar la partida.</summary>
        int PaletteIndex { get; }
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
        /// <summary>
        /// Solo el Master: crea un objeto de la sala, que no pertenece a ningún jugador y sigue aunque
        /// quien lo pidió se desconecte. Sin sala, lo crea solo localmente. Los datos llegan a todos los
        /// clientes en NetworkObjectInfo.Data, también a quien entra o vuelve después.
        /// </summary>
        GameObject SpawnShared(string prefabId, Vector3 position, Quaternion rotation, object[] data = null);
        /// <summary>Destruye un objeto propio (o de la sala, si es el Master) en todos los clientes.</summary>
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

        // Disparos y barriles
        /// <summary>Dispara: el proyectil aparece al instante en este cliente y los demás lo adelantan según la latencia.</summary>
        void RequestFire(int shotId, Vector3 origin, Vector3 velocity);
        event Action<FireInfo> FireReceived;
        /// <summary>Solo el Master: anuncia un impacto a todos, incluido él mismo.</summary>
        void SendImpact(ImpactInfo impact);
        event Action<ImpactInfo> ImpactReceived;

        /// <summary>La autoridad anuncia una carga ganada e indica si esa carga conquistó el aro.</summary>
        void AnnounceRingCharge(Vector3 position, bool won);
        /// <summary>Notificación puntual del cruce, también para quien juega sin sala.</summary>
        event Action<Vector3, bool> RingChargeReceived;

        /// <summary>Pide al Master soltar un barril.</summary>
        void RequestDropBarrel(Vector3 position, Vector3 direction);
        /// <summary>Un jugador pidió soltar un barril: actor, posición y dirección. Solo le llega al Master.</summary>
        event Action<int, Vector3, Vector3> BarrelDropRequested;
        /// <summary>Solo el Master: barriles en el baúl de un jugador.</summary>
        void SetBarrelCount(int actorId, int count);

        /// <summary>Pide al Master el barril de un pickup.</summary>
        void RequestPickup(int pickupId);
        /// <summary>Un jugador pidió un pickup: actor y pickup. Solo le llega al Master.</summary>
        event Action<int, int> PickupRequested;
        /// <summary>Solo el Master: aceptado se anuncia a todos; rechazado, solo a quien lo pidió.</summary>
        void SendPickupResult(int requesterActorId, PickupResult result);
        event Action<PickupResult> PickupResultReceived;
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
