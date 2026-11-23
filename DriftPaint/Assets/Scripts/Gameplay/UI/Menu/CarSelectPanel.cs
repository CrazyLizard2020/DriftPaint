using System.Collections.Generic;
using DriftPaint.Cars;
using DriftPaint.Net;
using DriftPaint.Teams;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.UI.Menu
{
    /// <summary>
    /// Ventana para elegir auto desde el lobby. Muestra el auto marcado en grande y una tarjeta por cada
    /// auto del catálogo (clonada de la plantilla de la escena). Confirmar guarda la elección del jugador.
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

        public void Open()
        {
            _mediator = NetworkServices.Mediator;
            if (_catalog == null || _catalog.Count == 0) return;

            PlayerSummary local = LocalPlayer();
            _team = local.Team == TeamId.B ? TeamId.B : TeamId.A;
            _current = Mathf.Max(0, _catalog.IndexFor(local.CarId, local.ActorId));
            _selected = _current;

            gameObject.SetActive(true);
            BuildCards();
            Refresh();
        }

        public void Close() => gameObject.SetActive(false);

        void Confirm()
        {
            if (_catalog != null && _selected >= 0 && _selected < _catalog.Count)
                _mediator?.SetLocalCar(_catalog.Get(_selected).Id);
            Close();
        }

        void BuildCards()
        {
            if (_cardTemplate == null) return;

            while (_cards.Count < _catalog.Count)
            {
                CarCardView card = Instantiate(_cardTemplate, _cardTemplate.transform.parent);
                card.gameObject.SetActive(true);
                _cards.Add(card);
            }

            Color accent = _teamPalette != null ? _teamPalette.Get(_team).Main : Color.white;
            for (int i = 0; i < _cards.Count; i++)
            {
                bool used = i < _catalog.Count;
                _cards[i].gameObject.SetActive(used);
                if (!used) continue;

                int index = i;
                CarCatalogSO.Entry entry = _catalog.Get(i);
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

            for (int i = 0; i < _cards.Count && i < _catalog.Count; i++)
                _cards[i].SetState(i == _selected, i == _current);
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
