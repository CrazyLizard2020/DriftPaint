using UnityEngine;

namespace DriftPaint.Combat
{
    /// <summary>Pinta todos los renderers del objeto con un color, sin crear materiales nuevos.</summary>
    static class RendererTint
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColor = Shader.PropertyToID("_Color");

        public static void Apply(Renderer[] renderers, Color color)
        {
            if (renderers == null) return;

            var block = new MaterialPropertyBlock();
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(block);
                block.SetColor(BaseColor, color);
                block.SetColor(LegacyColor, color);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
