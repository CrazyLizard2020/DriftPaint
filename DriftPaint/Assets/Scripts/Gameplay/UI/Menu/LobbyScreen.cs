using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using DriftPaint.LiveOperations;
using DriftPaint.Match;
using DriftPaint.Net;
using DriftPaint.Teams;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.UI.Menu
{
    /// <summary>
    /// Sala de espera: jugadores por equipo, configuración, chat y botón de inicio.
    /// Solo el host puede cambiar el mapa y la duración e iniciar la partida; alcanza con un jugador.
    /// Los mapas que se pueden elegir son los que habilita LiveOps en la copia de la sala.
    /// </summary>
    public class LobbyScreen : MonoBehaviour
    {
        const int DurationStep = 60;
        const int MaxChatLines = 40;

        [Header("Encabezado")]
        [SerializeField] TMP_Text _roomTitleText;
        [SerializeField] TMP_Text _roomIdText;
        [SerializeField] Button _copyIdButton;

        [Header("Jugadores")]
        [SerializeField] TMP_Text _playersHeaderText;
        [SerializeField] PlayerSlotView[] _slots;
        [SerializeField] TeamPaletteSO _teamPalette;

        [Header("Configuración")]
        [SerializeField] TMP_Text _mapText;
        [Tooltip("Miniatura del mapa elegido, tomada de su MapDefinitionSO.")]
        [SerializeField] Image _mapThumbnail;
        [Tooltip("Flechas del mapa. Solo las ve el host, y solo si LiveOps habilita más de un mapa.")]
        [SerializeField] Button _mapPrevious;
        [SerializeField] Button _mapNext;
        [SerializeField] TMP_Text _modeText;
        [SerializeField] TMP_Text _maxPlayersText;
        [SerializeField] TMP_Text _durationText;
        [SerializeField] Button _durationMinus;
        [SerializeField] Button _durationPlus;

        [Header("Chat")]
        [SerializeField] TMP_Text _chatLog;
        [Tooltip("Lista desplazable que contiene el registro. Sigue los mensajes nuevos salvo que se haya subido a leer.")]
        [SerializeField] ScrollRect _chatScroll;
        [SerializeField] TMP_InputField _chatInput;
        [SerializeField] Button _chatSendButton;
        [SerializeField] Color _systemColor = new Color(0.094f, 0.874f, 0.941f);

        [Header("Equipo y auto")]
        [Tooltip("Pasa al jugador local al otro equipo. Solo se habilita si los equipos quedan parejos.")]
        [SerializeField] Button _switchTeamButton;
        [SerializeField] Button _changeCarButton;
        [SerializeField] CarSelectPanel _carPanel;

        [Header("Acciones")]
        [SerializeField] Button _backButton;
        [SerializeField] Button _startButton;
        [SerializeField] TMP_Text _startLabel;

        static readonly Regex NoParseClose = new Regex("</?noparse>", RegexOptions.IgnoreCase);

        readonly List<PlayerSummary> _ordered = new List<PlayerSummary>();
        readonly Queue<ChatMessage> _chatLines = new Queue<ChatMessage>();
        readonly StringBuilder _chatBuilder = new StringBuilder();
        INetworkMediator _mediator;
        string _shownRoomId;

        void Awake()
        {
            _copyIdButton?.onClick.AddListener(CopyRoomId);
            _durationMinus?.onClick.AddListener(() => ChangeDuration(-DurationStep));
            _durationPlus?.onClick.AddListener(() => ChangeDuration(DurationStep));
            _mapPrevious?.onClick.AddListener(() => ChangeMap(-1));
            _mapNext?.onClick.AddListener(() => ChangeMap(1));
            _chatSendButton?.onClick.AddListener(SendChat);
            _chatInput?.onSubmit.AddListener(_ => SendChat());
            _backButton?.onClick.AddListener(() => _mediator?.LeaveRoom());
            _startButton?.onClick.AddListener(OnStartOrReady);
            _switchTeamButton?.onClick.AddListener(() => _mediator?.RequestTeamChange());
            _changeCarButton?.onClick.AddListener(() => _carPanel?.Open());
        }

        void OnEnable()
        {
            _mediator = NetworkServices.Mediator;
            if (_mediator == null) return;

            GameLanguage.Changed += RefreshLanguage;
            LiveOps.Changed += RefreshRoom;
            _mediator.RoomChanged += RefreshRoom;
            _mediator.PlayersChanged += RefreshPlayers;
            _mediator.ChatReceived += AppendChat;
            RefreshRoom();
            RefreshPlayers();
        }

        void OnDisable()
        {
            _carPanel?.Close();
            if (_mediator == null) return;
            GameLanguage.Changed -= RefreshLanguage;
            LiveOps.Changed -= RefreshRoom;
            _mediator.RoomChanged -= RefreshRoom;
            _mediator.PlayersChanged -= RefreshPlayers;
            _mediator.ChatReceived -= AppendChat;
        }

        void RefreshLanguage()
        {
            RefreshRoom();
            RefreshPlayers();
            RenderChat();
        }

        void RefreshRoom()
        {
            RoomSummary room = _mediator.CurrentRoom;
            if (!room.IsValid) return;

            if (room.Id != _shownRoomId)
            {
                _shownRoomId = room.Id;
                _chatLines.Clear();
                RenderChat();
            }

            if (_roomTitleText != null) _roomTitleText.text = room.Title;
            if (_roomIdText != null) _roomIdText.text = $"ID: {room.Id}";
            if (_mapText != null) _mapText.text = MapDisplayName(room.MapId);
            if (_mapThumbnail != null)
            {
                Sprite thumbnail = MapLibrarySO.Find(room.MapId)?.Thumbnail;
                _mapThumbnail.sprite = thumbnail;
                _mapThumbnail.enabled = thumbnail != null;
            }
            if (_modeText != null) _modeText.text = GameLanguage.Text("Pintura");
            if (_maxPlayersText != null) _maxPlayersText.text = room.MaxPlayers.ToString();
            if (_durationText != null) _durationText.text = RoomsScreen.FormatDuration(room.DurationSeconds);

            bool canEdit = _mediator.IsMaster && _mediator.Phase.IsBetweenMatches();
            SetVisible(_durationMinus, canEdit);
            SetVisible(_durationPlus, canEdit);
            bool canPickMap = canEdit && EnabledMaps().Count > 1;
            SetVisible(_mapPrevious, canPickMap);
            SetVisible(_mapNext, canPickMap);

            RefreshStartButton();

            RefreshPlayersHeader();
            RefreshTeamAndCarButtons();
        }

        void RefreshTeamAndCarButtons()
        {
            bool betweenMatches = _mediator.Phase.IsBetweenMatches();
            if (_switchTeamButton != null)
                _switchTeamButton.interactable = betweenMatches && TeamRules.CanSwitch(_mediator.Players, _mediator.LocalActorId);
            if (_changeCarButton != null) _changeCarButton.interactable = betweenMatches;
        }

        void RefreshPlayers()
        {
            _ordered.Clear();
            AddTeam(TeamId.A);
            AddTeam(TeamId.B);
            foreach (PlayerSummary player in _mediator.Players)
                if (player.Team != TeamId.A && player.Team != TeamId.B) _ordered.Add(player);

            int capacity = _mediator.CurrentRoom.MaxPlayers > 0 ? _mediator.CurrentRoom.MaxPlayers : _slots.Length;
            for (int i = 0; i < _slots.Length; i++)
            {
                PlayerSlotView slot = _slots[i];
                if (slot == null) continue;

                slot.gameObject.SetActive(i < capacity);
                if (i < _ordered.Count) slot.ShowPlayer(_ordered[i], _teamPalette);
                else slot.ShowEmpty();
            }

            RefreshPlayersHeader();
            RefreshStartButton();
            RefreshTeamAndCarButtons();
        }

        void OnStartOrReady()
        {
            if (_mediator == null) return;
            if (_mediator.IsMaster) _mediator.RequestStartMatch();
            else _mediator.SetLocalReady(!_mediator.LocalReady);
        }

        void RefreshStartButton()
        {
            var (ready, required) = _mediator.ClientReadiness;
            bool waiting = _mediator.IsMaster && ready < required;
            if (_startButton != null)
                _startButton.interactable = _mediator.Phase.IsBetweenMatches() && !waiting;
            if (_startLabel == null) return;
            if (waiting)
                _startLabel.text = string.Format(GameLanguage.Text("ESPERANDO · {0}/{1} LISTOS"), ready, required);
            else if (_mediator.IsMaster)
                _startLabel.text = GameLanguage.Text("INICIAR PARTIDA");
            else
                _startLabel.text = GameLanguage.Text(_mediator.LocalReady ? "CANCELAR LISTO" : "LISTO");
        }

        void AddTeam(byte team)
        {
            foreach (PlayerSummary player in _mediator.Players)
                if (player.Team == team) _ordered.Add(player);
        }

        void RefreshPlayersHeader()
        {
            if (_playersHeaderText == null) return;
            int max = _mediator.CurrentRoom.MaxPlayers;
            _playersHeaderText.text = string.Format(GameLanguage.Text("JUGADORES  <color=#18DFF0>({0} / {1})</color>"), _mediator.Players.Count, max);
        }

        void ChangeDuration(int delta)
        {
            _mediator.SetRoomDuration(_mediator.CurrentRoom.DurationSeconds + delta);
        }

        static IReadOnlyList<string> EnabledMaps() => LiveOps.Room.EnabledMaps(MapCatalog.All);

        /// <summary>Pasa al mapa habilitado anterior o siguiente, dando la vuelta al llegar a un extremo.</summary>
        void ChangeMap(int step)
        {
            IReadOnlyList<string> maps = EnabledMaps();
            if (maps.Count == 0) return;

            int index = -1;
            for (int i = 0; i < maps.Count; i++)
                if (maps[i] == _mediator.CurrentRoom.MapId) index = i;
            // Si el mapa actual no está habilitado, la flecha lleva al primero o al último.
            int next = index < 0 ? (step > 0 ? 0 : maps.Count - 1) : (index + step + maps.Count) % maps.Count;
            _mediator.SetRoomMap(maps[next]);
        }

        void CopyRoomId()
        {
            if (_mediator.CurrentRoom.IsValid) GUIUtility.systemCopyBuffer = _mediator.CurrentRoom.Id;
        }

        void SendChat()
        {
            if (_chatInput == null) return;

            // Al escribir se vuelve al final, así el mensaje propio se ve aunque se estuviera leyendo más arriba.
            if (_chatScroll != null) _chatScroll.verticalNormalizedPosition = 0f;
            _mediator.SendChat(_chatInput.text);
            _chatInput.text = "";
            _chatInput.ActivateInputField();
        }

        void AppendChat(ChatMessage message)
        {
            _chatLines.Enqueue(message);
            while (_chatLines.Count > MaxChatLines) _chatLines.Dequeue();
            RenderChat();
        }

        string FormatChat(ChatMessage message)
        {
            string line;
            if (message.IsSystem)
            {
                string text = GameLanguage.Text(message.Text);
                if (message.Argument != null)
                    text = string.Format(text, "<noparse>" + NoParseClose.Replace(message.Argument, "") + "</noparse>");
                line = $"<color=#{ColorUtility.ToHtmlStringRGB(_systemColor)}>[{GameLanguage.Text("Sistema")}]</color> {text}";
            }
            else
            {
                Color nameColor = _teamPalette != null ? _teamPalette.Get(message.SenderTeam).Main : Color.white;
                string safeName = NoParseClose.Replace(message.SenderName, "");
                string safeText = NoParseClose.Replace(message.Text, "");
                line = $"<color=#{ColorUtility.ToHtmlStringRGB(nameColor)}><b><noparse>{safeName}</noparse>:</b></color> <noparse>{safeText}</noparse>";
            }

            return line;
        }

        void RenderChat()
        {
            if (_chatLog == null) return;

            // Se mira antes de cambiar el texto: quien subió a leer mensajes viejos no es arrastrado hacia abajo.
            bool following = _chatScroll == null || _chatScroll.verticalNormalizedPosition <= 0.01f || _chatLines.Count <= 1;

            _chatBuilder.Clear();
            foreach (var message in _chatLines)
            {
                if (_chatBuilder.Length > 0) _chatBuilder.Append('\n');
                _chatBuilder.Append(FormatChat(message));
            }
            _chatLog.text = _chatBuilder.ToString();

            if (_chatScroll == null || !following) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_chatScroll.content);
            _chatScroll.verticalNormalizedPosition = 0f;
        }

        static string MapDisplayName(string mapId) => MapCatalog.DisplayName(mapId);

        static void SetVisible(Component component, bool visible)
        {
            if (component != null) component.gameObject.SetActive(visible);
        }
    }
}
