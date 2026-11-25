using System.Collections.Generic;
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
                    if (!light.TryGetComponent<ArenaLighting>(out var lighting))
                        lighting = light.gameObject.AddComponent<ArenaLighting>();
                    lighting.Apply();
                }
                if (root.TryGetComponent<Paint.PaintSurfaceView>(out var paint))
                    ConnectPaint(scene, paint);
                var projectile = root.GetComponentInChildren<ProjectileView>(true);
                if (projectile != null)
                {
                    projectile.transform.localScale = new Vector3(.40f, .40f, .86f);
                    var wake = Child(projectile.transform, "Estela", Vector3.zero);
                    if (wake.TryGetComponent(out TrailRenderer ribbon)) Object.DestroyImmediate(ribbon);
                    var renderer = InkMesh(wake, "Tubo de proyectil", ProjectileTube, "Cola de tinta", "DriftPaint/Estela ondulada");
                    renderer.enabled = false;
                    MenuSceneBuilder.Bind(projectile, ("_wake", renderer));
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
                surface.receiveShadows = true;
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
                                  ("_rightStream", Stream(right)), ("_contactSplash", ContactSplash(root.transform)));
        }

        static MeshRenderer InkMesh(Transform target, string meshName, System.Func<Mesh> bake, string material, string shader)
        {
            if (!target.TryGetComponent(out MeshFilter filter)) filter = target.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = EditorAssetUtility.LoadOrCreate("Assets/Art/Generated/" + meshName + ".asset", bake);
            if (!target.TryGetComponent(out MeshRenderer renderer)) renderer = target.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialFor(material, shader);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        static Mesh ProjectileTube()
        {
            const int sides = 8, rings = 33;
            var vertices = new Vector3[sides * rings];
            var triangles = new int[6 * sides * (rings - 1)];
            for (int ring = 0; ring < rings; ring++)
                for (int side = 0; side < sides; side++)
                {
                    float angle = 2f * Mathf.PI * side / sides;
                    int i = ring * sides + side;
                    vertices[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), ring / (rings - 1f));
                    if (ring == rings - 1) continue;
                    int next = ring * sides + (side + 1) % sides;
                    // El eje de la malla avanza desde la cabeza hacia la cola.
                    int k = i * 6;
                    triangles[k] = i; triangles[k + 1] = i + sides; triangles[k + 2] = next;
                    triangles[k + 3] = next; triangles[k + 4] = i + sides; triangles[k + 5] = next + sides;
                }
            return new Mesh { name = "Tubo de tinta", vertices = vertices, triangles = triangles };
        }

        static ParticleSystem ContactSplash(Transform car)
        {
            // Un solo emisor por auto conserva las huellas de los dos escapes en sus puntos de contacto.
            var ps = Drops(car, "Tinta que se asienta", Vector3.zero, 0, 24);
            ps.transform.localRotation = Quaternion.identity;
            var main = ps.main;
            main.startSize = 1f;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.startRotation3D = true;
            main.startLifetime = .34f;
            var shape = ps.shape;
            shape.enabled = false;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = true;
            var spread = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, .26f, 1f, 1.7f));
            size.x = spread;
            size.z = spread;
            size.y = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .3f),
                new Keyframe(.19f, 1f), new Keyframe(.76f, .07f), new Keyframe(1f, .015f)));
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(.92f, 0), new GradientAlphaKey(.85f, .47f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.mesh = EditorAssetUtility.LoadOrCreate("Assets/Art/Generated/Contacto de escape.asset", Deposit);
            renderer.sharedMaterial = MaterialFor("Tinta fresca", "DriftPaint/Depósito de pintura");
            return ps;
        }

        static Mesh Deposit()
        {
            const int sides = 22;
            var vertices = new Vector3[1 + sides * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[sides * 9];
            uv[0] = Vector2.one * .5f;
            for (int i = 0; i < sides; i++)
            {
                float a = i * 2f * Mathf.PI / sides;
                Vector2 radial = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float lobe = .5f + .5f * Mathf.Sin(5f * a + Mathf.Cos(3f * a));
                int lip = 1 + i, edge = lip + sides, next = 1 + (i + 1) % sides;
                vertices[lip] = new Vector3(radial.x * .31f, .06f + .18f * lobe, radial.y * .31f);
                vertices[edge] = new Vector3(radial.x, 0, radial.y) * (.40f + .12f * lobe);
                uv[lip] = uv[0] + radial * .34f;
                uv[edge] = uv[0] + radial * .50f;
                int k = i * 9;
                triangles[k] = 0; triangles[k + 1] = next; triangles[k + 2] = lip;
                triangles[k + 3] = lip; triangles[k + 4] = next; triangles[k + 5] = edge;
                triangles[k + 6] = edge; triangles[k + 7] = next; triangles[k + 8] = next + sides;
            }
            var mesh = new Mesh { name = "Depósito húmedo", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            return mesh;
        }

        static ParticleSystem ExhaustDrops(Transform exhaust)
        {
            // Además de la salida del escape, el mismo sistema recibe las gotas que saltan del golpe contra el piso.
            var drops = Drops(exhaust, JetDropsName, Vector3.zero, 30, JetDropCap);
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
            Transform t = Child(exhaust, StreamName, Vector3.zero);
            t.localRotation = Quaternion.Euler(0, 180, 0);
            if (!t.TryGetComponent(out MeshFilter filter))
                filter = t.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = StreamMesh();
            if (!t.TryGetComponent(out MeshRenderer renderer))
                renderer = t.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialFor("ChorroEscape", ExhaustJetShader);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }

        // El shader arma la forma del chorro sobre esta malla; StreamSides tiene que coincidir con SIDES del shader.
        const string StreamMeshPath = "Assets/Art/Generated/InkStream.asset";
        const string StreamName = "Tinta continua";
        const string JetDropsName = "Chorro";
        const string ExhaustJetShader = "DriftPaint/Chorro de escape";
        const int StreamSides = 12, StreamRings = 33, JetDropCap = 24;
        static readonly string[] Exhausts = { "Exhaust_L", "Exhaust_R" };

        static Mesh StreamMesh()
        {
            var mesh = EditorAssetUtility.LoadOrCreate(StreamMeshPath, BakeStream);
            if (!StreamMeshOutdated(mesh))
                return mesh;
            var updated = BakeStream();
            EditorUtility.CopySerialized(updated, mesh);
            Object.DestroyImmediate(updated);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static bool StreamMeshOutdated(Mesh mesh) => mesh.vertexCount != (StreamSides + 1) * StreamRings;

        /// <summary>
        /// Tubo de radio 1 y largo 1 con muchos anillos, para que el shader pueda doblarlo hasta el piso y
        /// dibujar los dientes del borde. La costura repite la primera columna para que la vuelta no salte.
        /// </summary>
        static Mesh BakeStream()
        {
            const int columns = StreamSides + 1;
            var vertices = new Vector3[columns * StreamRings];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[StreamSides * (StreamRings - 1) * 6];
            for (int row = 0; row < StreamRings; row++)
            {
                float d = row / (StreamRings - 1f);
                for (int j = 0; j < columns; j++)
                {
                    float a = j * 2 * Mathf.PI / StreamSides;
                    int p = row * columns + j;
                    normals[p] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    vertices[p] = normals[p] + Vector3.forward * d;
                    uv[p] = new Vector2(j / (float)StreamSides, d);
                    if (row == StreamRings - 1 || j == StreamSides)
                        continue;
                    int k = (row * StreamSides + j) * 6;
                    triangles[k] = p;
                    triangles[k + 1] = p + 1;
                    triangles[k + 2] = p + columns;
                    triangles[k + 3] = p + columns;
                    triangles[k + 4] = p + 1;
                    triangles[k + 5] = p + columns + 1;
                }
            }
            var mesh = new Mesh { name = "Chorro de escape", vertices = vertices, normals = normals, uv = uv, triangles = triangles };
            // La forma final la decide el shader: los límites cubren el chorro más largo y una caída hasta un piso en rampa.
            mesh.bounds = new Bounds(new Vector3(0f, -.4f, .75f), new Vector3(2f, 1.6f, 1.9f));
            return mesh;
        }

        static IEnumerable<string> CarPrefabPaths() =>
            CarPrefabBuilder.Models.Select(spec => CarPrefabBuilder.PrefabFolder + "/Car_" + spec.Name + ".prefab");

        /// <summary>Algún auto todavía usa la malla o el material anteriores del chorro.</summary>
        internal static bool ExhaustStreamsOutdated()
        {
            if (Shader.Find(ExhaustJetShader) == null)
                return false;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(StreamMeshPath);
            if (mesh == null || StreamMeshOutdated(mesh))
                return true;
            foreach (string path in CarPrefabPaths())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent<CarPaintEffects>() == null)
                    continue;
                foreach (string exhaust in Exhausts)
                {
                    Transform outlet = prefab.transform.Find(exhaust);
                    Transform stream = outlet != null ? outlet.Find(StreamName) : null;
                    if (stream != null && stream.TryGetComponent(out MeshRenderer renderer) &&
                        (renderer.sharedMaterial == null || renderer.sharedMaterial.shader.name != ExhaustJetShader))
                        return true;
                    Transform jet = outlet != null ? outlet.Find(JetDropsName) : null;
                    if (jet != null && jet.TryGetComponent(out ParticleSystem drops) && drops.main.maxParticles < JetDropCap)
                        return true;
                }
            }
            return false;
        }

        /// <summary>Pasa los chorros de los autos a la malla y el material nuevos sin tocar el resto del prefab.</summary>
        internal static void UpdateExhaustStreams()
        {
            if (Shader.Find(ExhaustJetShader) == null)
                return;
            StreamMesh();
            foreach (string path in CarPrefabPaths())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent<CarPaintEffects>() == null)
                    continue;
                using (var contents = new PrefabUtility.EditPrefabContentsScope(path))
                    foreach (string exhaust in Exhausts)
                    {
                        Transform outlet = contents.prefabContentsRoot.transform.Find(exhaust);
                        if (outlet == null)
                            continue;
                        if (outlet.Find(StreamName) != null)
                            Stream(outlet);
                        Transform jet = outlet.Find(JetDropsName);
                        if (jet != null && jet.TryGetComponent(out ParticleSystem drops))
                        {
                            var main = drops.main;
                            main.maxParticles = JetDropCap;
                            var emission = drops.emission;
                            emission.rateOverTime = 30;
                        }
                    }
            }
            AssetDatabase.SaveAssets();
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
