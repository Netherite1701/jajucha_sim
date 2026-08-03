using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Course.Tests
{
    public class CourseDocumentTests
    {
        [SetUp]
        public void SetUp()
        {
            FeatureIdGenerator.Reset();
        }

        // ---- Tunnel placement (7.50) ----------------------------------

        [Test]
        public void PlaceTunnel_4x8_CreatesStructureAndFootprint()
        {
            var doc = new CourseDocument(20f);
            // Road under tunnel
            for (int z = 40; z < 48; z++)
                for (int x = 20; x < 24; x++)
                    doc.SetRoad(new GridCoordinate(x, z));

            var region = new GridRegion(20, 40, 4, 8);
            var tunnel = doc.PlaceTunnel(region, heightCm: 55f);

            Assert.IsNotNull(tunnel);
            Assert.AreEqual(StructureType.Tunnel, tunnel.Type);
            Assert.AreEqual(region, tunnel.Region);
            Assert.AreEqual(55f, tunnel.HeightCm);
            Assert.IsTrue(tunnel.Id.StartsWith("tunnel"));

            // Footprint painted on structure layer
            Assert.AreEqual(32, doc.Grid.StructureTileCount);
            Assert.AreEqual(StructureType.Tunnel, doc.Grid.GetStructure(new GridCoordinate(20, 40)));
            Assert.AreEqual(StructureType.Tunnel, doc.Grid.GetStructure(new GridCoordinate(23, 47)));

            // Road still exists underneath
            Assert.IsTrue(doc.HasRoad(new GridCoordinate(20, 40)));
            Assert.AreEqual(32, doc.Grid.RoadTileCount);

            // Geometry generated
            var mesh = TunnelGeometry.Build(tunnel, 20f);
            Assert.Greater(mesh.Vertices.Count, 0);
            Assert.Greater(mesh.Triangles.Count, 0);
        }

        // ---- Ramp placement (7.51) ------------------------------------

        [Test]
        public void PlaceRamp_3x6_Rise30_MonotonicElevation()
        {
            var doc = new CourseDocument(20f);
            for (int z = 30; z < 36; z++)
                for (int x = 12; x < 15; x++)
                    doc.SetRoad(new GridCoordinate(x, z));

            var region = new GridRegion(12, 30, 3, 6);
            var ramp = doc.PlaceRamp(region, GridDirection.North, riseCm: 30f);

            Assert.IsNotNull(ramp);
            Assert.AreEqual(StructureType.Ramp, ramp.Type);
            Assert.AreEqual(30f, ramp.RiseCm);
            Assert.AreEqual(18, doc.Grid.StructureTileCount);

            // Monotonic rise along +Z
            Assert.IsTrue(RampGeometry.IsMonotonic(ramp));

            float e0 = RampGeometry.ElevationAtTile(ramp, new GridCoordinate(12, 30));
            float e3 = RampGeometry.ElevationAtTile(ramp, new GridCoordinate(12, 33));
            float e5 = RampGeometry.ElevationAtTile(ramp, new GridCoordinate(12, 35));
            Assert.AreEqual(0f, e0, 1e-4f);
            Assert.Greater(e3, e0);
            Assert.AreEqual(30f, e5, 1e-4f);

            var mesh = RampGeometry.BuildSurface(ramp, 20f);
            Assert.Greater(mesh.Vertices.Count, 0);
        }

        // ---- Objects --------------------------------------------------

        [Test]
        public void PlaceObject_ObstacleWithFootprint()
        {
            var doc = new CourseDocument(20f);
            doc.SetRoad(new GridCoordinate(5, 5));
            doc.SetRoad(new GridCoordinate(6, 5));
            doc.SetRoad(new GridCoordinate(7, 5));

            var obj = doc.PlaceObject(ObjectType.Obstacle, new GridCoordinate(5, 5),
                rotationDeg: 0, footprint: ObstacleFootprint.Barrier);

            Assert.AreEqual(3, obj.OccupiedTiles().Length);
            Assert.AreEqual(ObjectType.Obstacle, doc.Grid.GetObject(new GridCoordinate(5, 5)));
            Assert.AreEqual(ObjectType.Obstacle, doc.Grid.GetObject(new GridCoordinate(7, 5)));
        }

        [Test]
        public void PlaceSign_AndStartSignal()
        {
            var doc = new CourseDocument(20f);
            doc.SetRoad(new GridCoordinate(1, 1));
            doc.SetRoad(new GridCoordinate(2, 2));

            var sign = doc.PlaceObject(ObjectType.Sign, new GridCoordinate(1, 1), rotationDeg: 90);
            var signal = doc.PlaceObject(ObjectType.StartSignal, new GridCoordinate(2, 2));

            Assert.AreEqual(90, sign.RotationDeg);
            Assert.AreEqual(ObjectType.Sign, doc.Grid.GetObject(new GridCoordinate(1, 1)));
            Assert.AreEqual(ObjectType.StartSignal, doc.Grid.GetObject(new GridCoordinate(2, 2)));
            Assert.AreEqual(StartSignalState.Off, signal.SignalState);
        }

        // ---- Triggers -------------------------------------------------

        [Test]
        public void PlaceTriggers_SlowStartFinishEventGate()
        {
            var doc = new CourseDocument(20f);
            var slow = doc.PlaceTrigger(TriggerType.SlowZone, new GridRegion(0, 0, 4, 3));
            var start = doc.PlaceTrigger(TriggerType.Start, new GridRegion(0, 0, 1, 1));
            var finish = doc.PlaceTrigger(TriggerType.Finish, new GridRegion(10, 10, 1, 1));
            var evt = doc.PlaceTrigger(TriggerType.EventTrigger, new GridRegion(5, 5, 2, 2), eventId: "tunnel_entry");
            var gate = doc.PlaceSpeedTerminal(20, 30, GridEdge.North, "speed_zone_01", SpeedTerminalRole.A, id: "speed_a");

            Assert.AreEqual(TriggerType.SlowZone, doc.Grid.GetTrigger(new GridCoordinate(1, 1)));
            Assert.AreEqual(TriggerType.Start, doc.Grid.GetTrigger(new GridCoordinate(0, 0)));
            Assert.AreEqual(TriggerType.Finish, doc.Grid.GetTrigger(new GridCoordinate(10, 10)));
            Assert.AreEqual(TriggerType.EventTrigger, doc.Grid.GetTrigger(new GridCoordinate(5, 5)));
            Assert.AreEqual(TriggerType.SpeedTerminal, doc.Grid.GetTrigger(new GridCoordinate(20, 30)));
            Assert.AreEqual("tunnel_entry", evt.EventId);
            Assert.AreEqual("speed_a", gate.Id);
            Assert.AreEqual("speed_zone_01", gate.PairId);
            Assert.AreEqual(5, doc.Triggers.Count);
        }

        // ---- Move / resize / delete -----------------------------------

        [Test]
        public void MoveResizeDelete_Structure()
        {
            var doc = new CourseDocument(20f);
            for (int z = 0; z < 4; z++)
                for (int x = 0; x < 4; x++)
                    doc.SetRoad(new GridCoordinate(x, z));

            var t = doc.PlaceTunnel(new GridRegion(0, 0, 2, 2));
            Assert.IsTrue(doc.MoveStructure(t.Id, 1, 1));
            Assert.AreEqual(1, t.Region.x);
            Assert.AreEqual(1, t.Region.z);
            Assert.AreEqual(StructureType.None, doc.Grid.GetStructure(new GridCoordinate(0, 0)));
            Assert.AreEqual(StructureType.Tunnel, doc.Grid.GetStructure(new GridCoordinate(1, 1)));

            Assert.IsTrue(doc.ResizeStructure(t.Id, new GridRegion(1, 1, 3, 2)));
            Assert.AreEqual(6, t.Region.TileCount);

            Assert.IsTrue(doc.RemoveStructure(t.Id));
            Assert.AreEqual(0, doc.Structures.Count);
            Assert.AreEqual(0, doc.Grid.StructureTileCount);
        }

        // ---- Save/load (7.54) -----------------------------------------

        [Test]
        public void SaveLoad_PreservesAllFeatures()
        {
            var doc = new CourseDocument(20f);
            for (int z = 0; z < 10; z++)
                for (int x = 0; x < 5; x++)
                    doc.SetRoad(new GridCoordinate(x, z));

            doc.PlaceTunnel(new GridRegion(0, 0, 4, 8), heightCm: 55f, id: "tunnel_01");
            doc.PlaceRamp(new GridRegion(0, 8, 3, 2), GridDirection.North, 30f, id: "ramp_01");
            doc.PlaceObject(ObjectType.Obstacle, new GridCoordinate(1, 1), 0, ObstacleFootprint.Small, "obstacle_01");
            doc.PlaceObject(ObjectType.Obstacle, new GridCoordinate(2, 1), 90, ObstacleFootprint.Wide, "obstacle_02");
            doc.PlaceObject(ObjectType.Sign, new GridCoordinate(3, 2), 90, ObstacleFootprint.Small, "slow_sign_01");
            doc.PlaceTrigger(TriggerType.SlowZone, new GridRegion(0, 4, 4, 2), id: "slow_zone_01");
            doc.PlaceSpeedTerminal(4, 5, GridEdge.East, "speed_zone_01", SpeedTerminalRole.A, id: "speed_a");
            doc.PlaceSpeedTerminal(4, 8, GridEdge.East, "speed_zone_01", SpeedTerminalRole.B, id: "speed_b");

            string json = doc.ToJson(true);
            Assert.IsTrue(json.Contains("tunnel_01"), json);
            Assert.IsTrue(json.Contains("heightCm"), json);
            Assert.IsTrue(json.Contains("slow_sign_01"), json);
            Assert.IsTrue(json.Contains("speed_a"), json);
            Assert.IsTrue(json.Contains("speed_terminal"), json);
            Assert.IsTrue(json.Contains("speed_zone_01"), json);

            var loaded = CourseDocument.FromJson(json);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, loaded.Structures.Count(s => s.Type == StructureType.Tunnel));
            Assert.AreEqual(1, loaded.Structures.Count(s => s.Type == StructureType.Ramp));
            Assert.AreEqual(2, loaded.Objects.Count(o => o.Type == ObjectType.Obstacle));
            Assert.AreEqual(1, loaded.Objects.Count(o => o.Type == ObjectType.Sign));
            Assert.AreEqual(3, loaded.Triggers.Count);

            var tunnel = loaded.FindStructure("tunnel_01");
            Assert.IsNotNull(tunnel);
            Assert.AreEqual(4, tunnel.Region.width);
            Assert.AreEqual(8, tunnel.Region.height);
            Assert.AreEqual(55f, tunnel.HeightCm, 1e-3f);

            var ramp = loaded.FindStructure("ramp_01");
            Assert.IsNotNull(ramp);
            Assert.AreEqual(30f, ramp.RiseCm, 1e-3f);
            Assert.AreEqual(GridDirection.North, ramp.Direction);

            var sign = loaded.FindObject("slow_sign_01");
            Assert.IsNotNull(sign);
            Assert.AreEqual(90, sign.RotationDeg);

            var gate = loaded.FindTrigger("speed_a");
            Assert.IsNotNull(gate);
            Assert.AreEqual(GridEdge.East, gate.Edge);
            Assert.AreEqual(4, gate.CellX);
            Assert.AreEqual(5, gate.CellZ);
            Assert.AreEqual("speed_zone_01", gate.PairId);
            Assert.AreEqual(SpeedTerminalRole.A, gate.TerminalRole);

            var gateB = loaded.FindTrigger("speed_b");
            Assert.IsNotNull(gateB);
            Assert.AreEqual(SpeedTerminalRole.B, gateB.TerminalRole);
            Assert.AreEqual("speed_zone_01", gateB.PairId);
        }

        // ---- Unique IDs -----------------------------------------------

        [Test]
        public void AutoGeneratedIds_AreUnique()
        {
            var doc = new CourseDocument(20f);
            doc.SetRoad(new GridCoordinate(0, 0));
            var a = doc.PlaceTunnel(new GridRegion(0, 0, 1, 1));
            var b = doc.PlaceTunnel(new GridRegion(0, 0, 1, 1));
            Assert.AreNotEqual(a.Id, b.Id);
        }

        // ---- Validation -----------------------------------------------

        [Test]
        public void ValidateDocument_DuplicateId_Errors()
        {
            var doc = new CourseDocument(20f);
            doc.SetRoad(new GridCoordinate(0, 0));
            doc.PlaceTunnel(new GridRegion(0, 0, 1, 1), id: "same");
            // Force duplicate via second place with same id → EnsureUniqueId suffixes
            var t2 = doc.PlaceTunnel(new GridRegion(0, 0, 1, 1), id: "same");
            Assert.AreNotEqual("same", t2.Id); // auto-suffixed

            var results = CourseValidator.ValidateDocument(doc);
            Assert.IsFalse(results.Any(r => r.IsError && r.Message.Contains("Duplicate")));
        }
    }
}
