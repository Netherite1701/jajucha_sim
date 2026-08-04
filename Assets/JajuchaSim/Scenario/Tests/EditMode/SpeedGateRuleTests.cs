using JajuchaSim.Core;
using JajuchaSim.Course;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Scenario.Tests
{
    /// <summary>
    /// Speed-gate pair measurement into the run session (Step 8.21/8.22,
    /// 8.53). The official value v = d / (t2 - t1) comes from
    /// <see cref="SpeedMeasuredEvent"/> (Step 7/8), never Rigidbody velocity.
    /// </summary>
    public class SpeedGateRuleTests
    {
        private const float Dt = 0.01f;

        [Test]
        public void SpeedGate_MeasurementRecordedIntoSession()
        {
            var clock = new SimulationClock(Dt);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));

            var doc = new CourseDocument(20f);
            doc.PlaceSpeedTerminal(0, 5, GridEdge.North, "speed_pair_01", SpeedTerminalRole.A, 2, "speed_a");
            doc.PlaceSpeedTerminal(0, 6, GridEdge.North, "speed_pair_01", SpeedTerminalRole.B, 2, "speed_b");
            doc.PlaceTrigger(TriggerType.Finish, new GridRegion(0, 10, 2, 1), id: "finish_line");

            var def = ScenarioDefinition.Default();
            def.finishTriggerId = "finish_line";
            manager.PrepareRun(def, doc);
            manager.RequestStart(StartMode.Immediate);

            events.Publish(new SpeedTerminalCrossedEvent(
                "speed_a", "speed_pair_01", SpeedTerminalRole.A,
                14.21, new Vector3(0, 0, 90), new Vector3(0, 0, 100), 0.5f));
            events.Publish(new SpeedTerminalCrossedEvent(
                "speed_b", "speed_pair_01", SpeedTerminalRole.B,
                15.02, new Vector3(0, 0, 190), new Vector3(0, 0, 200), 0.5f));

            var result = new SpeedMeasurementResult(
                "speed_pair_01", "speed_a", "speed_b",
                14.21, 15.02, 20f, 24.69f);
            events.Publish(new SpeedMeasuredEvent(result));

            events.Publish(new TriggerEnteredEvent(default, TriggerType.Finish, "finish_line"));

            Assert.AreEqual(1, manager.Session.Measurements.Count);
            var m = manager.Session.Measurements[0];
            Assert.AreEqual("speed_pair_01", m.PairId);
            Assert.AreEqual("speed_a", m.FirstGate);
            Assert.AreEqual("speed_b", m.SecondGate);
            Assert.AreEqual(20f, m.DistanceCm, 1e-3f);
            Assert.AreEqual(14.21, m.StartTime, 1e-6);
            Assert.AreEqual(15.02, m.EndTime, 1e-6);
            Assert.AreEqual(24.69f, m.AverageSpeedCmS, 1e-3f);
        }

        [Test]
        public void SpeedGate_CrossEvents_LoggedWithTimestamps()
        {
            var clock = new SimulationClock(Dt);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));

            var doc = new CourseDocument(20f);
            var def = ScenarioDefinition.Default();
            manager.PrepareRun(def, doc);
            manager.RequestStart(StartMode.Immediate);

            events.Publish(new SpeedTerminalCrossedEvent(
                "speed_a", "speed_pair_01", SpeedTerminalRole.A,
                12.0, Vector3.zero, Vector3.forward, 0.5f));

            bool foundCross = false;
            foreach (var ev in manager.Session.Events)
            {
                if (ev.Message.Contains("speed_a CROSS") && ev.SimulationTime > 0.0)
                    foundCross = true;
            }
            Assert.IsTrue(foundCross);
        }
    }
}
