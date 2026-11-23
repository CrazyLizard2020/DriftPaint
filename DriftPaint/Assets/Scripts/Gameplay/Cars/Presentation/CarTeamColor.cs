using System;
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

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        MaterialPropertyBlock _block;
        Renderer[] _renderers;

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

        void CollectRenderers()
        {
            if (_renderers != null) return;
            if (_visualRoot == null) _visualRoot = transform.Find("Visual");
            if (_visualRoot == null) _visualRoot = transform;
            _renderers = _visualRoot.GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
        }

        void Apply()
        {
            CollectRenderers();
            if (_palette == null) return;

            TeamPaletteSO.TeamColors colors = _palette.Get(_team);
            foreach (Renderer r in _renderers)
            {
                Material[] materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;

                    string slot = materials[i].name.Replace(" (Instance)", "");
                    if (Matches(slot, _mainSlots)) SetColor(r, i, colors.Main);
                    else if (Matches(slot, _detailSlots)) SetColor(r, i, colors.Detail);
                }
            }
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
            if (Application.isPlaying && _renderers != null) Apply();
        }
    }
}
