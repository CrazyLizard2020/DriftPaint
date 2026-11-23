using DriftPaint.Net;
using DriftPaint.Networking;
using DriftPaint.Transport;
using UnityEngine;

namespace DriftPaint.App
{
    /// <summary>
    /// Arma las capas de red antes de cargar la primera escena: crea el transporte de Photon,
    /// lo envuelve en el NetworkManager y lo registra como mediator del juego.
    /// Es el único lugar que conoce las tres capas a la vez.
    /// </summary>
    public static class NetworkBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistration()
        {
            // Con el recargado de dominio desactivado, lo estático sobrevive entre sesiones de Play.
            NetworkServices.Register(null);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            var host = new GameObject("Network");
            Object.DontDestroyOnLoad(host);

            INetworkProvider transport = PhotonTransport.Create(host);
            var manager = new NetworkManager(transport);
            host.AddComponent<NetworkTicker>().Bind(manager);
            NetworkServices.Register(manager);
        }
    }
}
