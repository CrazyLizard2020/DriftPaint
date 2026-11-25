#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DriftPaint.Cars;
using DriftPaint.LiveOperations;
using DriftPaint.Paint;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DriftPaint.Tests
{
    /// <summary>
    /// Recorre todas las rampas de las escenas reales con cada auto y deja en CSV el salto, el aterrizaje y si cruzó
    /// un aro. Es explícita: no forma parte de la corrida normal de tests.
    /// </summary>
    [Explicit, Category("Lab")]
    public sealed class VehicleArenaReport
    {
        static readonly string[] Scenes = { "Sandbox_Car", "Match_DriftArena", "Match_RoyalDriftRoad" };
        static readonly float[] Speeds = { 15f, 22f, 30f };
        /// <summary>Radio libre de los aros: la mitad del diámetro menos el margen del borde.</summary>
        const float Aperture = 3.35f;

        [UnityTest, Timeout(3600000)]
        public IEnumerator Ramps()
        {
            LogAssert.ignoreFailingMessages = true;
            var rows = new List<string> { "scene;model;ramp;speed;takeoff_speed;flight_s;range_m;apex_m;ring;restitution;rebound_m;retention;final_tilt_deg;max_tilt_deg;teleports;upright_drive;rebound_after_s;rebound_over;body_hit" };
            float scale = Time.timeScale, maximum = Time.maximumDeltaTime;

            foreach (string scene in Scenes)
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode($"Assets/Scenes/{scene}.unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                LiveOps.EnterRoom("{}");
                Time.maximumDeltaTime = 0.5f;
                Time.timeScale = 8f;

                foreach (CarController other in Object.FindObjectsByType<CarController>(FindObjectsSortMode.None)) other.gameObject.SetActive(false);
                Transform[] ramps = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("Ramp_") && t.GetComponent<Collider>() != null).OrderBy(t => t.name).ToArray();
                PaintRing[] rings = Object.FindObjectsByType<PaintRing>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                foreach (string model in VehicleLab.Models)
                foreach (Transform ramp in ramps)
                foreach (float speed in Speeds)
                    yield return Jump(scene, model, ramp, speed, rings, rows);
            }

            Time.timeScale = scale;
            Time.maximumDeltaTime = maximum;
            LiveOps.LeaveRoom();
            VehicleLab.Write("arenas.csv", rows);
        }

        static IEnumerator Jump(string scene, string model, Transform ramp, float speed, PaintRing[] rings, List<string> rows)
        {
            Vector3 forward = Vector3.ProjectOnPlane(ramp.forward, Vector3.up).normalized;
            Vector3 low = ramp.position - ramp.forward * (ramp.lossyScale.z * 0.5f);
            Vector3 lip = ramp.position + ramp.forward * (ramp.lossyScale.z * 0.5f);
            Vector3 start = low - forward * 9f;
            start.y = 0.1f;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Cars/{model}.prefab");
            GameObject instance = Object.Instantiate(prefab, start, Quaternion.LookRotation(forward, Vector3.up));
            instance.name = model;
            CarController car = instance.GetComponent<CarController>();
            car.MovementInput.SetInputType(CarInputType.Bot);
            int teleports = 0;
            car.Teleported += (p, r) => teleports++;
            for (float t = 0f; t < 0.5f; t += Time.fixedDeltaTime) yield return VehicleLab.Step;
            float rest = car.Body.position.y;
            car.Body.linearVelocity = forward * speed;

            Vector3 previous = car.Body.worldCenterOfMass, takeoff = default, lastVelocity = car.Body.linearVelocity;
            float flight = 0f, apex = 0f, vnPre = 0f, vnPost = 0f, vtPre = 0f, vtPost = 0f, rebound = 0f, minAfter = float.MaxValue, maxTilt = 0f, since = 0f, range = 0f, takeoffSpeed = 0f;
            float reboundAt = 0f; string reboundOver = "-", bodyHit = "-";
            bool flying = false, landed = false, crossed = false, fell = false;
            for (float t = 0f; t < 6f && since < 2f; t += Time.fixedDeltaTime)
            {
                bool past = Vector3.Dot(car.Body.position - lip, forward) > -0.5f;
                car.MovementInput.SetExternalInput(new FrameInput { Throttle = past ? 0f : Mathf.Clamp01(speed / car.Stats.MaxSpeed + (speed - car.ForwardSpeed) * 0.5f) });
                yield return VehicleLab.Step;
                float dt = Time.fixedDeltaTime;
                Vector3 center = car.Body.worldCenterOfMass, velocity = car.Body.linearVelocity;
                if (teleports == 0)
                    foreach (PaintRing ring in rings) crossed |= ring.Crosses(previous, center, Aperture);
                previous = center;
                float height = car.Body.position.y - rest;

                if (!flying)
                {
                    if (past && Clearance(car) > 0.2f) { flying = true; takeoff = car.Body.position; takeoffSpeed = velocity.magnitude; }
                }
                else if (!landed)
                {
                    flight += dt;
                    apex = Mathf.Max(apex, height);
                    if (Clearance(car) < 0.07f)
                    {
                        landed = true;
                        range = Vector3.ProjectOnPlane(car.Body.position - takeoff, Vector3.up).magnitude;
                        vnPre = lastVelocity.y;
                        vtPre = Vector3.ProjectOnPlane(lastVelocity, Vector3.up).magnitude;
                    }
                }
                if (landed)
                {
                    since += dt;
                    if (since < 0.35f) vnPost = Mathf.Max(vnPost, velocity.y);
                    if (vtPost == 0f && since > 0.1f) vtPost = Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude;
                    minAfter = Mathf.Min(minAfter, height);
                    if (since > 0.05f && velocity.y > 0f && since < 1.6f && height > rebound) { rebound = height; reboundAt = since; reboundOver = Below(car); }
                    if (bodyHit == "-" && car.GroundSensor.Sample.HasBodyGroundContact) bodyHit = since.ToString("0.00", CultureInfo.InvariantCulture);
                    maxTilt = Mathf.Max(maxTilt, Vector3.Angle(car.Body.rotation * Vector3.up, Vector3.up));
                }
                lastVelocity = velocity;
                if (teleports > 0) { fell = true; break; }
            }

            float finalTilt = Vector3.Angle(car.Body.rotation * Vector3.up, Vector3.up);
            string F(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);
            rows.Add(string.Join(";", scene, model, ramp.name, F(speed), F(takeoffSpeed), F(flight), F(range), F(apex), crossed ? "1" : "0",
                F(vnPre < -0.5f ? vnPost / -vnPre : 0f), F(rebound), F(vtPre > 1f ? vtPost / vtPre : 1f), F(finalTilt), F(maxTilt), teleports.ToString(), !fell && finalTilt < 25f ? "1" : "0", F(reboundAt), reboundOver, bodyHit));
            Object.Destroy(instance);
            yield return null;
        }

        /// <summary>Qué hay debajo del auto.</summary>
        static string Below(CarController car)
        {
            foreach (RaycastHit hit in Physics.RaycastAll(car.Body.worldCenterOfMass, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (hit.rigidbody != car.Body) return $"{hit.collider.name} {Vector3.Angle(hit.normal, Vector3.up):0}° en {car.Body.position}";
            return "-";
        }

        /// <summary>Distancia libre debajo de la rueda más baja, medida con rayos contra la escena.</summary>
        static float Clearance(CarController car)
        {
            float min = float.MaxValue;
            foreach (Transform probe in car.GroundSensor.Probes)
            {
                Vector3 local = car.transform.InverseTransformPoint(probe.position);
                Vector3 world = car.Body.position + car.Body.rotation * local;
                if (Physics.Raycast(world, Vector3.down, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore) && hit.rigidbody != car.Body)
                    min = Mathf.Min(min, hit.distance - car.GroundSensor.WheelRadius);
            }
            return min;
        }
    }
}
#endif
