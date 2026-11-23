using System.Collections.Generic;
using DriftPaint.Cars;
using DriftPaint.Teams;
using UnityEditor;
using UnityEngine;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Arma los prefabs de autos a partir de los modelos modulares (carrocería, llantas y alerón).
    /// Las piezas vienen sin ubicar entre sí, así que el builder las acomoda usando los bounds
    /// de la carrocería: orienta el largo sobre Z, apoya la carrocería en el piso, crea un pivote
    /// por rueda (X = eje) y ubica el alerón atrás. Todo queda editable en el prefab.
    /// </summary>
    public static class CarPrefabBuilder
    {
        public const string PrefabFolder = "Assets/Prefabs/Cars";
        const string ModelsRoot = "Assets/3D_Models/Cars/";

        /// <summary>Piezas y ajustes de ubicación de un modelo.</summary>
        public class CarModelSpec
        {
            public string Name;
            public string Body;
            public string RimLeft;
            public string RimRight;
            public string Spoiler;
            /// <summary>Posición de cada eje como fracción del largo, medida desde el centro.</summary>
            public float AxleFraction = 0.31f;
            /// <summary>Metros que la rueda entra hacia adentro desde el borde de la carrocería.</summary>
            public float WheelInset = 0.04f;
            /// <summary>Altura del fondo de la carrocería sobre el piso.</summary>
            public float BodyClearance = 0.05f;
            /// <summary>Invierte el frente si la detección automática lo deja al revés.</summary>
            public bool FlipForward;
        }

        public static readonly CarModelSpec[] Models =
        {
            new CarModelSpec
            {
                Name = "CC96",
                Body = ModelsRoot + "CC96/Body/CC96_Body_1.fbx",
                RimLeft = ModelsRoot + "CC96/Rims/Rims1_L.fbx",
                RimRight = ModelsRoot + "CC96/Rims/Rims1_R.fbx",
                Spoiler = ModelsRoot + "CC96/Spoiler/CC96_Spoiler.fbx"
            },
            new CarModelSpec
            {
                Name = "Gc14",
                Body = ModelsRoot + "Gc14_Coupe/Body/Gc14_Body_1.fbx",
                RimLeft = ModelsRoot + "Gc14_Coupe/Rims/Gc14_Rims_1_L.fbx",
                RimRight = ModelsRoot + "Gc14_Coupe/Rims/Gc14_Rims_1_R.fbx",
                Spoiler = ModelsRoot + "Gc14_Coupe/Spoiler/Gc14_Spoiler.fbx"
            },
            new CarModelSpec
            {
                Name = "A_R7",
                Body = ModelsRoot + "A_R7/Body/A_R7_Body_1.fbx",
                RimLeft = ModelsRoot + "A_R7/Rims/Rims_2_L.fbx",
                RimRight = ModelsRoot + "A_R7/Rims/Rims_1_R.fbx",
                Spoiler = ModelsRoot + "A_R7/Spoilers/A_R7_Spoiler_1fbx.fbx"
            }
        };

        public static List<GameObject> BuildAll(CarDataSO carData, TeamPaletteSO palette)
        {
            EditorAssetUtility.EnsureFolder(PrefabFolder);
            var prefabs = new List<GameObject>();
            foreach (CarModelSpec spec in Models)
            {
                GameObject prefab = Build(spec, carData, palette);
                if (prefab != null) prefabs.Add(prefab);
            }
            return prefabs;
        }

        public static GameObject Build(CarModelSpec spec, CarDataSO carData, TeamPaletteSO palette)
        {
            var bodyAsset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.Body);
            var rimLeftAsset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.RimLeft);
            var rimRightAsset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.RimRight);
            var spoilerAsset = string.IsNullOrEmpty(spec.Spoiler) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(spec.Spoiler);

            if (bodyAsset == null || rimLeftAsset == null || rimRightAsset == null)
            {
                Debug.LogError($"CarPrefabBuilder: faltan piezas del modelo {spec.Name}.");
                return null;
            }

            var root = new GameObject("Car_" + spec.Name);
            try
            {
                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);

                // Carrocería: largo sobre Z, frente hacia +Z, centrada y apoyada en el piso.
                var model = new GameObject("Model").transform;
                model.SetParent(visual, false);
                var body = (GameObject)PrefabUtility.InstantiatePrefab(bodyAsset, model);
                body.name = "Body";
                OrientBody(body.transform, spec.FlipForward);

                Bounds bodyBounds = EditorAssetUtility.GetRendererBounds(body);
                body.transform.position -= new Vector3(bodyBounds.center.x, bodyBounds.min.y - spec.BodyClearance, bodyBounds.center.z);
                bodyBounds = EditorAssetUtility.GetRendererBounds(body);

                float length = bodyBounds.size.z;
                float width = bodyBounds.size.x;
                float height = bodyBounds.size.y;

                // Ruedas: un pivote por rueda con X como eje de giro.
                var wheelsRoot = new GameObject("Wheels").transform;
                wheelsRoot.SetParent(visual, false);

                Bounds rimBounds = MeasureAlignedRim(rimLeftAsset);
                float wheelRadius = Mathf.Max(rimBounds.extents.y, rimBounds.extents.z);
                float wheelHalfWidth = rimBounds.extents.x;
                float axleZ = length * spec.AxleFraction;
                float wheelX = width * 0.5f - wheelHalfWidth - spec.WheelInset;

                Transform fl = CreateWheel(wheelsRoot, "Wheel_FL", rimLeftAsset, new Vector3(-wheelX, wheelRadius, axleZ));
                Transform fr = CreateWheel(wheelsRoot, "Wheel_FR", rimRightAsset, new Vector3(wheelX, wheelRadius, axleZ));
                Transform rl = CreateWheel(wheelsRoot, "Wheel_RL", rimLeftAsset, new Vector3(-wheelX, wheelRadius, -axleZ));
                Transform rr = CreateWheel(wheelsRoot, "Wheel_RR", rimRightAsset, new Vector3(wheelX, wheelRadius, -axleZ));

                if (spoilerAsset != null)
                    PlaceSpoiler(model, spoilerAsset, bodyBounds);

                // Puntos de salida de la pintura (caños de escape).
                CreateMarker(root.transform, "Exhaust_L", new Vector3(-width * 0.22f, 0.3f, bodyBounds.min.z));
                CreateMarker(root.transform, "Exhaust_R", new Vector3(width * 0.22f, 0.3f, bodyBounds.min.z));

                // Física: el auto lo sostiene la suspensión de las ruedas. La caja de la carrocería
                // arranca por encima del piso para que su canto no choque al entrar a una rampa.
                var rb = root.AddComponent<Rigidbody>();
                rb.mass = 1200f;

                var box = root.AddComponent<BoxCollider>();
                float boxBottom = wheelRadius * 0.85f;
                float boxTop = Mathf.Max(boxBottom + 0.3f, bodyBounds.max.y * 0.85f);
                box.size = new Vector3(width * 0.95f, boxTop - boxBottom, length * 0.92f);
                box.center = new Vector3(0f, (boxTop + boxBottom) * 0.5f, 0f);

                root.AddComponent<CarMovementInput>();
                root.AddComponent<CarAbilityInput>();
                var ground = root.AddComponent<CarGroundSensor>();
                root.AddComponent<CarStatusEffects>();
                root.AddComponent<CarMotorLocomotion>();
                var controller = root.AddComponent<CarController>();
                var visuals = root.AddComponent<CarVisuals>();
                root.AddComponent<CarBlinkEffect>();
                root.AddComponent<CarFlipRecovery>();
                var teamColor = root.AddComponent<CarTeamColor>();

                var controllerSo = new SerializedObject(controller);
                controllerSo.FindProperty("_carData").objectReferenceValue = carData;
                controllerSo.ApplyModifiedPropertiesWithoutUndo();

                var teamColorSo = new SerializedObject(teamColor);
                teamColorSo.FindProperty("_palette").objectReferenceValue = palette;
                teamColorSo.ApplyModifiedPropertiesWithoutUndo();

                var groundSo = new SerializedObject(ground);
                SerializedProperty probes = groundSo.FindProperty("_probes");
                probes.arraySize = 4;
                probes.GetArrayElementAtIndex(0).objectReferenceValue = fl;
                probes.GetArrayElementAtIndex(1).objectReferenceValue = fr;
                probes.GetArrayElementAtIndex(2).objectReferenceValue = rl;
                probes.GetArrayElementAtIndex(3).objectReferenceValue = rr;
                groundSo.FindProperty("_wheelRadius").floatValue = wheelRadius;
                groundSo.ApplyModifiedPropertiesWithoutUndo();

                var visualsSo = new SerializedObject(visuals);
                visualsSo.FindProperty("_wheelFrontLeft").objectReferenceValue = fl;
                visualsSo.FindProperty("_wheelFrontRight").objectReferenceValue = fr;
                visualsSo.FindProperty("_wheelRearLeft").objectReferenceValue = rl;
                visualsSo.FindProperty("_wheelRearRight").objectReferenceValue = rr;
                visualsSo.FindProperty("_wheelRadius").floatValue = wheelRadius;
                visualsSo.ApplyModifiedPropertiesWithoutUndo();

                StripColliders(visual);

                string path = $"{PrefabFolder}/Car_{spec.Name}.prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"CarPrefabBuilder: {path} (largo {length:0.00} m, ancho {width:0.00} m, radio de rueda {wheelRadius:0.00} m).");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Gira la carrocería para que el largo quede sobre Z y el frente hacia +Z.
        /// El frente se estima por la cabina: el techo suele estar detrás del centro del auto.
        /// </summary>
        static void OrientBody(Transform body, bool flip)
        {
            Bounds bounds = EditorAssetUtility.GetRendererBounds(body.gameObject);
            if (bounds.size.x > bounds.size.z)
                body.rotation = Quaternion.Euler(0f, 90f, 0f) * body.rotation;

            bounds = EditorAssetUtility.GetRendererBounds(body.gameObject);
            float roofZ = AverageZOfTopVertices(body.gameObject, bounds, 0.2f);
            bool roofIsBehindCenter = roofZ < bounds.center.z;

            bool rotate = !roofIsBehindCenter;
            if (flip) rotate = !rotate;
            if (rotate)
                body.rotation = Quaternion.Euler(0f, 180f, 0f) * body.rotation;
        }

        static float AverageZOfTopVertices(GameObject go, Bounds bounds, float topFraction)
        {
            float threshold = bounds.max.y - bounds.size.y * topFraction;
            double sum = 0;
            int count = 0;
            foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;
                Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector3 world = matrix.MultiplyPoint3x4(vertex);
                    if (world.y < threshold) continue;
                    sum += world.z;
                    count++;
                }
            }
            return count > 0 ? (float)(sum / count) : bounds.center.z;
        }

        /// <summary>Bounds de la llanta ya alineada (X = eje) y centrada.</summary>
        static Bounds MeasureAlignedRim(GameObject rimAsset)
        {
            var temp = new GameObject("RimProbe");
            try
            {
                var rim = (GameObject)PrefabUtility.InstantiatePrefab(rimAsset, temp.transform);
                AlignRim(rim.transform);
                return EditorAssetUtility.GetRendererBounds(rim);
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        static Transform CreateWheel(Transform parent, string wheelName, GameObject rimAsset, Vector3 localPosition)
        {
            var pivot = new GameObject(wheelName).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = localPosition;

            var rim = (GameObject)PrefabUtility.InstantiatePrefab(rimAsset, pivot);
            rim.name = "Rim";
            AlignRim(rim.transform);
            return pivot;
        }

        /// <summary>
        /// Rota la llanta para que su lado más angosto (el eje) quede sobre X del pivote
        /// y la centra en el pivote.
        /// </summary>
        static void AlignRim(Transform rim)
        {
            Transform pivot = rim.parent;
            rim.localPosition = Vector3.zero;

            Bounds b = BoundsInSpace(rim.gameObject, pivot);
            Vector3 size = b.size;
            if (size.y < size.x && size.y <= size.z)
                rim.localRotation = Quaternion.Euler(0f, 0f, 90f) * rim.localRotation;
            else if (size.z < size.x && size.z < size.y)
                rim.localRotation = Quaternion.Euler(0f, 90f, 0f) * rim.localRotation;

            b = BoundsInSpace(rim.gameObject, pivot);
            rim.localPosition -= b.center;
        }

        static Bounds BoundsInSpace(GameObject go, Transform space)
        {
            Bounds world = EditorAssetUtility.GetRendererBounds(go);
            if (space == null) return world;
            Vector3 center = space.InverseTransformPoint(world.center);
            Vector3 size = space.InverseTransformVector(world.size);
            return new Bounds(center, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)));
        }

        static void PlaceSpoiler(Transform model, GameObject spoilerAsset, Bounds bodyBounds)
        {
            var spoiler = (GameObject)PrefabUtility.InstantiatePrefab(spoilerAsset, model);
            spoiler.name = "Spoiler";

            Bounds b = EditorAssetUtility.GetRendererBounds(spoiler);
            if (b.size.z > b.size.x)
                spoiler.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * spoiler.transform.rotation;

            b = EditorAssetUtility.GetRendererBounds(spoiler);
            Vector3 target = new Vector3(
                bodyBounds.center.x,
                bodyBounds.min.y + bodyBounds.size.y * 0.72f,
                bodyBounds.min.z + Mathf.Max(0.15f, b.size.z * 0.5f));
            spoiler.transform.position += new Vector3(target.x - b.center.x, target.y - b.min.y, target.z - b.center.z);
        }

        static void CreateMarker(Transform parent, string markerName, Vector3 localPosition)
        {
            var marker = new GameObject(markerName).transform;
            marker.SetParent(parent, false);
            marker.localPosition = localPosition;
        }

        static void StripColliders(Transform root)
        {
            foreach (Collider col in root.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(col);
        }
    }
}
