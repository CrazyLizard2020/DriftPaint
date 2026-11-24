using System.Linq;
using DriftPaint.Combat;
using DriftPaint.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Modelo del barril (Tools/Blender/barrel.py lo exporta a FBX): lo arma en el prefab del barril soltado,
    /// con su aro pulsante, y en los pickups de las escenas de partida, con la banda blanca. Cada pieza del
    /// FBX tiene su material opaco y su versión transparente para titilar al vencer.
    /// </summary>
    public static class BarrelArtBuilder
    {
        public const string ModelPath = "Assets/3D_Models/Props/Barrel.fbx";
        const string PrefabPath = "Assets/Prefabs/Combat/" + BarrelSystem.PrefabId + ".prefab";
        const string MaterialFolder = "Assets/Art/Materials/Barrel";
        const string ModelName = "Modelo";
        const string RingName = "Aro";
        const string BandPart = "Barrel_Band";

        /// <summary>Altura del aro sobre el piso, para que no parpadee contra la pintura.</summary>
        const float RingHeight = 0.04f;
        /// <summary>Tamaño del pickup respecto del barril soltado.</summary>
        const float PickupScale = 0.85f;
        /// <summary>Mitad de la altura del modelo: el pickup gira sobre su centro.</summary>
        const float ModelHalfHeight = 0.55f;

        readonly struct Part
        {
            public readonly string Mesh;
            public readonly Color Color;
            public readonly float Smoothness;
            public readonly float Metallic;

            public Part(string mesh, Color color, float smoothness, float metallic = 0f)
            {
                Mesh = mesh;
                Color = color;
                Smoothness = smoothness;
                Metallic = metallic;
            }
        }

        static readonly Part[] Parts =
        {
            new Part("Barrel_Body", new Color(0.17f, 0.16f, 0.22f), 0.45f),
            new Part("Barrel_Gold", DriftPaintPalette.SunsetGold, 0.6f, 0.65f),
            new Part("Barrel_Stripe", new Color(0.09f, 0.09f, 0.12f), 0.35f),
            new Part(BandPart, DriftPaintPalette.OffWhite, 0.5f),
            new Part("Barrel_Emblem", new Color(0.07f, 0.07f, 0.09f), 0.3f),
        };

        /// <summary>El prefab del barril todavía es el de primitivas, sin el modelo nuevo.</summary>
        public static bool Outdated()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null) return false;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return prefab != null && prefab.GetComponentInChildren<BarrelLook>(true) == null;
        }

        public static void Apply()
        {
            ConfigureImporter();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return;

            BarrelSettingsSO settings = CombatSettingsBuilder.LoadOrCreateBarrelSettings();
            using (var contents = new PrefabUtility.EditPrefabContentsScope(PrefabPath))
                DressBarrel(contents.prefabContentsRoot, settings);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Cambia el barril de primitivas del prefab por el modelo con su aro.</summary>
        internal static void DressBarrel(GameObject root, BarrelSettingsSO settings)
        {
            ClearChildren(root.transform);
            BarrelLook look = CreateModel(root.transform, settings != null ? settings.TriggerRadius : 1.6f);
            if (root.TryGetComponent(out BarrelView view))
                MenuSceneBuilder.Bind(view, ("_look", look), ("_settings", settings));
        }

        /// <summary>Paso de escena: los pickups pasan al modelo nuevo con la banda blanca.</summary>
        internal static void InstallPickups(Scene scene)
        {
            ConfigureImporter();
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (BarrelPickup pickup in root.GetComponentsInChildren<BarrelPickup>(true))
            {
                var serialized = new SerializedObject(pickup);
                var visual = serialized.FindProperty("_visual").objectReferenceValue as GameObject;
                if (visual == null) continue;
                DressPickup(visual.transform);
            }
        }

        /// <summary>Arma el modelo de un pickup dentro de su objeto visual, centrado para que gire sobre sí mismo.</summary>
        internal static void DressPickup(Transform visual)
        {
            ClearChildren(visual);
            BarrelLook look = CreateModel(visual, ringRadius: 0f);
            look.transform.localScale = Vector3.one * PickupScale;
            look.transform.localPosition = new Vector3(0f, -ModelHalfHeight * PickupScale, 0f);
        }

        /// <summary>
        /// Crea el modelo con una pieza por material. Con ringRadius mayor que cero suma el aro del piso,
        /// del tamaño de la zona en la que un rival hace explotar el barril.
        /// </summary>
        static BarrelLook CreateModel(Transform parent, float ringRadius)
        {
            Mesh[] meshes = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Mesh>().ToArray();
            var model = new GameObject(ModelName);
            model.transform.SetParent(parent, false);

            var renderers = new Renderer[Parts.Length];
            var fades = new Material[Parts.Length];
            Renderer band = null;
            for (int i = 0; i < Parts.Length; i++)
            {
                Part part = Parts[i];
                Mesh mesh = meshes.FirstOrDefault(m => m.name == part.Mesh);
                var go = new GameObject(part.Mesh);
                go.transform.SetParent(model.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                Material solid = SolidMaterial(part);
                renderer.sharedMaterial = solid;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderers[i] = renderer;
                fades[i] = FadeMaterial(part, solid);
                if (part.Mesh == BandPart) band = renderer;
            }

            Renderer ring = null;
            if (ringRadius > 0f)
            {
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.name = RingName;
                quad.transform.SetParent(model.transform, false);
                quad.transform.localPosition = new Vector3(0f, RingHeight, 0f);
                quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                // El borde de la onda llega un poco más allá de la zona de explosión.
                quad.transform.localScale = Vector3.one * (ringRadius + 0.2f) * 2f;
                ring = quad.GetComponent<MeshRenderer>();
                ring.sharedMaterial = RingMaterial();
                ring.shadowCastingMode = ShadowCastingMode.Off;
                ring.receiveShadows = false;
                ring.enabled = false;
            }

            BarrelLook look = model.AddComponent<BarrelLook>();
            var serialized = new SerializedObject(look);
            serialized.FindProperty("_band").objectReferenceValue = band;
            serialized.FindProperty("_ring").objectReferenceValue = ring;
            SetArray(serialized.FindProperty("_parts"), renderers);
            SetArray(serialized.FindProperty("_fadeMaterials"), fades);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return look;
        }

        static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        /// <summary>El FBX trae solo mallas: sin materiales, cámaras, luces ni animación.</summary>
        static void ConfigureImporter()
        {
            if (!(AssetImporter.GetAtPath(ModelPath) is ModelImporter importer)) return;
            if (importer.materialImportMode == ModelImporterMaterialImportMode.None && !importer.importAnimation
                && !importer.importCameras && !importer.importLights)
                return;

            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.SaveAndReimport();
        }

        static Material SolidMaterial(Part part)
        {
            string path = $"{MaterialFolder}/M_{part.Mesh}.mat";
            Material material = EditorAssetUtility.LoadOrCreate(path, () =>
            {
                var created = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                created.SetColor("_BaseColor", part.Color);
                created.SetFloat("_Smoothness", part.Smoothness);
                created.SetFloat("_Metallic", part.Metallic);
                if (part.Mesh == BandPart)
                {
                    // La banda brilla con el color del equipo: el bloque de propiedades pone el color de emisión.
                    created.EnableKeyword("_EMISSION");
                    created.SetColor("_EmissionColor", Color.black);
                    created.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                return created;
            });
            return material;
        }

        /// <summary>Copia transparente del material opaco: la usa la pieza mientras el barril titila.</summary>
        static Material FadeMaterial(Part part, Material solid)
        {
            string path = $"{MaterialFolder}/M_{part.Mesh}_Fade.mat";
            return EditorAssetUtility.LoadOrCreate(path, () =>
            {
                var fade = new Material(solid);
                fade.SetFloat("_Surface", 1f);
                fade.SetFloat("_Blend", 0f);
                fade.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                fade.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                fade.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                fade.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                fade.SetFloat("_ZWrite", 0f);
                fade.SetOverrideTag("RenderType", "Transparent");
                fade.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                fade.renderQueue = (int)RenderQueue.Transparent;
                fade.SetShaderPassEnabled("DepthOnly", false);
                fade.SetShaderPassEnabled("ShadowCaster", false);
                return fade;
            });
        }

        static Material RingMaterial() => EditorAssetUtility.LoadOrCreate($"{MaterialFolder}/M_BarrelRing.mat",
            () => new Material(Shader.Find("DriftPaint/Aro de barril")));
    }
}
