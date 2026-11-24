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
        Vector2 _quitWithReservation;
        bool? _shownReservation;

        void Awake()
        {
            if (_roomsButton != null) _roomsButton.onClick.AddListener(OpenRooms);
            if (_rejoinButton != null) _rejoinButton.onClick.AddListener(Rejoin);
            if (_quitButton != null)
            {
                _quitButton.onClick.AddListener(Quit);
                _quitWithReservation = ((RectTransform)_quitButton.transform).anchoredPosition;
            }
        }

        void OnEnable()
        {
            RefreshReservation();
            // La caché también puede reproducirse al recuperar un asiento desde la lista de salas.
            var mediator = NetworkServices.Mediator;
            if (mediator != null && _rejoinPrefabs != null)
                foreach (GameObject prefab in _rejoinPrefabs)
                    if (prefab != null) mediator.RegisterPrefab(prefab.name, prefab);
        }
        void Update() => RefreshReservation();

        void RefreshReservation()
        {
            bool show = NetworkServices.Mediator?.CanRejoinLastMatch == true;
            if (_shownReservation == show || _rejoinButton == null) return;
            _shownReservation = show;
            _rejoinButton.gameObject.SetActive(show);
            if (_quitButton != null)
                ((RectTransform)_quitButton.transform).anchoredPosition = show ? _quitWithReservation :
                    ((RectTransform)_rejoinButton.transform).anchoredPosition;
        }

        void Rejoin()
        {
            NetworkServices.Mediator?.RejoinLastMatch();
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
