using System;
using System.Collections.Generic;

namespace DriftPaint.Networking
{
    /// <summary>A quién va un evento de red.</summary>
    public enum NetTarget
    {
        All,
        Others,
        Master
    }

    /// <summary>Opciones para crear una sala, sin tipos del transporte.</summary>
    public sealed class NetRoomOptions
    {
        public int MaxPlayers;
        /// <summary>Milisegundos que un jugador desconectado conserva su lugar.</summary>
        public int PlayerTtlMs;
        /// <summary>Milisegundos que la sala sigue viva sin jugadores.</summary>
        public int EmptyRoomTtlMs;
        /// <summary>Si es false, los objetos de un jugador que se va quedan en la sala.</summary>
        public bool CleanupCacheOnLeave;
        public Dictionary<string, object> Properties = new Dictionary<string, object>();
        /// <summary>Claves de Properties que se ven en la lista de salas.</summary>
        public string[] LobbyProperties = Array.Empty<string>();
    }

    /// <summary>Sala del transporte: nombre único, ocupación y propiedades.</summary>
    public readonly struct NetRoomInfo
    {
        public readonly string Name;
        public readonly int PlayerCount;
        public readonly int MaxPlayers;
        public readonly bool IsOpen;
        public readonly IReadOnlyDictionary<string, object> Properties;

        public NetRoomInfo(string name, int playerCount, int maxPlayers, bool isOpen, IReadOnlyDictionary<string, object> properties)
        {
            Name = name;
            PlayerCount = playerCount;
            MaxPlayers = maxPlayers;
            IsOpen = isOpen;
            Properties = properties;
        }
    }

    /// <summary>Jugador del transporte con sus propiedades.</summary>
    public readonly struct NetPlayerInfo
    {
        public readonly int ActorNumber;
        public readonly string NickName;
        public readonly bool IsMaster;
        public readonly bool IsLocal;
        public readonly bool IsInactive;
        public readonly IReadOnlyDictionary<string, object> Properties;

        public NetPlayerInfo(int actorNumber, string nickName, bool isMaster, bool isLocal, bool isInactive,
            IReadOnlyDictionary<string, object> properties)
        {
            ActorNumber = actorNumber;
            NickName = nickName;
            IsMaster = isMaster;
            IsLocal = isLocal;
            IsInactive = isInactive;
            Properties = properties;
        }
    }

    /// <summary>
    /// Contrato de transporte que define la capa de Red: conexión, salas, eventos y propiedades.
    /// No sabe qué es una celda, un barril o un equipo. Lo implementa la capa de Transporte.
    /// </summary>
    public interface INetworkProvider
    {
        bool IsConnected { get; }
        bool InLobby { get; }
        bool InRoom { get; }
        bool IsMaster { get; }
        int LocalActorNumber { get; }
        double ServerTime { get; }
        int Ping { get; }
        string NickName { get; set; }

        NetRoomInfo CurrentRoom { get; }
        IReadOnlyList<NetRoomInfo> RoomList { get; }
        /// <summary>Jugadores de la sala actual, incluidos los inactivos.</summary>
        IReadOnlyList<NetPlayerInfo> Players { get; }

        bool Connect();
        void Disconnect();
        bool JoinLobby();
        bool CreateRoom(string name, NetRoomOptions options);
        bool JoinRoom(string name);
        bool LeaveRoom();

        bool SendEvent(byte code, object payload, NetTarget target, bool reliable);
        /// <summary>Evento para un solo jugador.</summary>
        bool SendEventTo(byte code, object payload, int actorNumber, bool reliable);
        /// <summary>Registra un tipo propio con su serialización, para mandarlo como contenido de eventos.</summary>
        void RegisterCustomType(Type type, byte typeCode, Func<object, byte[]> serialize, Func<byte[], object> deserialize);
        void SetMessageQueuePaused(bool paused);

        void RegisterPrefab(string prefabId, UnityEngine.GameObject prefab);
        /// <summary>Crea en red un objeto del jugador local. Devuelve null si no se pudo.</summary>
        UnityEngine.GameObject Instantiate(string prefabId, UnityEngine.Vector3 position, UnityEngine.Quaternion rotation);
        void Destroy(UnityEngine.GameObject instance);

        /// <summary>Objeto de red creado en este cliente: instancia, dueño, si es propio y prefab.</summary>
        event Action<UnityEngine.GameObject, int, bool, string> ObjectInstantiated;
        event Action<UnityEngine.GameObject> ObjectDestroyed;
        bool SetRoomProperties(IDictionary<string, object> properties);
        bool SetPlayerProperties(int actorNumber, IDictionary<string, object> properties);

        /// <summary>Conectado al servidor y listo para entrar al lobby (también al salir de una sala).</summary>
        event Action ConnectedToServer;
        event Action<string> Disconnected;
        event Action JoinedLobby;
        event Action RoomListUpdated;
        event Action JoinedRoom;
        event Action LeftRoom;
        event Action<string> CreateRoomFailed;
        event Action<string> JoinRoomFailed;
        event Action<int> PlayerEntered;
        event Action<int> PlayerLeft;
        event Action<int> MasterSwitched;
        event Action<IReadOnlyDictionary<string, object>> RoomPropertiesChanged;
        event Action<int, IReadOnlyDictionary<string, object>> PlayerPropertiesChanged;
        /// <summary>Evento recibido: código, emisor y contenido.</summary>
        event Action<byte, int, object> EventReceived;
    }
}
