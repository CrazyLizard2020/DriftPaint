using System.IO;
using DriftPaint.Level;
using DriftPaint.Teams;
using UnityEditor;
using UnityEngine;

namespace DriftPaint.EditorTools
{
    public static class CrowdArtBuilder
    {
        internal const string ProfilePath = "Assets/ScriptableObjects/Crowd/StadiumCrowd.asset";
        const string PrefabPath = "Assets/Prefabs/StadiumCrowd.prefab";
        const string Sprites = "Assets/Art/Generated/Crowd";

        [MenuItem("DriftPaint/Arte/Regenerar sprites del público")]
        public static void BakeSheets()
        {
            var profile = AssetDatabase.LoadAssetAtPath<CrowdProfile>(ProfilePath);
            int size = profile ? Mathf.Clamp(profile.framePixels, 64, 512) : 256;
            EditorAssetUtility.EnsureFolder(Sprites);
            Sheet("Celebrate", true, size);
            Sheet("Wave", false, size);
        }

        static void Sheet(string name, bool bothArms, int size)
        {
            // Coordenadas de planta de 32 x 32. El origen y los pies coinciden en los seis cuadros.
            var sheet = new Texture2D(size * 3, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * 3 * size];
            for (int pose = 0; pose < 3; pose++)
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + .5f, y + .5f) * (32f / size);
                float d = Vector2.Distance(p, new Vector2(16, 25)) - 3.45f;
                void Stroke(float ax, float ay, float bx, float by, float radius)
                {
                    Vector2 a = new Vector2(ax, ay), ab = new Vector2(bx - ax, by - ay);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    d = Mathf.Min(d, (p - a - t * ab).magnitude - radius);
                }
                Stroke(16, 18.5f, 16, 11, 3.35f);
                Stroke(14.4f, 11, 13.2f, 2.4f, 1.65f);
                Stroke(17.6f, 11, 18.8f, 2.4f, 1.65f);
                for (int arm = -1; arm <= 1; arm += 2)
                {
                    int lift = arm > 0 || bothArms ? pose : 0;
                    float elbow = lift == 0 ? 5 : 6;
                    float elbowY = lift == 0 ? 16 : lift == 1 ? 22 : 19;
                    float hand = lift == 0 ? 6 : lift == 1 ? 8 : 9;
                    float handY = lift == 0 ? 13 : lift == 1 ? 26 : 21.5f;
                    Stroke(16 + arm * 2, 19, 16 + arm * elbow, elbowY, 1.45f);
                    Stroke(16 + arm * elbow, elbowY, 16 + arm * hand, handY, 1.4f);
                }
                byte alpha = (byte)Mathf.RoundToInt(255 * Mathf.Clamp01(.5f - d * size / 32));
                pixels[y * size * 3 + pose * size + x] = new Color32(255, 255, 255, alpha);
            }
            sheet.SetPixels32(pixels);
            sheet.Apply();
            string path = $"{Sprites}/{name}.png";
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 1;
            importer.alphaIsTransparency = true;
            // El color lo lleva la malla. Conservar solo cobertura evita pagar RGBA por las tres poses.
            var format = importer.GetDefaultPlatformTextureSettings();
            format.format = TextureImporterFormat.Alpha8;
            importer.SetPlatformTextureSettings(format);
            importer.SaveAndReimport();
        }

        public static void Install(Transform backdrop)
        {
            if (backdrop.Find("StadiumCrowd")) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab) prefab = CreatePrefab();
            PrefabUtility.InstantiatePrefab(prefab, backdrop);
        }

        static GameObject CreatePrefab()
        {
            var profile = EditorAssetUtility.LoadOrCreate(ProfilePath, () =>
            {
                var settings = ScriptableObject.CreateInstance<CrowdProfile>();
                settings.palette = AssetDatabase.LoadAssetAtPath<TeamPaletteSO>(ScriptableObjectPaths.TeamPalette);
                settings.cheers = new[] { Clip("01"), Clip("02") };
                return settings;
            });
            if (!profile.material)
            {
                BakeSheets();
                var mat = ArenaArtBuilder.MaterialFor("Publico", "DriftPaint/Público de tribuna");
                mat.SetTexture("_Celebrate", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Sprites}/Celebrate.png"));
                mat.SetTexture("_Wave", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Sprites}/Wave.png"));
                profile.material = mat;
                EditorUtility.SetDirty(profile);
                EditorUtility.SetDirty(mat);
            }
            var root = new GameObject("StadiumCrowd");
            try
            {
                string[] sides = { "Norte", "Este", "Sur", "Oeste" };
                for (int edge = 0; edge < sides.Length; edge++)
                {
                    var side = new GameObject(sides[edge]);
                    side.transform.SetParent(root.transform, false);
                    var yaw = Quaternion.Euler(0, 90 * edge, 0);
                    side.transform.localRotation = yaw;
                    side.transform.localPosition = yaw * new Vector3(0, 5.1f, 69);
                    var stand = side.AddComponent<CrowdStand>();
                    stand.profile = profile;
                    stand.seed += edge * 101;
                    stand.leftTeam = edge < 2 ? CrowdStand.Side.B : CrowdStand.Side.A;
                    stand.rightTeam = edge == 0 || edge == 3 ? CrowdStand.Side.B : CrowdStand.Side.A;
                    float towerA = edge % 2 == 0 ? -25 : -35;
                    float towerB = edge % 2 == 0 ? 43 : 17;
                    var gaps = new System.Collections.Generic.List<Vector2>
                    {
                        new Vector2(towerA - 2, towerA + 2), new Vector2(towerB - 2, towerB + 2)
                    };
                    if (edge < 2)
                    {
                        float sign = edge == 0 ? -9 : 6;
                        gaps.Add(new Vector2(sign - 12, sign + 12));
                    }
                    stand.aisles = gaps.ToArray();
                    var speaker = new GameObject("Ambiente");
                    speaker.transform.SetParent(side.transform, false);
                    speaker.transform.localPosition = new Vector3(0, 1.6f, 2.3f);
                    var ambience = speaker.AddComponent<CrowdAmbience>();
                    speaker.GetComponent<AudioSource>().playOnAwake = false;
                    ambience.stand = stand;
                    ambience.clipIndex = edge % 2;
                    ambience.startFraction = edge * .233f;
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally { Object.DestroyImmediate(root); }
        }

        static AudioClip Clip(string n) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>($"{GeneratedAudioImport.CrowdFolder}/crowd_{n}_mono.ogg");
    }
}
