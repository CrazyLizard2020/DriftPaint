using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Muestra el auto semitransparente, por ejemplo mientras su dueño está desconectado. Cambia todos los
    /// materiales del modelo por un material transparente compartido y le pasa a cada slot su color y
    /// textura originales con un MaterialPropertyBlock, así el auto se reconoce. Al volver repone los
    /// materiales originales sin haber creado copias.
    /// </summary>
    [DisallowMultipleComponent]
    public class CarGhostVisual : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        Renderer[] _renderers;
        Material[][] _originalMaterials;
        ShadowCastingMode[] _originalShadows;
        MaterialPropertyBlock _block;
        bool _isGhost;

        public bool IsGhost => _isGhost;

        /// <param name="ghostMaterial">Material transparente que reemplaza a todos los del modelo.</param>
        /// <param name="alpha">Opacidad del auto mientras es fantasma.</param>
        public void SetGhost(bool ghost, Material ghostMaterial, float alpha)
        {
            if (ghost == _isGhost) return;
            if (ghost && ghostMaterial == null) return;

            CollectRenderers();
            if (ghost) Apply(ghostMaterial, Mathf.Clamp01(alpha));
            else Restore();
            _isGhost = ghost;
        }

        void CollectRenderers()
        {
            if (_renderers != null) return;

            Transform root = transform.Find("Visual");
            if (root == null) root = transform;

            var renderers = new List<Renderer>();
            foreach (Renderer candidate in root.GetComponentsInChildren<Renderer>(true))
                if (candidate is MeshRenderer || candidate is SkinnedMeshRenderer) renderers.Add(candidate);

            _renderers = renderers.ToArray();
            _originalMaterials = new Material[_renderers.Length][];
            _originalShadows = new ShadowCastingMode[_renderers.Length];
            _block = new MaterialPropertyBlock();
        }

        void Apply(Material ghostMaterial, float alpha)
        {
            for (int r = 0; r < _renderers.Length; r++)
            {
                Renderer target = _renderers[r];
                if (target == null) continue;

                Material[] originals = target.sharedMaterials;
                _originalMaterials[r] = originals;
                _originalShadows[r] = target.shadowCastingMode;

                var ghosts = new Material[originals.Length];
                for (int i = 0; i < originals.Length; i++)
                {
                    ghosts[i] = ghostMaterial;
                    Material original = originals[i];

                    target.GetPropertyBlock(_block, i);
                    // El color del equipo ya viene en el bloque; los demás slots toman el de su material.
                    Color color = _block.HasColor(BaseColorId) ? _block.GetColor(BaseColorId) : BaseColorOf(original);
                    color.a = alpha;
                    _block.SetColor(BaseColorId, color);
                    _block.SetColor(ColorId, color);

                    if (TryGetTexture(original, out Texture texture, out Vector4 scaleOffset))
                    {
                        _block.SetTexture(BaseMapId, texture);
                        _block.SetVector(BaseMapStId, scaleOffset);
                    }
                    target.SetPropertyBlock(_block, i);
                }

                target.sharedMaterials = ghosts;
                // Un fantasma no proyecta sombra.
                target.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        void Restore()
        {
            for (int r = 0; r < _renderers.Length; r++)
            {
                Renderer target = _renderers[r];
                Material[] originals = _originalMaterials[r];
                if (target == null || originals == null) continue;

                target.sharedMaterials = originals;
                target.shadowCastingMode = _originalShadows[r];

                // El bloque conserva color y textura iguales a los originales: solo se devuelve la opacidad.
                for (int i = 0; i < originals.Length; i++)
                {
                    target.GetPropertyBlock(_block, i);
                    if (!_block.HasColor(BaseColorId)) continue;
                    Color color = _block.GetColor(BaseColorId);
                    color.a = 1f;
                    _block.SetColor(BaseColorId, color);
                    _block.SetColor(ColorId, color);
                    target.SetPropertyBlock(_block, i);
                }
                _originalMaterials[r] = null;
            }
        }

        static Color BaseColorOf(Material material)
        {
            if (material == null) return Color.white;
            if (material.HasProperty(BaseColorId)) return material.GetColor(BaseColorId);
            if (material.HasProperty(ColorId)) return material.GetColor(ColorId);
            return Color.white;
        }

        static bool TryGetTexture(Material material, out Texture texture, out Vector4 scaleOffset)
        {
            texture = null;
            scaleOffset = new Vector4(1f, 1f, 0f, 0f);
            if (material == null) return false;

            int id = material.HasProperty(BaseMapId) ? BaseMapId : material.HasProperty(MainTexId) ? MainTexId : -1;
            if (id < 0) return false;

            texture = material.GetTexture(id);
            if (texture == null) return false;

            Vector2 scale = material.GetTextureScale(id);
            Vector2 offset = material.GetTextureOffset(id);
            scaleOffset = new Vector4(scale.x, scale.y, offset.x, offset.y);
            return true;
        }
    }
}
