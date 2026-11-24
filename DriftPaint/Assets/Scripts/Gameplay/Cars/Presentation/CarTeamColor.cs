using System;
using System.Collections.Generic;
using DriftPaint.Teams;
using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Pinta la carrocería con los colores del equipo. Busca en los renderers del modelo los
    /// slots de material cuyo nombre está en las listas y les cambia el color base con un
    /// MaterialPropertyBlock, sin crear copias de los materiales.
    /// </summary>
    public class CarTeamColor : MonoBehaviour
    {
        [SerializeField] TeamPaletteSO _palette;

        [Tooltip("Equipo con el que arranca el auto.")]
        [SerializeField] byte _team = TeamId.A;

        [Tooltip("Raíz de los modelos a pintar. Si está vacío se usa el hijo Visual.")]
        [SerializeField] Transform _visualRoot;

        [Tooltip("Slots de material que toman el color principal del equipo.")]
        [SerializeField] string[] _mainSlots = { "Car" };

        [Tooltip("Slots de material que toman el color de detalle del equipo.")]
        [SerializeField] string[] _detailSlots = { "Hood", "Hood2" };
        [SerializeField] string[] _glassSlots = { "Glass", "Glass.002" };

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        MaterialPropertyBlock _block;
        readonly List<(Renderer Renderer, int Index, bool Main)> _slots = new();

        public byte Team => _team;
        /// <summary>Pigmento y reflejos que comparten la carrocería y los escapes.</summary>
        public TeamPaletteSO.TeamColors Colors => _palette != null ? _palette.Get(_team)
            : new TeamPaletteSO.TeamColors { Main = Color.white, Glow = Color.white };

        void Awake() => Apply();

        /// <summary>Cambia el equipo y repinta. También funciona fuera de Play, por ejemplo para generar imágenes.</summary>
        public void SetTeam(byte team)
        {
            _team = team;
            Apply();
        }

        /// <summary>Usa una dupla concreta para la captura de referencia del editor.</summary>
        public void PreviewTeam(byte team, int pair)
        {
            CollectRenderers();
            if (_palette != null) ApplyColors(_palette.Get(team, pair));
        }

        void CollectRenderers()
        {
            if (_block != null) return;
            if (_visualRoot == null) _visualRoot = transform.Find("Visual");
            if (_visualRoot == null) _visualRoot = transform;
            _block = new MaterialPropertyBlock();
            var art = DrivingArtProfile.Active;
            foreach (Renderer renderer in _visualRoot.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var original = materials[i];
                    if (original == null) continue;
                    string slot = original.name.Replace(" (Instance)", "");
                    if (art != null && art.glassMaterial != null && Matches(slot, _glassSlots))
                    {
                        materials[i] = art.glassMaterial;
                        changed = true;
                        continue;
                    }
                    bool main = Matches(slot, _mainSlots);
                    if (!main && !Matches(slot, _detailSlots)) continue;
                    _slots.Add((renderer, i, main));
                    if (art == null || art.bodyMaterial == null) continue;
                    renderer.GetPropertyBlock(_block, i);
                    if (original.mainTexture != null)
                    {
                        _block.SetTexture("_BaseMap", original.mainTexture);
                        Vector2 scale = original.mainTextureScale, offset = original.mainTextureOffset;
                        _block.SetVector("_BaseMap_ST", new Vector4(scale.x, scale.y, offset.x, offset.y));
                        renderer.SetPropertyBlock(_block, i);
                    }
                    materials[i] = art.bodyMaterial;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        void Apply()
        {
            CollectRenderers();
            if (_palette == null) return;

            ApplyColors(_palette.Get(_team));
        }

        void ApplyColors(TeamPaletteSO.TeamColors colors)
        {
            foreach (var slot in _slots)
                SetColor(slot.Renderer, slot.Index, slot.Main ? colors.Main : colors.Detail);
        }

        void SetColor(Renderer r, int materialIndex, Color color)
        {
            r.GetPropertyBlock(_block, materialIndex);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            r.SetPropertyBlock(_block, materialIndex);
        }

        static bool Matches(string slot, string[] names)
        {
            foreach (string name in names)
                if (string.Equals(slot, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        void OnValidate()
        {
            if (Application.isPlaying && _block != null) Apply();
        }
    }
}
