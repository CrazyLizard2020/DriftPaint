using System.IO;
using UnityEditor;
using UnityEngine;

namespace DriftPaint.EditorTools
{
    /// <summary>Helpers de AssetDatabase compartidos por las herramientas de editor.</summary>
    public static class EditorAssetUtility
    {
        /// <summary>Crea la carpeta (y sus padres) si no existe. Recibe rutas tipo "Assets/A/B".</summary>
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folder = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }

        /// <summary>Bounds en espacio de mundo de todos los Renderers del objeto.</summary>
        public static Bounds GetRendererBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>Devuelve el asset si existe; si no, lo crea con la función dada.</summary>
        public static T LoadOrCreate<T>(string path, System.Func<T> create) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            T asset = create();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Guarda una textura como PNG y la importa con la configuración pedida.</summary>
        public static Texture2D SaveTexture(Texture2D texture, string path, bool asSprite, TextureWrapMode wrap)
        {
            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = asSprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            // Explícito: con el modo Multiple (default de algunos proyectos) no se crea ningún sprite.
            if (asSprite) importer.spriteImportMode = SpriteImportMode.Single;
            importer.wrapMode = wrap;
            importer.mipmapEnabled = !asSprite;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = asSprite ? 1 : 8;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Material LoadOrCreateLitMaterial(string path, Color color, Texture texture = null,
            Vector2? tiling = null, float smoothness = 0.2f)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (texture != null)
            {
                material.SetTexture("_BaseMap", texture);
                if (tiling.HasValue) material.SetTextureScale("_BaseMap", tiling.Value);
            }

            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
