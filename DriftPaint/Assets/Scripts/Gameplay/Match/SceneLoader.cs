using DriftPaint.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DriftPaint.Match
{
    /// <summary>
    /// Carga escenas frenando los mensajes de red mientras tanto. Sin esto, un auto creado por otro jugador
    /// durante la carga aparecería en la escena vieja y se destruiría con ella.
    /// </summary>
    public static class SceneLoader
    {
        public static bool IsLoading { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() => IsLoading = false;

        public static bool CanLoad(string scene) => Application.CanStreamedLevelBeLoaded(scene);

        public static bool Load(string scene)
        {
            if (IsLoading || !CanLoad(scene)) return false;

            IsLoading = true;
            INetworkMediator mediator = NetworkServices.Mediator;
            mediator?.SetIncomingPaused(true);

            AsyncOperation operation = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            operation.completed += _ =>
            {
                IsLoading = false;
                // La escena nueva ya se suscribió en Awake y OnEnable: recién ahora entran los mensajes en cola.
                mediator?.SetIncomingPaused(false);
            };
            return true;
        }
    }
}
