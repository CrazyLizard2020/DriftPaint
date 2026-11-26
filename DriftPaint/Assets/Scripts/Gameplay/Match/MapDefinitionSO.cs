using System;
using UnityEngine;

namespace DriftPaint.Match
{
    /// <summary>
    /// Presentación de un mapa: la miniatura del selector y la pantalla de carga que se ve antes de cada partida.
    /// Los textos se escriben en español y se traducen con la tabla de textos del juego, como el resto de la UI.
    /// </summary>
    [CreateAssetMenu(menuName = "DriftPaint/Mapa", fileName = "Map_")]
    public sealed class MapDefinitionSO : ScriptableObject
    {
        [Serializable]
        public sealed class Step
        {
            public Sprite image;
            public string title;
            [TextArea(2, 4)] public string body;
        }

        [Tooltip("Identificador que viaja en la sala. Tiene que coincidir con uno de MapCatalog.")]
        [SerializeField] string _mapId;
        [Tooltip("Imagen del mapa en la tarjeta del lobby.")]
        [SerializeField] Sprite _thumbnail;

        [Header("Pantalla de carga")]
        [SerializeField] Sprite _loadingBackground;
        [Tooltip("Objetivo del mapa en una línea, debajo del nombre.")]
        [SerializeField, TextArea(1, 3)] string _tagline;
        [Tooltip("Instrucciones en pasos. La pantalla muestra hasta tres.")]
        [SerializeField] Step[] _steps = Array.Empty<Step>();
        [Tooltip("Consejos propios de este mapa. Rotan junto con los consejos generales de la biblioteca de mapas.")]
        [SerializeField, TextArea(1, 2)] string[] _tips = Array.Empty<string>();

        [Header("Tiempos de la pantalla de carga")]
        [Tooltip("Segundos mínimos desde que el host arranca hasta que empieza la cuenta regresiva, " +
                 "aunque todos hayan cargado antes. Da tiempo a leer las instrucciones.")]
        [SerializeField, Min(0f)] float _minimumSeconds = 6f;
        [Tooltip("Segundos máximos de espera a los jugadores que no terminan de cargar (por ejemplo, si a alguien se " +
                 "le colgó el juego). Pasado este tiempo la partida arranca igual para el resto y quien faltaba, si " +
                 "termina de cargar, entra a la partida empezada.")]
        [SerializeField, Min(5f)] float _maximumWaitSeconds = 20f;

        public string MapId => _mapId;
        public Sprite Thumbnail => _thumbnail;
        public Sprite LoadingBackground => _loadingBackground;
        public string Tagline => _tagline;
        public Step[] Steps => _steps;
        public string[] Tips => _tips ?? Array.Empty<string>();
        public float MinimumSeconds => _minimumSeconds;
        public float MaximumWaitSeconds => Mathf.Max(_minimumSeconds, _maximumWaitSeconds);

        void OnValidate() => _maximumWaitSeconds = Mathf.Max(_maximumWaitSeconds, _minimumSeconds);
    }
}
