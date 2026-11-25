using UnityEngine;

namespace DriftPaint.UI
{
    /// <summary>Colores del juego para equipos, escenario y HUD.</summary>
    public static class DriftPaintPalette
    {
        public static readonly Color Cyan = Hex(0x18DFF0);
        public static readonly Color CyanBright = Hex(0x59F3FF);
        public static readonly Color CyanDeep = Hex(0x0EA9BE);

        public static readonly Color Orange = Hex(0xFF7A1A);
        public static readonly Color OrangeBright = Hex(0xFFA13A);
        public static readonly Color OrangeDeep = Hex(0xD85B07);

        public static readonly Color Asphalt = Hex(0x7E7887);
        public static readonly Color Slate = Hex(0x535064);

        public static readonly Color HudNavy = Hex(0x1F2338);
        public static readonly Color OffWhite = Hex(0xF4F7FB);

        public static readonly Color SkyLavender = Hex(0xB28FD8);
        public static readonly Color SkyBlue = Hex(0x89A9E8);

        public static readonly Color SunsetGold = Hex(0xFDBA4D);
        public static readonly Color WarmPeach = Hex(0xFFC8A2);

        public static Color Hex(int rgb, float alpha = 1f)
        {
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                alpha);
        }

        public static Color WithAlpha(this Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
