using DriftPaint.Net;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DriftPaint.UI.Menu
{
    /// <summary>
    /// Elige qué pantalla del menú se ve según el estado de la conexión, muestra los avisos de espera
    /// y de error, y carga la escena de partida cuando la sala arranca.
    /// </summary>
    public class MenuFlow : MonoBehaviour
    {
        [Header("Pantallas")]
        [SerializeField] GameObject _mainMenu;
        [SerializeField] GameObject _rooms;
        [SerializeField] GameObject _lobby;

        [Header("Aviso de espera")]
        [SerializeField] GameObject _busyRoot;
        [SerializeField] TMP_Text _busyText;

        [Header("Aviso de error")]
        [SerializeField] GameObject _errorRoot;
        [SerializeField] TMP_Text _errorText;
        [SerializeField] Button _errorCloseButton;

        [Header("Partida")]
        [Tooltip("Escena que se carga cuando el host inicia la partida.")]
        [SerializeField] string _matchScene = "Sandbox_Car";

        INetworkMediator _mediator;
        bool _loadingMatch;

        void Awake()
        {
            if (_errorCloseButton != null) _errorCloseButton.onClick.AddListener(HideError);
            if (_errorRoot != null) _errorRoot.SetActive(false);
        }

        void OnEnable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _mediator = NetworkServices.Mediator;
            if (_mediator == null)
            {
                ShowOnly(_mainMenu);
                SetBusy(null);
                ShowError("La red no está inicializada. Revisá que el ensamblado DriftPaint.App esté en el proyecto.");
                return;
            }

            _mediator.StateChanged += Refresh;
            _mediator.ErrorRaised += ShowError;
            _mediator.PhaseChanged += OnPhaseChanged;
            Refresh();
        }

        void OnDisable()
        {
            if (_mediator == null) return;
            _mediator.StateChanged -= Refresh;
            _mediator.ErrorRaised -= ShowError;
            _mediator.PhaseChanged -= OnPhaseChanged;
        }

        void Refresh()
        {
            switch (_mediator.State)
            {
                case ConnectionState.Disconnected:
                    ShowOnly(_mainMenu);
                    SetBusy(null);
                    break;
                case ConnectionState.Connecting:
                    SetBusy("CONECTANDO...");
                    break;
                case ConnectionState.InLobby:
                    ShowOnly(_rooms);
                    SetBusy(null);
                    break;
                case ConnectionState.JoiningRoom:
                    SetBusy("ENTRANDO A LA SALA...");
                    break;
                case ConnectionState.InRoom:
                    ShowOnly(_lobby);
                    SetBusy(null);
                    TryLoadMatch(_mediator.Phase);
                    break;
                case ConnectionState.LeavingRoom:
                    SetBusy("SALIENDO DE LA SALA...");
                    break;
            }
        }

        void OnPhaseChanged(MatchPhase phase) => TryLoadMatch(phase);

        void TryLoadMatch(MatchPhase phase)
        {
            if (_loadingMatch || phase == MatchPhase.Lobby || phase == MatchPhase.Results) return;

            if (!Application.CanStreamedLevelBeLoaded(_matchScene))
            {
                ShowError($"La escena \"{_matchScene}\" no está en la lista de escenas del build.");
                return;
            }

            _loadingMatch = true;
            SetBusy("CARGANDO PARTIDA...");
            SceneManager.LoadScene(_matchScene);
        }

        void ShowOnly(GameObject screen)
        {
            if (_mainMenu != null) _mainMenu.SetActive(screen == _mainMenu);
            if (_rooms != null) _rooms.SetActive(screen == _rooms);
            if (_lobby != null) _lobby.SetActive(screen == _lobby);
        }

        void SetBusy(string message)
        {
            if (_busyRoot == null) return;
            bool show = !string.IsNullOrEmpty(message);
            _busyRoot.SetActive(show);
            if (show && _busyText != null) _busyText.text = message;
        }

        void ShowError(string message)
        {
            if (_errorRoot == null) return;
            _errorRoot.SetActive(true);
            if (_errorText != null) _errorText.text = message;
        }

        void HideError()
        {
            if (_errorRoot != null) _errorRoot.SetActive(false);
        }
    }
}
