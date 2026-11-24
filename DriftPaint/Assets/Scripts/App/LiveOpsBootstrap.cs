using DriftPaint.Audio;
using DriftPaint.LiveOperations;
using DriftPaint.Match;
using DriftPaint.Net;
using UnityEngine.SceneManagement;

namespace DriftPaint.App
{
    /// <summary>
    /// Conecta LiveOps con el resto del juego: pide la configuración al iniciar y cada vez que se vuelve al
    /// menú principal, toma la copia de la sala al entrar y la suelta al salir, y le pasa a la música los
    /// temas que indica la configuración vigente.
    /// </summary>
    public static class LiveOpsBootstrap
    {
        static INetworkMediator _mediator;

        /// <summary>Lo llama el arranque de la red, con el mediator ya creado.</summary>
        public static void Start(INetworkMediator mediator)
        {
            if (_mediator != null) _mediator.RoomChanged -= FollowRoom;
            _mediator = mediator;
            if (_mediator != null) _mediator.RoomChanged += FollowRoom;

            LiveOps.Changed -= ApplyMusic;
            LiveOps.Changed += ApplyMusic;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            RemoteConfigLiveOps.Fetch();
        }

        /// <summary>Al volver al menú se pide de nuevo, así un cambio publicado se ve sin reiniciar el juego.</summary>
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single && scene.name == MapCatalog.MenuScene) RemoteConfigLiveOps.Fetch();
        }

        /// <summary>Dentro de una sala vale la copia que dejó el host en cfg; afuera, la configuración local.</summary>
        static void FollowRoom()
        {
            RoomSummary room = _mediator.CurrentRoom;
            if (room.IsValid) LiveOps.EnterRoom(room.Config);
            else LiveOps.LeaveRoom();
        }

        static void ApplyMusic() => MusicManager.ReplaceContextOverrides(LiveOps.Room.MusicOverrides);
    }
}
