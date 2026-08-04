using JajuchaSim.Core;
using JajuchaSim.Course;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Scenario.Tests
{
    /// <summary>
    /// Course-departure scoring (Step 10.8): footprint outside the road layer
    /// is a debounced COURSE_DEPARTURE episode with configurable penalty.
    /// </summary>
    public class CourseDepartureRuleTests
    {
        private const float Dt = 0.01f;

        private static (SimulationClock clock, SimulationEventBus events, ScenarioManager manager, CourseDocument doc, ScenarioDefinition def) CreateHarness()
        {
            var clock = new SimulationClock(Dt);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));

            var doc = new CourseDocument(20f);
            for (int x = 0; x < 5; x++)
                for (int z = 0; z < 5; z++)
                    doc.SetRoad(new GridCoordinate(x, z));

            var def = ScenarioDefinition.Default();
            def.name = "Course Departure Test";
            def.startTimingMode = StartTimingMode.SignalGreen;
            def.redDurationSec = 0.01f;
            def.yellowDurationSec = 0.01f;
            def.scoring.courseDeparturePenalty = 6f;

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
        public void CourseDeparture_FootprintOutsideRoad_ViolationWithPenalty()
        {
            var (c, e, m, _, _) = CreateHarness();
            m.RequestStart(StartMode.Immediate);
            // (150, 150) cm → tile (7,7) — outside the 5×5 road block.
            m.GetTelemetry = () => VehicleTelemetry.At(new Vector3(150, 0, 150), 10f);

            Tick(m, c, 5);

            Assert.AreEqual(1, m.Session.CourseDepartureCount);
            Assert.AreEqual(1, m.Score.Result.CourseDepartureCount);
            Assert.AreEqual(1, m.Session.Penalties.Count);
            Assert.AreEqual(6f, m.Session.Penalties[0].Value, 1e-4f);
            Assert.AreEqual("course_departure", m.Session.Penalties[0].EventType);
        }

        [Test]
        public void CourseDeparture_OnRoad_NoViolation()
        {
            var (c, e, m, _, _) = CreateHarness();
            m.RequestStart(StartMode.Immediate);
            m.GetTelemetry = () => VehicleTelemetry.At(new Vector3(50, 0, 50), 10f); // tile (2,2) on road

            Tick(m, c, 50);

            Assert.AreEqual(0, m.Session.CourseDepartureCount);
            Assert.AreEqual(0, m.Session.Penalties.Count);
        }

        [Test]
        public void CourseDeparture_Debounce_StaysOutside_OneViolation()
        {
            var (c, e, m, _, _) = CreateHarness();
            m.RequestStart(StartMode.Immediate);
            m.GetTelemetry = () => VehicleTelemetry.At(new Vector3(150, 0, 150), 10f);

            Tick(m, c, 200);

            Assert.AreEqual(1, m.Session.CourseDepartureCount);
            Assert.AreEqual(1, m.Session.Penalties.Count);
        }

        [Test]
        public void CourseDeparture_EpisodeEnds_NewDeparture_NewViolation()
        {
            var (c, e, m, _, _) = CreateHarness();
            m.RequestStart(StartMode.Immediate);

            m.GetTelemetry = () => VehicleTelemetry.At(new Vector3(150, 0, 150), 10f);
            Tick(m, c, 5);
            Assert.AreEqual(1, m.Session.CourseDepartureCount);

            m.GetTelemetry = () => VehicleTelemetry.At(new Vector3(50, 0, 50), 10f);
            Tick(m, c, 5);
            Assert.AreEqual(1, m.Session.CourseDepartureCount);

            m.GetTelemetry = () => VehicleTelemetry.At(new Vector3(150, 0, 150), 10f);
            Tick(m, c, 5);

            Assert.AreEqual(2, m.Session.CourseDepartureCount);
            Assert.AreEqual(2, m.Session.Penalties.Count);
        }

        [Test]
        public void CourseDeparture_MinorityOutside_NotDeparture()
        {
            var (c, e, m, _, _) = CreateHarness();
            m.RequestStart(StartMode.Immediate);
            // 3 sample points: 1 outside (fraction 0.33 ≤ 0.5) → no departure.
            m.GetTelemetry = () => new VehicleTelemetry
            {
                Position = new Vector3(50, 0, 50),
                ForwardSpeedCmS = 10f,
                SamplePoints = new[]
                {
                    new Vector3(50, 0, 50),   // tile (2,2) road
                    new Vector3(60, 0, 50),   // tile (3,2) road
                    new Vector3(150, 0, 50)   // tile (7,2) outside
                }
            };

            Tick(m, c, 10);

            Assert.AreEqual(0, m.Session.CourseDepartureCount);
        }

        [Test]
        public void CourseDeparture_MajorityOutside_Departure()
        {
            var (c, e, m, _, _) = CreateHarness();
            m.RequestStart(StartMode.Immediate);
            // 3 sample points: 2 outside (fraction 0.67 > 0.5) → departure.
            m.GetTelemetry = () => new VehicleTelemetry
            {
                Position = new Vector3(50, 0, 50),
                ForwardSpeedCmS = 10f,
                SamplePoints = new[]
                {
                    new Vector3(150, 0, 50),  // outside
                    new Vector3(60, 0, 50),   // road
                    new Vector3(150, 0, 150)  // outside
                }
            };

            Tick(m, c, 10);

            Assert.AreEqual(1, m.Session.CourseDepartureCount);
        }
    }
}
