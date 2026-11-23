using DriftPaint.Net;
using DriftPaint.Profile;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.UI.Menu
{
    /// <summary>Pantalla de inicio: SALAS conecta al servidor y SALIR cierra el juego.</summary>
    public class MainMenuScreen : MonoBehaviour
    {
        [SerializeField] Button _roomsButton;
        [SerializeField] Button _quitButton;

        void Awake()
        {
            if (_roomsButton != null) _roomsButton.onClick.AddListener(OpenRooms);
            if (_quitButton != null) _quitButton.onClick.AddListener(Quit);
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
