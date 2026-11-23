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

        public TeamColors Get(byte team)
        {
            switch (team)
            {
                case TeamId.A: return _teamA;
                case TeamId.B: return _teamB;
                default: return _neutral;
            }
        }
    }
}
