using System;
using System.Collections.Generic;
using DriftPaint.Networking;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace DriftPaint.Transport
{
    /// <summary>
    /// Implementación de INetworkProvider sobre Photon PUN. Es la única clase del proyecto que conoce Photon:
    /// convierte callbacks, Hashtables y opciones de Photon en los tipos neutros de la capa de Red.
    /// </summary>
    public sealed class PhotonManager : MonoBehaviourPunCallbacks, IOnEventCallback, INetworkProvider
    {
        /// <summary>Los códigos desde 200 los reserva Photon para sus propios mensajes.</summary>
        const byte FirstReservedEventCode = 200;
        /// <summary>Región que se usa si PhotonServerSettings no tiene una fija (sa = Sudamérica).</summary>
        const string FallbackRegion = "sa";

        readonly Dictionary<string, NetRoomInfo> _roomCache = new Dictionary<string, NetRoomInfo>();
        readonly List<NetRoomInfo> _roomList = new List<NetRoomInfo>();
        readonly List<NetPlayerInfo> _players = new List<NetPlayerInfo>();

        PhotonPrefabPool _pool;

        void Awake()
        {
            PhotonNetwork.AutomaticallySyncScene = false;
            PhotonNetwork.SendRate = 30;
            PhotonNetwork.SerializationRate = 20;

            _pool = new PhotonPrefabPool(
                (instance, owner, isMine, prefabId) => ObjectInstantiated?.Invoke(instance, owner, isMine, prefabId),
                instance => ObjectDestroyed?.Invoke(instance));
            PhotonNetwork.PrefabPool = _pool;
        }

        #region Estado

        public bool IsConnected => PhotonNetwork.IsConnectedAndReady;
        public bool InLobby => PhotonNetwork.InLobby;
        public bool InRoom => PhotonNetwork.InRoom;
        public bool IsMaster => PhotonNetwork.IsMasterClient;
        public int LocalActorNumber => PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
        public double ServerTime => PhotonNetwork.IsConnected ? PhotonNetwork.Time : Time.realtimeSinceStartupAsDouble;
        public int Ping => PhotonNetwork.IsConnected ? PhotonNetwork.GetPing() : 0;

        public string NickName
        {
            get => PhotonNetwork.NickName;
            set => PhotonNetwork.NickName = value;
        }

        public NetRoomInfo CurrentRoom => PhotonNetwork.CurrentRoom != null ? ToNetRoom(PhotonNetwork.CurrentRoom) : default;
        public IReadOnlyList<NetRoomInfo> RoomList => _roomList;

        public IReadOnlyList<NetPlayerInfo> Players
        {
            get
            {
                _players.Clear();
                Room room = PhotonNetwork.CurrentRoom;
                if (room == null) return _players;

                foreach (Player player in room.Players.Values)
                    _players.Add(ToNetPlayer(player));
                return _players;
            }
        }

        #endregion

        #region Acciones

        public bool Connect()
        {
            if (PhotonNetwork.IsConnected) return true;

            // Sin región fija, cada cliente elige la de menor ping y dos jugadores pueden terminar
            // en servidores distintos sin verse las salas.
            AppSettings settings = PhotonNetwork.PhotonServerSettings.AppSettings;
            if (string.IsNullOrEmpty(settings.FixedRegion)) settings.FixedRegion = FallbackRegion;

            return PhotonNetwork.ConnectUsingSettings();
        }

        public void Disconnect()
        {
            if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
        }

        public bool JoinLobby()
        {
            if (PhotonNetwork.InLobby) return true;
            return PhotonNetwork.JoinLobby();
        }

        public bool CreateRoom(string name, NetRoomOptions options)
        {
            var roomOptions = new RoomOptions
            {
                MaxPlayers = options.MaxPlayers,
                PlayerTtl = options.PlayerTtlMs,
                EmptyRoomTtl = options.EmptyRoomTtlMs,
                CleanupCacheOnLeave = options.CleanupCacheOnLeave,
                IsOpen = true,
                IsVisible = true,
                CustomRoomProperties = ToHashtable(options.Properties),
                CustomRoomPropertiesForLobby = options.LobbyProperties
            };
            return PhotonNetwork.CreateRoom(name, roomOptions);
        }

        public bool JoinRoom(string name) => PhotonNetwork.JoinRoom(name);

        /// <summary>Sale de la sala sin reservar el lugar: el jugador eligió irse.</summary>
        public bool LeaveRoom() => PhotonNetwork.InRoom && PhotonNetwork.LeaveRoom(false);

        public bool SendEvent(byte code, object payload, NetTarget target, bool reliable)
        {
            if (!PhotonNetwork.InRoom || code >= FirstReservedEventCode) return false;

            var options = new RaiseEventOptions { Receivers = ToReceivers(target) };
            return PhotonNetwork.RaiseEvent(code, payload, options, reliable ? SendOptions.SendReliable : SendOptions.SendUnreliable);
        }

        public void SetMessageQueuePaused(bool paused) => PhotonNetwork.IsMessageQueueRunning = !paused;

        public void RegisterPrefab(string prefabId, GameObject prefab) => _pool.Register(prefabId, prefab);

        public GameObject Instantiate(string prefabId, Vector3 position, Quaternion rotation)
        {
            if (!PhotonNetwork.InRoom || !_pool.Contains(prefabId)) return null;
            return PhotonNetwork.Instantiate(prefabId, position, rotation);
        }

        public void Destroy(GameObject instance)
        {
            if (instance == null) return;

            if (PhotonNetwork.InRoom && instance.TryGetComponent(out PhotonView view) && (view.IsMine || PhotonNetwork.IsMasterClient))
            {
                // También lo borra del caché de la sala, así no reaparece para quien entra después.
                PhotonNetwork.Destroy(instance);
                return;
            }

            ObjectDestroyed?.Invoke(instance);
            UnityEngine.Object.Destroy(instance);
        }

        public bool SetRoomProperties(IDictionary<string, object> properties)
        {
            Room room = PhotonNetwork.CurrentRoom;
            return room != null && room.SetCustomProperties(ToHashtable(properties));
        }

        public bool SetPlayerProperties(int actorNumber, IDictionary<string, object> properties)
        {
            Room room = PhotonNetwork.CurrentRoom;
            Player player = room?.GetPlayer(actorNumber);
            return player != null && player.SetCustomProperties(ToHashtable(properties));
        }

        #endregion

        #region Eventos de INetworkProvider

        public event Action ConnectedToServer;
        public event Action<string> Disconnected;
        public event Action JoinedLobby;
        public event Action RoomListUpdated;
        public event Action JoinedRoom;
        public event Action LeftRoom;
        public event Action<string> CreateRoomFailed;
        public event Action<string> JoinRoomFailed;
        public event Action<int> PlayerEntered;
        public event Action<int> PlayerLeft;
        public event Action<int> MasterSwitched;
        public event Action<IReadOnlyDictionary<string, object>> RoomPropertiesChanged;
        public event Action<int, IReadOnlyDictionary<string, object>> PlayerPropertiesChanged;
        public event Action<byte, int, object> EventReceived;
        public event Action<GameObject, int, bool, string> ObjectInstantiated;
        public event Action<GameObject> ObjectDestroyed;

        #endregion

        #region Callbacks de Photon

        public override void OnConnectedToMaster() => ConnectedToServer?.Invoke();

        public override void OnDisconnected(DisconnectCause cause)
        {
            ClearRoomCache();
            Disconnected?.Invoke(cause == DisconnectCause.DisconnectByClientLogic ? null : cause.ToString());
        }

        public override void OnJoinedLobby()
        {
            ClearRoomCache();
            JoinedLobby?.Invoke();
        }

        public override void OnLeftLobby() => ClearRoomCache();

        public override void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            foreach (RoomInfo info in roomList)
            {
                if (info.RemovedFromList || !info.IsVisible) _roomCache.Remove(info.Name);
                else _roomCache[info.Name] = ToNetRoom(info);
            }

            _roomList.Clear();
            _roomList.AddRange(_roomCache.Values);
            RoomListUpdated?.Invoke();
        }

        public override void OnJoinedRoom()
        {
            ClearRoomCache();
            JoinedRoom?.Invoke();
        }

        public override void OnLeftRoom() => LeftRoom?.Invoke();

        public override void OnCreateRoomFailed(short returnCode, string message) => CreateRoomFailed?.Invoke(message);

        public override void OnJoinRoomFailed(short returnCode, string message) => JoinRoomFailed?.Invoke(DescribeJoinFailure(returnCode, message));

        public override void OnPlayerEnteredRoom(Player newPlayer) => PlayerEntered?.Invoke(newPlayer.ActorNumber);

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            // Con CleanupCacheOnLeave en false los objetos de quien se va quedan en la sala. Si se fue
            // del todo (no está esperando reconectarse), el Master los borra para que no queden autos fantasma.
            if (PhotonNetwork.IsMasterClient && !otherPlayer.IsInactive)
                PhotonNetwork.DestroyPlayerObjects(otherPlayer);

            PlayerLeft?.Invoke(otherPlayer.ActorNumber);
        }

        public override void OnMasterClientSwitched(Player newMasterClient) => MasterSwitched?.Invoke(newMasterClient.ActorNumber);

        public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) =>
            RoomPropertiesChanged?.Invoke(ToDictionary(propertiesThatChanged));

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) =>
            PlayerPropertiesChanged?.Invoke(targetPlayer.ActorNumber, ToDictionary(changedProps));

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code >= FirstReservedEventCode) return;
            EventReceived?.Invoke(photonEvent.Code, photonEvent.Sender, photonEvent.CustomData);
        }

        #endregion

        #region Conversiones

        void ClearRoomCache()
        {
            _roomCache.Clear();
            _roomList.Clear();
        }

        static NetRoomInfo ToNetRoom(RoomInfo room)
        {
            return new NetRoomInfo(room.Name, room.PlayerCount, room.MaxPlayers, room.IsOpen, ToDictionary(room.CustomProperties));
        }

        static NetPlayerInfo ToNetPlayer(Player player)
        {
            return new NetPlayerInfo(player.ActorNumber, player.NickName, player.IsMasterClient, player.IsLocal,
                player.IsInactive, ToDictionary(player.CustomProperties));
        }

        static ReceiverGroup ToReceivers(NetTarget target)
        {
            switch (target)
            {
                case NetTarget.Others: return ReceiverGroup.Others;
                case NetTarget.Master: return ReceiverGroup.MasterClient;
                default: return ReceiverGroup.All;
            }
        }

        static string DescribeJoinFailure(short returnCode, string message)
        {
            switch (returnCode)
            {
                case ErrorCode.GameFull: return "La sala está llena.";
                case ErrorCode.GameClosed: return "La sala ya no acepta jugadores.";
                case ErrorCode.GameDoesNotExist: return "La sala no existe.";
                default: return message;
            }
        }

        static Hashtable ToHashtable(IEnumerable<KeyValuePair<string, object>> properties)
        {
            var table = new Hashtable();
            if (properties == null) return table;
            foreach (KeyValuePair<string, object> pair in properties)
                table[pair.Key] = pair.Value;
            return table;
        }

        static Dictionary<string, object> ToDictionary(Hashtable table)
        {
            var dictionary = new Dictionary<string, object>();
            if (table == null) return dictionary;
            foreach (System.Collections.DictionaryEntry entry in table)
                if (entry.Key is string key) dictionary[key] = entry.Value;
            return dictionary;
        }

        #endregion
    }
}
