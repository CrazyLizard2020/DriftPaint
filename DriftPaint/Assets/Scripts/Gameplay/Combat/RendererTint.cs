using UnityEngine;

namespace DriftPaint.Combat
{
    /// <summary>Pinta todos los renderers del objeto con un color, sin crear materiales nuevos.</summary>
    static class RendererTint
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColor = Shader.PropertyToID("_Color");

        static MaterialPropertyBlock _block;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBlock() => _block = null;

        public static void Apply(Renderer[] renderers, Color color)
        {
            if (renderers == null) return;

            var block = _block ??= new MaterialPropertyBlock();
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
