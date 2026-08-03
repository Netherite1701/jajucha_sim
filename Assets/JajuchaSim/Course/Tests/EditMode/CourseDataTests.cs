using NUnit.Framework;
using System.Linq;

namespace JajuchaSim.Course.Tests
{
    public class CourseDataTests
    {
        [Test]
        public void ToData_RoundTrip_PreservesAllLayers()
        {
            var grid = new CourseGrid(25f);
            grid.SetRoad(new GridCoordinate(0, 0));
            grid.SetRoad(new GridCoordinate(1, 0));
            grid.SetRoad(new GridCoordinate(2, 0));
            grid.SetStructure(new GridCoordinate(1, 0), StructureType.Tunnel);
            grid.SetStructure(new GridCoordinate(1, 1), StructureType.Tunnel);
            grid.SetObject(new GridCoordinate(0, 1), ObjectType.Obstacle);
            grid.SetObject(new GridCoordinate(3, 0), ObjectType.Sign);
            grid.SetTrigger(new GridCoordinate(0, 2), TriggerType.SlowZone);
            grid.SetTrigger(new GridCoordinate(1, 2), TriggerType.SlowZone);
            grid.SetTrigger(new GridCoordinate(5, 5), TriggerType.SpeedGate);
            var data = CourseSerializer.ToData(grid);
            Assert.AreEqual(25f, data.tileSizeCm);
            Assert.AreEqual(3, data.road.Length);
            Assert.AreEqual(2, data.structures.Length);
            Assert.AreEqual(2, data.objects.Length);
            Assert.AreEqual(3, data.triggers.Length);
            var grid2 = CourseSerializer.ToGrid(data);
            Assert.AreEqual(25f, grid2.TileSizeCm, 1e-6f);
            Assert.IsTrue(grid2.HasRoad(new GridCoordinate(0, 0)));
            Assert.IsTrue(grid2.HasRoad(new GridCoordinate(1, 0)));
            Assert.IsTrue(grid2.HasRoad(new GridCoordinate(2, 0)));
            Assert.IsFalse(grid2.HasRoad(new GridCoordinate(99, 99)));
            Assert.AreEqual(3, grid2.RoadTileCount);
            Assert.AreEqual(StructureType.Tunnel, grid2.GetStructure(new GridCoordinate(1, 0)));
            Assert.AreEqual(StructureType.Tunnel, grid2.GetStructure(new GridCoordinate(1, 1)));
            Assert.AreEqual(StructureType.None, grid2.GetStructure(new GridCoordinate(0, 0)));
            Assert.AreEqual(2, grid2.StructureTileCount);
            Assert.AreEqual(ObjectType.Obstacle, grid2.GetObject(new GridCoordinate(0, 1)));
            Assert.AreEqual(ObjectType.Sign, grid2.GetObject(new GridCoordinate(3, 0)));
            Assert.AreEqual(ObjectType.None, grid2.GetObject(new GridCoordinate(99, 99)));
            Assert.AreEqual(2, grid2.ObjectTileCount);
            Assert.AreEqual(TriggerType.SlowZone, grid2.GetTrigger(new GridCoordinate(0, 2)));
            Assert.AreEqual(TriggerType.SlowZone, grid2.GetTrigger(new GridCoordinate(1, 2)));
            Assert.AreEqual(TriggerType.SpeedGate, grid2.GetTrigger(new GridCoordinate(5, 5)));
            Assert.AreEqual(TriggerType.None, grid2.GetTrigger(new GridCoordinate(99, 99)));
            Assert.AreEqual(3, grid2.TriggerTileCount);
        }

        [Test]
        public void ToData_EmptyGrid()
        {
            var grid = new CourseGrid(20f);
            var data = CourseSerializer.ToData(grid);
            Assert.AreEqual(20f, data.tileSizeCm);
            Assert.AreEqual(0, data.road.Length);
            Assert.AreEqual(0, data.structures.Length);
            Assert.AreEqual(0, data.objects.Length);
            Assert.AreEqual(0, data.triggers.Length);
        }

        [Test]
        public void ToGrid_EmptyData()
        {
            var data = new CourseData();
            var grid = CourseSerializer.ToGrid(data);
            Assert.AreEqual(20f, grid.TileSizeCm, 1e-6f);
            Assert.AreEqual(0, grid.RoadTileCount);
            Assert.AreEqual(0, grid.StructureTileCount);
            Assert.AreEqual(0, grid.ObjectTileCount);
            Assert.AreEqual(0, grid.TriggerTileCount);
        }

        [Test]
        public void ToData_ProducesIndividualEntriesWithIds()
        {
            var grid = new CourseGrid(20f);
            grid.SetStructure(new GridCoordinate(0, 0), StructureType.Tunnel);
            grid.SetStructure(new GridCoordinate(0, 1), StructureType.Tunnel);
            grid.SetStructure(new GridCoordinate(1, 0), StructureType.Ramp);
            var data = CourseSerializer.ToData(grid);
            Assert.AreEqual(3, data.structures.Length);
            foreach (var entry in data.structures)
            {
                Assert.IsNotNull(entry.id);
                Assert.IsTrue(entry.id.Length > 0);
                Assert.IsTrue(entry.region.IsValid);
                Assert.AreEqual(1, entry.region.width);
                Assert.AreEqual(1, entry.region.height);
            }
        }

