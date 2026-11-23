using System;
using System.Collections.Generic;
using UnityEngine;

namespace DriftPaint.Audio
{
    /// <summary>
    /// Qué música suena en cada pantalla o mapa. Es el único lugar que hay que tocar para cambiar la
    /// música del juego: cambiar la pista de una fila alcanza, sin abrir escenas ni tocar código.
    /// </summary>
    [CreateAssetMenu(menuName = "DriftPaint/Audio/Biblioteca de música", fileName = "MusicLibrary")]
    public class MusicLibrary : ScriptableObject
    {
        [Serializable]
        public struct Route
        {
            [Tooltip("Pantalla o mapa: \"menu\" o el identificador del mapa, por ejemplo \"graffithasia\".")]
            public string Context;
            public MusicTrack Track;
        }

        [Tooltip("Pista de cada pantalla o mapa.")]
        [SerializeField] List<Route> _routes = new List<Route>();
        [Tooltip("Pista para un contexto que no está en la lista. Vacío: silencio.")]
        [SerializeField] MusicTrack _fallback;
        [Tooltip("Pistas que no están asignadas a ningún contexto pero se pueden pedir por id, por ejemplo una música de desempate.")]
        [SerializeField] List<MusicTrack> _extraTracks = new List<MusicTrack>();
        [Tooltip("Forma de los fundidos entre pistas: de 0 a 1 en ambos ejes. Por defecto entra y sale suave.")]
        [SerializeField] AnimationCurve _fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public AnimationCurve FadeCurve => _fadeCurve;
        public IReadOnlyList<Route> Routes => _routes;

        /// <summary>Pista asignada al contexto, o la de reserva si no tiene una.</summary>
        public MusicTrack ForContext(string context)
        {
            foreach (Route route in _routes)
                if (string.Equals(route.Context, context, StringComparison.OrdinalIgnoreCase))
                    return route.Track;
            return _fallback;
        }

        /// <summary>Busca una pista por id entre todas las que conoce la biblioteca.</summary>
        public MusicTrack Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (Route route in _routes)
                if (Matches(route.Track, id)) return route.Track;
            foreach (MusicTrack track in _extraTracks)
                if (Matches(track, id)) return track;
            return Matches(_fallback, id) ? _fallback : null;
        }

        static bool Matches(MusicTrack track, string id) =>
            track != null && string.Equals(track.Id, id, StringComparison.OrdinalIgnoreCase);
    }
}
