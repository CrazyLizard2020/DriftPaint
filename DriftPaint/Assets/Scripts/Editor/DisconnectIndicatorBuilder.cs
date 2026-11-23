using DriftPaint.Match;
using DriftPaint.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Indicadores de jugador desconectado: el material transparente de los autos fantasma y el ícono
    /// junto al nombre flotante. Los crea si faltan y los conecta en la escena de partida abierta, sin
    /// regenerarla.
    /// </summary>
    public static class DisconnectIndicatorBuilder
    {
        const string GhostMaterialPath = "Assets/Art/Materials/M_CarGhost.mat";
        const string IconPath = "Assets/Art/Generated/T_IconDisconnected.png";
        const string IconObjectName = "Disconnected Icon";
        const int IconResolution = 128;

        /// <summary>Material Lit transparente; cada auto le pasa su color y textura con un bloque de propiedades.</summary>
        public static Material LoadOrCreateGhostMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(GhostMaterialPath);
            if (material != null) return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_CarGhost" };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Smoothness", 0.3f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.35f));

            EditorAssetUtility.EnsureFolder(System.IO.Path.GetDirectoryName(GhostMaterialPath).Replace('\\', '/'));
            AssetDatabase.CreateAsset(material, GhostMaterialPath);
            return material;
        }

        /// <summary>Señal de wifi tachada, en blanco: el color lo pone la imagen.</summary>
        public static Sprite LoadOrCreateIcon()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            if (sprite != null) return sprite;

            var texture = new Texture2D(IconResolution, IconResolution, TextureFormat.RGBA32, false);
            var pixels = new Color32[IconResolution * IconResolution];
            for (int y = 0; y < IconResolution; y++)
                for (int x = 0; x < IconResolution; x++)
                {
                    float alpha = IconCoverage(new Vector2(x + 0.5f, y + 0.5f));
                    pixels[y * IconResolution + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply();

            EditorAssetUtility.SaveTexture(texture, IconPath, true, TextureWrapMode.Clamp);
            Object.DestroyImmediate(texture);
            return AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        }

        /// <summary>Cobertura de un píxel: punto y tres arcos de señal, cruzados por una barra con un margen vacío.</summary>
        static float IconCoverage(Vector2 p)
        {
            var origin = new Vector2(64f, 24f);
            Vector2 offset = p - origin;
            float distance = offset.magnitude;

            float shape = Fill(9f - distance);
            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            if (angle >= 42f && angle <= 138f)
            {
                shape = Mathf.Max(shape, Band(distance, 26f, 36f));
                shape = Mathf.Max(shape, Band(distance, 46f, 56f));
                shape = Mathf.Max(shape, Band(distance, 66f, 76f));
            }

            float toSlash = DistanceToSegment(p, new Vector2(22f, 110f), new Vector2(106f, 14f));
            float slash = Fill(7f - toSlash);
            float gap = Fill(toSlash - 14f);
            return Mathf.Max(slash, shape * gap);
        }

        static float Band(float distance, float inner, float outer) => Mathf.Min(Fill(distance - inner), Fill(outer - distance));

        /// <summary>Borde suavizado de un píxel: 1 adentro, 0 afuera.</summary>
        static float Fill(float signedInside) => Mathf.Clamp01(signedInside + 0.5f);

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Agrega el ícono a la izquierda del marcador de equipo, apagado, y lo conecta. No duplica.</summary>
        public static void AddIcon(CarNameplate plate)
        {
            if (plate == null) return;

            Transform existing = plate.transform.Find(IconObjectName);
            GameObject icon;
            if (existing != null)
            {
                icon = existing.gameObject;
            }
            else
            {
                Image image = UiFactory.CreatePanel(plate.transform, IconObjectName, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(-20f, 0f), new Vector2(48f, 48f), DriftPaintPalette.OrangeBright);
                image.sprite = LoadOrCreateIcon();
                image.preserveAspect = true;
                icon = image.gameObject;
            }

            icon.SetActive(false);
            MenuSceneBuilder.Bind(plate, ("_disconnectedIcon", icon));
        }

        /// <summary>Conecta el material fantasma y el ícono en el MatchSpawner y su plantilla de nombres.</summary>
        public static void Connect(MatchSpawner spawner)
        {
            if (spawner == null) return;

            MenuSceneBuilder.Bind(spawner, ("_ghostMaterial", LoadOrCreateGhostMaterial()));
            var so = new SerializedObject(spawner);
            AddIcon(so.FindProperty("_nameplateTemplate")?.objectReferenceValue as CarNameplate);
        }

        [MenuItem("DriftPaint/Conectar indicadores de desconexión")]
        public static void ConnectInOpenScene()
        {
            int connected = 0;
            foreach (MatchSpawner spawner in Object.FindObjectsByType<MatchSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Connect(spawner);
                connected++;
            }

            AssetDatabase.SaveAssets();
            if (connected > 0)
            {
                EditorSceneManager.MarkAllScenesDirty();
                Debug.Log("Indicadores de desconexión conectados. Guardá la escena para conservarlos.");
            }
            else
            {
                LoadOrCreateGhostMaterial();
                LoadOrCreateIcon();
                Debug.Log("Indicadores de desconexión creados. Abrí la escena de partida y volvé a usar el menú para conectarlos.");
            }
        }
    }
}
