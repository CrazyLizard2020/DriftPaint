using System.Collections.Generic;
using System.Linq;
using System.Text;
using DriftPaint.Cars;
using DriftPaint.Teams;
using DriftPaint.UI;
using UnityEditor;
using UnityEngine;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Arma el catálogo de autos a partir de los prefabs de Assets/Prefabs/Cars y saca una foto de cada
    /// auto con los colores de cada equipo, para la pantalla de selección. El orden es por nombre de
    /// prefab, igual que la lista de autos de la escena de partida.
    /// </summary>
    public static class CarCatalogBuilder
    {
        const string CatalogPath = ScriptableObjectPaths.CarCatalog;
        const string PrefabFolder = "Assets/Prefabs/Cars";
        const string PreviewFolder = "Assets/Art/Generated/CarPreviews";
        const string PalettePath = ScriptableObjectPaths.TeamPalette;
        const int PreviewWidth = 768;
        const int PreviewHeight = 384;

        /// <summary>Fondo de las fotos: el mismo color que las tarjetas del menú.</summary>
        static readonly Color PreviewBackground = DriftPaintPalette.Hex(0x2A2F4A);

        [MenuItem("DriftPaint/Generar catálogo de autos")]
        public static void BuildFromMenu()
        {
            CarCatalogSO catalog = Build();
            if (catalog != null) Debug.Log($"Catálogo de autos generado en {CatalogPath} con {catalog.Count} autos.");
        }

        /// <summary>El catálogo existente, o uno nuevo si no hay, está vacío o le falta alguna imagen.</summary>
        internal static CarCatalogSO LoadOrBuild()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalogSO>(CatalogPath);
            int count = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }).Length;
            return IsComplete(catalog) && catalog.Count == count ? catalog : Build();
        }

        static bool IsComplete(CarCatalogSO catalog)
        {
            if (catalog == null || catalog.Count == 0) return false;
            for (int i = 0; i < catalog.Count; i++)
                if (catalog.Get(i).PreviewFor(TeamId.A) == null || catalog.Get(i).PreviewFor(TeamId.B) == null) return false;
            return true;
        }

        /// <summary>
        /// Sprite de una imagen ya guardada. Si se importó en modo Multiple (sin sprites adentro), la pasa a
        /// Single para que exista el sprite.
        /// </summary>
        static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static CarCatalogSO Build()
        {
            List<GameObject> prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(p => p != null && p.GetComponent<CarController>() != null)
                .OrderBy(p => p.name, System.StringComparer.Ordinal)
                .ToList();
            if (prefabs.Count == 0)
            {
                Debug.LogWarning($"No hay prefabs de autos en {PrefabFolder}. Generá primero la escena de partida.");
                return null;
            }

            var palette = AssetDatabase.LoadAssetAtPath<TeamPaletteSO>(PalettePath);
            CarCatalogSO catalog = EditorAssetUtility.LoadOrCreate(CatalogPath, ScriptableObject.CreateInstance<CarCatalogSO>);

            var so = new SerializedObject(catalog);
            SerializedProperty entries = so.FindProperty("_entries");
            var names = new Dictionary<string, string>();
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                names[entry.FindPropertyRelative("_id").stringValue] = entry.FindPropertyRelative("_displayName").stringValue;
            }
            entries.arraySize = prefabs.Count;
            for (int i = 0; i < prefabs.Count; i++)
            {
                GameObject prefab = prefabs[i];
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_id").stringValue = prefab.name;

                SerializedProperty displayName = entry.FindPropertyRelative("_displayName");
                // El nombre se respeta si ya se editó a mano en el asset.
                displayName.stringValue = names.TryGetValue(prefab.name, out string saved) && !string.IsNullOrEmpty(saved)
                    ? saved : DisplayNameFor(prefab.name);

                entry.FindPropertyRelative("_previewTeamA").objectReferenceValue = RenderPreview(prefab, TeamId.A, palette);
                entry.FindPropertyRelative("_previewTeamB").objectReferenceValue = RenderPreview(prefab, TeamId.B, palette);
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            if (!IsComplete(catalog))
                Debug.LogWarning($"Al catálogo {CatalogPath} le faltan imágenes de autos. Revisá los avisos anteriores.");
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        /// <summary>"Car_Gc14" → "GC-14", "Car_A_R7" → "A-R7".</summary>
        internal static string DisplayNameFor(string prefabName)
        {
            string name = prefabName.StartsWith("Car_") ? prefabName.Substring(4) : prefabName;
            name = name.Replace('_', '-').ToUpperInvariant();
            if (name.Contains("-")) return name;

            var builder = new StringBuilder(name.Length + 1);
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsDigit(name[i]) && char.IsLetter(name[i - 1])) builder.Append('-');
                builder.Append(name[i]);
            }
            return builder.ToString();
        }

        /// <summary>Foto del auto de tres cuartos de frente, con los colores del equipo.</summary>
        static Sprite RenderPreview(GameObject prefab, byte team, TeamPaletteSO palette)
        {
            string path = $"{PreviewFolder}/T_{prefab.name}_{(team == TeamId.B ? "B" : "A")}.png";
            var preview = new PreviewRenderUtility();
            try
            {
                GameObject instance = preview.InstantiatePrefabInScene(prefab);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                if (instance.TryGetComponent(out CarTeamColor teamColor))
                {
                    if (palette != null)
                    {
                        var colorSo = new SerializedObject(teamColor);
                        colorSo.FindProperty("_palette").objectReferenceValue = palette;
                        colorSo.ApplyModifiedPropertiesWithoutUndo();
                    }
                    teamColor.SetTeam(team);
                }

                Bounds bounds = EditorAssetUtility.GetRendererBounds(instance.transform.Find("Visual").gameObject);
                Camera camera = preview.camera;
                camera.fieldOfView = 22f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 200f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = PreviewBackground;

                // De frente y a la derecha del auto, un poco desde arriba.
                Vector3 direction = new Vector3(1.15f, 0.45f, 1.5f).normalized;
                float radius = bounds.extents.magnitude;
                float distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.57f;
                camera.transform.position = bounds.center + direction * distance;
                camera.transform.LookAt(bounds.center);

                preview.lights[0].intensity = 1.25f;
                preview.lights[0].transform.rotation = Quaternion.LookRotation(new Vector3(-0.5f, -0.8f, -0.6f));
                preview.lights[1].intensity = 0.5f;
                preview.lights[1].transform.rotation = Quaternion.LookRotation(new Vector3(0.7f, -0.3f, 0.6f));
                preview.ambientColor = new Color(0.45f, 0.45f, 0.55f);

                preview.BeginStaticPreview(new Rect(0f, 0f, PreviewWidth, PreviewHeight));
                camera.aspect = (float)PreviewWidth / PreviewHeight;
                preview.Render(true, false);
                Texture2D rendered = preview.EndStaticPreview();

                var output = new Texture2D(rendered.width, rendered.height, TextureFormat.RGBA32, false);
                // El estudio tiene fondo uniforme. Dejarlo transparente permite ver la pista detrás del auto.
                var pixels = rendered.GetPixels32();
                Color32 background = pixels[0];
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].r == background.r && pixels[i].g == background.g && pixels[i].b == background.b)
                        pixels[i].a = 0;
                }
                output.SetPixels32(pixels);
                output.Apply();
                EditorAssetUtility.SaveTexture(output, path, true, TextureWrapMode.Clamp);
                Object.DestroyImmediate(output);
                Object.DestroyImmediate(rendered);
                return LoadSprite(path);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"No se pudo generar la imagen de {prefab.name}: {exception.Message}");
                return LoadSprite(path);
            }
            finally
            {
                preview.Cleanup();
            }
        }
    }
}
