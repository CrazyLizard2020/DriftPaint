using DriftPaint.Net;
using DriftPaint.Profile;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.UI.Menu
{
    /// <summary>Pantalla de inicio con acceso a salas y a la última partida reservada.</summary>
    public class MainMenuScreen : MonoBehaviour
    {
        [SerializeField] Button _roomsButton;
        [SerializeField] Button _rejoinButton;
        [SerializeField] GameObject[] _rejoinPrefabs;
        [SerializeField] Button _quitButton;

        void Awake()
        {
            if (_roomsButton != null) _roomsButton.onClick.AddListener(OpenRooms);
            if (_rejoinButton != null) _rejoinButton.onClick.AddListener(Rejoin);
            if (_quitButton != null) _quitButton.onClick.AddListener(Quit);
        }

        void OnEnable() => RefreshReservation();
        void Update() => RefreshReservation();

        void RefreshReservation()
        {
            bool show = NetworkServices.Mediator?.CanRejoinLastMatch == true;
            if (_rejoinButton != null && _rejoinButton.gameObject.activeSelf != show)
                _rejoinButton.gameObject.SetActive(show);
        }

        void Rejoin()
        {
            INetworkMediator mediator = NetworkServices.Mediator;
            if (mediator == null) return;
            // Al reiniciar, la caché de la sala puede llegar antes de que se cargue la pista.
            if (_rejoinPrefabs != null)
                foreach (GameObject prefab in _rejoinPrefabs)
                    if (prefab != null) mediator.RegisterPrefab(prefab.name, prefab);
            mediator.RejoinLastMatch();
        }

        void OpenRooms()
        {
            NetworkServices.Mediator?.Connect(PlayerProfile.LoadNickname());
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
