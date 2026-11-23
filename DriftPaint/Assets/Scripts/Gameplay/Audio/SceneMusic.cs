using DriftPaint.Match;
using DriftPaint.Net;
using UnityEngine;

namespace DriftPaint.Audio
{
    /// <summary>
    /// Pide la música de la escena al arrancar. En el menú usa un contexto fijo; en la partida usa el
    /// mapa de la sala, así cada mapa suena con la pista que le asigna la biblioteca.
    /// Sus métodos públicos se pueden llamar desde eventos de la escena para cambiar la música.
    /// </summary>
    public class SceneMusic : MonoBehaviour
    {
        [SerializeField] MusicLibrary _library;
        [Tooltip("Contexto de esta escena, por ejemplo \"menu\". Con el mapa de la sala activado, se usa cuando no hay sala.")]
        [SerializeField] string _context = MusicContexts.Menu;
        [Tooltip("Usa el mapa de la sala actual como contexto.")]
        [SerializeField] bool _useRoomMap;
        [Tooltip("Segundos del fundido al entrar a la escena. Negativo: los de cada pista.")]
        [SerializeField] float _fade = -1f;

        float? Fade => _fade >= 0f ? _fade : (float?)null;

        void Start()
        {
            MusicManager music = MusicManager.Instance;
            music.UseLibrary(_library);
            music.PlayContext(ResolveContext(), Fade);
        }

        string ResolveContext()
        {
            if (!_useRoomMap) return _context;
            INetworkMediator mediator = NetworkServices.Mediator;
            if (mediator != null && mediator.CurrentRoom.IsValid && !string.IsNullOrEmpty(mediator.CurrentRoom.MapId))
                return mediator.CurrentRoom.MapId;
            return string.IsNullOrEmpty(_context) ? MapCatalog.DefaultMapId : _context;
        }

        public void PlayContext(string context) => MusicManager.Instance.PlayContext(context, Fade);

        public void PlayTrack(MusicTrack track) => MusicManager.Instance.Play(track, Fade);

        public void PlayTrackById(string id) => MusicManager.Instance.PlayById(id, Fade);

        public void PushTrack(MusicTrack track) => MusicManager.Instance.PushOverride(track, Fade);

        public void PopTrack() => MusicManager.Instance.PopOverride(Fade);

        public void StopMusic() => MusicManager.Instance.Stop(Fade);
    }
}
