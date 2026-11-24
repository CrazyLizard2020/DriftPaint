using System;
using System.Collections.Generic;
using DriftPaint.Net;
using DriftPaint.Profile;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.UI.Menu
{
    /// <summary>
    /// Lista de salas con búsqueda y páginas, y panel para crear una sala nueva.
    /// Las filas se clonan desde una plantilla inactiva que vive en la escena.
    /// </summary>
    public class RoomsScreen : MonoBehaviour
    {
        const int MinDuration = 120;
        const int MaxDuration = 300;
        const int DurationStep = 60;
        const string DefaultMapId = DriftPaint.Match.MapCatalog.DefaultMapId;

        [Header("Lista")]
        [SerializeField] RoomRowView _rowTemplate;
        [SerializeField] Transform _rowContainer;
        [SerializeField, Min(1)] int _rowsPerPage = 5;
        [SerializeField] TMP_Text _emptyText;
        [SerializeField] TMP_InputField _searchInput;
        [SerializeField] Button _previousPageButton;
        [SerializeField] Button _nextPageButton;
        [SerializeField] TMP_Text _pageText;

        [Header("Acciones")]
        [SerializeField] Button _backButton;
        [SerializeField] Button _refreshButton;
        [SerializeField] Button _openCreateButton;

        [Header("Crear sala")]
        [SerializeField] GameObject _createPanel;
        [SerializeField] TMP_InputField _createTitleInput;
        [SerializeField] TMP_Text _createDurationText;
        [SerializeField] Button _createDurationMinus;
        [SerializeField] Button _createDurationPlus;
        [SerializeField] Button _createConfirmButton;
        [SerializeField] Button _createCancelButton;

        readonly List<RoomRowView> _rows = new List<RoomRowView>();
        readonly List<RoomSummary> _filtered = new List<RoomSummary>();
        INetworkMediator _mediator;
        int _page;
        int _createDuration = MinDuration;

        void Awake()
        {
            if (_rowTemplate != null) _rowTemplate.gameObject.SetActive(false);

            _searchInput?.onValueChanged.AddListener(_ => { _page = 0; Render(); });
            _previousPageButton?.onClick.AddListener(() => { _page--; Render(); });
            _nextPageButton?.onClick.AddListener(() => { _page++; Render(); });
            _backButton?.onClick.AddListener(() => _mediator?.Disconnect());
            _refreshButton?.onClick.AddListener(Render);
            _openCreateButton?.onClick.AddListener(OpenCreatePanel);

            _createDurationMinus?.onClick.AddListener(() => ChangeCreateDuration(-DurationStep));
            _createDurationPlus?.onClick.AddListener(() => ChangeCreateDuration(DurationStep));
            _createConfirmButton?.onClick.AddListener(ConfirmCreate);
            _createCancelButton?.onClick.AddListener(() => _createPanel.SetActive(false));
        }

        float _nextPing;

        void Update()
        {
            if (_mediator == null || Time.unscaledTime < _nextPing) return;
            _nextPing = Time.unscaledTime + 1f;
            foreach (var row in _rows) if (row.gameObject.activeSelf) row.SetPing(_mediator.Ping);
        }

        void OnEnable()
        {
            GameLanguage.Changed += RefreshLanguage;
            if (_createPanel != null) _createPanel.SetActive(false);

            _mediator = NetworkServices.Mediator;
            if (_mediator != null) _mediator.RoomListChanged += Render;
            Render();
        }

        void RefreshLanguage()
        {
            Render();
            RefreshPlaceholder();
        }

        void RefreshPlaceholder()
        {
            if (_createTitleInput != null && _createTitleInput.placeholder is TMP_Text label)
                label.text = string.Format(GameLanguage.Text("Sala de {0}"), PlayerProfile.LoadNickname());
        }

        void OnDisable()
        {
            GameLanguage.Changed -= RefreshLanguage;
            if (_mediator != null) _mediator.RoomListChanged -= Render;
        }

        void Render()
        {
            _filtered.Clear();
            if (_mediator != null)
            {
                string query = _searchInput != null ? _searchInput.text.Trim() : "";
                foreach (RoomSummary room in _mediator.Rooms)
                    if (Matches(room, query)) _filtered.Add(room);
            }

            int pageCount = Mathf.Max(1, Mathf.CeilToInt(_filtered.Count / (float)_rowsPerPage));
            _page = Mathf.Clamp(_page, 0, pageCount - 1);

            EnsureRows();
            int first = _page * _rowsPerPage;
            int ping = _mediator?.Ping ?? 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                int index = first + i;
                bool visible = index < _filtered.Count;
                _rows[i].gameObject.SetActive(visible);
                if (visible) _rows[i].Bind(_filtered[index], ping, Join);
            }

            if (_emptyText != null) _emptyText.gameObject.SetActive(_filtered.Count == 0);
            if (_pageText != null) _pageText.text = $"{_page + 1} / {pageCount}";
            if (_previousPageButton != null) _previousPageButton.interactable = _page > 0;
            if (_nextPageButton != null) _nextPageButton.interactable = _page < pageCount - 1;
        }

        void EnsureRows()
        {
            if (_rowTemplate == null || _rowContainer == null) return;
            while (_rows.Count < _rowsPerPage)
            {
                RoomRowView row = Instantiate(_rowTemplate, _rowContainer);
                row.name = $"Room Row {_rows.Count + 1}";
                _rows.Add(row);
            }
        }

        void Join(string roomId)
        {
            _mediator?.JoinRoom(roomId);
        }

        void OpenCreatePanel()
        {
            if (_createPanel == null) return;
            _createPanel.SetActive(true);

            if (_createTitleInput != null)
            {
                _createTitleInput.text = "";
                RefreshPlaceholder();
            }
            ChangeCreateDuration(0);
        }

        void ChangeCreateDuration(int delta)
        {
            _createDuration = Mathf.Clamp(_createDuration + delta, MinDuration, MaxDuration);
            if (_createDurationText != null) _createDurationText.text = FormatDuration(_createDuration);
            if (_createDurationMinus != null) _createDurationMinus.interactable = _createDuration > MinDuration;
            if (_createDurationPlus != null) _createDurationPlus.interactable = _createDuration < MaxDuration;
        }

        void ConfirmCreate()
        {
            if (_mediator == null) return;

            var settings = new RoomSettings
            {
                Title = _createTitleInput != null ? _createTitleInput.text : "",
                MaxPlayers = 8,
                DurationSeconds = _createDuration,
                MapId = DefaultMapId
            };
            if (_mediator.CreateRoom(settings)) _createPanel.SetActive(false);
        }

        static bool Matches(RoomSummary room, string query)
        {
            if (query.Length == 0) return true;
            return room.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                   || room.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string FormatDuration(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";
    }
}
