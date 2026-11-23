using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.UI.Menu
{
    /// <summary>
    /// Inclina horizontalmente la malla de un Graphic para dar el corte diagonal de los botones y barras.
    /// El ángulo positivo inclina la parte de arriba hacia la derecha.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class UISkew : BaseMeshEffect
    {
        [SerializeField, Range(-45f, 45f)] float _angle = 12f;

        public float Angle
        {
            get => _angle;
            set
            {
                _angle = value;
                if (graphic != null) graphic.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive() || Mathf.Approximately(_angle, 0f)) return;

            float centerY = ((RectTransform)transform).rect.center.y;
            float shear = Mathf.Tan(_angle * Mathf.Deg2Rad);
            UIVertex vertex = default;

            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                vertex.position.x += (vertex.position.y - centerY) * shear;
                vertices.SetUIVertex(vertex, i);
            }
        }
    }
}
