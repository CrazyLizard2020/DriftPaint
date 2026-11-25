#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using DriftPaint.Cars;
using DriftPaint.LiveOperations;
using UnityEditor;
using UnityEngine;

namespace DriftPaint.Tests
{
    /// <summary>
    /// Banco de pruebas físico reproducible. Arma pista, rampas y caídas con geometría propia, maneja un
    /// prefab real con input fijo y mide el resultado con una geometría independiente del sensor del auto.
    /// </summary>
    public sealed class VehicleLab : IDisposable
    {
        public static readonly string[] Models = { "Car_AE86", "Car_A_R7", "Car_CC96", "Car_Gc14", "Car_RX7" };

        /// <summary>Altura de la rueda sobre el piso (m) por debajo de la cual se cuenta contacto al medir.</summary>
        public const float ContactClearance = 0.07f;

        /// <summary>Separación entre escenarios para que no interactúen.</summary>
        static readonly Vector3 Origin = new Vector3(0f, 500f, 0f);

        readonly GameObject _root;
        readonly float _previousScale, _previousMaximum, _previousFixed;
        CarController _car;

        public CarController Car => _car;
        public Transform Root => _root.transform;

        public VehicleLab(float fixedDeltaTime = 0f)
        {
            LiveOps.EnterRoom(Environment.GetEnvironmentVariable("DRIFTPAINT_LAB_CFG") ?? "{}");
            _previousScale = Time.timeScale;
            _previousMaximum = Time.maximumDeltaTime;
            _previousFixed = Time.fixedDeltaTime;
            if (fixedDeltaTime > 0f) Time.fixedDeltaTime = fixedDeltaTime;
            Time.maximumDeltaTime = 0.5f;
            Time.timeScale = 12f;
            _root = new GameObject("VehicleLab");
            _root.transform.position = Origin;
        }

        public void Dispose()
        {
            if (_car != null) UnityEngine.Object.Destroy(_car.gameObject);
            UnityEngine.Object.Destroy(_root);
            Time.timeScale = _previousScale;
            Time.maximumDeltaTime = _previousMaximum;
            Time.fixedDeltaTime = _previousFixed;
            LiveOps.LeaveRoom();
        }

        #region Geometría

        public void Clear()
        {
            foreach (Transform child in _root.transform) UnityEngine.Object.Destroy(child.gameObject);
        }

        public GameObject Box(string name, Vector3 local, Vector3 size, Quaternion rotation)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(_root.transform, false);
            box.transform.localPosition = local;
            box.transform.localRotation = rotation;
            box.transform.localScale = size;
            return box;
        }

        public void Floor() => Box("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(800f, 1f, 1600f), Quaternion.identity);

        /// <summary>Rampa con el extremo bajo a ras del piso, igual que las de las arenas.</summary>
        public void Ramp(Vector3 lowEnd, float yaw, float angle, float width, float length)
        {
            const float thickness = 0.6f;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-angle, 0f, 0f);
            Vector3 center = lowEnd + rotation * Vector3.forward * (length * 0.5f) - rotation * Vector3.up * (thickness * 0.5f);
            Box("Ramp", center, new Vector3(width, thickness, length), rotation);
        }

        #endregion

        #region Auto

        public CarController Spawn(string model, Vector3 localPosition, Quaternion rotation)
        {
            if (_car != null) UnityEngine.Object.Destroy(_car.gameObject);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Cars/{model}.prefab");
            GameObject instance = UnityEngine.Object.Instantiate(prefab, Origin + localPosition, rotation);
            instance.name = model;
            _car = instance.GetComponent<CarController>();
            _car.MovementInput.SetInputType(CarInputType.Bot);
            return _car;
        }

        public void Drive(float throttle, float steer = 0f, bool drift = false) =>
            _car.MovementInput.SetExternalInput(new FrameInput { Throttle = throttle, Steer = steer, Drift = drift });

        public void Launch(Vector3 velocity, Vector3 angular = default)
        {
            _car.Body.linearVelocity = velocity;
            _car.Body.angularVelocity = angular;
        }

        public Vector3 LocalPosition => _car.Body.position - Origin;
        public Vector3 WorldOf(Vector3 local) => Origin + local;

        /// <summary>Altura libre de cada rueda sobre el plano dado (0 = rueda apoyada en reposo).</summary>
        public float WheelClearance(int wheel, Vector3 planePoint, Vector3 normal)
        {
            Transform probe = _car.GroundSensor.Probes[wheel];
            // Los pivotes siguen al Transform, que puede estar interpolado: se reconstruye desde el cuerpo.
            Vector3 local = _car.transform.InverseTransformPoint(probe.position);
            Vector3 world = _car.Body.position + _car.Body.rotation * local;
            return Vector3.Dot(world - planePoint, normal) - _car.GroundSensor.WheelRadius;
        }

