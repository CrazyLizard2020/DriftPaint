using System;
using DriftPaint.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.UI.Menu
{
    /// <summary>Fila de la lista de salas. Se clona desde una plantilla de la escena.</summary>
    public class RoomRowView : MonoBehaviour
    {
        [SerializeField] TMP_Text _titleText;
        [SerializeField] TMP_Text _modeText;
        [SerializeField] TMP_Text _playersText;
        [SerializeField] TMP_Text _pingText;
        [SerializeField] Button _joinButton;
        [SerializeField] TMP_Text _joinLabel;

        string _roomId;
        Action<string> _onJoin;

        void Awake()
        {
            if (_joinButton != null) _joinButton.onClick.AddListener(() => _onJoin?.Invoke(_roomId));
        }

        public void Bind(RoomSummary room, int ping, Action<string> onJoin)
        {
            _roomId = room.Id;
            _onJoin = onJoin;

            if (_titleText != null) _titleText.text = room.Title;
            if (_modeText != null) _modeText.text = "PINTURA";
            if (_playersText != null) _playersText.text = $"{room.PlayerCount} / {room.MaxPlayers}";
            if (_pingText != null) _pingText.text = ping > 0 ? $"{ping} ms" : "-";

            bool playing = !room.IsOpen || room.Phase != MatchPhase.Lobby;
            bool canJoin = !playing && !room.IsFull;
            if (_joinButton != null) _joinButton.interactable = canJoin;
            if (_joinLabel != null) _joinLabel.text = playing ? "EN JUEGO" : room.IsFull ? "LLENA" : "UNIRSE";
        }
    }
}
