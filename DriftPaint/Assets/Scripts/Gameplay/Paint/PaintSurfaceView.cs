using DriftPaint.Teams;
using UnityEngine;

namespace DriftPaint.Paint
{
    /// <summary>
    /// Muestra la grilla que se ve en una textura de 128 x 128 px (un pixel por celda) sobre el piso.
    /// Solo se escriben los pixeles de las celdas que cambian y la textura se sube una vez por frame.
    /// Es solo visual: nunca se manda por red.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class PaintSurfaceView : MonoBehaviour
    {
        [SerializeField] PaintSystem _paint;
        [SerializeField] TeamPaletteSO _palette;
        [SerializeField] MeshRenderer[] _ramps;

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        Texture2D _texture;
        Color32[] _pixels;
        Color32 _colorA;
        Color32 _colorB;
        bool _dirty;

        void Awake()
        {
            _texture = new Texture2D(PaintGrid.Size, PaintGrid.Size, TextureFormat.RGBA32, false)
            {
                name = "PaintSurface",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _pixels = new Color32[PaintGrid.CellCount];
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);

            _colorA = _palette != null ? (Color32)_palette.Get(TeamId.A).Main : new Color32(24, 223, 240, 255);
            _colorB = _palette != null ? (Color32)_palette.Get(TeamId.B).Main : new Color32(255, 122, 26, 255);

            var block = new MaterialPropertyBlock();
            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.GetPropertyBlock(block);
            block.SetTexture(BaseMapId, _texture);
            block.SetTexture(MainTexId, _texture);
            meshRenderer.SetPropertyBlock(block);
            if (_ramps != null)
                foreach (var ramp in _ramps)
                    if (ramp != null) ramp.SetPropertyBlock(block);
        }

        void OnEnable()
        {
            if (_paint == null) return;
            _paint.DisplayChanged += OnDisplayChanged;

            for (int i = 0; i < PaintGrid.CellCount; i++)
                _pixels[i] = ColorOf(_paint.GetDisplayTeam(i));
            _dirty = true;
        }

        void OnDisable()
        {
            if (_paint != null) _paint.DisplayChanged -= OnDisplayChanged;
        }

        void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
        }

        void OnDisplayChanged(int cell, byte team)
        {
            _pixels[cell] = ColorOf(team);
            _dirty = true;
        }

        void LateUpdate()
        {
            if (!_dirty) return;
            _dirty = false;
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
        }

        Color32 ColorOf(byte team)
        {
            switch (team)
            {
                case TeamId.A: return _colorA;
                case TeamId.B: return _colorB;
                default: return new Color32(0, 0, 0, 0);
            }
        }
    }
}
