using System.Collections.Generic;
using System.Linq;
using DriftPaint.Cars;
using DriftPaint.LiveOperations;
using DriftPaint.Match;
using NUnit.Framework;

namespace DriftPaint.Tests
{
    public class LiveOpsConfigTests
    {
        static readonly string[] KnownCars = { "Car_AE86", "Car_A_R7", "Car_CC96", "Car_Gc14", "Car_RX7" };

        [TearDown]
        public void LeaveRoom()
        {
            LiveOps.LeaveRoom();
            LiveOps.SetLocal(LiveOpsConfig.Default);
        }

        [Test]
        public void Default_EnablesEverything()
        {
            LiveOpsConfig config = LiveOpsConfig.Default;
            CollectionAssert.AreEqual(MapCatalog.All, config.EnabledMaps(MapCatalog.All));
            CollectionAssert.AreEqual(KnownCars, config.EnabledCars(KnownCars));
            Assert.AreEqual(0, config.StatsFor("Car_AE86").Count);
            Assert.AreEqual(0, config.MusicOverrides.Count);
        }

        [Test]
        public void RemoteLists_FilterInGameOrder()
        {
            LiveOpsConfig config = LiveOpsConfig.FromRemote(" royal_drift_road , graffithasia ", "Car_RX7,Car_AE86", "{}", "{}");
            CollectionAssert.AreEqual(new[] { MapCatalog.GraffithasiaId, MapCatalog.RoyalDriftRoadId }, config.EnabledMaps(MapCatalog.All));
            CollectionAssert.AreEqual(new[] { "Car_AE86", "Car_RX7" }, config.EnabledCars(KnownCars));
        }

        [Test]
        public void UnknownIds_AreIgnored_AndOnlyUnknownFallsBackToAll()
        {
            LiveOpsConfig someUnknown = LiveOpsConfig.FromRemote("royal_drift_road,luna", "Car_Nope", "{}", "{}");
            CollectionAssert.AreEqual(new[] { MapCatalog.RoyalDriftRoadId }, someUnknown.EnabledMaps(MapCatalog.All));
            CollectionAssert.AreEqual(KnownCars, someUnknown.EnabledCars(KnownCars), "Si ningún auto existe se usan todos");
        }

        [Test]
        public void CarStats_KeepOnlyPositiveNumbers()
        {
            const string json = "{\"Car_AE86\": {\"accel\": 5.5, \"brake\": 0, \"turn\": -1}, \"Car_RX7\": {\"turn\": \"rápido\"}, \"Car_CC96\": 3}";
            LiveOpsConfig config = LiveOpsConfig.FromRemote("", "", json, "{}");

            IReadOnlyDictionary<string, float> ae86 = config.StatsFor("Car_AE86");
            Assert.AreEqual(1, ae86.Count);
            Assert.AreEqual(5.5f, ae86[CarStats.KeyTimeToMaxSpeed], 1e-5f);
            Assert.AreEqual(0, config.StatsFor("Car_RX7").Count);
            Assert.AreEqual(0, config.StatsFor("Car_CC96").Count);
        }

        [Test]
        public void BrokenJson_UsesDefaults()
        {
            LiveOpsConfig config = LiveOpsConfig.FromRemote(null, "  ", "{\"Car_AE86\": {", "no es json");
            Assert.AreEqual(0, config.Maps.Count);
            Assert.AreEqual(0, config.Cars.Count);
            Assert.AreEqual(0, config.CarsWithStats.Count());
            Assert.AreEqual(0, config.MusicOverrides.Count);
        }

        [Test]
        public void RoomCopy_RoundTrips()
        {
            LiveOpsConfig original = LiveOpsConfig.FromRemote("royal_drift_road", "Car_AE86,Car_RX7",
                "{\"Car_AE86\": {\"accel\": 5.5, \"turn\": 2.6}}", "{\"menu\": \"roadrift\", \"graffithasia\": \"say \\\"hi\\\"\"}");

            LiveOpsConfig copy = LiveOpsConfig.Deserialize(original.Serialize());

            CollectionAssert.AreEqual(original.Maps, copy.Maps);
            CollectionAssert.AreEqual(original.Cars, copy.Cars);
            Assert.AreEqual(2.6f, copy.StatsFor("Car_AE86")["turn"], 1e-5f);
            Assert.AreEqual("roadrift", copy.MusicOverrides["menu"]);
            Assert.AreEqual("say \"hi\"", copy.MusicOverrides["graffithasia"]);
        }

        [Test]
        public void RoomWithoutCopy_UsesDefaults()
        {
            Assert.AreSame(LiveOpsConfig.Default, LiveOpsConfig.Deserialize(null));
            Assert.AreSame(LiveOpsConfig.Default, LiveOpsConfig.Deserialize("{roto"));
        }

        [Test]
        public void Room_FollowsTheRoomCopy_AndReturnsToLocal()
        {
            LiveOps.SetLocal(LiveOpsConfig.FromRemote("graffithasia", "", "{}", "{}"));
            string roomCopy = LiveOpsConfig.FromRemote("royal_drift_road", "", "{}", "{}").Serialize();
            int changes = 0;
            void Count() => changes++;
            LiveOps.Changed += Count;
            try
            {
                LiveOps.EnterRoom(roomCopy);
                CollectionAssert.AreEqual(new[] { MapCatalog.RoyalDriftRoadId }, LiveOps.Room.EnabledMaps(MapCatalog.All));
                LiveOps.EnterRoom(roomCopy);
                Assert.AreEqual(1, changes, "La misma copia no vuelve a avisar");

                LiveOps.LeaveRoom();
                CollectionAssert.AreEqual(new[] { MapCatalog.GraffithasiaId }, LiveOps.Room.EnabledMaps(MapCatalog.All));
                Assert.AreEqual(2, changes);
            }
            finally
            {
                LiveOps.Changed -= Count;
            }
        }

        [Test]
        public void CarChoice_KeepsEnabledChoice_OrFallsBackByActor()
        {
            var enabled = new[] { "Car_AE86", "Car_RX7" };
            Assert.AreEqual("Car_RX7", CarChoice.Resolve("Car_RX7", 1, enabled));
            Assert.AreEqual("Car_AE86", CarChoice.Resolve("Car_CC96", 1, enabled));
            Assert.AreEqual("Car_RX7", CarChoice.Resolve(null, 2, enabled));
            Assert.IsNull(CarChoice.Resolve("Car_RX7", 1, new string[0]));
        }

        [Test]
        public void CarId_FromClonedObjectName()
        {
            Assert.AreEqual("Car_AE86", CarChoice.IdFromObjectName("Car_AE86(Clone)"));
            Assert.AreEqual("Car_AE86", CarChoice.IdFromObjectName("Car_AE86"));
        }
    }
}
