using System.Collections.Generic;
using UnityEngine;

namespace DriftPaint.Paint
{
    /// <summary>Ancho de la pintura según cómo va el auto.</summary>
    public static class PaintRules
    {
        /// <summary>Debajo de esta velocidad (km/h) el auto está quieto y no pinta.</summary>
        public const float MinPaintSpeedKmh = 0.5f;
        public const float MediumTierSpeedKmh = 40f;
        public const float LargeTierSpeedKmh = 90f;

        public const float SmallWidth = 1f;
        public const float MediumWidth = 2f;
        public const float LargeWidth = 3f;

        /// <summary>
        /// Ángulo mínimo entre la velocidad y la trompa para considerar que un auto ajeno está derrapando.
        /// Lo usa el Master, que no recibe el estado de drift de los demás.
        /// </summary>
        public const float DriftSlipAngle = 18f;

        /// <summary>Distancia máxima entre el caño de escape y el piso para pintar (en el aire no pinta).</summary>
        public const float MaxGroundDistance = 0.9f;

        /// <summary>Ancho en metros, o 0 si no pinta.</summary>
        public static float WidthFor(float speedKmh, bool drifting, bool stunned)
        {
            if (stunned || speedKmh < MinPaintSpeedKmh) return 0f;
            if (drifting || speedKmh > LargeTierSpeedKmh) return LargeWidth;
            if (speedKmh > MediumTierSpeedKmh) return MediumWidth;
            return SmallWidth;
        }

        /// <summary>Drift deducido del movimiento: el auto avanza de costado.</summary>
        public static bool LooksLikeDrift(Vector3 velocity, Vector3 forward, float minSpeed)
        {
            velocity.y = 0f;
            forward.y = 0f;
            if (velocity.magnitude < minSpeed || forward.sqrMagnitude < 0.0001f) return false;
            return Vector3.Angle(velocity, forward) > DriftSlipAngle && Vector3.Dot(velocity, forward) > 0f;
        }
    }

    /// <summary>
    /// Convierte el tramo que recorrió un caño de escape en celdas de la grilla: toma toda celda cuyo centro
    /// queda a menos de medio ancho del segmento, en el plano XZ. Solo usa sumas y productos en orden fijo,
    /// así la misma entrada da las mismas celdas en todos los clientes.
    /// </summary>
    public static class PaintRasterizer
    {
        public static void Rasterize(Vector3 from, Vector3 to, float width, List<int> cells)
        {
            if (width <= 0f) return;

            float radius = width * 0.5f;
            var a = new Vector2(from.x, from.z);
            var b = new Vector2(to.x, to.z);

            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius - PaintGrid.Origin));
            int maxX = Mathf.Min(PaintGrid.Size - 1, Mathf.FloorToInt(Mathf.Max(a.x, b.x) + radius - PaintGrid.Origin));
            int minZ = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius - PaintGrid.Origin));
            int maxZ = Mathf.Min(PaintGrid.Size - 1, Mathf.FloorToInt(Mathf.Max(a.y, b.y) + radius - PaintGrid.Origin));
            if (minX > maxX || minZ > maxZ) return;

            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            float radiusSq = radius * radius;

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var center = new Vector2(x + PaintGrid.Origin + 0.5f, z + PaintGrid.Origin + 0.5f);
                    float t = lengthSq > 0f ? Mathf.Clamp01(Vector2.Dot(center - a, ab) / lengthSq) : 0f;
                    Vector2 closest = a + ab * t;
                    if ((center - closest).sqrMagnitude <= radiusSq)
                        cells.Add(PaintGrid.IndexOf(x, z));
                }
            }
        }

        /// <summary>
        /// Cuadrado de size x size celdas alineado con la grilla, con la celda de center en el medio
        /// (con lado par, el centro queda en la mitad de abajo a la izquierda). Se recorta en los bordes.
        /// </summary>
        public static void RasterizeSquare(Vector3 center, int size, List<int> cells)
        {
            if (size <= 0) return;

            int startX = Mathf.FloorToInt(center.x - PaintGrid.Origin - size * 0.5f + 0.5f);
            int startZ = Mathf.FloorToInt(center.z - PaintGrid.Origin - size * 0.5f + 0.5f);
            int minX = Mathf.Max(0, startX);
            int minZ = Mathf.Max(0, startZ);
            int maxX = Mathf.Min(PaintGrid.Size - 1, startX + size - 1);
            int maxZ = Mathf.Min(PaintGrid.Size - 1, startZ + size - 1);

            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                    cells.Add(PaintGrid.IndexOf(x, z));
        }
    }
}
