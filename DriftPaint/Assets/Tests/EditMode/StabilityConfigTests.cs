using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DriftPaint.Cars;
using DriftPaint.LiveOperations;
using DriftPaint.Sandbox;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DriftPaint.Tests
{
    public class StabilityConfigTests
    {
        static readonly string[] Models = { "AE86", "A_R7", "CC96", "Gc14", "RX7" };
        const float Yaw = 3.6f;

        [TearDown]
        public void LeaveRoom()
        {
            LiveOps.LeaveRoom();
            LiveOps.SetLocal(LiveOpsConfig.Default);
        }

        static LiveOpsConfig Remote(string carStats) => LiveOpsConfig.FromRemote("", "", carStats, "");
        static CarDataSO Data(string model) => AssetDatabase.LoadAssetAtPath<CarDataSO>($"Assets/ScriptableObjects/Cars/CarData_{model}.asset");

        static float Number(StabilitySettings settings, string key)
        {
            Assert.IsTrue(settings.TryGetNumber(key, out float value), key);
            return value;
        }

        #region Esquema

        [Test]
        public void EveryNumericField_IsBound_AndDefaultsAreValid()
        {
            var settings = new StabilitySettings();
            foreach (StabilityField field in StabilitySchema.Fields)
            {
                if (field.Kind == StabilityFieldKind.Number || field.Kind == StabilityFieldKind.Integer)
                    Assert.IsTrue(settings.TryGetNumber(field.Key, out _), $"Falta enlazar {field.Key}");
                Assert.IsFalse(string.IsNullOrEmpty(field.Unit), field.Key);
                Assert.Less(field.Min, field.Max, field.Key);
            }
            CollectionAssert.AllItemsAreUnique(StabilitySchema.Fields.Select(field => field.Key));
            var warnings = new List<string>();
            Assert.IsTrue(settings.Sanitize(Yaw, warnings.Add), string.Join("; ", warnings));
        }

        [Test]
        public void EverySerializedField_IsInTheSchema()
        {
            int fields = typeof(StabilitySettings)
                .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Count(field => field.IsDefined(typeof(SerializeField), false));
            Assert.AreEqual(StabilitySchema.Fields.Length, fields);
            foreach (StabilityField field in StabilitySchema.Fields)
                Assert.IsNotNull(typeof(StabilitySettings).GetField("_" + field.Key, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic), field.Key);
        }

        #endregion

        #region Lectura remota

        [Test]
        public void ValidFields_OverrideOnlyTheirCar()
        {
            LiveOpsConfig config = Remote("{\"Car_RX7\":{\"accel\":5,\"stability\":{\"extraGravity\":12,\"minGroundedWheels\":2,\"inertiaTensorScale\":{\"x\":1.1,\"y\":1,\"z\":0.9}}}}");
            Assert.AreEqual(0, config.Warnings.Count);
            Assert.AreEqual(5f, config.StatsFor("Car_RX7")["accel"]);
            Assert.AreEqual(1, config.StatsFor("Car_RX7").Count, "El objeto anidado no es una estadística.");
            Assert.AreEqual(0, config.StabilityFor("Car_AE86").Count);

            var stats = new CarStats(Data("RX7"));
            stats.ApplyOverrides(config.StabilityFor("Car_RX7"));
            Assert.AreEqual(12f, stats.Stability.ExtraGravity);
            Assert.AreEqual(2, stats.Stability.MinGroundedWheels);
            Assert.AreEqual(1.1f, stats.Stability.InertiaTensorScale.x, 1e-4f);
            Assert.AreEqual(Data("RX7").Stability.LandingAlignKp, stats.Stability.LandingAlignKp);
        }

        [TestCase("\"extraGravity\":\"12\"")]
        [TestCase("\"extraGravity\":true")]
        [TestCase("\"extraGravity\":null")]
        [TestCase("\"extraGravity\":-1")]
        [TestCase("\"extraGravity\":31")]
        [TestCase("\"extraGravity\":1e999")]
        [TestCase("\"extraGravity\":{\"x\":1}")]
        [TestCase("\"minGroundedWheels\":2.5")]
        [TestCase("\"minGroundedWheels\":5")]
        [TestCase("\"inertiaTensorScale\":{\"x\":1,\"y\":1}")]
        [TestCase("\"inertiaTensorScale\":{\"x\":1,\"y\":1,\"z\":9}")]
        [TestCase("\"adhesionBySlope\":[]")]
        [TestCase("\"adhesionBySlope\":[{\"x\":10,\"y\":1},{\"x\":10,\"y\":2}]")]
        [TestCase("\"adhesionBySlope\":[{\"x\":0,\"y\":1},{\"x\":10,\"y\":99}]")]
        [TestCase("\"adhesionBySlope\":[{\"x\":0,\"y\":1,\"z\":2}]")]
        [TestCase("\"landingBounciness\":0.5")]
        [TestCase("\"noExiste\":1")]
        public void InvalidField_IsDroppedAlone(string field)
        {
            LiveOpsConfig config = Remote("{\"Car_RX7\":{\"stability\":{" + field + ",\"landingAlignKp\":150}}}");
            StabilityOverrides overrides = config.StabilityFor("Car_RX7");
            Assert.AreEqual(1, overrides.Count);
            Assert.IsTrue(overrides.TryGetNumber("landingAlignKp", out float kp));
            Assert.AreEqual(150f, kp);
            Assert.AreEqual(1, config.Warnings.Count);
        }

        [Test]
        public void StabilityThatIsNotAnObject_IsIgnored()
        {
            foreach (string value in new[] { "3", "\"x\"", "[1]", "null", "true" })
            {
                LiveOpsConfig config = Remote("{\"Car_RX7\":{\"brake\":20,\"stability\":" + value + "}}");
                Assert.AreEqual(0, config.StabilityFor("Car_RX7").Count);
                Assert.AreEqual(20f, config.StatsFor("Car_RX7")["brake"]);
            }
        }

        [Test]
        public void Warnings_AreBounded()
        {
            var json = new StringBuilder("{\"Car_RX7\":{\"stability\":{");
            for (int i = 0; i < 500; i++) json.Append(i > 0 ? "," : "").Append("\"x").Append(new string('y', 100)).Append(i).Append("\":1");
            LiveOpsConfig config = Remote(json.Append("}}}").ToString());
            Assert.LessOrEqual(config.Warnings.Count, 16);
            Assert.IsTrue(config.Warnings.All(warning => warning.Length < 160));
        }

        [Test]
        public void Curves_AreLinearWithConstantEnds()
        {
            LiveOpsConfig config = Remote("{\"Car_RX7\":{\"stability\":{\"adhesionBySlope\":[{\"x\":10,\"y\":1},{\"x\":30,\"y\":2}]}}}");
            var stats = new CarStats(Data("RX7"));
            stats.ApplyOverrides(config.StabilityFor("Car_RX7"));
            AnimationCurve curve = stats.Stability.AdhesionBySlope;
            Assert.AreEqual(1f, curve.Evaluate(0f), 1e-3f);
            Assert.AreEqual(1.5f, curve.Evaluate(20f), 1e-3f);
            Assert.AreEqual(1.25f, curve.Evaluate(15f), 1e-3f);
            Assert.AreEqual(2f, curve.Evaluate(60f), 1e-3f);
        }

        #endregion

        #region Relaciones

        static StabilitySettings Apply(string fields)
        {
            StabilitySettings settings = new StabilitySettings().Clone();
            settings.ApplyOverrides(Remote("{\"c\":{\"stability\":{" + fields + "}}}").StabilityFor("c"), Yaw);
            return settings;
        }

        [Test]
        public void IncoherentAngles_FallBackTogether_AndKeepTheRest()
        {
            var defaults = new StabilitySettings();
            StabilitySettings settings = Apply("\"autoRollMaxAngle\":80,\"flippedAngle\":60,\"extraGravity\":12");
            Assert.AreEqual(defaults.AutoRollMaxAngle, settings.AutoRollMaxAngle);
            Assert.AreEqual(defaults.FlippedAngle, settings.FlippedAngle);
            Assert.AreEqual(12f, settings.ExtraGravity);
        }

        [Test]
        public void IncoherentAngularLimits_FallBackTogether()
        {
            var defaults = new StabilitySettings();
            StabilitySettings settings = Apply("\"maxAngularVelocity\":3.2,\"maxPitchRate\":2");
            Assert.AreEqual(defaults.MaxAngularVelocity, settings.MaxAngularVelocity);
            Assert.AreEqual(defaults.MaxPitchRate, settings.MaxPitchRate);
        }

        [Test]
        public void IncoherentAdhesion_FallsBackTogether()
        {
            var defaults = new StabilitySettings();
            StabilitySettings settings = Apply("\"landingAdhesionAcceleration\":40,\"maxAdhesionAcceleration\":10");
            Assert.AreEqual(defaults.LandingAdhesionAcceleration, settings.LandingAdhesionAcceleration);
            Assert.AreEqual(defaults.MaxAdhesionAcceleration, settings.MaxAdhesionAcceleration);
        }

        [Test]
        public void FasterTurn_RaisesTheAngularLimit()
        {
            var stats = new CarStats(Data("RX7"));
            stats.ApplyOverrides(new Dictionary<string, float> { { "turn", 5f } });
            stats.ApplyOverrides(StabilityOverrides.None);
            Assert.GreaterOrEqual(stats.Stability.MaxAngularVelocity, stats.RequiredYawRate * StabilitySettings.YawMargin - 1e-3f);
        }

        [Test]
        public void InheritedDampings_FollowTheirSource()
        {
            StabilitySettings settings = Apply("\"airAlignKd\":20,\"landingAlignKp\":250,\"landingAlignKd\":31");
            Assert.AreEqual(20f, settings.AirPitchDamping);
            Assert.AreEqual(20f, settings.AirRollDamping);
            Assert.AreEqual(250f, settings.AutoRollKp);
            Assert.AreEqual(31f, settings.AutoRollKd);
            Assert.AreEqual(9f, Apply("\"airAlignKd\":20,\"airPitchDamping\":9").AirPitchDamping);
        }

        #endregion

        #region Copias y sala

        [Test]
        public void Stats_NeverTouchTheAsset()
        {
            CarDataSO data = Data("RX7");
            string before = EditorJsonUtility.ToJson(data);
            var stats = new CarStats(data);
            stats.ApplyOverrides(Remote("{\"c\":{\"stability\":{\"extraGravity\":3,\"adhesionBySpeed\":[{\"x\":0,\"y\":0.5}]}}}").StabilityFor("c"));
            stats.Stability.AdhesionBySlope.AddKey(5f, 9f);
            Assert.AreEqual(before, EditorJsonUtility.ToJson(data));
            Assert.AreNotSame(data.Stability, stats.Stability);
            Assert.AreNotSame(data.Stability.AdhesionBySlope, stats.Stability.AdhesionBySlope);
        }

        [Test]
        public void RoomSnapshot_RoundTrips_AndIsStable()
        {
            LiveOpsConfig config = Remote("{\"Car_RX7\":{\"accel\":5,\"stability\":{\"extraGravity\":12.5,\"minGroundedWheels\":3,\"inertiaTensorScale\":{\"x\":1.1,\"y\":1,\"z\":0.9},\"adhesionBySlope\":[{\"x\":0,\"y\":1},{\"x\":55,\"y\":1.5}]}}}");
            string text = config.Serialize();
            LiveOpsConfig copy = LiveOpsConfig.Deserialize(text);
            Assert.AreEqual(text, copy.Serialize());
            Assert.AreEqual(4, copy.StabilityFor("Car_RX7").Count);
            Assert.IsTrue(copy.StabilityFor("Car_RX7").TryGetNumber("extraGravity", out float value));
            Assert.AreEqual(12.5f, value);
        }

        [Test]
        public void RoomSnapshot_IsValidatedOnArrival()
        {
            LiveOpsConfig config = LiveOpsConfig.Deserialize("{\"stats\":{\"Car_RX7\":{\"stability\":{\"extraGravity\":9999,\"landingAlignKp\":150}}}}");
            Assert.AreEqual(1, config.StabilityFor("Car_RX7").Count);
        }

        [Test]
        public void Room_PrevailsOverLocal()
        {
            LiveOps.SetLocal(Remote("{\"Car_RX7\":{\"stability\":{\"extraGravity\":3}}}"));
            LiveOps.EnterRoom(Remote("{\"Car_RX7\":{\"stability\":{\"extraGravity\":14}}}").Serialize());
            Assert.IsTrue(LiveOps.Room.StabilityFor("Car_RX7").TryGetNumber("extraGravity", out float value));
            Assert.AreEqual(14f, value);
            LiveOps.EnterRoom(LiveOpsConfig.Default.Serialize());
            Assert.AreEqual(0, LiveOps.Room.StabilityFor("Car_RX7").Count);
        }

        static string FullStability()
        {
            string N(float v) => v.ToString("0.#####", CultureInfo.InvariantCulture);
            var json = new StringBuilder("{");
            foreach (StabilityField field in StabilitySchema.Fields)
            {
                if (!field.Remote) continue;
                if (json.Length > 1) json.Append(',');
                json.Append('"').Append(field.Key).Append("\":");
                float value = Mathf.Lerp(field.Min, field.Max, 0.4321987f);
                if (field.Kind == StabilityFieldKind.Vector) json.Append("{\"x\":").Append(N(value)).Append(",\"y\":").Append(N(value)).Append(",\"z\":").Append(N(value)).Append('}');
                else if (field.Kind == StabilityFieldKind.Curve)
                {
                    json.Append('[');
                    for (int i = 0; i < StabilitySchema.MaxCurvePoints; i++)
                        json.Append(i > 0 ? "," : "").Append("{\"x\":").Append(N(field.DomainMax * (i + 0.123f) / StabilitySchema.MaxCurvePoints)).Append(",\"y\":").Append(N(value)).Append('}');
                    json.Append(']');
                }
                else json.Append(field.Kind == StabilityFieldKind.Integer ? "2" : N(value));
            }
            return json.Append('}').ToString();
        }

        static string ManyCars(int count)
        {
            var json = new StringBuilder("{");
            for (int i = 0; i < count; i++)
            {
                string name = i < Models.Length ? "Car_" + Models[i] : "Car_" + new string((char)('a' + i), StabilitySchema.MaxKeyLength - 4);
                json.Append(i > 0 ? "," : "").Append('"').Append(name).Append("\":{\"accel\":5.12345,\"brake\":19.12345,\"turn\":2.12345,\"stability\":").Append(FullStability()).Append('}');
            }
            return json.Append('}').ToString();
        }

        [Test]
        public void RoomSnapshot_FitsInAPhotonString()
        {
            int empty = Encoding.UTF8.GetByteCount(LiveOpsConfig.Default.Serialize());
            int five = Encoding.UTF8.GetByteCount(Remote(ManyCars(5)).Serialize());
            LiveOpsConfig worst = Remote(ManyCars(40));
            int max = Encoding.UTF8.GetByteCount(worst.Serialize());
            TestContext.WriteLine($"cfg: vacía {empty} B, cinco autos completos {five} B, peor caso {max} B");
            Assert.AreEqual(StabilitySchema.MaxCars, worst.CarsWithStability.Count());
            Assert.AreEqual(StabilitySchema.Fields.Count(field => field.Remote), worst.StabilityFor("Car_RX7").Count);
            Assert.Less(max, short.MaxValue, "Photon serializa cada texto con un largo de 16 bits.");
        }

        #endregion

        #region Recursos

        [Test]
        public void Assets_AreMigratedAndValid([ValueSource(nameof(Models))] string model)
        {
            CarDataSO data = Data(model);
            var warnings = new List<string>();
            Assert.IsTrue(data.Stability.Clone().Sanitize(data.RequiredYawRate(data.MaxYawRate), warnings.Add), string.Join("; ", warnings));

            string text = File.ReadAllText($"Assets/ScriptableObjects/Cars/CarData_{model}.asset");
            StringAssert.Contains("_stability:", text);
            foreach (string removed in new[] { "\n  _extraGravity:", "\n  _suspensionStiffness:", "_suspensionReboundRatio:", "\n  _flippedAngle:" })
                StringAssert.DoesNotContain(removed, text);
        }

        [Test]
        public void Prefabs_HaveTheWholePipeline([ValueSource(nameof(Models))] string model)
        {
            string path = $"Assets/Prefabs/Cars/Car_{model}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.AreEqual(1, prefab.GetComponents<CarStabilityController>().Length);
            Assert.AreEqual(1, prefab.GetComponents<CarFlipWarning>().Length);
            Assert.AreEqual(1, prefab.GetComponents<CarLandingFeedback>().Length);
            Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab));
            Assert.IsNotNull(prefab.transform.Find("Visual/Model"));
            string text = File.ReadAllText(path);
            foreach (string removed in new[] { "_minGroundedWheels", "_maxGroundAngle" }) StringAssert.DoesNotContain(removed, text);
        }

        [Test]
        public void CenterOfMass_StaysInsideTheHull([ValueSource(nameof(Models))] string model)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Cars/Car_{model}.prefab");
            float baseHeight = new SerializedObject(prefab.GetComponent<CarController>()).FindProperty("_centerOfMassHeight").floatValue;
            Bounds hull = prefab.GetComponent<MeshCollider>().sharedMesh.bounds;
            Assert.That(baseHeight + Data(model).Stability.CenterOfMassOffsetY, Is.InRange(hull.min.y, hull.max.y));
        }

        [Test]
        public void Sandbox_HasTheStabilityPanel()
        {
            string text = File.ReadAllText("Assets/Scenes/Sandbox_Car.unity");
            string guid = AssetDatabase.AssetPathToGUID("Assets/Scripts/Gameplay/Sandbox/VehicleStabilityDebugger.cs");
            StringAssert.Contains(guid, text);
            StringAssert.Contains("StabilityPanel", text);
        }

        [Test]
        public void OnlyTheCoordinatorTicks()
        {
            foreach (System.Type type in new[] { typeof(CarGroundSensor), typeof(CarStatusEffects), typeof(CarStabilityController), typeof(CarMotorLocomotion) })
                Assert.IsNull(type.GetMethod("FixedUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public), type.Name);
            Assert.IsNotNull(typeof(CarController).GetMethod("FixedUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
        }

        #endregion
    }
}
