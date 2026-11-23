using DriftPaint.Networking;
using UnityEngine;

namespace DriftPaint.Transport
{
    /// <summary>
    /// Crea el transporte de Photon sin exponer tipos de Photon a quien lo pide.
    /// </summary>
    public static class PhotonTransport
    {
        public static INetworkProvider Create(GameObject host) => host.AddComponent<PhotonManager>();
    }
}
