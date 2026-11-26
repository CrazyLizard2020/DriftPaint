using UnityEngine;

namespace DriftPaint.Match
{
    /// <summary>
    /// Lista de mapas con su presentación y el prefab de la pantalla de carga. Vive en Resources porque la usan
    /// tanto el menú como las escenas de partida, y la pantalla de carga sobrevive al cambio de escena.
    /// </summary>
    [CreateAssetMenu(menuName = "DriftPaint/Biblioteca de mapas", fileName = "MapLibrary")]
    public sealed class MapLibrarySO : ScriptableObject
    {
        public const string ResourcePath = "MapLibrary";

        [SerializeField] MapDefinitionSO[] _maps = System.Array.Empty<MapDefinitionSO>();
        [Tooltip("Pantalla de carga que se muestra al entrar a una partida. Se llena con los datos del mapa.")]
        [SerializeField] GameObject _loadingScreen;
        [Tooltip("Consejos que valen para cualquier mapa. Rotan en la pantalla de carga, en español como clave de traducción.")]
        [SerializeField, TextArea(1, 2)] string[] _tips = System.Array.Empty<string>();

        static MapLibrarySO _active;

        public string[] Tips => _tips ?? System.Array.Empty<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() => _active = null;

        public static MapLibrarySO Active => _active != null ? _active : _active = Resources.Load<MapLibrarySO>(ResourcePath);

        public GameObject LoadingScreen => _loadingScreen;

        /// <summary>Presentación del mapa, o null si el mapa no tiene una.</summary>
        public static MapDefinitionSO Find(string mapId)
        {
            MapLibrarySO library = Active;
            if (library == null || string.IsNullOrEmpty(mapId)) return null;
            foreach (MapDefinitionSO map in library._maps)
                if (map != null && map.MapId == mapId) return map;
            return null;
        }
    }
}
