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
        [SerializeField] Image[] _pingBars;
        [SerializeField] Button _joinButton;
        [SerializeField] TMP_Text _joinLabel;

        string _roomId;
        Action<string> _onJoin;

        void Awake()
        {
            if (_joinButton != null) _joinButton.onClick.AddListener(() => _onJoin?.Invoke(_roomId));
        }

        /// <summary>La altura y el color indican la calidad de la conexión local al servidor de la sala.</summary>
        public void SetPing(int ping)
        {
            if (_pingText != null) _pingText.text = ping > 0 ? $"{ping} ms" : "–";
            int level = ping <= 0 ? 0 : ping < 80 ? 4 : ping < 150 ? 3 : ping < 250 ? 2 : 1;
            Color color = level == 4 ? new Color32(69, 222, 69, 255) : level == 3 ? Color.yellow : new Color32(255, 124, 52, 255);
            if (_pingBars != null)
                for (int i = 0; i < _pingBars.Length; i++) _pingBars[i].color = i < level ? color : new Color(.42f,.47f,.55f,.55f);
        }

        public void Bind(RoomSummary room, int ping, Action<string> onJoin)
        {
            _roomId = room.Id;
            _onJoin = onJoin;

            if (_titleText != null) _titleText.text = room.Title;
            if (_modeText != null) _modeText.text = GameLanguage.Text("PINTURA");
            if (_playersText != null) _playersText.text = $"{room.PlayerCount} / {room.MaxPlayers}";
            SetPing(ping);

            bool playing = !room.IsOpen || !room.Phase.IsBetweenMatches();
            bool canJoin = !playing && !room.IsFull;
            if (_joinButton != null) _joinButton.interactable = canJoin;
            if (_joinLabel != null) _joinLabel.text = GameLanguage.Text(playing ? "EN JUEGO" : room.IsFull ? "LLENA" : "UNIRSE");
        }
    }
}
