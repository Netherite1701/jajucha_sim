using JajuchaSim.Core;
using JajuchaSim.Course;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Scenario.Tests
{
    /// <summary>
    /// Line-contact scoring (Step 10.2/10.3): footprint (not just centre)
    /// sampling against boundary-line tiles, debounced violation episodes,
    /// configurable penalty.
    /// </summary>
    public class LineContactRuleTests
    {
        private const float Dt = 0.01f;

        private static (SimulationClock clock, SimulationEventBus events, ScenarioManager manager, CourseDocument doc, ScenarioDefinition def) CreateHarness()
        {
            var clock = new SimulationClock(Dt);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));

            var doc = new CourseDocument(20f);
            for (int x = 0; x < 8; x++)
                for (int z = 0; z < 8; z++)
                    doc.SetRoad(new GridCoordinate(x, z));
            // Boundary line down column x=4 at rows z=2..3.
            doc.SetLine(new GridCoordinate(4, 2));
            doc.SetLine(new GridCoordinate(4, 3));

            var def = ScenarioDefinition.Default();
            def.name = "Line Contact Test";
            def.startTimingMode = StartTimingMode.SignalGreen;
            def.redDurationSec = 0.01f;
            def.yellowDurationSec = 0.01f;
            def.scoring.lineContactPenalty = 5f;

            manager.PrepareRun(def, doc);
            return (clock, events, manager, doc, def);
        }

        private static void Tick(ScenarioManager m, SimulationClock c, int count)
        {
            for (int i = 0; i < count; i++)
            {
                c.AdvanceOneTick();
                m.SimulationTick(Dt);
            }
        }

        [Test]
        public void LineContact_FootprintOnLine_OneViolationWithPenalty()
        {
            var (c, e, m, _, _) = CreateHarness();
            m.RequestStart(StartMode.Immediate);
            // (90, 50) cm → tile (4, 2) which carries a boundary line.
            m.GetTelemetry = () => VehicleTelemetry.At(new Vector3(90, 0, 50), 10f);

            Tick(m, c, 5);

            Assert.AreEqual(1, m.Session.LineContactCount);
            Assert.AreEqual(1, m.Score.Result.LineContactCount);
            Assert.AreEqual(1, m.Session.Penalties.Count);
            Assert.AreEqual(5f, m.Session.Penalties[0].Value, 1e-4f);
            Assert.AreEqual("line_contact", m.Session.Penalties[0].EventType);
            Assert.AreEqual("LineContactRule", m.Session.Penalties[0].RuleId);
        }

        [Test]
        public void LineContact_Debounce_StaysOnLineFor2Seconds_OneViolation()
        {
            var (c, e, m, _, _) = CreateHarness();
            m.RequestStart(StartMode.Immediate);
            m.GetTelemetry = () => VehicleTelemetry.At(new Vector3(90, 0, 50), 10f);

            Tick(m, c, 200); // 2 s of ticks

            Assert.AreEqual(1, m.Session.LineContactCount);
            Assert.AreEqual(1, m.Session.Penalties.Count);
        }

        [Test]
        public void LineContact_EpisodeEnds_NewTouch_NewViolation()
        {
            var (c, e, m, _, _) = CreateHarness();
            m.RequestStart(StartMode.Immediate);

            // Touch the line.
            m.GetTelemetry = () => VehicleTelemetry.At(new Vector3(90, 0, 50), 10f);
            Tick(m, c, 5);
            Assert.AreEqual(1, m.Session.LineContactCount);

            // Leave the line → episode ends.
            m.GetTelemetry = () => VehicleTelemetry.At(new Vector3(50, 0, 50), 10f);
            Tick(m, c, 5);
            Assert.AreEqual(1, m.Session.LineContactCount);

            // Touch again → new violation.
            m.GetTelemetry = () => VehicleTelemetry.At(new Vector3(90, 0, 50), 10f);
            Tick(m, c, 5);

            Assert.AreEqual(2, m.Session.LineContactCount);
            Assert.AreEqual(2, m.Session.Penalties.Count);
        }

        [Test]
        public void LineContact_CornerSample_DetectsEvenWhenCenterClear()
        {
            var (c, e, m, _, _) = CreateHarness();
            m.RequestStart(StartMode.Immediate);
            // Center at tile (3,2) [no line]; front-left corner at tile (4,2) [line].
            m.GetTelemetry = () => new VehicleTelemetry
            {
                Position = new Vector3(70, 0, 50),
                ForwardSpeedCmS = 10f,
                SamplePoints = new[]
                {
                    new Vector3(70, 0, 50),
                    new Vector3(85, 0, 50), // front-right → tile (4,2)
                    new Vector3(85, 0, 40),
                    new Vector3(55, 0, 50),
                    new Vector3(55, 0, 40)
                }
            };

            Tick(m, c, 5);

            Assert.AreEqual(1, m.Session.LineContactCount, "A corner crossing the line must count");
        }

        [Test]
        public void LineContact_NoLineTilesOnMap_NoViolation()
        {
            var clock = new SimulationClock(Dt);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));

            var doc = new CourseDocument(20f);
            for (int x = 0; x < 4; x++)
                for (int z = 0; z < 4; z++)
                    doc.SetRoad(new GridCoordinate(x, z));

            var def = ScenarioDefinition.Default();
            manager.PrepareRun(def, doc);
            manager.RequestStart(StartMode.Immediate);
            manager.GetTelemetry = () => VehicleTelemetry.At(new Vector3(50, 0, 50), 10f);

            for (int i = 0; i < 50; i++) { clock.AdvanceOneTick(); manager.SimulationTick(Dt); }

            Assert.AreEqual(0, manager.Session.LineContactCount);
            Assert.AreEqual(0, manager.Session.Penalties.Count);
        }
    }
}
