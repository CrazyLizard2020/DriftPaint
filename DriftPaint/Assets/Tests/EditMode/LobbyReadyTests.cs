using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DriftPaint.Net;
using DriftPaint.Networking;
using DriftPaint.Profile;
using DriftPaint.Teams;
using DriftPaint.UI.Menu;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.Tests
{
    public class LobbyReadyTests
    {
        string reservationKey, savedReservation;

        [SetUp]
        public void SaveReservation()
        {
            reservationKey = "driftpaint.reservation." + PlayerProfile.LoadNetworkId();
            savedReservation = PlayerPrefs.HasKey(reservationKey) ? PlayerPrefs.GetString(reservationKey) : null;
        }

        [TearDown]
        public void RestoreReservation()
        {
            if (savedReservation == null) PlayerPrefs.DeleteKey(reservationKey);
            else PlayerPrefs.SetString(reservationKey, savedReservation);
            PlayerPrefs.Save();
        }

        [Test]
        public void DosClientesConfirmanPuedenCancelarYLaRevanchaRequiereConfirmarOtraVez()
        {
            var room = new LobbyTransport();
            var host = new NetworkManager(room);
            var clientRoom = new LobbyTransport { LocalActorNumber = 2 };
            var client = new NetworkManager(clientRoom);
            room.Join();
            clientRoom.Join();

            Assert.IsFalse(host.RequestStartMatch());
            client.SetLocalReady(true);
            Assert.IsTrue(client.LocalReady);
            Assert.IsFalse(client.RequestStartMatch());
            room.DeliverReady(2, clientRoom.ReadyValue(2));
            Assert.AreEqual((1, 2), host.ClientReadiness);
            client.SetLocalReady(false);
            room.DeliverReady(2, clientRoom.ReadyValue(2));
            Assert.AreEqual((0, 2), host.ClientReadiness);

            client.SetLocalReady(true);
            room.DeliverReady(2, clientRoom.ReadyValue(2));
            room.DeliverReady(3, clientRoom.ReadyValue(2));
            Assert.IsTrue(host.RequestStartMatch());
            room.DeliverPhase(MatchPhase.Results);
            room.DeliverPhase(MatchPhase.Lobby);
            Assert.AreEqual((0, 2), host.ClientReadiness);
            room.DeliverReady(3, clientRoom.ReadyValue(2)); // Confirmación demorada del lobby anterior.
            Assert.IsFalse(host.RequestStartMatch());
        }

        [Test]
        public void CambioDeHostYSalidasRecalculanLosJugadoresNecesarios()
        {
            var room = new LobbyTransport { LocalActorNumber = 2 };
            var manager = new NetworkManager(room);
            room.Join();
            manager.SetLocalReady(true);
            room.PromoteLocal();
            Assert.AreEqual((0, 2), manager.ClientReadiness);
            Assert.IsFalse(manager.LocalReady);
            Assert.IsFalse(manager.RequestStartMatch());
            room.Leave(1);
            room.Leave(3);
            Assert.AreEqual((0, 0), manager.ClientReadiness);
            room.Enter(4);
            Assert.AreEqual((0, 1), manager.ClientReadiness);
            Assert.IsFalse(manager.RequestStartMatch());
            room.Leave(4);
            Assert.IsTrue(manager.RequestStartMatch());
        }

        [Test]
        public void BotonDeLaEscenaAlternaListoYMuestraElConteoAlNuevoHost()
        {
            var room = new LobbyTransport { LocalActorNumber = 2 };
            var manager = new NetworkManager(room);
            room.Join();
            var previous = NetworkServices.Mediator;
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Menu.unity");
            var lobby = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LobbyScreen>(true)).Single();
            // Una escena de preview no ejecuta el ciclo de MonoBehaviour.
            void InvokeLifecycle(string method) => typeof(LobbyScreen)
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(lobby, null);
            try
            {
                NetworkServices.Register(manager);
                lobby.gameObject.SetActive(true);
                InvokeLifecycle("Awake");
                InvokeLifecycle("OnEnable");
                using var view = new SerializedObject(lobby);
                var button = (Button)view.FindProperty("_startButton").objectReferenceValue;
                var label = (TMP_Text)view.FindProperty("_startLabel").objectReferenceValue;
                Assert.IsTrue(button.interactable);
                Assert.AreEqual("LISTO", label.text);
                button.onClick.Invoke();
                Assert.AreEqual("CANCELAR LISTO", label.text);
                button.onClick.Invoke();
                Assert.AreEqual("LISTO", label.text);

                room.PromoteLocal();
                room.DeliverReady(1, "TEST");
                Assert.IsFalse(button.interactable);
                Assert.AreEqual("ESPERANDO · 1/2 LISTOS", label.text);
                room.DeliverReady(3, "TEST");
                Assert.IsTrue(button.interactable);
                Assert.AreEqual("INICIAR PARTIDA", label.text);
                button.onClick.Invoke();
                Assert.AreEqual(MatchPhase.Countdown, manager.Phase);
            }
            finally
            {
                InvokeLifecycle("OnDisable");
                NetworkServices.Register(previous);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void TildeVerdeAlLadoDelNombreSeOcultaAlCancelarODesocuparElLugar()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Menu.unity");
            try
            {
                var slot = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlayerSlotView>(true)).First();
                var palette = AssetDatabase.LoadAssetAtPath<TeamPaletteSO>("Assets/ScriptableObjects/Teams/TeamPalette_Default.asset");
                var ready = new PlayerSummary(2, "Cliente", TeamId.B, false, true, false, isReady: true);
                slot.ShowPlayer(ready, palette);
                var check = slot.GetComponentInChildren<MenuGlyph>(true);
                Assert.IsNotNull(check);
                Assert.IsTrue(check.gameObject.activeSelf);
                Assert.AreEqual("Name", check.transform.parent.name);
                Assert.AreEqual(palette.Get(TeamId.A, TeamPaletteSO.GreenFuchsiaPair).Main, check.color);
                slot.ShowPlayer(new PlayerSummary(2, "Cliente", TeamId.B, false, true, false), palette);
                Assert.IsFalse(check.gameObject.activeSelf);
                slot.ShowPlayer(ready, palette);
                slot.ShowEmpty();
                Assert.IsFalse(check.gameObject.activeSelf);
                Assert.AreEqual(1, slot.GetComponentsInChildren<MenuGlyph>(true).Length);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase(JoinFailure.InactiveSeat)]
        [TestCase(JoinFailure.Full)]
        public void UnirseRecuperaUnActorInactivoAunqueElEditorHayaPerdidoLaReservaLocal(JoinFailure firstFailure)
        {
            PlayerPrefs.DeleteKey(reservationKey);
            var room = new LobbyTransport();
            var manager = new NetworkManager(room);
            var errors = new List<string>();
            manager.ErrorRaised += errors.Add;
            room.ShowLobby();
            Assert.IsTrue(manager.JoinRoom(" test "));
            room.RejectJoin(firstFailure);
            Assert.AreEqual(new[] { "join TEST", "rejoin TEST" }, room.JoinAttempts);
            room.Join();
            Assert.AreEqual(ConnectionState.InRoom, manager.State);
            Assert.IsEmpty(errors);
        }

        [Test]
        public void SalaLlenaSinAsientoPropioVuelveAlListadoConElErrorOriginal()
        {
            PlayerPrefs.DeleteKey(reservationKey);
            var room = new LobbyTransport();
            var manager = new NetworkManager(room);
            var errors = new List<string>();
            manager.ErrorRaised += errors.Add;
            room.ShowLobby();
            manager.JoinRoom("TEST");
            room.RejectJoin(JoinFailure.Full, "La sala está llena.");
            room.RejectJoin(JoinFailure.NoReservedSeat);
            Assert.AreEqual(ConnectionState.InLobby, manager.State);
            Assert.AreEqual(new[] { "join TEST", "rejoin TEST" }, room.JoinAttempts);
            Assert.AreEqual(new[] { "No se pudo entrar a la sala. La sala está llena." }, errors);
        }

        [Test]
        public void AlReabrirElEditorSeRecuperaElAsientoYSiVencioSePruebaIngresoNormalUnaVez()
        {
            var originalRoom = new LobbyTransport();
            var original = new NetworkManager(originalRoom);
            originalRoom.Join();
            original.OnApplicationClosing();
            var room = new LobbyTransport();
            var manager = new NetworkManager(room);
            Assert.IsTrue(manager.HasLobbySeat("TEST"));
            Assert.IsFalse(manager.CanRejoinLastMatch);
            room.ShowLobby();
            Assert.IsTrue(manager.JoinRoom("TEST"));
            room.RejectJoin(JoinFailure.NoReservedSeat);
            room.RejectJoin(JoinFailure.Other);
            Assert.AreEqual(new[] { "rejoin TEST", "join TEST" }, room.JoinAttempts);
            Assert.AreEqual(ConnectionState.InLobby, manager.State);
            Assert.IsFalse(manager.HasLobbySeat("TEST"));
        }

        // Las propiedades y los callbacks entran por el mismo contrato que implementa Photon.
        sealed class LobbyTransport : INetworkProvider
        {
            readonly Dictionary<string, object> room = new();
            readonly Dictionary<int, Dictionary<string, object>> players = new()
            {
                [1] = new() { [PlayerKeys.Team] = (byte)1 },
                [2] = new() { [PlayerKeys.Team] = (byte)2 },
                [3] = new() { [PlayerKeys.Team] = (byte)1 }
            };
            int master = 1;
            public readonly List<string> JoinAttempts = new();
            public void ShowLobby() { InLobby = true; JoinedLobby?.Invoke(); }
            public void RejectJoin(JoinFailure reason, string message = "Sala no disponible") => JoinRoomFailed?.Invoke(reason, message);
            public int LocalActorNumber { get; set; } = 1;
            public bool IsMaster => master == LocalActorNumber;
            public bool InRoom => true;
            public NetRoomInfo CurrentRoom => new("TEST", players.Count, 8, true, room);
            public IReadOnlyList<NetPlayerInfo> Players => players.Select(p => new NetPlayerInfo(
                p.Key, $"P{p.Key}", p.Key == master, p.Key == LocalActorNumber, false, p.Value)).ToList();
            public void Join() => JoinedRoom?.Invoke();
            public void Enter(int id)
            {
                players.Add(id, new Dictionary<string, object> { [PlayerKeys.Team] = (byte)2 });
                PlayerEntered?.Invoke(id);
            }
            public void Leave(int id) { players.Remove(id); PlayerLeft?.Invoke(id); }
            public void PromoteLocal() { master = LocalActorNumber; MasterSwitched?.Invoke(master); }
            public object ReadyValue(int id) => players[id][PlayerKeys.Ready];
            public void DeliverReady(int id, object token) =>
                SetPlayerProperties(id, new Dictionary<string, object> { [PlayerKeys.Ready] = token });
            public void DeliverPhase(MatchPhase phase) =>
                SetRoomProperties(new Dictionary<string, object> { [RoomKeys.State] = (byte)phase });
            public bool SetRoomProperties(IDictionary<string, object> properties)
            {
                foreach (var property in properties) room[property.Key] = property.Value;
                RoomPropertiesChanged?.Invoke(new Dictionary<string, object>(properties));
                return true;
            }
            public bool SetPlayerProperties(int actorNumber, IDictionary<string, object> properties)
            {
                foreach (var property in properties) players[actorNumber][property.Key] = property.Value;
                // Los puntajes que se envían al inicio no se entregan durante el envío.
                if (properties.ContainsKey(PlayerKeys.Ready))
                    PlayerPropertiesChanged?.Invoke(actorNumber, new Dictionary<string, object>(properties));
                return true;
            }

            public event Action JoinedRoom;
            public event Action<int> PlayerEntered, PlayerLeft, MasterSwitched;
            public event Action<IReadOnlyDictionary<string, object>> RoomPropertiesChanged;
            public event Action<int, IReadOnlyDictionary<string, object>> PlayerPropertiesChanged;

            public bool IsConnected => true;
            public bool InLobby { get; private set; }
            public double ServerTime => 1;
            public int Ping => 0;
            public string NickName { get; set; }
            public IReadOnlyList<NetRoomInfo> RoomList => Array.Empty<NetRoomInfo>();
            public void RegisterCustomType(Type type, byte code, Func<object, byte[]> serialize, Func<byte[], object> deserialize) { }
            public bool Connect() => throw new NotSupportedException();
            public void Disconnect() => throw new NotSupportedException();
            public bool ReconnectAndRejoin() => throw new NotSupportedException();
            public bool JoinLobby() { ShowLobby(); return true; }
            public bool CreateRoom(string name, NetRoomOptions options) => throw new NotSupportedException();
            public bool JoinRoom(string name) { InLobby = false; JoinAttempts.Add("join " + name); return true; }
            public bool RejoinRoom(string name) { InLobby = false; JoinAttempts.Add("rejoin " + name); return true; }
            public bool LeaveRoom(bool reserveSeat = false) => throw new NotSupportedException();
            public bool SendEvent(byte code, object payload, NetTarget target, bool reliable) => throw new NotSupportedException();
            public bool SendEventTo(byte code, object payload, int actorNumber, bool reliable) => throw new NotSupportedException();
            public void SetMessageQueuePaused(bool paused) { }
            public void RegisterPrefab(string id, GameObject prefab) => throw new NotSupportedException();
            public GameObject Instantiate(string id, Vector3 position, Quaternion rotation) => throw new NotSupportedException();
            public GameObject InstantiateRoomObject(string id, Vector3 position, Quaternion rotation, object[] data) => throw new NotSupportedException();
            public void Destroy(GameObject instance) => throw new NotSupportedException();
            public void ClearLocalPlayerProperties(IReadOnlyList<string> keys) { }
            public event Action<JoinFailure, string> JoinRoomFailed;

#pragma warning disable CS0067 // Eventos del transporte que no intervienen en el lobby de prueba.
            public event Action ConnectedToServer, JoinedLobby, RoomListUpdated, LeftRoom;
            public event Action<string> Disconnected, CreateRoomFailed;
            public event Action<byte, int, object> EventReceived;
            public event Action<GameObject, int, bool, string, object[]> ObjectInstantiated;
            public event Action<GameObject> ObjectDestroyed;
#pragma warning restore CS0067
        }
    }
}
