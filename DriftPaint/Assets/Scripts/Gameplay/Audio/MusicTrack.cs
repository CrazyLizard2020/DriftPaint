using UnityEngine;

namespace DriftPaint.Audio
{
    /// <summary>
    /// Una pieza de música con su mezcla y sus fundidos. Se crea una por canción y la biblioteca de
    /// música decide en qué pantalla o mapa suena.
    /// </summary>
    [CreateAssetMenu(menuName = "DriftPaint/Audio/Pista de música", fileName = "MusicTrack_")]
    public class MusicTrack : ScriptableObject
    {
        [Tooltip("Nombre corto para pedir la pista desde código o desde una configuración remota. Vacío: se usa el nombre del asset.")]
        [SerializeField] string _id;
        [SerializeField] AudioClip _clip;
        [Tooltip("Volumen propio de la pista, para emparejar canciones masterizadas a distinto nivel.")]
        [SerializeField, Range(0f, 1f)] float _volume = 0.8f;
        [SerializeField] bool _loop = true;
        [Tooltip("Segundo desde el que arranca, por ejemplo para saltear una intro larga.")]
        [SerializeField, Min(0f)] float _startTime;
        [Tooltip("Segundos que tarda en subir el volumen cuando entra.")]
        [SerializeField, Min(0f)] float _fadeIn = 1.5f;
        [Tooltip("Segundos que tarda en bajar el volumen cuando sale.")]
        [SerializeField, Min(0f)] float _fadeOut = 1.5f;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public AudioClip Clip => _clip;
        public float Volume => _volume;
        public bool Loop => _loop;
        public float StartTime => _startTime;
        public float FadeIn => _fadeIn;
        public float FadeOut => _fadeOut;
    }
}
