using System.Collections.Generic;
using System.Linq;
using JajuchaSim.Core;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Course.Tests
{
    /// <summary>
    /// Two-terminal competition speed measurement:
    ///   v = d / (t2 - t1) using SimulationClock times, not Rigidbody velocity.
    /// </summary>
    public class SpeedTerminalPairTests
    {
        [Test]
        public void Geometry_DistanceFromWorldPositions()
        {
            // A at (20,30) north edge midpoint: x=210, z=640? 
            // cell (20,30), tile 20cm, north edge mid = (20.5*20, 31*20) = (410, 620)
            var a = TriggerInstance.SpeedTerminal("speed_a", 20, 30, GridEdge.North, "speed_zone_01", SpeedTerminalRole.A);
            var b = TriggerInstance.SpeedTerminal("speed_b", 20, 40, GridEdge.North, "speed_zone_01", SpeedTerminalRole.B);

            float d = SpeedTerminalGeometry.DistanceCm(a, b, 20f);
            // Mid A: ((20+0.5)*20? No - line mid of north edge of cell:
            // p0=(400,620), p1=(420,620), mid=(410,620)
            // Mid B: p0=(400,820), p1=(420,820), mid=(410,820)
            // distance = 200 cm
            Assert.AreEqual(200f, d, 1e-3f);
        }

        [Test]
        public void Geometry_MultiTileWidth_ExtendsLine()
        {
            SpeedTerminalGeometry.GetLineEndpoints(10, 10, GridEdge.North, 3, 20f, out var p0, out var p1);
            // North edge spanning 3 tiles: x=200..260, z=220
            Assert.AreEqual(200f, p0.x, 1e-3f);
            Assert.AreEqual(260f, p1.x, 1e-3f);
            Assert.AreEqual(220f, p0.z, 1e-3f);
            Assert.AreEqual(220f, p1.z, 1e-3f);
        }

        [Test]
        public void PairState_AThenB_ComputesSpeed()
        {
            var state = new SpeedTerminalPairState("speed_zone_01", "speed_a", "speed_b", distanceCm: 20f);

            Assert.IsFalse(state.TryRecordCrossing(SpeedTerminalRole.A, 31.240, out _));
            Assert.IsTrue(state.TryRecordCrossing(SpeedTerminalRole.B, 31.890, out float speed));

            // v = 20 / (31.890 - 31.240) = 20 / 0.65 ≈ 30.769
            Assert.AreEqual(20f / 0.65f, speed, 1e-3f);
            Assert.AreEqual(speed, state.MeasuredSpeedCmS.Value, 1e-3f);
            Assert.AreEqual(31.240, state.T1.Value, 1e-9);
            Assert.AreEqual(31.890, state.T2.Value, 1e-9);
        }

        [Test]
        public void PairState_ReverseOrder_IgnoredByDefault()
        {
            var state = new SpeedTerminalPairState("p", "a", "b", 20f, allowReverse: false);
            Assert.IsFalse(state.TryRecordCrossing(SpeedTerminalRole.B, 10.0, out _));
            Assert.IsFalse(state.HasMeasurement);
            Assert.IsFalse(state.T1.HasValue);
        }

        [Test]
        public void PairState_Reset_ClearsMeasurement()
        {
            var state = new SpeedTerminalPairState("p", "a", "b", 20f);
            state.TryRecordCrossing(SpeedTerminalRole.A, 1.0, out _);
            state.TryRecordCrossing(SpeedTerminalRole.B, 2.0, out _);
            Assert.IsTrue(state.HasMeasurement);

            state.Reset();
            Assert.IsFalse(state.HasMeasurement);
            Assert.IsFalse(state.T1.HasValue);
            Assert.IsFalse(state.T2.HasValue);
        }

        [Test]
        public void BuildFromDocument_PairsByPairId()
        {
            var doc = new CourseDocument(20f);
            doc.PlaceSpeedTerminal(20, 30, GridEdge.North, "speed_zone_01", SpeedTerminalRole.A, id: "speed_a");
            doc.PlaceSpeedTerminal(20, 40, GridEdge.North, "speed_zone_01", SpeedTerminalRole.B, id: "speed_b");

            var pairs = SpeedTerminalPair.BuildFromDocument(doc);
            Assert.AreEqual(1, pairs.Count);
            Assert.AreEqual("speed_zone_01", pairs[0].PairId);
            Assert.AreEqual("speed_a", pairs[0].TerminalA.Id);
            Assert.AreEqual("speed_b", pairs[0].TerminalB.Id);
            Assert.AreEqual(200f, pairs[0].DistanceCm, 1e-3f);
        }

        [Test]
        public void Rule_MeasuresOfficialSpeed_FromTerminalCrossings()
        {
            var doc = new CourseDocument(20f);
            // Terminals 1 tile apart on Z → d = 20 cm (north edges of (10,10) and (10,11))
            doc.PlaceSpeedTerminal(10, 10, GridEdge.North, "speed_zone_01", SpeedTerminalRole.A, id: "speed_a");
            doc.PlaceSpeedTerminal(10, 11, GridEdge.North, "speed_zone_01", SpeedTerminalRole.B, id: "speed_b");

            var bus = new SimulationEventBus();
            var clock = new SimulationClock(0.01f);
            var ctx = new SimulationContext(clock, bus, new SimulationRandom(1));

            var det = new TriggerDetectionSystem(doc);
            var rule = new SpeedTerminalPairRule(doc);
            var log = new EventLogSystem();

            det.Initialize(ctx);
            rule.Initialize(ctx);
            log.Initialize(ctx);

            SpeedMeasuredEvent? measured = null;
            bus.Subscribe<SpeedMeasuredEvent>(e => measured = e);

            // Cross A: north edge of (10,10) at z=220, x in [200,220]
            clock.AdvanceOneTick(); // Time = 0.01
            det.GetVehiclePose = () => Pose(210, 200);
            det.SimulationTick(0.01f);

            clock.AdvanceOneTick(); // Time = 0.02
            det.GetVehiclePose = () => Pose(210, 240); // crosses z=220
            det.SimulationTick(0.01f);

            // Cross B: north edge of (10,11) at z=240
            // Move further so next segment crosses z=240
            clock.AdvanceOneTick(); // 0.03
            det.GetVehiclePose = () => Pose(210, 230);
            det.SimulationTick(0.01f);

            clock.AdvanceOneTick(); // 0.04
            det.GetVehiclePose = () => Pose(210, 250); // crosses z=240
            det.SimulationTick(0.01f);

            Assert.IsTrue(measured.HasValue, "Expected SpeedMeasuredEvent");
            Assert.AreEqual("speed_zone_01", measured.Value.PairId);
            Assert.Greater(measured.Value.SpeedCmS, 0f);
            Assert.AreEqual(20f, measured.Value.DistanceCm, 1e-3f);

            // Official speed must equal d/(t2-t1), not an arbitrary velocity.
            float expected = measured.Value.DistanceCm / (float)(measured.Value.T2 - measured.Value.T1);
            Assert.AreEqual(expected, measured.Value.SpeedCmS, 1e-3f);

            var lines = log.ToDisplayLines(20);
            Assert.IsTrue(lines.Any(l => l.Contains("CROSS")), string.Join(" | ", lines));
            Assert.IsTrue(lines.Any(l => l.Contains("SPEED =")), string.Join(" | ", lines));

            string panel = rule.FormatDebugPanel();
            Assert.IsTrue(panel.Contains("SPEED MEASUREMENT"), panel);
            Assert.IsTrue(panel.Contains("speed_zone_01"), panel);
            Assert.IsTrue(panel.Contains("Measured Speed"), panel);

            log.Shutdown();
            rule.Shutdown();
            det.Shutdown();
        }

        [Test]
        public void Rule_ReverseOrder_DoesNotMeasure()
        {
            var doc = new CourseDocument(20f);
            doc.PlaceSpeedTerminal(10, 10, GridEdge.North, "p1", SpeedTerminalRole.A, id: "a");
            doc.PlaceSpeedTerminal(10, 11, GridEdge.North, "p1", SpeedTerminalRole.B, id: "b");

            var bus = new SimulationEventBus();
            var clock = new SimulationClock(0.01f);
            var ctx = new SimulationContext(clock, bus, new SimulationRandom(1));

            var det = new TriggerDetectionSystem(doc);
            var rule = new SpeedTerminalPairRule(doc) { AllowReverse = false };
            det.Initialize(ctx);
            rule.Initialize(ctx);

            int measureCount = 0;
            bus.Subscribe<SpeedMeasuredEvent>(_ => measureCount++);

            // Cross B first (north of (10,11) at z=240)
            clock.AdvanceOneTick();
            det.GetVehiclePose = () => Pose(210, 230);
            det.SimulationTick(0.01f);
            clock.AdvanceOneTick();
            det.GetVehiclePose = () => Pose(210, 250);
            det.SimulationTick(0.01f);

            // Then A (z=220) going south — still reverse completion path ignored
            clock.AdvanceOneTick();
            det.GetVehiclePose = () => Pose(210, 230);
            det.SimulationTick(0.01f);
            clock.AdvanceOneTick();
            det.GetVehiclePose = () => Pose(210, 210);
            det.SimulationTick(0.01f);

            Assert.AreEqual(0, measureCount);
            Assert.IsFalse(rule.LatestResult.HasValue);

            rule.Shutdown();
            det.Shutdown();
        }

        [Test]
        public void Document_JsonRoundTrip_PreservesPairFields()
        {
            var doc = new CourseDocument(20f);
            doc.PlaceSpeedTerminal(20, 30, GridEdge.North, "speed_zone_01", SpeedTerminalRole.A, widthTiles: 2, id: "speed_a");
            doc.PlaceSpeedTerminal(20, 40, GridEdge.North, "speed_zone_01", SpeedTerminalRole.B, widthTiles: 2, id: "speed_b");

            string json = doc.ToJson(true);
            Assert.IsTrue(json.Contains("\"type\": \"speed_terminal\"") || json.Contains("\"type\":\"speed_terminal\""), json);
            Assert.IsTrue(json.Contains("pairId"), json);

            var loaded = CourseDocument.FromJson(json);
            Assert.IsNotNull(loaded);
            var a = loaded.FindTrigger("speed_a");
            var b = loaded.FindTrigger("speed_b");
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.AreEqual("speed_zone_01", a.PairId);
            Assert.AreEqual(SpeedTerminalRole.B, b.TerminalRole);
            Assert.AreEqual(2, a.WidthTiles);
        }

        [Test]
        public void Document_LoadsLegacySpeedGateJson()
        {
            string json = @"{
              ""tileSizeCm"": 20,
              ""road"": [],
              ""structures"": [],
              ""objects"": [],
              ""triggers"": [
                {
                  ""id"": ""speed_gate_001"",
                  ""type"": ""speed_gate"",
                  ""cellX"": 20,
                  ""cellZ"": 30,
                  ""edge"": ""north""
                }
              ]
            }";

            var doc = CourseDocument.FromJson(json);
            Assert.IsNotNull(doc);
            Assert.AreEqual(1, doc.Triggers.Count);
            Assert.AreEqual(TriggerType.SpeedTerminal, doc.Triggers[0].Type);
            Assert.AreEqual(20, doc.Triggers[0].CellX);
            Assert.AreEqual(30, doc.Triggers[0].CellZ);
        }

        [Test]
        public void Validator_WarnsOnIncompletePair()
        {
            var doc = new CourseDocument(20f);
            doc.PlaceSpeedTerminal(1, 1, GridEdge.North, "zone", SpeedTerminalRole.A, id: "only_a");
            var results = CourseValidator.ValidateDocument(doc);
            Assert.IsTrue(results.Any(r => r.Message.Contains("incomplete")), string.Join("; ", results));
        }

        [Test]
        public void CrossEvent_IncludesPairAndSimTime()
        {
            var doc = new CourseDocument(20f);
            doc.PlaceSpeedTerminal(10, 10, GridEdge.North, "zone", SpeedTerminalRole.A, id: "term_a");

            var bus = new SimulationEventBus();
            var clock = new SimulationClock(0.01f);
            var ctx = new SimulationContext(clock, bus, new SimulationRandom(1));

            var det = new TriggerDetectionSystem(doc);
            det.Initialize(ctx);

            var crosses = new List<SpeedTerminalCrossedEvent>();
            bus.Subscribe<SpeedTerminalCrossedEvent>(e => crosses.Add(e));

            clock.AdvanceOneTick();
            det.GetVehiclePose = () => Pose(210, 200);
            det.SimulationTick(0.01f);
            clock.AdvanceOneTick();
            det.GetVehiclePose = () => Pose(210, 240);
            det.SimulationTick(0.01f);

            Assert.AreEqual(1, crosses.Count);
            Assert.AreEqual("term_a", crosses[0].TerminalId);
            Assert.AreEqual("zone", crosses[0].PairId);
            Assert.AreEqual(SpeedTerminalRole.A, crosses[0].Role);
            Assert.Greater(crosses[0].SimTime, 0.0);

            det.Shutdown();
        }

        private static VehiclePose Pose(float x, float z)
        {
            var p = new Vector3(x, 0f, z);
            return new VehiclePose { Position = p, SamplePoints = new[] { p } };
        }
    }
}
