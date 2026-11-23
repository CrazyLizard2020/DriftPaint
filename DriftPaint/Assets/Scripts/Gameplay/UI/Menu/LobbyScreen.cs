using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using DriftPaint.Net;
using DriftPaint.Teams;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.UI.Menu
{
    /// <summary>
    /// Sala de espera: jugadores por equipo, configuración, chat y botón de inicio.
    /// Solo el host puede cambiar la duración e iniciar la partida; alcanza con un jugador.
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
        [SerializeField] TMP_Text _modeText;
        [SerializeField] TMP_Text _maxPlayersText;
        [SerializeField] TMP_Text _durationText;
        [SerializeField] Button _durationMinus;
        [SerializeField] Button _durationPlus;

        [Header("Chat")]
        [SerializeField] TMP_Text _chatLog;
        [SerializeField] TMP_InputField _chatInput;
        [SerializeField] Button _chatSendButton;
        [SerializeField] Color _systemColor = new Color(0.094f, 0.874f, 0.941f);

        [Header("Acciones")]
        [SerializeField] Button _backButton;
        [SerializeField] Button _startButton;
        [SerializeField] TMP_Text _startLabel;

        static readonly Regex NoParseClose = new Regex("</?noparse>", RegexOptions.IgnoreCase);

        readonly List<PlayerSummary> _ordered = new List<PlayerSummary>();
        readonly Queue<string> _chatLines = new Queue<string>();
        readonly StringBuilder _chatBuilder = new StringBuilder();
        INetworkMediator _mediator;
        string _shownRoomId;

        void Awake()
        {
            _copyIdButton?.onClick.AddListener(CopyRoomId);
            _durationMinus?.onClick.AddListener(() => ChangeDuration(-DurationStep));
            _durationPlus?.onClick.AddListener(() => ChangeDuration(DurationStep));
            _chatSendButton?.onClick.AddListener(SendChat);
            _chatInput?.onSubmit.AddListener(_ => SendChat());
            _backButton?.onClick.AddListener(() => _mediator?.LeaveRoom());
            _startButton?.onClick.AddListener(() => _mediator?.RequestStartMatch());
        }

        void OnEnable()
        {
            _mediator = NetworkServices.Mediator;
            if (_mediator == null) return;

            _mediator.RoomChanged += RefreshRoom;
            _mediator.PlayersChanged += RefreshPlayers;
            _mediator.ChatReceived += AppendChat;
            RefreshRoom();
            RefreshPlayers();
        }

        void OnDisable()
        {
            if (_mediator == null) return;
            _mediator.RoomChanged -= RefreshRoom;
            _mediator.PlayersChanged -= RefreshPlayers;
            _mediator.ChatReceived -= AppendChat;
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
            if (_modeText != null) _modeText.text = "Pintura";
            if (_maxPlayersText != null) _maxPlayersText.text = room.MaxPlayers.ToString();
            if (_durationText != null) _durationText.text = RoomsScreen.FormatDuration(room.DurationSeconds);

            bool canEdit = _mediator.IsMaster && _mediator.Phase.IsBetweenMatches();
            SetVisible(_durationMinus, canEdit);
            SetVisible(_durationPlus, canEdit);

            if (_startButton != null) _startButton.interactable = canEdit;
            if (_startLabel != null) _startLabel.text = _mediator.IsMaster ? "INICIAR PARTIDA" : "ESPERANDO AL HOST";

            RefreshPlayersHeader();
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
            // El host puede cambiar de manos cuando alguien se va.
            if (_startLabel != null) _startLabel.text = _mediator.IsMaster ? "INICIAR PARTIDA" : "ESPERANDO AL HOST";
            if (_startButton != null) _startButton.interactable = _mediator.IsMaster && _mediator.Phase.IsBetweenMatches();
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
            _playersHeaderText.text = $"JUGADORES  <color=#18DFF0>({_mediator.Players.Count} / {max})</color>";
        }

        void ChangeDuration(int delta)
        {
            _mediator.SetRoomDuration(_mediator.CurrentRoom.DurationSeconds + delta);
        }

        void CopyRoomId()
        {
            if (_mediator.CurrentRoom.IsValid) GUIUtility.systemCopyBuffer = _mediator.CurrentRoom.Id;
        }

        void SendChat()
        {
            if (_chatInput == null) return;

            _mediator.SendChat(_chatInput.text);
            _chatInput.text = "";
            _chatInput.ActivateInputField();
        }

        void AppendChat(ChatMessage message)
        {
            string line;
            if (message.IsSystem)
            {
                line = $"<color=#{ColorUtility.ToHtmlStringRGB(_systemColor)}>[Sistema]</color> {message.Text}";
            }
            else
            {
                Color nameColor = _teamPalette != null ? _teamPalette.Get(message.SenderTeam).Main : Color.white;
                string safeName = NoParseClose.Replace(message.SenderName, "");
                string safeText = NoParseClose.Replace(message.Text, "");
                line = $"<color=#{ColorUtility.ToHtmlStringRGB(nameColor)}><b><noparse>{safeName}</noparse>:</b></color> <noparse>{safeText}</noparse>";
            }

            _chatLines.Enqueue(line);
            while (_chatLines.Count > MaxChatLines) _chatLines.Dequeue();
            RenderChat();
        }

        void RenderChat()
        {
            if (_chatLog == null) return;

            _chatBuilder.Clear();
            foreach (string line in _chatLines)
            {
                if (_chatBuilder.Length > 0) _chatBuilder.Append('\n');
                _chatBuilder.Append(line);
            }
            _chatLog.text = _chatBuilder.ToString();
        }

        static string MapDisplayName(string mapId) => DriftPaint.Match.MapCatalog.DisplayName(mapId);

        static void SetVisible(Component component, bool visible)
        {
            if (component != null) component.gameObject.SetActive(visible);
        }
    }
}
