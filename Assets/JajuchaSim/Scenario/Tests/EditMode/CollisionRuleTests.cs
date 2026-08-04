using JajuchaSim.Core;
using JajuchaSim.Course;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Scenario.Tests
{
    /// <summary>
    /// Collision recording + debounce (Step 8.17–8.20, 8.61).
    /// A car resting against one obstacle must produce one incident, not 200.
    /// </summary>
    public class CollisionRuleTests
    {
        private const float Dt = 0.01f;

        // ---- 8.61: debounce via collision sessions ----------------------

        [Test]
        public void Debounce_StayTouching2Seconds_OneIncident()
        {
            var tracker = new CollisionSessionTracker();

            // Begin collision
            Assert.IsTrue(tracker.OnCollisionBegin("obstacle_01"));
            Assert.AreEqual(1, tracker.IncidentCount);

            // Stay touching for 2 seconds of callbacks (~200 ticks @ 100 Hz)
            for (int i = 0; i < 200; i++)
            {
                Assert.IsFalse(tracker.OnCollisionBegin("obstacle_01"));
            }
            Assert.AreEqual(1, tracker.IncidentCount);

            // Separate → session ends
            tracker.OnCollisionEnd("obstacle_01");

            // New contact → new incident
            Assert.IsTrue(tracker.OnCollisionBegin("obstacle_01"));
            Assert.AreEqual(2, tracker.IncidentCount);
        }

        [Test]
        public void Debounce_MultipleObjects_TrackedIndependently()
        {
            var tracker = new CollisionSessionTracker();
            tracker.OnCollisionBegin("obstacle_01");
            tracker.OnCollisionBegin("obstacle_02");
            Assert.AreEqual(2, tracker.IncidentCount);

            // Repeated begin for obstacle_01 while touching → no new incident
            tracker.OnCollisionBegin("obstacle_01");
            Assert.AreEqual(2, tracker.IncidentCount);

            tracker.OnCollisionEnd("obstacle_02");
            tracker.OnCollisionBegin("obstacle_02");
            Assert.AreEqual(3, tracker.IncidentCount);
        }

        [Test]
        public void Debounce_Reset_ClearsState()
        {
            var tracker = new CollisionSessionTracker();
            tracker.OnCollisionBegin("obstacle_01");
            tracker.Reset();
            Assert.AreEqual(0, tracker.IncidentCount);
            Assert.IsTrue(tracker.OnCollisionBegin("obstacle_01"));
        }

        // ---- 8.18/8.20: rule records published collisions ---------------

        [Test]
        public void CollisionRule_RecordsIncidentIntoSession()
        {
            var clock = new SimulationClock(Dt);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));

            var doc = new CourseDocument(20f);
            doc.PlaceTrigger(TriggerType.Finish, new GridRegion(0, 10, 2, 1), id: "finish_line");

            var def = ScenarioDefinition.Default();
            def.finishTriggerId = "finish_line";
            manager.PrepareRun(def, doc);
            manager.RequestStart(StartMode.Immediate);

            events.Publish(new VehicleCollisionEvent("obstacle_01", 18.2f, clock.Time, clock.Tick));
            events.Publish(new VehicleCollisionEvent("tunnel_wall", 5.1f, clock.Time, clock.Tick));

            // Finish → finalize copies incidents into the session.
            events.Publish(new TriggerEnteredEvent(default, TriggerType.Finish, "finish_line"));

            Assert.AreEqual(2, manager.Session.Collisions.Count);
            Assert.AreEqual("obstacle_01", manager.Session.Collisions[0].ObjectId);
            Assert.AreEqual(18.2f, manager.Session.Collisions[0].RelativeVelocityCmS, 1e-4f);
            Assert.AreEqual(2, manager.Score.Result.CollisionCount);
        }

        [Test]
        public void CollisionRule_PenaltyMode_RecordsPenalty()
        {
            var clock = new SimulationClock(Dt);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));

            var doc = new CourseDocument(20f);
            var def = ScenarioDefinition.Default();
            def.collisions.violationMode = ViolationMode.Penalty;
            def.collisions.penalty = 4f;
            manager.PrepareRun(def, doc);
            manager.RequestStart(StartMode.Immediate);

            events.Publish(new VehicleCollisionEvent("obstacle_01", 12f, clock.Time, clock.Tick));

            Assert.AreEqual(1, manager.Session.Penalties.Count);
            Assert.AreEqual(4f, manager.Session.Penalties[0].Value, 1e-4f);
        }

        [Test]
        public void CollisionRule_InformationalMode_NoPenalty()
        {
            var clock = new SimulationClock(Dt);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));

            var doc = new CourseDocument(20f);
            var def = ScenarioDefinition.Default();
            def.collisions.violationMode = ViolationMode.Informational;
            manager.PrepareRun(def, doc);
            manager.RequestStart(StartMode.Immediate);

            events.Publish(new VehicleCollisionEvent("obstacle_01", 12f, clock.Time, clock.Tick));

            Assert.AreEqual(0, manager.Session.Penalties.Count);
        }

        // ---- 8.51: collisions after finish do not alter result ----------

        [Test]
        public void Collision_AfterFinish_NotRecorded()
        {
            var clock = new SimulationClock(Dt);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));

            var doc = new CourseDocument(20f);
            doc.PlaceTrigger(TriggerType.Finish, new GridRegion(0, 10, 2, 1), id: "finish_line");
            var def = ScenarioDefinition.Default();
            def.finishTriggerId = "finish_line";
            manager.PrepareRun(def, doc);
            manager.RequestStart(StartMode.Immediate);

            events.Publish(new TriggerEnteredEvent(default, TriggerType.Finish, "finish_line"));
            Assert.AreEqual(ScenarioState.Finished, manager.State);
            int count = manager.Session.Collisions.Count;

            events.Publish(new VehicleCollisionEvent("obstacle_02", 20f, clock.Time, clock.Tick));

            Assert.AreEqual(count, manager.Session.Collisions.Count);
            Assert.AreEqual(0, manager.Score.Result.CollisionCount);
        }
    }
}
