using System;
using System.Collections.Generic;
using System.Text;
using DriftPaint.Net;
using DriftPaint.Paint;
using DriftPaint.Profile;
using DriftPaint.Teams;
using UnityEngine;

namespace DriftPaint.Networking
{
    /// <summary>
    /// Traduce entre el lenguaje del juego y el transporte: arma las salas, asigna equipos,
    /// serializa los pedidos de partida y publica lo que llega como eventos del juego.
    /// No depende de ninguna librería de red concreta, solo de INetworkProvider.
    /// </summary>
    public sealed class NetworkManager : INetworkMediator
    {
        const string RoomIdAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        const int RoomIdLength = 5;
        const float ChatCooldownSeconds = 0.5f;

        readonly INetworkProvider _provider;
        readonly List<RoomSummary> _rooms = new List<RoomSummary>();
        readonly List<PlayerSummary> _players = new List<PlayerSummary>();
        readonly System.Random _random = new System.Random();
        readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
        readonly List<NetworkObjectInfo> _objects = new List<NetworkObjectInfo>();
        int _scoreA;
        int _scoreB;
        int _round;
        byte _winner;

        // Último puntaje enviado, para publicar solo lo que cambió.
        int _sentScoreA = -1;
        int _sentScoreB = -1;
        readonly Dictionary<int, (int points, int standing)> _sentPlayerScores = new Dictionary<int, (int points, int standing)>();

        ConnectionState _state = ConnectionState.Disconnected;
        string _nickname = "";
        RoomSummary _currentRoom;
        MatchPhase _phase = MatchPhase.Lobby;
        double _matchStartTime;
        bool _disconnectRequested;
        bool _disconnectAfterLeave;
        float _nextChatTime;

        public NetworkManager(INetworkProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));

            _provider.ConnectedToServer += OnConnectedToServer;
            _provider.Disconnected += OnDisconnected;
            _provider.JoinedLobby += OnJoinedLobby;
            _provider.RoomListUpdated += OnRoomListUpdated;
            _provider.JoinedRoom += OnJoinedRoom;
            _provider.LeftRoom += OnLeftRoom;
            _provider.CreateRoomFailed += OnCreateRoomFailed;
            _provider.JoinRoomFailed += OnJoinRoomFailed;
            _provider.PlayerEntered += OnPlayerEntered;
            _provider.PlayerLeft += OnPlayerLeft;
            _provider.MasterSwitched += OnMasterSwitched;
            _provider.RoomPropertiesChanged += OnRoomPropertiesChanged;
            _provider.PlayerPropertiesChanged += OnPlayerPropertiesChanged;
            _provider.EventReceived += OnEventReceived;
            _provider.ObjectInstantiated += OnObjectInstantiated;
            _provider.ObjectDestroyed += OnObjectDestroyed;

