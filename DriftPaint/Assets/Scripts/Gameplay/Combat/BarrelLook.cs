using UnityEngine;

namespace DriftPaint.Combat
{
    /// <summary>
    /// Aspecto del barril: la banda central toma el color del equipo (sin equipo queda con el blanco del
    /// material, como en los pickups), el aro del piso pulsa con ese mismo color y, al vencer, todas las
    /// piezas pasan a su versión transparente para titilar. No crea materiales: usa bloques de propiedades.
    /// </summary>
    public class BarrelLook : MonoBehaviour
    {
        [Tooltip("Banda central: toma el color del equipo. Sin equipo conserva el color de su material.")]
        [SerializeField] Renderer _band;
        [Tooltip("Brillo de la banda, en proporción al color del equipo.")]
        [SerializeField, Range(0f, 2f)] float _bandGlow = 0.35f;
        [Tooltip("Piezas del barril que se vuelven transparentes al titilar (incluye la banda).")]
        [SerializeField] Renderer[] _parts;
        [Tooltip("Versión transparente del material de cada pieza, en el mismo orden que las piezas.")]
        [SerializeField] Material[] _fadeMaterials;
        [Tooltip("Aro pulsante del piso. Lo muestra solo quien es del mismo equipo que el barril.")]
        [SerializeField] Renderer _ring;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        MaterialPropertyBlock _block;
        Material[] _solidMaterials;
        Color[] _partColors;
        Color _teamColor;
        bool _hasTeam;
        bool _ringWanted;
        float _opacity = 1f;

        bool Fading => _opacity < 0.999f;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            int count = _parts != null ? _parts.Length : 0;
            _solidMaterials = new Material[count];
            _partColors = new Color[count];
            for (int i = 0; i < count; i++)
            {
                if (_parts[i] == null) continue;
                _solidMaterials[i] = _parts[i].sharedMaterial;
                _partColors[i] = _solidMaterials[i] != null && _solidMaterials[i].HasProperty(BaseColor)
                    ? _solidMaterials[i].GetColor(BaseColor)
                    : Color.white;
            }
            if (_ring != null) _ring.enabled = false;
        }

        void OnEnable()
        {
            // Las instancias pueden reciclarse: cada barril nuevo arranca opaco y sin aro hasta saber su equipo.
            if (_block == null) return;
            _ringWanted = false;
            SetOpacity(1f);
            Apply();
        }

        public void SetTeamColor(Color color)
        {
            _teamColor = color;
            _hasTeam = true;
            Apply();
        }

        public void SetRingVisible(bool visible)
        {
            if (_ringWanted == visible) return;
            _ringWanted = visible;
            Apply();
        }

        /// <summary>1 es opaco. Por debajo de 1 las piezas usan su material transparente.</summary>
        public void SetOpacity(float opacity)
        {
            opacity = Mathf.Clamp01(opacity);
            if (Mathf.Approximately(opacity, _opacity)) return;

            bool wasFading = Fading;
            _opacity = opacity;
            if (wasFading != Fading) SwapMaterials(Fading);
            Apply();
        }

        void SwapMaterials(bool transparent)
        {
            for (int i = 0; i < _parts.Length; i++)
            {
                if (_parts[i] == null) continue;
                Material fade = _fadeMaterials != null && i < _fadeMaterials.Length ? _fadeMaterials[i] : null;
                _parts[i].sharedMaterial = transparent && fade != null ? fade : _solidMaterials[i];
            }
        }

        void Apply()
        {
            if (_block == null) return;

            for (int i = 0; i < _parts.Length; i++)
            {
                Renderer part = _parts[i];
                if (part == null) continue;

                bool tinted = part == _band && _hasTeam;
                if (!tinted && !Fading)
                {
                    // Opaca y con su color de siempre: sin bloque, así comparte el lote con los demás barriles.
                    part.SetPropertyBlock(null);
                    continue;
                }

                Color color = tinted ? _teamColor : _partColors[i];
                color.a = _opacity;
                _block.Clear();
                _block.SetColor(BaseColor, color);
                if (tinted) _block.SetColor(EmissionColor, _teamColor * (_bandGlow * _opacity));
                part.SetPropertyBlock(_block);
            }

            if (_ring == null) return;
            _ring.enabled = _ringWanted && _hasTeam;
            if (!_ring.enabled) return;
            Color ringColor = _teamColor;
            ringColor.a = _opacity;
            _block.Clear();
            _block.SetColor(BaseColor, ringColor);
            _ring.SetPropertyBlock(_block);
        }
    }
}
