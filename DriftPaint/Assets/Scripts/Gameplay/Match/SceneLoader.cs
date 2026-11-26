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
        static AsyncOperation _operation;

        /// <summary>Avance de la carga en curso, de 0 a 1. Sin carga en curso vale 1.</summary>
        public static float Progress => IsLoading && _operation != null ? Mathf.Clamp01(_operation.progress / 0.9f) : 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            IsLoading = false;
            _operation = null;
        }

        public static bool CanLoad(string scene) => Application.CanStreamedLevelBeLoaded(scene);

        /// <summary>Durante el regreso, traslada a la escena nueva los objetos ya recibidos de la sala.</summary>
        public static bool Load(string scene, bool keepReceivedObjects = false)
        {
            if (IsLoading || !CanLoad(scene)) return false;

            IsLoading = true;
            INetworkMediator mediator = NetworkServices.Mediator;
            mediator?.SetIncomingPaused(true);
            if (scene == MapCatalog.MenuScene && mediator?.State == ConnectionState.InRoom)
                mediator.RetireMatchObjects();

            var received = new System.Collections.Generic.List<GameObject>();
            if (keepReceivedObjects && mediator != null)
            {
                foreach (NetworkObjectInfo info in mediator.Objects)
                {
                    if (info.Instance == null) continue;
                    Object.DontDestroyOnLoad(info.Instance);
                    received.Add(info.Instance);
                }
            }
            AsyncOperation operation = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            _operation = operation;
            operation.completed += _ =>
            {
                IsLoading = false;
                _operation = null;
                Scene destination = SceneManager.GetActiveScene();
                foreach (GameObject instance in received)
                    if (instance != null) SceneManager.MoveGameObjectToScene(instance, destination);
                // La escena nueva ya se suscribió en Awake y OnEnable: recién ahora entran los mensajes en cola.
                mediator?.SetIncomingPaused(false);
            };
            return true;
        }
    }
}
