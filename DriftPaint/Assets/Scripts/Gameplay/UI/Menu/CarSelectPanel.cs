using System.Collections.Generic;
using DriftPaint.Cars;
using DriftPaint.LiveOperations;
using DriftPaint.Net;
using DriftPaint.Teams;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.UI.Menu
{
    /// <summary>
    /// Ventana para elegir auto desde el lobby. Muestra el auto marcado en grande y una tarjeta por cada
    /// auto del catálogo habilitado por LiveOps en la sala (clonada de la plantilla de la escena).
    /// Confirmar guarda la elección del jugador.
    /// </summary>
    public class CarSelectPanel : MonoBehaviour
    {
        [SerializeField] CarCatalogSO _catalog;
        [SerializeField] TeamPaletteSO _teamPalette;

        [Header("Vista grande")]
        [SerializeField] Image _previewImage;
        [SerializeField] TMP_Text _previewName;
        [Tooltip("Cartel que aparece si el auto marcado es el que el jugador tiene puesto.")]
        [SerializeField] GameObject _currentBadge;

        [Header("Tarjetas")]
        [Tooltip("Tarjeta inactiva que se clona por cada auto del catálogo.")]
        [SerializeField] CarCardView _cardTemplate;

        [Header("Botones")]
        [SerializeField] Button _closeButton;
        [SerializeField] Button _cancelButton;
        [SerializeField] Button _confirmButton;

        readonly List<CarCardView> _cards = new List<CarCardView>();
        // Posiciones en el catálogo de los autos habilitados por LiveOps, en el orden en que se muestran.
        readonly List<int> _visible = new List<int>();
        INetworkMediator _mediator;
        int _selected;
        int _current;
        byte _team;

        void Awake()
        {
            if (_cardTemplate != null) _cardTemplate.gameObject.SetActive(false);
            _closeButton?.onClick.AddListener(Close);
            _cancelButton?.onClick.AddListener(Close);
            _confirmButton?.onClick.AddListener(Confirm);
        }

        void OnEnable() => LiveOps.Changed += Reload;
        void OnDisable() => LiveOps.Changed -= Reload;

        public void Open()
        {
            _mediator = NetworkServices.Mediator;
            if (_catalog == null || _catalog.Count == 0) return;

            gameObject.SetActive(true);
            Reload();
        }

        public void Close() => gameObject.SetActive(false);

        /// <summary>Arma las tarjetas con los autos habilitados. Si la sala cambia de configuración con la ventana abierta, se rearma.</summary>
        void Reload()
        {
            if (!gameObject.activeInHierarchy || _catalog == null || _catalog.Count == 0) return;

            var ids = new List<string>(_catalog.Count);
            for (int i = 0; i < _catalog.Count; i++) ids.Add(_catalog.Get(i).Id);
            IReadOnlyList<string> enabled = LiveOps.Room.EnabledCars(ids);
            _visible.Clear();
            foreach (string id in enabled) _visible.Add(_catalog.IndexOf(id));

            PlayerSummary local = LocalPlayer();
            _team = local.Team == TeamId.B ? TeamId.B : TeamId.A;
            // El auto puesto es el que va a usar en la partida: el elegido si sigue habilitado, o el de reemplazo.
            _current = Mathf.Max(0, _catalog.IndexOf(CarChoice.Resolve(local.CarId, local.ActorId, enabled)));
            _selected = _current;

            BuildCards();
            Refresh();
        }

        void Confirm()
        {
            if (_catalog != null && _selected >= 0 && _selected < _catalog.Count)
                _mediator?.SetLocalCar(_catalog.Get(_selected).Id);
            Close();
        }

        void BuildCards()
        {
            if (_cardTemplate == null) return;

            while (_cards.Count < _visible.Count)
            {
                CarCardView card = Instantiate(_cardTemplate, _cardTemplate.transform.parent);
                card.gameObject.SetActive(true);
                _cards.Add(card);
            }

            Color accent = _teamPalette != null ? _teamPalette.Get(_team).Main : Color.white;
            for (int i = 0; i < _cards.Count; i++)
            {
                bool used = i < _visible.Count;
                _cards[i].gameObject.SetActive(used);
                if (!used) continue;

                int index = _visible[i];
                CarCatalogSO.Entry entry = _catalog.Get(index);
                _cards[i].name = $"Card {entry.Id}";
                _cards[i].Bind(entry.DisplayName, entry.PreviewFor(_team), accent, () => Select(index));
            }
        }

        void Select(int index)
        {
            _selected = index;
            Refresh();
        }

        void Refresh()
        {
            CarCatalogSO.Entry entry = _catalog.Get(_selected);
            if (_previewName != null) _previewName.text = entry.DisplayName;
            if (_previewImage != null)
            {
                Sprite sprite = entry.PreviewFor(_team);
                _previewImage.sprite = sprite;
                _previewImage.enabled = sprite != null;
            }
            if (_currentBadge != null) _currentBadge.SetActive(_selected == _current);

            for (int i = 0; i < _cards.Count && i < _visible.Count; i++)
                _cards[i].SetState(_visible[i] == _selected, _visible[i] == _current);
        }

        PlayerSummary LocalPlayer()
        {
            if (_mediator != null)
                foreach (PlayerSummary player in _mediator.Players)
                    if (player.IsLocal) return player;
            return new PlayerSummary(_mediator?.LocalActorId ?? 1, "", TeamId.A, false, true, false);
        }
    }
}
