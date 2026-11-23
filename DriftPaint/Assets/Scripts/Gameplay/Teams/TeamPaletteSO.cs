using UnityEngine;

namespace DriftPaint.Teams
{
    /// <summary>Identificadores de equipo. Coinciden con el valor guardado en cada celda de la grilla.</summary>
    public static class TeamId
    {
        public const byte None = 0;
        public const byte A = 1;
        public const byte B = 2;

        public static byte Other(byte team) => team == A ? B : team == B ? A : None;
    }

    /// <summary>
    /// Colores de cada equipo: principal (carrocería, pintura, HUD), brillo (proyectiles y efectos)
    /// y detalle (partes secundarias del auto y sombras).
    /// </summary>
    [CreateAssetMenu(fileName = "TeamPalette", menuName = "DriftPaint/Teams/Team Palette")]
    public class TeamPaletteSO : ScriptableObject
    {
        [System.Serializable]
        public struct TeamColors
        {
            public string DisplayName;
            public Color Main;
            public Color Glow;
            public Color Detail;
        }

        [SerializeField] TeamColors _teamA = new TeamColors
        {
            DisplayName = "CYAN",
            Main = new Color(0.094f, 0.874f, 0.941f),
            Glow = new Color(0.349f, 0.953f, 1f),
            Detail = new Color(0.055f, 0.663f, 0.745f)
        };

        [SerializeField] TeamColors _teamB = new TeamColors
        {
            DisplayName = "NARANJA",
            Main = new Color(1f, 0.478f, 0.102f),
            Glow = new Color(1f, 0.631f, 0.227f),
            Detail = new Color(0.847f, 0.357f, 0.027f)
        };

        [Tooltip("Colores para autos sin equipo asignado.")]
        [SerializeField] TeamColors _neutral = new TeamColors
        {
            DisplayName = "SIN EQUIPO",
            Main = new Color(0.957f, 0.969f, 0.984f),
            Glow = Color.white,
            Detail = new Color(0.325f, 0.314f, 0.392f)
        };

        /// <summary>Incluye la dupla base y las cinco alternativas de la arena.</summary>
        public const int PairCount = 6;

        [System.Serializable]
        struct Pair
        {
            public TeamColors A;
            public TeamColors B;
            public Pair(TeamColors a, TeamColors b) { A = a; B = b; }
        }

        [SerializeField] Pair[] _alternatives =
        {
            new Pair(Ink("LIMA", 0xA8E72E), Ink("PÚRPURA", 0x7A2BE2)),
            new Pair(Ink("AMARILLO", 0xF4D21F), Ink("VIOLETA", 0x8A1EF2)),
            new Pair(Ink("TURQUESA", 0x1FD4C9), Ink("ROSA", 0xE73F8B)),
            new Pair(Ink("VERDE", 0x57D93B), Ink("FUCSIA", 0xC33DD6)),
            new Pair(Ink("AGUA", 0x20CFC3), Ink("ROJO", 0xE94A3A))
        };

        static TeamColors Ink(string name, int rgb)
        {
            Color main = UI.DriftPaintPalette.Hex(rgb);
            return new TeamColors { DisplayName = name, Main = main, Glow = Color.Lerp(main, Color.white, .32f),
                Detail = Color.Lerp(main, UI.DriftPaintPalette.HudNavy, .32f) };
        }

        /// <summary>Devuelve los colores de la partida actual o los del menú antes del sorteo.</summary>
        public TeamColors Get(byte team) => Get(team, ActiveIndex);

        static int? _soloPair;
        internal static int ActiveIndex => _soloPair ?? Net.NetworkServices.Mediator?.PaletteIndex ?? 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ClearSoloPair() => _soloPair = null;

        internal static void StartSoloMatch() => _soloPair = Random.Range(0, PairCount);

        /// <summary>Resuelve una dupla guardada, incluso para salas creadas con versiones anteriores.</summary>
        public TeamColors Get(byte team, int index)
        {
            if (index > 0 && index <= _alternatives.Length)
            {
                Pair pair = _alternatives[index - 1];
                return team == TeamId.A ? pair.A : team == TeamId.B ? pair.B : _neutral;
            }
            switch (team)
            {
                case TeamId.A: return _teamA;
                case TeamId.B: return _teamB;
                default: return _neutral;
            }
        }
    }
}
