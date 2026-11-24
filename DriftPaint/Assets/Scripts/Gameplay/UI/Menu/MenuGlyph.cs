using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.UI.Menu
{
    /// <summary>Pictogramas de navegación que conservan sus bordes al escalar el lienzo.</summary>
    public sealed class MenuGlyph : MaskableGraphic
    {
        public enum Shape { Play, Back, Crown, Check, Refresh }
        [SerializeField] Shape _shape;

        public void SetShape(Shape shape)
        {
            _shape = shape;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            switch (_shape)
            {
                case Shape.Play:
                    Polygon(mesh, new(0.14f, 0), new(0.14f, 1), new(.98f, .5f));
                    break;
                case Shape.Back:
                    Polygon(mesh, new(0, .52f), new(.48f, 1), new(.48f, 0));
                    Polygon(mesh, new(.40f, .36f), new(.40f, .69f), new(1, .69f), new(1, .36f));
                    break;
                case Shape.Crown:
                    Polygon(mesh, new(.15f, .29f), new(0, .87f), new(.34f, .59f), new(.50f, .30f));
                    Polygon(mesh, new(.22f, .29f), new(.50f, 1), new(.78f, .29f));
                    Polygon(mesh, new(.50f, .30f), new(.66f, .59f), new(1, .87f), new(.85f, .29f));
                    Polygon(mesh, new(.15f, .08f), new(.15f, .22f), new(.85f, .22f), new(.85f, .08f));
                    break;
                case Shape.Refresh:
                    for (int i = 0; i < 16; i++)
                    {
                        float a = (32 + i * 17) * Mathf.Deg2Rad;
                        float b = a + 17 * Mathf.Deg2Rad;
                        Vector2 u = new(Mathf.Cos(a), Mathf.Sin(a));
                        Vector2 v = new(Mathf.Cos(b), Mathf.Sin(b));
                        Vector2 center = Vector2.one * .5f;
                        Polygon(mesh, center + u * .46f, center + v * .46f, center + v * .28f, center + u * .28f);
                    }
                    Polygon(mesh, new(.56f, .65f), new(.95f, .53f), new(.98f, .97f));
                    break;
                case Shape.Check:
                    Polygon(mesh, new(.02f, .52f), new(.16f, .68f), new(.49f, .34f), new(.34f, .17f));
                    Polygon(mesh, new(.34f, .17f), new(.34f, .48f), new(.85f, .99f), new(.99f, .84f));
                    break;
            }
        }

        void Polygon(VertexHelper mesh, params Vector2[] points)
        {
            Rect rect = GetPixelAdjustedRect();
            int start = mesh.currentVertCount;
            foreach (Vector2 p in points)
                mesh.AddVert(new Vector3(rect.x + rect.width * p.x, rect.y + rect.height * p.y), color, Vector2.zero);
            for (int i = 2; i < points.Length; i++) mesh.AddTriangle(start, start + i - 1, start + i);
        }
    }
}
