using System.Linq;
using DriftPaint.Cars;
using DriftPaint.Combat;
using DriftPaint.Match;
using DriftPaint.Teams;
using DriftPaint.UI.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DriftPaint.EditorTools
{
    /// <summary>Conecta la presentación a los objetos existentes sin reemplazar las escenas ni sus
    /// ajustes.</summary>
    public static class ArenaArtBuilder
    {
        [MenuItem("DriftPaint/Conectar arte de arena y efectos")]
        static void ConnectOpen()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MenuSceneBuilder.ScenePath && scene.path != MatchSceneBuilder.ScenePath)
                return;
            PrepareFleet();
            Install(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>Aplica los recursos a las escenas guardadas desde el editor por lotes.</summary>
        public static void ApplySavedScenes()
        {
            if (!Application.isBatchMode)
                return;
            PrepareFleet();
            foreach (string path in new[] { MenuSceneBuilder.ScenePath, MatchSceneBuilder.ScenePath })
            {
                var scene = EditorSceneManager.OpenScene(path);
                Install(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new System.IO.IOException(path);
            }
            AssetDatabase.SaveAssets();
        }

        static void PrepareFleet()
        {
            var data = AssetDatabase.LoadAssetAtPath<CarDataSO>(ScriptableObjectPaths.CarData);
            var palette = AssetDatabase.LoadAssetAtPath<TeamPaletteSO>(ScriptableObjectPaths.TeamPalette);
            foreach (var spec in CarPrefabBuilder.Models)
            {
                string path = CarPrefabBuilder.PrefabFolder + "/Car_" + spec.Name + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    CarPrefabBuilder.Build(spec, data, palette);
                    continue;
                }
                using (var contents = new PrefabUtility.EditPrefabContentsScope(path))
                    ConnectCar(contents.prefabContentsRoot);
            }
            CarCatalogBuilder.LoadOrBuild();
        }

        internal static Material MaterialFor(string name, string shader) => EditorAssetUtility.LoadOrCreate(
            "Assets/Art/Materials/M_" + name + ".mat", () => new Material(Shader.Find(shader)));

        internal static void Install(Scene scene)
        {
            var palette = AssetDatabase.LoadAssetAtPath<TeamPaletteSO>(ScriptableObjectPaths.TeamPalette);
            var fleet = AssetDatabase.FindAssets("t:Prefab", new[] { CarPrefabBuilder.PrefabFolder })
                            .Select(AssetDatabase.GUIDToAssetPath)
                            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                            .Where(p => p.GetComponent<CarController>() != null)
                            .OrderBy(p => p.name, System.StringComparer.Ordinal)
                            .ToArray();
            ArenaInterfaceBuilder.Install(scene, palette);
            bool matchScene = scene.GetRootGameObjects().Any(root => root.GetComponent<MatchSpawner>() != null);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Arena")
                    ArenaBackdropBuilder.Install(root.transform);
                if (root.TryGetComponent<MatchSpawner>(out var spawner))
                    BindFleet(spawner, "_carPrefabs", fleet);
                var main = root.GetComponentInChildren<MainMenuScreen>(true);
                if (main != null)
                    BindFleet(main, "_rejoinPrefabs",
                              fleet
                                  .Concat(new[] { AssetDatabase.LoadAssetAtPath<GameObject>(
                                      "Assets/Prefabs/Combat/Barrel.prefab") })
                                  .Where(p => p != null)
                                  .ToArray());
                if (!matchScene)
                    continue;
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component is MatchSpawner || component is WeaponSystem || component is BarrelSystem ||
                        component is Paint.PaintSurfaceView || component is UI.Score.ScoreTableView ||
                        component is UI.Score.ResultsScreen)
                        MenuSceneBuilder.Bind(component, ("_palette", palette));
                }
                if (root.TryGetComponent<Camera>(out var camera))
                    camera.clearFlags = CameraClearFlags.Skybox;
                if (root.TryGetComponent<CarCameraRig>(out var rig))
                {
                    var framing = new SerializedObject(rig);
                    framing.FindProperty("_distanceAtRest").floatValue = 4.4f;
                    framing.FindProperty("_distanceAtMaxSpeed").floatValue = 5.8f;
                    framing.FindProperty("_fovAtRest").floatValue = 52f;
                    framing.FindProperty("_fovAtMaxSpeed").floatValue = 63f;
                    framing.ApplyModifiedPropertiesWithoutUndo();
                }
                if (root.TryGetComponent<Light>(out var light) && light.type == LightType.Directional)
                {
                    light.color = new Color(1f, .82f, .66f);
                    light.intensity = 1.2f;
                    light.transform.rotation = Quaternion.Euler(29, -135, 0);
                }
                if (root.TryGetComponent<Paint.PaintSurfaceView>(out var paint))
                    ConnectPaint(scene, paint);
                var projectile = root.GetComponentInChildren<ProjectileView>(true);
                if (projectile != null)
                {
                    projectile.transform.localScale = new Vector3(.40f, .40f, .86f);
                    TrailRenderer trail = Trail(projectile.transform, "Estela", Vector3.zero, .19f, .34f);
                    MenuSceneBuilder.Bind(projectile, ("_trail", trail));
                }
                var impact = root.GetComponentInChildren<ImpactBurst>(true);
                if (impact != null)
                {
                    impact.GetComponent<MeshRenderer>().sharedMaterial =
                        MaterialFor("Chorro", "DriftPaint/Chorro viscoso");
                    var crown = EditorAssetUtility.LoadOrCreate("Assets/Art/Generated/InkCrown.asset", Crown);
                    if (crown.vertexCount != CrownSegments * 3)
                    {
                        var updated = Crown();
                        EditorUtility.CopySerialized(updated, crown);
                        Object.DestroyImmediate(updated);
                        EditorUtility.SetDirty(crown);
                    }
                    impact.GetComponent<MeshFilter>().sharedMesh = crown;
                    var drops = Drops(impact.transform, "Gotas", Vector3.zero, 0, 48);
                    var mainModule = drops.main;
                    mainModule.playOnAwake = false;
                    mainModule.loop = false;
                    mainModule.startLifetime = new ParticleSystem.MinMaxCurve(.24f, .58f);
                    mainModule.gravityModifier = 1.8f;
                    mainModule.scalingMode = ParticleSystemScalingMode.Shape;
                    var shape = drops.shape;
                    shape.angle = 67;
                    shape.radius = .11f;
                    drops.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                    MenuSceneBuilder.Bind(impact, ("_drops", drops));
                    var so = new SerializedObject(impact);
                    so.FindProperty("_duration").floatValue = .61f;
                    so.FindProperty("_heightRatio").floatValue = .16f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        static void BindFleet(Component target, string field, GameObject[] fleet)
        {
            var so = new SerializedObject(target);
            var list = so.FindProperty(field);
            list.arraySize = fleet.Length;
            for (int i = 0; i < fleet.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = fleet[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConnectPaint(Scene scene, Paint.PaintSurfaceView paint)
        {
            Material material = MaterialFor("PinturaHumeda", "DriftPaint/Pintura húmeda");
            paint.GetComponent<MeshRenderer>().sharedMaterial = material;
            var ramps = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Arena")?.transform.Find("Ramps");
            if (ramps == null)
                return;
            var so = new SerializedObject(paint);
            var surfaces = so.FindProperty("_ramps");
            surfaces.arraySize = ramps.childCount;
            for (int i = 0; i < ramps.childCount; i++)
            {
                Transform ramp = ramps.GetChild(i);
                if (ramp.TryGetComponent<MeshRenderer>(out var deck))
                    deck.sharedMaterial = MaterialFor("Rampa", "DriftPaint/Rampa de pista");
                Transform top = ramp.Find("Pintura");
                if (top == null)
                {
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.DestroyImmediate(quad.GetComponent<Collider>());
                    quad.name = "Pintura";
                    top = quad.transform;
                    top.SetParent(ramp, false);
                }
                top.localPosition = Vector3.up * .505f;
                top.localRotation = Quaternion.Euler(90, 0, 0);
                var surface = top.GetComponent<MeshRenderer>();
                surface.sharedMaterial = material;
                surface.shadowCastingMode = ShadowCastingMode.Off;
                surface.receiveShadows = false;
                surfaces.GetArrayElementAtIndex(i).objectReferenceValue = surface;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void ConnectCar(GameObject root)
        {
            Transform left = root.transform.Find("Exhaust_L"), right = root.transform.Find("Exhaust_R");
            if (left == null || right == null)
                throw new System.InvalidOperationException("Faltan los escapes de " + root.name);
            var a = ExhaustDrops(left);
            var b = ExhaustDrops(right);
            var drift = Drops(root.transform, "Salpicadura de derrape", new Vector3(0, .15f, left.localPosition.z),
                              22, 16);
            var shape = drift.shape;
            var driftMain = drift.main;
            driftMain.startSize = new ParticleSystem.MinMaxCurve(.045f, .11f);
            driftMain.startLifetime = new ParticleSystem.MinMaxCurve(.12f, .21f);
            shape.angle = 65f;
            shape.radius = .45f;
            drift.transform.localRotation = Quaternion.Euler(-30, 180, 0);
            var wheel = root.GetComponent<CarVisuals>();
            var wheels = new SerializedObject(wheel);
            var rl = (Transform)wheels.FindProperty("_wheelRearLeft").objectReferenceValue;
            var rr = (Transform)wheels.FindProperty("_wheelRearRight").objectReferenceValue;
            var l = Trail(root.transform, "Marca trasera izquierda",
                          new Vector3(rl.localPosition.x, .055f, rl.localPosition.z), 1.35f, .14f);
            var r = Trail(root.transform, "Marca trasera derecha",
                          new Vector3(rr.localPosition.x, .055f, rr.localPosition.z), 1.35f, .14f);
            l.startColor = r.startColor = new Color(.12f, .10f, .16f, .68f);
            l.endColor = r.endColor = new Color(.15f, .12f, .17f, 0);
            if (!root.TryGetComponent(out CarPaintEffects fx))
                fx = root.AddComponent<CarPaintEffects>();
            MenuSceneBuilder.Bind(fx, ("_leftJet", a), ("_rightJet", b), ("_drift", drift), ("_leftTrack", l),
                                  ("_rightTrack", r), ("_leftStream", Stream(left)),
                                  ("_rightStream", Stream(right)));
        }

        static ParticleSystem ExhaustDrops(Transform exhaust)
        {
            var drops = Drops(exhaust, "Chorro", Vector3.zero, 45, 14);
            var main = drops.main;
            // Las gotas se funden con el charco antes de alcanzar la cámara de seguimiento.
            main.startSize = new ParticleSystem.MinMaxCurve(.045f, .105f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(.065f, .13f);
            var size = drops.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, .18f));
            var renderer = drops.GetComponent<ParticleSystemRenderer>();
            renderer.velocityScale = .025f;
            renderer.lengthScale = 1.15f;
            return drops;
        }

        static MeshRenderer Stream(Transform exhaust)
        {
            Transform t = Child(exhaust, "Tinta continua", Vector3.zero);
            t.localRotation = Quaternion.Euler(0, 180, 0);
            if (!t.TryGetComponent(out MeshFilter filter))
                filter = t.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh =
                EditorAssetUtility.LoadOrCreate("Assets/Art/Generated/InkStream.asset", BakeStream);
            if (!t.TryGetComponent(out MeshRenderer renderer))
                renderer = t.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialFor("Chorro", "DriftPaint/Chorro viscoso");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }

        static Mesh BakeStream()
        {
            const int sides = 9, rings = 6;
            var vertices = new Vector3[sides * rings];
            var triangles = new int[sides * (rings - 1) * 6];
            for (int row = 0; row < rings; row++)
            {
                float d = row / (rings - 1f);
                for (int j = 0; j < sides; j++)
                {
                    float a = j * 2 * Mathf.PI / sides;
                    float radius = (.085f + .074f * d) * (1 + .12f * Mathf.Sin(a * 3 + d * 11));
                    int p = row * sides + j, next = row * sides + (j + 1) % sides;
                    vertices[p] = new Vector3(Mathf.Cos(a) * radius,
                                              Mathf.Sin(a) * radius * (1 - .65f * d) - .30f * d * d, 1.38f * d);
                    if (row == rings - 1)
                        continue;
                    int k = p * 6;
                    triangles[k] = p;
                    triangles[k + 1] = next;
                    triangles[k + 2] = p + sides;
                    triangles[k + 3] = p + sides;
                    triangles[k + 4] = next;
                    triangles[k + 5] = next + sides;
                }
            }
            var mesh = new Mesh { name = "Chorro de escape", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Transform Child(Transform parent, string name, Vector3 pos)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(parent, false);
            }
            t.localPosition = pos;
            return t;
        }

        static ParticleSystem Drops(Transform parent, string name, Vector3 pos, float rate, int cap)
        {
            Transform t = Child(parent, name, pos);
            if (!t.TryGetComponent(out ParticleSystem ps))
                ps = t.gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.maxParticles = cap;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.12f, .29f);
            main.startSize = new ParticleSystem.MinMaxCurve(.13f, .34f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.8f, 5.5f);
            main.gravityModifier = .65f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10;
            shape.radius = .075f;
            t.localRotation = Quaternion.Euler(14, 180, 0);
            var emission = ps.emission;
            emission.rateOverTime = rate;
            emission.enabled = false;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = .105f;
            renderer.lengthScale = 1.35f;
            renderer.sharedMaterial = MaterialFor("Gotas", "DriftPaint/Tinta en movimiento");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        static TrailRenderer Trail(Transform parent, string name, Vector3 pos, float time, float width)
        {
            Transform t = Child(parent, name, pos);
            if (!t.TryGetComponent(out TrailRenderer trail))
                trail = t.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = MaterialFor("Estelas", "Sprites/Default");
            trail.time = time;
            trail.startWidth = width;
            trail.endWidth = width * .30f;
            trail.minVertexDistance = .20f;
            trail.emitting = name == "Estela";
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            if (name != "Estela")
            {
                trail.alignment = LineAlignment.TransformZ;
                t.localRotation = Quaternion.Euler(90, 0, 0);
            }
            return trail;
        }

        const int CrownSegments = 96;

        static Mesh Crown()
        {
            const int count = CrownSegments;
            var points = new Vector3[count * 3];
            var triangles = new int[count * 12];
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2 / count;
                float wave = Mathf.Sin(a * 7 + .75f * Mathf.Sin(4 * a));
                float spike = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.20f, .92f, wave));
                spike *= .74f + .26f * Mathf.Sin(a * 3);
                Vector3 radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                points[i * 3] = radial * .08f;
                points[i * 3 + 1] = radial * (.27f + .04f * Mathf.Sin(5 * a));
                points[i * 3 + 2] = radial * (.35f + .15f * spike) + Vector3.up * (.1f + .95f * spike);
                for (int ring = 0; ring < 2; ring++)
                {
                    int p = i * 3 + ring, n = ((i + 1) % count) * 3 + ring, j = i * 12 + ring * 6;
                    triangles[j] = p;
                    triangles[j + 1] = n;
                    triangles[j + 2] = p + 1;
                    triangles[j + 3] = p + 1;
                    triangles[j + 4] = n;
                    triangles[j + 5] = n + 1;
                }
            }
            var mesh = new Mesh { name = "Corona de pintura", vertices = points, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
