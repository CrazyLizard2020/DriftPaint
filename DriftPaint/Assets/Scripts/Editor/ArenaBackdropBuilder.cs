using System.Collections.Generic;
using DriftPaint.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DriftPaint.EditorTools
{
    /// <summary>Hornea tribunas, torres de iluminación y ciudad en una sola malla decorativa.</summary>
    internal static class ArenaBackdropBuilder
    {
        internal static void Install(Transform arena)
        {
            const string path = "Assets/Art/Generated/ArenaPerimeter.asset";
            Mesh mesh = EditorAssetUtility.LoadOrCreate(path, Bake);
            Transform found = arena.Find("Tribunas y ciudad");
            if (found == null)
            {
                found = new GameObject("Tribunas y ciudad", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                found.SetParent(arena, false);
            }
            found.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = found.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = ArenaArtBuilder.MaterialFor("Tribunas", "DriftPaint/Tribunas");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            AddSign(arena, new Vector3(-9f, 10f, 66f), 0f, "Cartel norte");
            AddSign(arena, new Vector3(65.7f, 10f, -6f), 90f, "Cartel este");
            RenderSettings.skybox = ArenaArtBuilder.MaterialFor("Atardecer", "DriftPaint/Cielo de atardecer");
        }

        static void AddSign(Transform parent, Vector3 position, float yaw, string name)
        {
            Transform sign = parent.Find(name);
            if (sign == null)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.name = name;
                sign = quad.transform;
                sign.SetParent(parent, false);
            }
            sign.localPosition = position;
            sign.gameObject.layer = LayerMask.NameToLayer("SinContorno");
            sign.localRotation = Quaternion.Euler(0, yaw, 0);
            sign.localScale = new Vector3(22, 8, 1);
            Material ink = ArenaArtBuilder.MaterialFor("Cartel", "Universal Render Pipeline/Unlit");
            ink.SetTexture("_BaseMap",
                           AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/driftpaint_logo.png"));
            ink.SetFloat("_AlphaClip", 1);
            ink.SetFloat("_Cutoff", .48f);
            ink.EnableKeyword("_ALPHATEST_ON");
            EditorUtility.SetDirty(ink);
            var renderer = sign.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = ink;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        [MenuItem("DriftPaint/Arte/Regenerar tribunas")]
        public static void Rebuild()
        {
            var target = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Generated/ArenaPerimeter.asset");
            if (target == null) return;
            var mesh = Bake();
            Undo.RecordObject(target, "Regenerar tribunas");
            EditorUtility.CopySerialized(mesh, target);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
        }

        static Mesh Bake()
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var indices = new List<int>();
            var random = new System.Random(317);
            var steel = DriftPaintPalette.Hex(0x42394F);
            var deck = DriftPaintPalette.Hex(0x776579);
            var amber = DriftPaintPalette.SunsetGold;
            var cubeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cube = cubeObject.GetComponent<MeshFilter>().sharedMesh;
            var points = cube.vertices;
            var triangles = cube.triangles;
            Object.DestroyImmediate(cubeObject);
            Quaternion side = Quaternion.identity;

            void Box(float x, float y, float z, float w, float h, float d, Color color, float angle = 0)
            {
                var transform = Matrix4x4.TRS(side * new Vector3(x, y, z), side * Quaternion.Euler(0, 0, angle),
                                              new Vector3(w, h, d));
                int start = vertices.Count;
                Color pigment = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
                foreach (var v in points)
                {
                    vertices.Add(transform.MultiplyPoint3x4(v));
                    colors.Add(pigment);
                }
                foreach (int idx in triangles)
                    indices.Add(start + idx);
            }

            for (int edge = 0; edge < 4; edge++)
            {
                side = Quaternion.Euler(0, 90 * edge, 0);
                for (int k = -4; k <= 4; k++)
                {
                    float x = k * 14;
                    Color stripe = (k + edge) % 2 == 0 ? amber : deck;
                    Box(x, 2, 66, 13.5f, 4, 2, steel);
                    Box(x, 4.2f, 66, 13.6f, .4f, 2.2f, stripe);
                    Box(x, 3.65f, 64.9f, 6.4f, .50f, .18f, steel);
                    Box(x, 3.65f, 64.76f, 5.95f, .25f, .10f, new Color(1, .98f, .83f));
                    for (int mark = 0; mark < 3; mark++)
                        Box(x - 4.7f + mark * .8f, 2.35f, 64.84f, .46f, 1.2f, .12f, amber, -34);
                    for (int tier = 0; tier < 4; tier++)
                        Box(x, 4.6f + tier * .9f, 69 + tier * 2.3f, 13.8f, 1, 2.4f, tier % 2 == 0 ? deck : steel);
                    Box(x, 10, 77, 13.7f, .21f, .21f, amber);
                    Box(x, 9.3f, 77, 13.7f, .12f, .16f, steel);
                    Box(x, 9, 77, .17f, 2, .17f, amber);
                }
                if (edge == 0 || edge == 1)
                {
                    float center = edge == 0 ? -9 : 6;
                    Box(center, 10, 68.0f, 23.5f, 7.4f, .8f, steel);
                    Box(center, 10, 67.5f, 22.9f, 6.9f, .2f, deck);
                    Box(center, 13.55f, 67.3f, 22.9f, .16f, .15f, amber);
                }
                // Dos reflectores por lado, escalonados para que las torres de lados contiguos
                // se distingan al mirar hacia la esquina de la arena.
                foreach (float x in edge % 2 == 0 ? new[] { -25f, 43f } : new[] { -35f, 17f })
                {
                    Box(x - 1.4f, 10, 72, .42f, 20, .55f, steel);
                    Box(x + 1.4f, 10, 72, .42f, 20, .55f, steel);
                    for (int j = 0; j < 6; j++)
                        Box(x, 2 + j * 3, 72, .26f, 4.05f, .3f, steel, j % 2 == 0 ? 43 : -43);
                    Box(x, 20, 72, 6.4f, 3.8f, .9f, steel);
                    Box(x, 20, 71.52f, 6.16f, 3.55f, .10f, new Color(1, .98f, .83f));
                    for (int row = 0; row < 2; row++)
                        for (int col = -1; col <= 1; col++)
                            Box(x + col * 1.85f, 19.25f + row * 1.6f, 71.49f, 1.45f, 1.18f, .15f, Color.white);
                    Box(x, 13, 71.4f, 2.2f, 5, .15f,
                        x < 0 ? DriftPaintPalette.Cyan : DriftPaintPalette.Orange);
                    Box(x, 13, 71.25f, .20f, 1.75f, .09f, Color.white, 38);
                    Box(x, 13, 71.23f, .20f, 1.75f, .09f, Color.white, -38);
                }
                for (int j = -10; j <= 10; j++)
                {
                    float h = 12 + random.Next(23);
                    Box(j * 14, h / 2, 134 + random.Next(20), 8 + random.Next(8), h, 9,
                        Color.Lerp(DriftPaintPalette.Hex(0x714887), DriftPaintPalette.Hex(0xAE719B),
                                   (float)random.NextDouble()));
                }
            }
            Mesh mesh = new Mesh { name = "Tribunas y ciudad" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
