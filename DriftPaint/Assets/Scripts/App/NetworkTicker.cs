using DriftPaint.Networking;
using UnityEngine;

namespace DriftPaint.App
{
    /// <summary>Le da un paso por frame al NetworkManager, que no es un componente, para sus reintentos de conexión.</summary>
    public sealed class NetworkTicker : MonoBehaviour
    {
        NetworkManager _manager;

        public void Bind(NetworkManager manager) => _manager = manager;

        void Update() => _manager?.Tick();

#if UNITY_EDITOR
        void OnEnable() => UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;

        void OnDisable() => UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;

        void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) _manager?.OnApplicationClosing();
        }
#endif
        void OnApplicationQuit() => _manager?.OnApplicationClosing();
    }
}