        public float MinClearance(Vector3 planePoint, Vector3 normal)
        {
            float min = float.MaxValue;
            for (int i = 0; i < 4; i++) min = Mathf.Min(min, WheelClearance(i, planePoint, normal));
            return min;
        }

        public int WheelsDown(Vector3 planePoint, Vector3 normal, float clearance = 0.12f)
        {
            int count = 0;
            for (int i = 0; i < 4; i++) if (WheelClearance(i, planePoint, normal) <= clearance) count++;
            return count;
        }

        /// <summary>Mezcla de manejo efectiva (0 a 1).</summary>
        public float DriveBlend() => _car.Stability.Frame.DriveBlend;

        public string StateName() => _car.Stability.State.ToString();

        public static readonly WaitForFixedUpdate Step = new WaitForFixedUpdate();

        public IEnumerator Settle(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.fixedDeltaTime) yield return Step;
        }

        #endregion

        #region Aterrizajes

        /// <summary>Resultado de un aterrizaje medido contra el plano de caída.</summary>
        public sealed class Landing
        {
            public string Model, Case;
            public bool Contacted, Normal;
            public float SpeedPre, VnPre, VnPost, VtPre, VtPost, Rebound, TimeToDrive, AirAfterContact, MaxTilt, FinalTilt, MaxRate, TakeoffSpeed, FlightTime, FlightRange, Apex;
            public int Teleports;
            public float TimeToUpright;

            public float Restitution => VnPre < -0.1f ? Mathf.Max(0f, VnPost) / -VnPre : 0f;
            public float Retention => VtPre > 1f ? VtPost / VtPre : 1f;
        }

        public const string Header = "model;case;normal;contacted;flight_s;range_m;apex_m;speed_pre;vn_pre;vn_post;restitution;vt_retention;rebound_m;time_to_drive_s;air_after_contact_s;max_tilt_deg;final_tilt_deg;max_tilt_rate;time_upright_s;teleports";

        public static string Row(Landing l)
        {
            string F(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);
            return string.Join(";", l.Model, l.Case, l.Normal ? "1" : "0", l.Contacted ? "1" : "0", F(l.FlightTime), F(l.FlightRange), F(l.Apex),
                F(l.SpeedPre), F(l.VnPre), F(l.VnPost), F(l.Restitution), F(l.Retention), F(l.Rebound), F(l.TimeToDrive),
                F(l.AirAfterContact), F(l.MaxTilt), F(l.FinalTilt), F(l.MaxRate), F(l.TimeToUpright), l.Teleports.ToString());
        }

        /// <summary>
        /// Mide desde el vuelo hasta 2,5 s después del primer contacto con el plano. El auto ya tiene que
        /// estar en el aire o por salir de la rampa; hasBeenAirborne indica si ya despegó.
        /// </summary>
        public IEnumerator Measure(Landing result, Vector3 planeLocal, Vector3 normal, Func<bool> armed, float timeout = 8f, float window = 2.5f, StringBuilder trace = null)
        {
            Vector3 point = Origin + planeLocal;
            Rigidbody body = _car.Body;
            int teleports = 0;
            void Count(Vector3 p, Quaternion r) => teleports++;
            _car.Teleported += Count;

            bool airborne = false, contacted = false;
            Vector3 takeoff = default, previousVelocity = body.linearVelocity;
            float contactTime = 0f, time = 0f, takeoffTime = 0f, apex = 0f, sinceOut = 0f, groundedFor = 0f;
            float comRest = float.NaN, minHeight = float.MaxValue, peakAfterMin = 0f, vnPost = float.MinValue;
            bool turned = false, tangentialTaken = false, drivable = false;
            float uprightSince = -1f;
            result.TimeToDrive = -1f;
            result.TimeToUpright = -1f;

            while (time < timeout)
            {
                yield return Step;
                float dt = Time.fixedDeltaTime;
                time += dt;
                Vector3 velocity = body.linearVelocity;
                float clearance = MinClearance(point, normal);
                // Altura del centro de masa sobre el plano respecto de la que tiene en reposo.
                float height = Vector3.Dot(body.worldCenterOfMass - point, normal);
                if (float.IsNaN(comRest)) comRest = Vector3.Dot(body.rotation * body.centerOfMass, body.rotation * Vector3.up);
                height -= comRest;
                float tilt = Vector3.Angle(body.rotation * Vector3.up, normal);
                trace?.AppendLine(string.Join(";", result.Model, result.Case, time.ToString("0.000", CultureInfo.InvariantCulture), StateName(),
                    LocalPosition.y.ToString("0.000", CultureInfo.InvariantCulture), LocalPosition.z.ToString("0.00", CultureInfo.InvariantCulture),
                    velocity.y.ToString("0.00", CultureInfo.InvariantCulture), velocity.magnitude.ToString("0.00", CultureInfo.InvariantCulture),
                    tilt.ToString("0.0", CultureInfo.InvariantCulture), clearance.ToString("0.000", CultureInfo.InvariantCulture), DriveBlend().ToString("0.00", CultureInfo.InvariantCulture)));

                if (!airborne)
                {
                    if (armed() && clearance > 0.2f)
                    {
                        airborne = true;
                        takeoff = body.position;
                        takeoffTime = time;
                        result.TakeoffSpeed = velocity.magnitude;
                        // La reserva de plataforma arranca recién en vuelo.
                    }
                    previousVelocity = velocity;
                    continue;
                }

                if (!contacted)
                {
                    apex = Mathf.Max(apex, height);
                    Collider hull = _car.GetComponent<Collider>();
                    float hullClearance = Vector3.Dot(hull.ClosestPoint(body.worldCenterOfMass - normal * 10f) - point, normal);
                    if (clearance <= ContactClearance || hullClearance <= 0.02f)
                    {
                        contacted = true;
                        contactTime = time;
                        result.Contacted = true;
                        result.FlightTime = time - takeoffTime;
                        result.FlightRange = Vector3.ProjectOnPlane(body.position - takeoff, Vector3.up).magnitude;
                        result.Apex = apex;
                        result.SpeedPre = previousVelocity.magnitude;
                        result.VnPre = Vector3.Dot(previousVelocity, normal);
                        result.VtPre = Vector3.ProjectOnPlane(previousVelocity, normal).magnitude;
                    }
                    previousVelocity = velocity;
                    if (!contacted) continue;
                }

                float since = time - contactTime;
                float vn = Vector3.Dot(velocity, normal);
                if (since <= 0.35f) vnPost = Mathf.Max(vnPost, vn);
                if (!turned)
                {
                    if (height < minHeight) minHeight = height;
                    if (vn > -0.05f) turned = true;
                }
                else peakAfterMin = Mathf.Max(peakAfterMin, since < 1.6f ? height : peakAfterMin);

                if (!tangentialTaken && (turned || since > 0.25f))
                {
                    tangentialTaken = true;
                    result.VtPost = Vector3.ProjectOnPlane(velocity, normal).magnitude;
                }

                if (since < 1.6f && WheelsDown(point, normal) == 0) sinceOut += dt;
                result.MaxTilt = Mathf.Max(result.MaxTilt, tilt);
                Vector3 omega = body.angularVelocity;
                result.MaxRate = Mathf.Max(result.MaxRate, Vector3.ProjectOnPlane(omega, normal).magnitude);

                if (!drivable)
                {
                    if (DriveBlend() >= 0.999f)
                    {
                        if (groundedFor <= 0f) result.TimeToDrive = since;
                        groundedFor += dt;
                        if (groundedFor >= 0.1f) drivable = true;
                    }
                    else
                    {
                        groundedFor = 0f;
                        result.TimeToDrive = -1f;
                    }
                }

                if (tilt < 20f && WheelsDown(point, normal) >= 3)
                {
                    if (uprightSince < 0f) uprightSince = since;
                    if (result.TimeToUpright < 0f && since - uprightSince >= 0.1f) result.TimeToUpright = uprightSince;
                }
                else uprightSince = -1f;

                result.FinalTilt = tilt;
                if (since >= window) break;
            }

            result.VnPost = vnPost > float.MinValue ? vnPost : 0f;
            result.Rebound = Mathf.Max(0f, peakAfterMin);
            result.AirAfterContact = sinceOut;
            result.Teleports = teleports;
            _car.Teleported -= Count;
        }

        #endregion

        public static string OutputFolder
        {
            get
            {
                string folder = Environment.GetEnvironmentVariable("DRIFTPAINT_LAB_OUT");
                if (string.IsNullOrEmpty(folder)) folder = Path.Combine(Path.GetTempPath(), "DriftPaintLab");
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        public static void Write(string file, IEnumerable<string> lines) => File.WriteAllLines(Path.Combine(OutputFolder, file), lines, Encoding.UTF8);
    }
}
#endif
