using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DriftPaint.LiveOperations
{
    /// <summary>
    /// Valores de LiveOps ya validados: qué mapas y autos están habilitados, las estadísticas que pisan las
    /// de cada auto y el tema de música de cada pantalla. No se modifica después de crearse.
    /// <para>
    /// Una lista vacía significa "todos". No conoce los catálogos del juego: quien pregunta pasa los
    /// identificadores que existen, y los que el juego no conoce se ignoran ahí.
    /// </para>
    /// </summary>
    public sealed class LiveOpsConfig
    {
        const string RoomMaps = "maps";
        const string RoomCars = "cars";
        const string RoomStats = "stats";
        const string RoomMusic = "music";
        const string StabilityKey = "stability";
        const int MaxWarnings = 16;

        static readonly IReadOnlyDictionary<string, float> NoStats = new Dictionary<string, float>();

        /// <summary>Todo habilitado, sin cambios de estadísticas ni de música.</summary>
        public static readonly LiveOpsConfig Default = new LiveOpsConfig(null, null, null, null);

        readonly string[] _maps;
        readonly string[] _cars;
        readonly Dictionary<string, IReadOnlyDictionary<string, float>> _stats;
        readonly Dictionary<string, StabilityOverrides> _stability;
        readonly List<string> _warnings;
        readonly Dictionary<string, string> _music;

        LiveOpsConfig(IEnumerable<string> maps, IEnumerable<string> cars, Dictionary<string, object> stats, Dictionary<string, string> music)
        {
            _maps = CleanList(maps);
            _cars = CleanList(cars);
            _warnings = new List<string>();
            _stats = ReadStats(stats);
            _stability = ReadStability(stats);
            _music = music ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Mapas habilitados tal como llegaron. Vacía significa todos.</summary>
        public IReadOnlyList<string> Maps => _maps;
        /// <summary>Autos habilitados tal como llegaron. Vacía significa todos.</summary>
        public IReadOnlyList<string> Cars => _cars;
        /// <summary>Autos que tienen alguna estadística cambiada.</summary>
        public IEnumerable<string> CarsWithStats => _stats.Keys;
        /// <summary>Autos que tienen algún valor de estabilidad cambiado.</summary>
        public IEnumerable<string> CarsWithStability => _stability.Keys;
        /// <summary>Valores de estabilidad que llegaron y se descartaron, hasta un máximo.</summary>
        public IReadOnlyList<string> Warnings => _warnings;
        /// <summary>Tema de música por contexto (pantalla o mapa).</summary>
        public IReadOnlyDictionary<string, string> MusicOverrides => _music;

        /// <summary>
        /// Arma la configuración con los valores tal como vienen de Remote Config. Lo que no se entiende se
        /// descarta y esa parte queda con el valor por defecto.
        /// </summary>
        public static LiveOpsConfig FromRemote(string mapsEnabled, string carsEnabled, string carStatsJson, string musicJson)
        {
            return new LiveOpsConfig(SplitList(mapsEnabled), SplitList(carsEnabled),
                LiveOpsJson.ParseObject(carStatsJson), ReadMusic(LiveOpsJson.ParseObject(musicJson)));
        }

        /// <summary>
        /// Mapas habilitados entre los que el juego conoce, en el orden del juego. Si ninguno de los habilitados
        /// existe, devuelve todos los conocidos: un error de tipeo no deja el juego sin mapas.
        /// </summary>
        public IReadOnlyList<string> EnabledMaps(IEnumerable<string> known) => Filter(known, _maps);

        /// <summary>Autos habilitados entre los que el juego conoce, con el mismo criterio que <see cref="EnabledMaps"/>.</summary>
        public IReadOnlyList<string> EnabledCars(IEnumerable<string> known) => Filter(known, _cars);

        /// <summary>Estadísticas que pisan las de un auto. Vacío si el auto no tiene cambios.</summary>
        public IReadOnlyDictionary<string, float> StatsFor(string carId) =>
            !string.IsNullOrEmpty(carId) && _stats.TryGetValue(carId, out IReadOnlyDictionary<string, float> stats) ? stats : NoStats;

        /// <summary>Estabilidad que pisa la de un auto. Sin campos si el auto no tiene cambios.</summary>
        public StabilityOverrides StabilityFor(string carId) =>
            !string.IsNullOrEmpty(carId) && _stability.TryGetValue(carId, out StabilityOverrides overrides) ? overrides : StabilityOverrides.None;

        #region Copia de la sala

        /// <summary>Texto que viaja en la propiedad cfg de la sala.</summary>
        public string Serialize()
        {
            var builder = new StringBuilder(128);
            builder.Append('{');
            WriteList(builder, RoomMaps, _maps);
            builder.Append(',');
            WriteList(builder, RoomCars, _cars);
            builder.Append(',');
            LiveOpsJson.WriteString(builder, RoomStats);
            builder.Append(":{");
            bool firstCar = true;
            foreach (string car in _stats.Keys.Union(_stability.Keys, StringComparer.Ordinal))
            {
                if (!firstCar) builder.Append(',');
                firstCar = false;
                LiveOpsJson.WriteString(builder, car);
                builder.Append(":{");
                bool firstStat = true;
                foreach (KeyValuePair<string, float> stat in StatsFor(car))
                {
                    if (!firstStat) builder.Append(',');
                    firstStat = false;
                    LiveOpsJson.WriteString(builder, stat.Key);
                    builder.Append(':');
                    LiveOpsJson.WriteNumber(builder, stat.Value);
                }
                if (_stability.TryGetValue(car, out StabilityOverrides stability))
                {
                    if (!firstStat) builder.Append(',');
                    LiveOpsJson.WriteString(builder, StabilityKey);
                    builder.Append(':');
                    stability.Write(builder);
                }
                builder.Append('}');
            }
            builder.Append("},");
            LiveOpsJson.WriteString(builder, RoomMusic);
            builder.Append(":{");
            bool firstTrack = true;
            foreach (KeyValuePair<string, string> track in _music)
            {
                if (!firstTrack) builder.Append(',');
                firstTrack = false;
                LiveOpsJson.WriteString(builder, track.Key);
                builder.Append(':');
                LiveOpsJson.WriteString(builder, track.Value);
            }
            builder.Append("}}");
            return builder.ToString();
        }

        /// <summary>Lee la copia de la sala. Una sala sin copia, o con una que no se entiende, usa los valores por defecto.</summary>
        public static LiveOpsConfig Deserialize(string text)
        {
            Dictionary<string, object> root = LiveOpsJson.ParseObject(text);
            if (root == null) return Default;

            return new LiveOpsConfig(ReadList(root, RoomMaps), ReadList(root, RoomCars),
                root.TryGetValue(RoomStats, out object stats) ? stats as Dictionary<string, object> : null,
                ReadMusic(root.TryGetValue(RoomMusic, out object music) ? music as Dictionary<string, object> : null));
        }

        static void WriteList(StringBuilder builder, string name, IReadOnlyList<string> items)
        {
            LiveOpsJson.WriteString(builder, name);
            builder.Append(":[");
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) builder.Append(',');
                LiveOpsJson.WriteString(builder, items[i]);
            }
            builder.Append(']');
        }

        static IEnumerable<string> ReadList(Dictionary<string, object> root, string name) =>
            root.TryGetValue(name, out object value) && value is List<object> items ? items.OfType<string>() : null;

        #endregion

        /// <summary>Resumen de una línea para la consola.</summary>
        public string Describe()
        {
            string maps = _maps.Length == 0 ? "todos" : string.Join(", ", _maps);
            string cars = _cars.Length == 0 ? "todos" : string.Join(", ", _cars);
            string stats = _stats.Count == 0 ? "sin cambios" : string.Join(", ", _stats.Keys);
            string stability = _stability.Count == 0 ? "sin cambios" : string.Join(", ", _stability.Select(pair => $"{pair.Key} ({pair.Value.Count})"));
            string music = _music.Count == 0 ? "sin cambios" : string.Join(", ", _music.Select(pair => $"{pair.Key}={pair.Value}"));
            return $"mapas: {maps} | autos: {cars} | estadísticas: {stats} | estabilidad: {stability} | música: {music}";
        }

        #region Validación

        static IReadOnlyList<string> Filter(IEnumerable<string> known, string[] enabled)
        {
            List<string> all = known?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList()
                               ?? new List<string>();
            if (enabled.Length == 0) return all;

            List<string> picked = all.Where(id => enabled.Contains(id, StringComparer.OrdinalIgnoreCase)).ToList();
            return picked.Count > 0 ? picked : all;
        }

        static IEnumerable<string> SplitList(string text) =>
            string.IsNullOrWhiteSpace(text) ? null : text.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        static string[] CleanList(IEnumerable<string> items)
        {
            if (items == null) return Array.Empty<string>();
            return items.Select(item => item?.Trim()).Where(item => !string.IsNullOrEmpty(item))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        /// <summary>Se quedan solo los números mayores que cero: un cero o un negativo dejaría el auto inmanejable.</summary>
        static Dictionary<string, IReadOnlyDictionary<string, float>> ReadStats(Dictionary<string, object> json)
        {
            var result = new Dictionary<string, IReadOnlyDictionary<string, float>>(StringComparer.Ordinal);
            if (json == null) return result;

            foreach (KeyValuePair<string, object> car in json)
            {
                if (string.IsNullOrWhiteSpace(car.Key) || !(car.Value is Dictionary<string, object> values)) continue;

                var stats = new Dictionary<string, float>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, object> stat in values)
                {
                    if (!(stat.Value is double number)) continue;
                    float value = (float)number;
                    if (value > 0f && !float.IsInfinity(value) && !float.IsNaN(value)) stats[stat.Key.Trim()] = value;
                }
                if (stats.Count > 0) result[car.Key.Trim()] = stats;
            }
            return result;
        }

        /// <summary>Lee el objeto stability de cada auto. Los campos inválidos se descartan de a uno y quedan anotados.</summary>
        Dictionary<string, StabilityOverrides> ReadStability(Dictionary<string, object> json)
        {
            var result = new Dictionary<string, StabilityOverrides>(StringComparer.Ordinal);
            if (json == null) return result;

            foreach (KeyValuePair<string, object> car in json)
            {
                if (string.IsNullOrWhiteSpace(car.Key) || !(car.Value is Dictionary<string, object> values)) continue;
                if (!values.TryGetValue(StabilityKey, out object raw)) continue;

                string id = car.Key.Trim();
                if (!(raw is Dictionary<string, object> fields))
                {
                    Warn($"{Shorten(id)}.stability: se esperaba un objeto");
                    continue;
                }
                if (id.Length > StabilitySchema.MaxKeyLength || result.Count >= StabilitySchema.MaxCars)
                {
                    Warn($"{Shorten(id)}: estabilidad descartada por nombre largo o exceso de autos");
                    continue;
                }
                StabilityOverrides overrides = StabilityOverrides.Read(fields, id, Warn);
                if (overrides.Count > 0) result[id] = overrides;
            }
            return result;
        }

        void Warn(string message)
        {
            if (_warnings.Count < MaxWarnings) _warnings.Add(message);
        }

        static string Shorten(string id) => id.Length <= StabilitySchema.MaxKeyLength ? id : id.Substring(0, StabilitySchema.MaxKeyLength) + "…";

        static Dictionary<string, string> ReadMusic(Dictionary<string, object> json)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (json == null) return result;

            foreach (KeyValuePair<string, object> pair in json)
                if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value is string track && !string.IsNullOrWhiteSpace(track))
                    result[pair.Key.Trim()] = track.Trim();
            return result;
        }

        #endregion
    }
}
