using UnityEngine;

namespace DriftPaint.Combat
{
    /// <summary>Aspecto de un proyectil en vuelo. Lo mueve el sistema de disparos; no tiene física.</summary>
    public class ProjectileView : MonoBehaviour
    {
        [SerializeField] Renderer[] _tinted;

        public void SetColor(Color color) => RendererTint.Apply(_tinted, color);
    }
}