            _provider.RegisterCustomType(typeof(PaintDelta), NetTypeCodes.PaintDelta,
                value => PaintCodec.EncodeDelta((PaintDelta)value), data => PaintCodec.DecodeDelta(data));
            _provider.RegisterCustomType(typeof(PaintSnapshot), NetTypeCodes.PaintSnapshot,
                value => PaintCodec.EncodeSnapshot((PaintSnapshot)value), data => PaintCodec.DecodeSnapshot(data));
        }

        #region INetworkMediator: estado

        public ConnectionState State => _state;
        public string LocalNickname => _nickname;
        public int LocalActorId => _provider.LocalActorNumber;
        public bool IsMaster => _provider.InRoom && _provider.IsMaster;
        public double ServerTime => _provider.ServerTime;
        public int Ping => _provider.Ping;
        public IReadOnlyList<RoomSummary> Rooms => _rooms;
        public RoomSummary CurrentRoom => _currentRoom;
        public IReadOnlyList<PlayerSummary> Players => _players;
        public MatchPhase Phase => _phase;
        public double MatchStartTime => _matchStartTime;
        public int ScoreA => _scoreA;
        public int ScoreB => _scoreB;
        public int Round => _round;
        public byte Winner => _winner;
        public IReadOnlyList<NetworkObjectInfo> Objects
        {
            get
            {
                _objects.RemoveAll(o => o.Instance == null);
                return _objects;
            }
        }

        public event Action StateChanged;
        public event Action<string> ErrorRaised;
        public event Action RoomListChanged;
        public event Action RoomChanged;
        public event Action PlayersChanged;
        public event Action<MatchPhase> PhaseChanged;
        public event Action<ChatMessage> ChatReceived;
        public event Action<FireInfo> FireReceived;
        public event Action<ImpactInfo> ImpactReceived;
        public event Action<NetworkObjectInfo> ObjectSpawned;
        public event Action<GameObject> ObjectDespawned;
        public event Action<PaintDelta> PaintDeltaReceived;
        public event Action<int> PaintSnapshotRequested;
        public event Action<PaintSnapshot> PaintSnapshotReceived;
        public event Action<int, Vector3, Vector3> BarrelDropRequested;
        public event Action<int, int> PickupRequested;
        public event Action<PickupResult> PickupResultReceived;

        #endregion

        #region INetworkMediator: pintura

        public void SendPaintDelta(PaintDelta delta)
        {
            if (!IsMaster || delta == null || delta.IsEmpty) return;
            _provider.SendEvent(NetEventCodes.PaintDelta, delta, NetTarget.Others, true);
        }

        public void RequestPaintSnapshot()
        {
            if (!_provider.InRoom || IsMaster) return;
            _provider.SendEvent(NetEventCodes.PaintSnapshotRequest, null, NetTarget.Master, true);
        }

        public void SendPaintSnapshot(int actorId, PaintSnapshot snapshot)
        {
            if (!IsMaster || snapshot == null) return;
            _provider.SendEventTo(NetEventCodes.PaintSnapshot, snapshot, actorId, true);
        }

        #endregion

        #region INetworkMediator: conexión y salas

        public void Connect(string nickname)
        {
            if (_state != ConnectionState.Disconnected) return;

            SetNickname(nickname);
            _disconnectRequested = false;
            SetState(ConnectionState.Connecting);

            if (!_provider.Connect())
            {
                SetState(ConnectionState.Disconnected);
                RaiseError("No se pudo iniciar la conexión con el servidor.");
            }
        }

        public void Disconnect()
        {
            if (_state == ConnectionState.Disconnected) return;

            _disconnectRequested = true;
            _provider.Disconnect();
        }

        public void SetNickname(string nickname)
        {
            string clean = PlayerProfile.Sanitize(nickname);
            if (clean.Length == 0) clean = PlayerProfile.LoadNickname();

            _nickname = clean;
            _provider.NickName = clean;
            if (_provider.InRoom) RefreshPlayers();
        }

        public bool CreateRoom(RoomSettings settings)
        {
            if (_state != ConnectionState.InLobby) return false;

            string title = CleanText(settings.Title, 32);
            if (title.Length == 0) title = $"Sala de {_nickname}";

            var options = new NetRoomOptions
            {
                MaxPlayers = Mathf.Clamp(settings.MaxPlayers <= 0 ? RoomRules.MaxPlayers : settings.MaxPlayers, 1, RoomRules.MaxPlayers),
                PlayerTtlMs = RoomRules.PlayerTtlMs,
                EmptyRoomTtlMs = 0,
                CleanupCacheOnLeave = false,
                LobbyProperties = RoomKeys.Lobby
            };
            options.Properties[RoomKeys.Title] = title;
            options.Properties[RoomKeys.State] = (byte)MatchPhase.Lobby;
            options.Properties[RoomKeys.Duration] = ClampDuration(settings.DurationSeconds);
            options.Properties[RoomKeys.Map] = string.IsNullOrEmpty(settings.MapId) ? RoomRules.DefaultMapId : settings.MapId;
            options.Properties[RoomKeys.Round] = 0;
            options.Properties[RoomKeys.ScoreA] = 0;
            options.Properties[RoomKeys.ScoreB] = 0;

            _provider.ClearLocalPlayerProperties(PlayerKeys.RoomScoped);
            if (!_provider.CreateRoom(NewRoomId(), options)) return false;

            SetState(ConnectionState.JoiningRoom);
            return true;
        }

        public bool JoinRoom(string roomId)
        {
            if (_state != ConnectionState.InLobby || string.IsNullOrWhiteSpace(roomId)) return false;
            // Sin esto, el equipo y el puntaje de la sala anterior llegan a la nueva y el Master no reparte.
            _provider.ClearLocalPlayerProperties(PlayerKeys.RoomScoped);
            if (!_provider.JoinRoom(roomId.Trim().ToUpperInvariant())) return false;

            SetState(ConnectionState.JoiningRoom);
            return true;
        }

        public void LeaveRoom()
        {
            if (_state != ConnectionState.InRoom) return;
            if (_provider.LeaveRoom()) SetState(ConnectionState.LeavingRoom);
        }

        public void LeaveRoomAndDisconnect()
        {
            if (_state != ConnectionState.InRoom)
            {
                Disconnect();
                return;
            }

            // Salir primero libera el lugar en la sala; desconectarse directo lo dejaría reservado.
            _disconnectAfterLeave = true;
            LeaveRoom();
        }

        #endregion

        #region INetworkMediator: sala actual

        public void SetRoomDuration(int seconds)
        {
            if (!IsMaster || !_phase.IsBetweenMatches()) return;
            _provider.SetRoomProperties(new Dictionary<string, object> { { RoomKeys.Duration, ClampDuration(seconds) } });
        }

        public void RequestTeamChange()
        {
            if (_state != ConnectionState.InRoom) return;
            if (IsMaster) HandleTeamChange(LocalActorId);
            else _provider.SendEvent(NetEventCodes.TeamChangeRequest, (byte)0, NetTarget.Master, true);
        }

        public void SetLocalCar(string carId)
        {
            if (_state != ConnectionState.InRoom || string.IsNullOrEmpty(carId)) return;
            _provider.SetPlayerProperties(LocalActorId, new Dictionary<string, object> { { PlayerKeys.Car, carId } });
        }

        /// <summary>Solo el Master: pasa al jugador al otro equipo si se está entre partidas y quedan parejos.</summary>
        void HandleTeamChange(int actorId)
        {
            if (!IsMaster || !_phase.IsBetweenMatches() || !TeamRules.CanSwitch(_players, actorId)) return;
            if (!TryGetPlayer(actorId, out PlayerSummary player)) return;
            AssignTeam(actorId, TeamId.Other(player.Team));
        }

        public void SendChat(string text)
        {
            if (_state != ConnectionState.InRoom) return;

            string clean = CleanText(text, RoomRules.MaxChatLength);
            if (clean.Length == 0) return;
            if (Time.realtimeSinceStartup < _nextChatTime) return;
            _nextChatTime = Time.realtimeSinceStartup + ChatCooldownSeconds;

            if (_provider.SendEvent(NetEventCodes.Chat, clean, NetTarget.Others, true))
                ChatReceived?.Invoke(new ChatMessage(false, _nickname, LocalTeam(), clean));
        }

        public bool RequestStartMatch()
        {
            if (!IsMaster || !_phase.IsBetweenMatches()) return false;

            // Los puntajes de la partida anterior se limpian antes del cambio de estado: llegan en ese orden.
            ResetSentScores();
            // El inventario de barriles se borra: el Master lo vuelve a llenar al arrancar la partida.
            foreach (PlayerSummary player in _players)
                _provider.SetPlayerProperties(player.ActorId, new Dictionary<string, object>
                {
                    { PlayerKeys.Points, new[] { 0, 0 } },
                    { PlayerKeys.Barrels, null }
                });

            return _provider.SetRoomProperties(new Dictionary<string, object>
            {
                { RoomKeys.State, (byte)MatchPhase.Countdown },
                { RoomKeys.StartTime, ServerTime + RoomRules.CountdownSeconds },
                { RoomKeys.Round, 1 },
                { RoomKeys.ScoreA, 0 },
                { RoomKeys.ScoreB, 0 },
                { RoomKeys.Winner, TeamId.None }
            });
        }

        public void PublishTeamScores(int cellsA, int cellsB)
        {
            if (!IsMaster || (cellsA == _sentScoreA && cellsB == _sentScoreB)) return;

            if (_provider.SetRoomProperties(new Dictionary<string, object> { { RoomKeys.ScoreA, cellsA }, { RoomKeys.ScoreB, cellsB } }))
            {
                _sentScoreA = cellsA;
                _sentScoreB = cellsB;
            }
        }

        public void PublishPlayerScore(int actorId, int points, int standing)
        {
            if (!IsMaster) return;
            if (_sentPlayerScores.TryGetValue(actorId, out (int points, int standing) sent) && sent.points == points && sent.standing == standing)
                return;

            if (_provider.SetPlayerProperties(actorId, new Dictionary<string, object> { { PlayerKeys.Points, new[] { points, standing } } }))
                _sentPlayerScores[actorId] = (points, standing);
        }

        public bool FinishMatch(int cellsA, int cellsB, byte winner)
        {
            if (!IsMaster) return false;

            bool sent = _provider.SetRoomProperties(new Dictionary<string, object>
            {
                { RoomKeys.ScoreA, cellsA },
                { RoomKeys.ScoreB, cellsB },
                { RoomKeys.Winner, winner },
                { RoomKeys.State, (byte)MatchPhase.Results }
            });
            if (sent)
            {
                _sentScoreA = cellsA;
                _sentScoreB = cellsB;
            }
            return sent;
        }

        public bool StartTiebreak(double startTime)
        {
            if (!IsMaster) return false;

            return _provider.SetRoomProperties(new Dictionary<string, object>
            {
                { RoomKeys.State, (byte)MatchPhase.Tiebreak },
                { RoomKeys.StartTime, startTime },
                { RoomKeys.Round, Mathf.Max(1, _round) + 1 }
            });
        }

        public bool SetMatchPhase(MatchPhase phase)
        {
            if (!IsMaster || phase == _phase) return false;

            var properties = new Dictionary<string, object> { { RoomKeys.State, (byte)phase } };
            if (phase == MatchPhase.Lobby)
            {
                properties[RoomKeys.StartTime] = 0d;
                properties[RoomKeys.Round] = 0;
            }
            return _provider.SetRoomProperties(properties);
        }

        public void SetIncomingPaused(bool paused) => _provider.SetMessageQueuePaused(paused);

        #endregion

        #region INetworkMediator: objetos de red

        public void RegisterPrefab(string prefabId, GameObject prefab)
        {
            if (string.IsNullOrEmpty(prefabId) || prefab == null) return;
            _prefabs[prefabId] = prefab;
            _provider.RegisterPrefab(prefabId, prefab);
        }

        public GameObject SpawnOwned(string prefabId, Vector3 position, Quaternion rotation)
        {
            if (_provider.InRoom) return _provider.Instantiate(prefabId, position, rotation);
            return SpawnLocal(prefabId, position, rotation);
        }

        public GameObject SpawnShared(string prefabId, Vector3 position, Quaternion rotation)
        {
            if (_provider.InRoom) return IsMaster ? _provider.InstantiateRoomObject(prefabId, position, rotation) : null;
            return SpawnLocal(prefabId, position, rotation);
        }

        /// <summary>Sin sala se juega solo: el objeto se crea localmente y se anuncia igual.</summary>
        GameObject SpawnLocal(string prefabId, Vector3 position, Quaternion rotation)
        {
            if (!_prefabs.TryGetValue(prefabId, out GameObject prefab))
            {
                RaiseError($"No hay un prefab registrado como \"{prefabId}\".");
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
            instance.name = prefabId;
            OnObjectInstantiated(instance, LocalActorId, true, prefabId);
            return instance;
        }

        public void Despawn(GameObject instance)
        {
            if (instance == null) return;

            if (_provider.InRoom)
            {
                _provider.Destroy(instance);
                return;
            }

            OnObjectDestroyed(instance);
            UnityEngine.Object.Destroy(instance);
        }

        #endregion

        #region INetworkMediator: partida

        // Sin sala, este cliente es la autoridad: los pedidos se atienden localmente por el mismo camino.

        public void RequestFire(int shotId, Vector3 origin, Vector3 velocity)
        {
            double fireTime = ServerTime;
            if (_provider.InRoom)
                _provider.SendEvent(NetEventCodes.FireRequest, new object[] { shotId, origin, velocity, fireTime }, NetTarget.Others, true);
            FireReceived?.Invoke(new FireInfo(LocalActorId, shotId, origin, velocity, fireTime));
        }

        public void SendImpact(ImpactInfo impact)
        {
            if (_provider.InRoom)
            {
                if (!IsMaster) return;
                byte code = impact.Kind == ImpactKind.Barrel ? NetEventCodes.BarrelExplode : NetEventCodes.ProjectileImpact;
                var payload = new object[]
                {
                    impact.Position, (byte)impact.Kind, impact.SourceActorId, impact.ShotId, impact.StunnedActorId, impact.SlowedActorIds
                };
                _provider.SendEvent(code, payload, NetTarget.Others, true);
            }
            ImpactReceived?.Invoke(impact);
        }

        public void RequestDropBarrel(Vector3 position, Vector3 direction)
        {
            if (_provider.InRoom)
                _provider.SendEvent(NetEventCodes.DropBarrelRequest, new object[] { position, direction }, NetTarget.Master, true);
            else
                BarrelDropRequested?.Invoke(LocalActorId, position, direction);
        }

        public void SetBarrelCount(int actorId, int count)
        {
            if (!IsMaster) return;
            _provider.SetPlayerProperties(actorId, new Dictionary<string, object> { { PlayerKeys.Barrels, count } });
        }

        public void RequestPickup(int pickupId)
        {
            if (_provider.InRoom)
                _provider.SendEvent(NetEventCodes.PickupRequest, pickupId, NetTarget.Master, true);
            else
                PickupRequested?.Invoke(LocalActorId, pickupId);
        }

        public void SendPickupResult(int requesterActorId, PickupResult result)
        {
            if (!_provider.InRoom)
            {
                PickupResultReceived?.Invoke(result);
                return;
            }
            if (!IsMaster) return;

            var payload = new object[] { result.PickupId, result.ActorId, result.RespawnTime };
            if (result.Accepted)
            {
                _provider.SendEvent(NetEventCodes.PickupResult, payload, NetTarget.Others, true);
                PickupResultReceived?.Invoke(result);
            }
            else if (requesterActorId == LocalActorId)
            {
                PickupResultReceived?.Invoke(result);
            }
            else
            {
                _provider.SendEventTo(NetEventCodes.PickupResult, payload, requesterActorId, true);
            }
        }

        #endregion

        #region Callbacks del transporte

        void OnConnectedToServer()
        {
            if (_disconnectRequested) return;
            if (_disconnectAfterLeave)
            {
                _disconnectAfterLeave = false;
                Disconnect();
                return;
            }

            if (!_provider.JoinLobby())
            {
                RaiseError("No se pudo entrar a la lista de salas.");
                _provider.Disconnect();
            }
        }

        void OnDisconnected(string cause)
        {
            bool wasInRoom = _currentRoom.IsValid;
            bool expected = _disconnectRequested;
            _disconnectRequested = false;
            _disconnectAfterLeave = false;

            ClearRoom();
            _rooms.Clear();
            SetState(ConnectionState.Disconnected);
            RoomListChanged?.Invoke();
            if (wasInRoom) RoomChanged?.Invoke();

            if (!expected) RaiseError(string.IsNullOrEmpty(cause) ? "Se perdió la conexión con el servidor." : $"Se perdió la conexión con el servidor ({cause}).");
        }

        void OnJoinedLobby()
        {
            SetState(ConnectionState.InLobby);
            OnRoomListUpdated();
        }

        void OnRoomListUpdated()
        {
            _rooms.Clear();
            foreach (NetRoomInfo room in _provider.RoomList)
                _rooms.Add(ToRoomSummary(room));

            _rooms.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));
            RoomListChanged?.Invoke();
        }

        void OnJoinedRoom()
        {
            RefreshRoom(false);
            RefreshPlayers();
            if (IsMaster) EnsureTeams();

            SetState(ConnectionState.InRoom);
            RoomChanged?.Invoke();
            PlayersChanged?.Invoke();
            ChatReceived?.Invoke(ChatMessage.System($"Entraste a la sala {_currentRoom.Title}."));
        }

        void OnLeftRoom()
        {
            ClearRoom();
            RoomChanged?.Invoke();
            PlayersChanged?.Invoke();

            if (_state != ConnectionState.Disconnected)
                SetState(_provider.InLobby ? ConnectionState.InLobby : ConnectionState.Connecting);
        }

        void OnCreateRoomFailed(string message)
        {
            SetState(_provider.InLobby ? ConnectionState.InLobby : ConnectionState.Connecting);
            RaiseError($"No se pudo crear la sala. {message}");
        }

        void OnJoinRoomFailed(string message)
        {
            SetState(_provider.InLobby ? ConnectionState.InLobby : ConnectionState.Connecting);
            RaiseError($"No se pudo entrar a la sala. {message}");
        }

        void OnPlayerEntered(int actorId)
        {
            RefreshPlayers();
            if (IsMaster) EnsureTeams();

            PlayersChanged?.Invoke();
            ChatReceived?.Invoke(ChatMessage.System($"{NameOf(actorId)} entró a la sala."));
        }

        void OnPlayerLeft(int actorId)
        {
            string name = NameOf(actorId);
            RefreshPlayers();
            bool stillReserved = TryGetPlayer(actorId, out PlayerSummary player) && player.IsInactive;
            if (IsMaster) EnsureTeams();

            PlayersChanged?.Invoke();
            ChatReceived?.Invoke(ChatMessage.System(stillReserved ? $"{name} se desconectó." : $"{name} salió de la sala."));
        }

        void OnMasterSwitched(int newMasterActorId)
        {
            ResetSentScores();
            RefreshPlayers();
            if (IsMaster) EnsureTeams();

            PlayersChanged?.Invoke();
            RoomChanged?.Invoke();
            ChatReceived?.Invoke(ChatMessage.System($"{NameOf(newMasterActorId)} ahora es el host."));
        }

        void OnRoomPropertiesChanged(IReadOnlyDictionary<string, object> changed)
        {
            MatchPhase previous = _phase;
            RefreshRoom(true);
            // Al volver al lobby se empareja lo que se desarmó durante la partida (gente que salió o entró).
            if (IsMaster && _phase.IsBetweenMatches() && !previous.IsBetweenMatches()) EnsureTeams();
            RoomChanged?.Invoke();
        }

        void OnPlayerPropertiesChanged(int actorId, IReadOnlyDictionary<string, object> changed)
        {
            RefreshPlayers();
            PlayersChanged?.Invoke();
        }

        void OnEventReceived(byte code, int senderActorId, object payload)
        {
            switch (code)
            {
                case NetEventCodes.Chat:
                    if (payload is string text)
                    {
                        string clean = CleanText(text, RoomRules.MaxChatLength);
                        if (clean.Length == 0) return;
                        TryGetPlayer(senderActorId, out PlayerSummary sender);
                        ChatReceived?.Invoke(new ChatMessage(false, NameOf(senderActorId), sender.Team, clean));
                    }
                    break;

                case NetEventCodes.FireRequest:
                    if (payload is object[] fire && fire.Length >= 4 && fire[1] is Vector3 origin && fire[2] is Vector3 velocity)
                        FireReceived?.Invoke(new FireInfo(senderActorId, ToInt(fire[0], 0), origin, velocity, ToDouble(fire[3], ServerTime)));
                    break;

                case NetEventCodes.DropBarrelRequest:
                    if (IsMaster && payload is object[] drop && drop.Length >= 2 && drop[0] is Vector3 dropPosition && drop[1] is Vector3 dropDirection)
                        BarrelDropRequested?.Invoke(senderActorId, dropPosition, dropDirection);
                    break;

                case NetEventCodes.TeamChangeRequest:
                    if (IsMaster) HandleTeamChange(senderActorId);
                    break;

                case NetEventCodes.PickupRequest:
                    if (IsMaster) PickupRequested?.Invoke(senderActorId, ToInt(payload, -1));
                    break;

                case NetEventCodes.PickupResult:
                    if (IsFromMaster(senderActorId) && payload is object[] pickup && pickup.Length >= 3)
                        PickupResultReceived?.Invoke(new PickupResult(ToInt(pickup[0], -1), ToInt(pickup[1], -1), ToDouble(pickup[2], 0)));
                    break;

                // La grilla oficial solo la escribe el Master: se ignora lo que mande cualquier otro.
                case NetEventCodes.PaintDelta:
                    if (payload is PaintDelta delta && IsFromMaster(senderActorId))
                        PaintDeltaReceived?.Invoke(delta);
                    break;

                case NetEventCodes.PaintSnapshot:
                    if (payload is PaintSnapshot snapshot && IsFromMaster(senderActorId))
                        PaintSnapshotReceived?.Invoke(snapshot);
                    break;

                case NetEventCodes.PaintSnapshotRequest:
                    if (IsMaster) PaintSnapshotRequested?.Invoke(senderActorId);
                    break;

                // Los impactos los decide el Master: se ignoran los de cualquier otro.
                case NetEventCodes.ProjectileImpact:
                case NetEventCodes.BarrelExplode:
                    if (IsFromMaster(senderActorId) && payload is object[] impact && impact.Length >= 6 && impact[0] is Vector3 position)
                    {
                        var kind = code == NetEventCodes.BarrelExplode ? ImpactKind.Barrel : ImpactKind.Projectile;
                        ImpactReceived?.Invoke(new ImpactInfo(position, kind, ToInt(impact[2], -1), ToInt(impact[3], -1),
                            ToInt(impact[4], -1), impact[5] as int[]));
                    }
                    break;
            }
        }

        void OnObjectInstantiated(GameObject instance, int ownerActorId, bool isLocal, string prefabId)
        {
            var info = new NetworkObjectInfo(instance, ownerActorId, isLocal, prefabId);
            _objects.RemoveAll(o => o.Instance == null || o.Instance == instance);
            _objects.Add(info);
            ObjectSpawned?.Invoke(info);
        }

        void OnObjectDestroyed(GameObject instance)
        {
            _objects.RemoveAll(o => o.Instance == null || o.Instance == instance);
            ObjectDespawned?.Invoke(instance);
        }

        #endregion

        #region Estado interno

        void SetState(ConnectionState state)
        {
            if (_state == state) return;
            _state = state;
            StateChanged?.Invoke();
        }

        void RaiseError(string message)
        {
            Debug.LogWarning($"[Red] {message}");
            ErrorRaised?.Invoke(message);
        }

        void ClearRoom()
        {
            _currentRoom = default;
            _players.Clear();
            _phase = MatchPhase.Lobby;
            _matchStartTime = 0;
            _scoreA = 0;
            _scoreB = 0;
            _round = 0;
            _winner = TeamId.None;
            _objects.Clear();
            ResetSentScores();
        }

        void ResetSentScores()
        {
            _sentScoreA = -1;
            _sentScoreB = -1;
            _sentPlayerScores.Clear();
        }

        void RefreshRoom(bool notifyPhase)
        {
            if (!_provider.InRoom) return;

            NetRoomInfo room = _provider.CurrentRoom;
            _currentRoom = ToRoomSummary(room);
            _matchStartTime = ToDouble(Get(room.Properties, RoomKeys.StartTime), 0);
            _scoreA = ToInt(Get(room.Properties, RoomKeys.ScoreA), 0);
            _scoreB = ToInt(Get(room.Properties, RoomKeys.ScoreB), 0);
            _round = ToInt(Get(room.Properties, RoomKeys.Round), 0);
            _winner = (byte)ToInt(Get(room.Properties, RoomKeys.Winner), TeamId.None);

            MatchPhase previous = _phase;
            _phase = _currentRoom.Phase;
            if (notifyPhase && previous != _phase) PhaseChanged?.Invoke(_phase);
        }

        void RefreshPlayers()
        {
            _players.Clear();
            foreach (NetPlayerInfo player in _provider.Players)
            {
                byte team = (byte)ToInt(Get(player.Properties, PlayerKeys.Team), TeamId.None);
                string name = string.IsNullOrEmpty(player.NickName) ? $"Jugador {player.ActorNumber}" : player.NickName;
                // pts viaja como { puntaje, celdas en pie }.
                int points = 0, standing = 0;
                if (Get(player.Properties, PlayerKeys.Points) is int[] pts && pts.Length >= 2)
                {
                    points = pts[0];
                    standing = pts[1];
                }
                int barrels = ToInt(Get(player.Properties, PlayerKeys.Barrels), -1);
                string car = Get(player.Properties, PlayerKeys.Car) as string;
                _players.Add(new PlayerSummary(player.ActorNumber, name, team, player.IsMaster, player.IsLocal, player.IsInactive,
                    points, standing, barrels, car));
            }
            _players.Sort((a, b) => a.ActorId.CompareTo(b.ActorId));
        }

        /// <summary>
        /// El Master mantiene los equipos parejos: da equipo a quien no tiene y, entre partidas, mueve
        /// jugadores si un equipo quedó con dos o más de ventaja. Durante la partida nadie cambia de equipo.
        /// </summary>
        void EnsureTeams()
        {
            foreach (TeamAssignment change in TeamRules.Balance(_players, _phase.IsBetweenMatches()))
                AssignTeam(change.ActorId, change.Team);
        }

        /// <summary>
        /// Las propiedades tardan un viaje al servidor, así que la lista local se actualiza a mano para
        /// que la próxima decisión ya cuente este cambio.
        /// </summary>
        void AssignTeam(int actorId, byte team)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerSummary p = _players[i];
                if (p.ActorId != actorId) continue;
                _players[i] = new PlayerSummary(p.ActorId, p.Nickname, team, p.IsMaster, p.IsLocal, p.IsInactive,
                    p.Points, p.Standing, p.Barrels, p.CarId);
                break;
            }
            _provider.SetPlayerProperties(actorId, new Dictionary<string, object> { { PlayerKeys.Team, team } });
            PlayersChanged?.Invoke();
        }

        bool TryGetPlayer(int actorId, out PlayerSummary player)
        {
            foreach (PlayerSummary candidate in _players)
            {
                if (candidate.ActorId != actorId) continue;
                player = candidate;
                return true;
            }
            player = default;
            return false;
        }

        bool IsFromMaster(int actorId) => TryGetPlayer(actorId, out PlayerSummary player) && player.IsMaster;

        string NameOf(int actorId)
        {
            return TryGetPlayer(actorId, out PlayerSummary player) ? player.Nickname : $"Jugador {actorId}";
        }

        byte LocalTeam()
        {
            return TryGetPlayer(LocalActorId, out PlayerSummary player) ? player.Team : TeamId.None;
        }

        string NewRoomId()
        {
            var builder = new StringBuilder(RoomIdLength);
            for (int i = 0; i < RoomIdLength; i++)
                builder.Append(RoomIdAlphabet[_random.Next(RoomIdAlphabet.Length)]);
            return builder.ToString();
        }

        static RoomSummary ToRoomSummary(NetRoomInfo room)
        {
            string title = Get(room.Properties, RoomKeys.Title) as string;
            string map = Get(room.Properties, RoomKeys.Map) as string;
            return new RoomSummary(
                room.Name,
                string.IsNullOrEmpty(title) ? room.Name : title,
                room.PlayerCount,
                room.MaxPlayers,
                room.IsOpen,
                string.IsNullOrEmpty(map) ? RoomRules.DefaultMapId : map,
                ToInt(Get(room.Properties, RoomKeys.Duration), RoomRules.DefaultDurationSeconds),
                (MatchPhase)ToInt(Get(room.Properties, RoomKeys.State), (int)MatchPhase.Lobby));
        }

        static int ClampDuration(int seconds)
        {
            if (seconds <= 0) seconds = RoomRules.DefaultDurationSeconds;
            int steps = Mathf.RoundToInt((float)seconds / RoomRules.DurationStepSeconds);
            return Mathf.Clamp(steps * RoomRules.DurationStepSeconds, RoomRules.MinDurationSeconds, RoomRules.MaxDurationSeconds);
        }

        /// <summary>Saca caracteres de control y espacios sobrantes, y corta el largo.</summary>
        static string CleanText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";

            var builder = new StringBuilder(text.Length);
            foreach (char c in text)
                if (!char.IsControl(c)) builder.Append(c);

            string clean = builder.ToString().Trim();
            return clean.Length > maxLength ? clean.Substring(0, maxLength) : clean;
        }

        static object Get(IReadOnlyDictionary<string, object> properties, string key)
        {
            return properties != null && properties.TryGetValue(key, out object value) ? value : null;
        }

        static int ToInt(object value, int fallback)
        {
            switch (value)
            {
                case int i: return i;
                case byte b: return b;
                case short s: return s;
                case long l: return (int)l;
                default: return fallback;
            }
        }

        static double ToDouble(object value, double fallback)
        {
            switch (value)
            {
                case double d: return d;
                case float f: return f;
                case int i: return i;
                default: return fallback;
            }
        }

        #endregion
    }
}
