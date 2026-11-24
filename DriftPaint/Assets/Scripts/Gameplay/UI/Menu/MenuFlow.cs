using System.Collections;
using DriftPaint.Match;
using DriftPaint.Net;
using TMPro;
using UnityEngine;
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

        INetworkMediator _mediator;
        bool _loadingMatch;
        Coroutine _waitingForMenu;

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
            if (_waitingForMenu != null)
            {
                StopCoroutine(_waitingForMenu);
                _waitingForMenu = null;
            }
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
                    SetBusy(_loadingMatch ? "CARGANDO PARTIDA..." : null);
                    TryLoadMatch(_mediator.Phase);
                    break;
                case ConnectionState.LeavingRoom:
                    SetBusy("SALIENDO DE LA SALA...");
                    break;
                case ConnectionState.Reconnecting:
                    SetBusy("RECONECTANDO...");
                    break;
            }
        }

        void OnPhaseChanged(MatchPhase phase) => TryLoadMatch(phase);

        void TryLoadMatch(MatchPhase phase)
        {
            if (_loadingMatch || _mediator.State != ConnectionState.InRoom || phase.IsBetweenMatches()) return;
            // OnEnable del menú puede ocurrir antes de que termine la vuelta desde resultados.
            if (SceneLoader.IsLoading)
            {
                if (_waitingForMenu == null)
                    _waitingForMenu = StartCoroutine(WaitForMenu());
                return;
            }

            string scene = MapCatalog.SceneFor(_mediator.CurrentRoom.MapId);
            if (!SceneLoader.CanLoad(scene))
            {
                ShowError(string.Format(GameLanguage.Text("La escena \"{0}\" no está en la lista de escenas del build. Generala con DriftPaint > Generar escena de partida."), scene));
                return;
            }

            _loadingMatch = SceneLoader.Load(scene, keepReceivedObjects: true);
            if (_loadingMatch) SetBusy("CARGANDO PARTIDA...");
        }

        IEnumerator WaitForMenu()
        {
            while (SceneLoader.IsLoading) yield return null;
            _waitingForMenu = null;
            TryLoadMatch(_mediator.Phase);
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
            if (show && _busyText != null) _busyText.text = GameLanguage.Text(message);
        }

        void ShowError(string message)
        {
            if (_errorRoot == null) return;
            _errorRoot.SetActive(true);
            if (_errorText != null) _errorText.text = GameLanguage.Text(message);
        }

        void HideError()
        {
            if (_errorRoot != null) _errorRoot.SetActive(false);
        }
    }
}
