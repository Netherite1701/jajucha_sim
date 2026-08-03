using System.Linq;
using JajuchaSim.Core;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Course.Tests
{
    public class EventLogTests
    {
        [Test]
        public void EventLog_RecordsEnterExitAndGate()
        {
            var grid = new CourseGrid(20f);
            grid.SetTrigger(new GridCoordinate(5, 5), TriggerType.SlowZone);
            grid.SetTrigger(new GridCoordinate(10, 10), TriggerType.SpeedGate);

            var bus = new SimulationEventBus();
            var clock = new SimulationClock(0.01f);
            var ctx = new SimulationContext(clock, bus, new SimulationRandom(1));

            var log = new EventLogSystem();
            log.Initialize(ctx);

            var det = new TriggerDetectionSystem(grid);
            det.Initialize(ctx);

            // Enter slow zone
            det.GetVehiclePose = () => new VehiclePose
            {
                Position = new Vector3(110, 0, 110),
                SamplePoints = new[] { new Vector3(110, 0, 110) }
            };
            det.SimulationTick(0.01f);
            clock.Advance(1);

            // Exit
            det.GetVehiclePose = () => new VehiclePose
            {
                Position = new Vector3(0, 0, 0),
                SamplePoints = new[] { new Vector3(0, 0, 0) }
            };
            det.SimulationTick(0.01f);
            clock.Advance(1);

            // Cross gate: north edge of (10,10) at z=220
            det.GetVehiclePose = () => new VehiclePose
            {
                Position = new Vector3(210, 0, 200),
                SamplePoints = new[] { new Vector3(210, 0, 200) }
            };
            det.SimulationTick(0.01f);
            det.GetVehiclePose = () => new VehiclePose
            {
                Position = new Vector3(210, 0, 240),
                SamplePoints = new[] { new Vector3(210, 0, 240) }
            };
            det.SimulationTick(0.01f);

            var lines = log.ToDisplayLines(20);
            Assert.IsTrue(lines.Any(l => l.Contains("ENTER")), "Expected ENTER in log");
            Assert.IsTrue(lines.Any(l => l.Contains("EXIT")), "Expected EXIT in log");
            Assert.IsTrue(lines.Any(l => l.Contains("CROSS")), "Expected CROSS in log");

            log.Shutdown();
            det.Shutdown();
        }

        [Test]
        public void EventLog_GenericEventTrigger_PublishesCourseEvent()
        {
            var doc = new CourseDocument(20f);
            doc.PlaceTrigger(TriggerType.EventTrigger, new GridRegion(5, 5, 1, 1), eventId: "tunnel_entry", id: "evt_01");

            var bus = new SimulationEventBus();
            var ctx = new SimulationContext(new SimulationClock(0.01f), bus, new SimulationRandom(1));

            CourseEventTriggeredEvent? got = null;
            bus.Subscribe<CourseEventTriggeredEvent>(e => got = e);

            var det = new TriggerDetectionSystem(doc);
            det.Initialize(ctx);
            det.GetVehiclePose = () => new VehiclePose
            {
                Position = new Vector3(110, 0, 110),
                SamplePoints = new[] { new Vector3(110, 0, 110) }
            };
            det.SimulationTick(0.01f);

            Assert.IsTrue(got.HasValue);
            Assert.AreEqual("tunnel_entry", got.Value.EventId);
            Assert.IsTrue(got.Value.IsEnter);

            det.Shutdown();
        }

        [Test]
        public void SegmentsIntersect_DetectsCrossing()
        {
            // Horizontal gate at z=220, x=200..220
            var g0 = new Vector3(200, 0, 220);
            var g1 = new Vector3(220, 0, 220);
            // Vertical movement crossing it
            var a0 = new Vector3(210, 0, 200);
            var a1 = new Vector3(210, 0, 240);
            Assert.IsTrue(TriggerDetectionSystem.SegmentsIntersect(a0, a1, g0, g1));

            // Parallel, no cross
            var b0 = new Vector3(210, 0, 200);
            var b1 = new Vector3(210, 0, 210);
            Assert.IsFalse(TriggerDetectionSystem.SegmentsIntersect(b0, b1, g0, g1));
        }
    }
}
