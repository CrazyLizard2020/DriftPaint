using UnityEngine;

namespace DriftPaint.Combat
{
    /// <summary>Aspecto de un proyectil en vuelo. Lo mueve el sistema de disparos; no tiene física.</summary>
    public class ProjectileView : MonoBehaviour
    {
        [SerializeField] Renderer[] _tinted;

        [SerializeField] TrailRenderer _trail;

        /// <summary>Prepara la estela al reutilizar un proyectil.</summary>
        public void SetColor(Color color)
        {
            RendererTint.Apply(_tinted, color);
            if (_trail == null) return;
            _trail.Clear();
            _trail.startColor = color;
            _trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }
    }
}