        [Test]
        public void ToData_ProducesIndividualTriggerEntries()
        {
            var grid = new CourseGrid(20f);
            grid.SetTrigger(new GridCoordinate(0, 0), TriggerType.SlowZone);
            grid.SetTrigger(new GridCoordinate(0, 1), TriggerType.SlowZone);
            grid.SetTrigger(new GridCoordinate(1, 0), TriggerType.SpeedGate);
            var data = CourseSerializer.ToData(grid);
            Assert.AreEqual(3, data.triggers.Length);
            foreach (var entry in data.triggers)
            {
                Assert.IsNotNull(entry.id);
                Assert.IsTrue(entry.id.Length > 0);
            }
        }

        [Test]
        public void CoordPair_ToAndFromGrid()
        {
            var c = new GridCoordinate(7, -3);
            var pair = CoordPair.FromGrid(c);
            Assert.AreEqual(7, pair.x);
            Assert.AreEqual(-3, pair.z);
            Assert.AreEqual(c, pair.ToGrid());
            var empty = new CoordPair();
            Assert.AreEqual(0, empty.x);
            Assert.AreEqual(0, empty.z);
            Assert.AreEqual(new GridCoordinate(0, 0), empty.ToGrid());
        }

        [Test]
        public void CourseData_StructureEntry_HasRegion()
        {
            var entry = new CourseData.StructureEntry
            {
                id = "test_001",
                type = "tunnel",
                region = new GridRegion(10, 20, 4, 8),
                heightCm = 55f,
                wallThicknessCm = 2f
            };
            Assert.AreEqual("test_001", entry.id);
            Assert.AreEqual("tunnel", entry.type);
            Assert.AreEqual(10, entry.region.x);
            Assert.AreEqual(20, entry.region.z);
            Assert.AreEqual(4, entry.region.width);
            Assert.AreEqual(8, entry.region.height);
            Assert.AreEqual(55f, entry.heightCm);
            Assert.AreEqual(2f, entry.wallThicknessCm);
        }

        [Test]
        public void CourseData_ObjectEntry_HasRotation()
        {
            var entry = new CourseData.ObjectEntry
            {
                id = "sign_001",
                type = "slow_sign",
                tile = new CoordPair(15, 24),
                rotationDeg = 90
            };
            Assert.AreEqual("sign_001", entry.id);
            Assert.AreEqual(90, entry.rotationDeg);
            Assert.AreEqual(15, entry.tile.x);
            Assert.AreEqual(24, entry.tile.z);
        }

        [Test]
        public void FeatureIdGenerator_ProducesUniqueIds()
        {
            FeatureIdGenerator.Reset();
            var id1 = FeatureIdGenerator.NextId("tunnel");
            var id2 = FeatureIdGenerator.NextId("tunnel");
            var id3 = FeatureIdGenerator.NextId("obstacle");

            Assert.AreEqual("tunnel_001", id1);
            Assert.AreEqual("tunnel_002", id2);
            Assert.AreEqual("obstacle_001", id3);
            Assert.AreNotEqual(id1, id2);
            Assert.AreNotEqual(id1, id3);
        }

        [Test]
        public void FeatureIdGenerator_Reset_ClearsCounters()
        {
            FeatureIdGenerator.Reset();
            FeatureIdGenerator.NextId("tunnel");
            FeatureIdGenerator.Reset();
            var id = FeatureIdGenerator.NextId("tunnel");
            Assert.AreEqual("tunnel_001", id);
        }

        [Test]
        public void FeatureIdGenerator_TypeToPrefix_ConvertsCorrectly()
        {
            Assert.AreEqual("tunnel", FeatureIdGenerator.TypeToPrefix("tunnel"));
            Assert.AreEqual("slow_zone", FeatureIdGenerator.TypeToPrefix("Slow Zone"));
            Assert.AreEqual("slow_sign", FeatureIdGenerator.TypeToPrefix("slow-sign"));
        }

        [Test]
        public void CourseData_TriggerEntry_HasTypes()
        {
            var slowZone = new CourseData.TriggerEntry
            {
                id = "sz_001",
                type = "slow_zone",
                region = new GridRegion(5, 5, 3, 4)
            };
            Assert.AreEqual("slow_zone", slowZone.type);

            var speedGate = new CourseData.TriggerEntry
            {
                id = "gate_a",
                type = "speed_gate",
                cellX = 20,
                cellZ = 30,
                edge = "north"
            };
            Assert.AreEqual("speed_gate", speedGate.type);
            Assert.AreEqual(20, speedGate.cellX);
            Assert.AreEqual(30, speedGate.cellZ);

            var evt = new CourseData.TriggerEntry
            {
                id = "evt_001",
                type = "event",
                region = new GridRegion(10, 10, 2, 2),
                eventId = "tunnel_entry"
            };
            Assert.AreEqual("event", evt.type);
            Assert.AreEqual("tunnel_entry", evt.eventId);
        }
    }
}
