using System.Linq;
using DriftPaint.Match;
using DriftPaint.Paint;
using DriftPaint.Scoring;
using DriftPaint.Teams;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DriftPaint.EditorTools
{
    /// <summary>Instala los objetivos una sola vez para conservar las ubicaciones elegidas por diseño.</summary>
    public static class PaintRingBuilder
    {
        [MenuItem("DriftPaint/Conectar aros de pintura")]
        static void Connect()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != MatchSceneBuilder.ScenePath) return;
            Install(scene);
        }

        internal static void Install(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var arena = roots.First(root => root.name == "Arena").transform;
            var match = roots.Select(root => root.GetComponent<MatchController>()).First(component => component != null);
            if (match.GetComponent<PaintRingSystem>() != null) return;

            var ramps = arena.Find("Ramps");
            var cyan = ramps.Find("Ramp_Cyan");
            var orange = ramps.Find("Ramp_Orange");
            var settings = EditorAssetUtility.LoadOrCreate(ScriptableObjectPaths.PaintRings, ScriptableObject.CreateInstance<PaintRingSettings>);
            var mesh = EditorAssetUtility.LoadOrCreate("Assets/Art/Generated/Aro de pintura.asset", CreateMesh);
            var white = EditorAssetUtility.LoadOrCreateLitMaterial("Assets/Art/Materials/M_Aro.mat", Color.white);
            var dark = EditorAssetUtility.LoadOrCreateLitMaterial("Assets/Art/Materials/M_CartelAro.mat", new Color(.03f, .035f, .075f));
            var parent = Node("Aros de Pintura", arena);
            var rings = new[] { Create("Aro Cian", 0, cyan), Create("Aro Naranja", 1, orange) };
            var system = match.gameObject.AddComponent<PaintRingSystem>();
            var paint = match.GetComponent<PaintSystem>();
            var palette = AssetDatabase.LoadAssetAtPath<TeamPaletteSO>(ScriptableObjectPaths.TeamPalette);
            MenuSceneBuilder.Bind(system, ("_settings", settings), ("_match", match), ("_paint", paint), ("_palette", palette));
            SetArray(system, "_rings", rings);
            MenuSceneBuilder.Bind(paint, ("_rings", system));
            MenuSceneBuilder.Bind(match.GetComponent<MatchScore>(), ("_rings", system));
            EditorSceneManager.MarkSceneDirty(scene);

            PaintRing Create(string label, int id, Transform ramp)
            {
                var root = Node(label, parent);
                var direction = Vector3.ProjectOnPlane(ramp.forward, Vector3.up).normalized;
                root.SetPositionAndRotation(ramp.TransformPoint(new Vector3(0,.5f,.5f)) + direction * 5.5f + Vector3.up * 2.1f,
                    Quaternion.LookRotation(direction));
                var ring = root.gameObject.AddComponent<PaintRing>();
                var rotor = Node("Corona", root);
                rotor.localScale = Vector3.one * settings.diameter;
                rotor.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var rim = rotor.gameObject.AddComponent<MeshRenderer>();
                rim.sharedMaterial = white;
                Box("Cartel", rotor, new Vector3(0,.56f,0), new Vector3(.35f,.095f,.024f), dark);
                var lamps = new Renderer[4];
                for (int i = 0; i < lamps.Length; i++)
                    lamps[i] = Box("Carga " + (i+1), rotor, new Vector3((i-1.5f)*.078f,.56f,0),
                        new Vector3(.067f,.064f,.028f), white);
                var spray = Node("Pintura", root).gameObject.AddComponent<ParticleSystem>();
                spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = spray.main;
                main.playOnAwake = false;
                main.loop = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startSize = new ParticleSystem.MinMaxCurve(.13f,.32f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(.65f,1.2f);
                main.gravityModifier = .85f;
                main.maxParticles = 96;
                var shape = spray.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radiusThickness = 0;
                shape.radius = settings.diameter * .5f;
                var emission = spray.emission;
                emission.rateOverTime = 0;
                var renderer = spray.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 1.15f;
                renderer.velocityScale = .045f;
                renderer.sharedMaterial = ArenaArtBuilder.MaterialFor("Gotas", "DriftPaint/Tinta en movimiento");
                MenuSceneBuilder.Bind(ring, ("_ramp", ramp), ("_rotor", rotor), ("_rim", rim), ("_spray", spray));
                var serialized = new SerializedObject(ring);
                serialized.FindProperty("_id").intValue = id;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                SetArray(ring, "_lamps", lamps);
                return ring;
            }
        }

        static Transform Node(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        static Renderer Box(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            var renderer = box.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        static void SetArray(Object component, string field, Object[] values)
        {
            var serialized = new SerializedObject(component);
            var array = serialized.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static Mesh CreateMesh()
        {
            const int sides = 64, tube = 8;
            var vertices = new Vector3[sides * tube];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[vertices.Length * 6];
            for (int i = 0; i < sides; i++)
            {
                float a = 2f * Mathf.PI * i / sides;
                Vector3 outward = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                for (int j = 0; j < tube; j++)
                {
                    float b = 2f * Mathf.PI * j / tube;
                    int slot = i * tube + j;
                    normals[slot] = outward * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                    vertices[slot] = outward * .5f + normals[slot] * .021f;
                    int next = (i+1) % sides * tube, along = (j+1) % tube, t = slot*6;
                    triangles[t] = slot; triangles[t+1] = next+j; triangles[t+2] = next+along;
                    triangles[t+3] = slot; triangles[t+4] = next+along; triangles[t+5] = i*tube+along;
                }
            }
            return new Mesh { name = "Aro de pintura", vertices = vertices, normals = normals, triangles = triangles };
        }
    }
}
