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
    }
}
