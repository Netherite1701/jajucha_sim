using JajuchaSim.Core;
using JajuchaSim.Course;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Scenario.Tests
{
    /// <summary>
    /// Objective system (Step 10.4–10.7, 10.13, 10.19, 10.37): pass/fail
    /// states, per-objective penalties, missing-terminal failure, and objective
    /// success recording.
    /// </summary>
    public class ObjectiveRuleTests
    {
        private const float Dt = 0.01f;

        private static (SimulationClock clock, SimulationEventBus events, ScenarioManager manager, CourseDocument doc, ScenarioDefinition def) CreateHarness()
        {
            var clock = new SimulationClock(Dt);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));

            var doc = new CourseDocument(20f);
            for (int x = 0; x < 10; x++)
                for (int z = 0; z < 10; z++)
                    doc.SetRoad(new GridCoordinate(x, z));
            doc.PlaceTrigger(TriggerType.Start, new GridRegion(0, 0, 2, 1), id: "start_line");
            doc.PlaceTrigger(TriggerType.Finish, new GridRegion(0, 8, 2, 1), id: "finish_line");
            doc.PlaceTunnel(new GridRegion(4, 3, 2, 1), id: "tunnel_01");
            doc.PlaceObject(ObjectType.Obstacle, new GridCoordinate(6, 5), id: "obstacle_01");
            doc.PlaceSpeedTerminal(0, 4, GridEdge.North, "speed_pair_01", SpeedTerminalRole.A, 2, "speed_a");
            doc.PlaceSpeedTerminal(0, 5, GridEdge.North, "speed_pair_01", SpeedTerminalRole.B, 2, "speed_b");

            var def = ScenarioDefinition.Default();
            def.name = "Objective Test";
            def.startTriggerId = "start_line";
            def.finishTriggerId = "finish_line";
            def.startTimingMode = StartTimingMode.SignalGreen;
            def.redDurationSec = 0.01f;
            def.yellowDurationSec = 0.01f;

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

        private static void SetSample(ScenarioManager m, params Vector3[] points)
        {
            m.GetTelemetry = () => new VehicleTelemetry
            {
                Position = points[0],
                ForwardSpeedCmS = 10f,
                SamplePoints = points
            };
        }

        [Test]
        public void TriggerObjective_PassesOnTriggerEntered()
        {
            var (c, e, m, _, def) = CreateHarness();
            def.objectives.Add(new ObjectiveDefinition
            {
                id = "start_signal",
                type = ObjectiveType.Trigger,
                targetId = "start_line"
            });
            m.RequestStart(StartMode.Immediate);

            e.Publish(new TriggerEnteredEvent(default, TriggerType.Start, "start_line"));

            Assert.AreEqual(ObjectiveState.Passed, m.Session.Objectives[0].State);
            Assert.IsTrue(m.Session.Objectives[0].Passed);
        }

        [Test]
        public void FinishObjective_PassesOnFinish()
        {
            var (c, e, m, _, def) = CreateHarness();
            def.objectives.Add(new ObjectiveDefinition
            {
                id = "finish",
                type = ObjectiveType.Finish
            });
            m.RequestStart(StartMode.Immediate);

            e.Publish(new TriggerEnteredEvent(default, TriggerType.Finish, "finish_line"));

            Assert.AreEqual(ScenarioState.Finished, m.State);
            Assert.AreEqual(ObjectiveState.Passed, m.Session.Objectives[0].State);
        }

        [Test]
        public void SpeedPairObjective_FailsWhenMeasurementExceedsLimit()
        {
            var (c, e, m, _, def) = CreateHarness();
            def.objectives.Add(new ObjectiveDefinition
            {
                id = "speed_test",
                type = ObjectiveType.SpeedPair,
                pairId = "speed_pair_01",
                maxSpeedCmS = 20f,
                failurePenalty = 5f
            });
            m.RequestStart(StartMode.Immediate);

            var speed = new SpeedMeasurementResult("speed_pair_01", "speed_a", "speed_b", 10.0, 10.81, 20f, 24.69f);
            e.Publish(new SpeedMeasuredEvent(speed));

            Assert.AreEqual(ObjectiveState.Failed, m.Session.Objectives[0].State);
            Assert.AreEqual(1, m.Session.Penalties.Count);
            Assert.AreEqual(5f, m.Session.Penalties[0].Value, 1e-4f);
            Assert.AreEqual("objective_failure", m.Session.Penalties[0].EventType);
            Assert.AreEqual("speed_test", m.Session.Penalties[0].TargetId);
        }

        [Test]
        public void SpeedPairObjective_PassesWithinLimit()
        {
            var (c, e, m, _, def) = CreateHarness();
            def.objectives.Add(new ObjectiveDefinition
            {
                id = "speed_test",
                type = ObjectiveType.SpeedPair,
                pairId = "speed_pair_01",
                maxSpeedCmS = 20f
            });
            m.RequestStart(StartMode.Immediate);

            var speed = new SpeedMeasurementResult("speed_pair_01", "speed_a", "speed_b", 10.0, 11.11, 20f, 18.0f);
            e.Publish(new SpeedMeasuredEvent(speed));

            Assert.AreEqual(ObjectiveState.Passed, m.Session.Objectives[0].State);
            Assert.AreEqual(0, m.Session.Penalties.Count);
        }

        [Test]
        public void SpeedPairObjective_MissingTerminalMeasurement_FailsAtFinish()
        {
            var (c, e, m, _, def) = CreateHarness();
            def.objectives.Add(new ObjectiveDefinition
            {
                id = "speed_test",
                type = ObjectiveType.SpeedPair,
                pairId = "speed_pair_01",
                maxSpeedCmS = 20f,
                failurePenalty = 5f
            });
            m.RequestStart(StartMode.Immediate);

            // No measurement published. Finish → objective must FAIL (Step 10.13).
            e.Publish(new TriggerEnteredEvent(default, TriggerType.Finish, "finish_line"));

            Assert.AreEqual(ObjectiveState.Failed, m.Session.Objectives[0].State);
            Assert.IsTrue(m.Session.Objectives[0].Failed);
            Assert.AreEqual(1, m.Session.Penalties.Count);
        }

        [Test]
        public void PassStructureObjective_PassesOnEnterExit()
        {
            var (c, e, m, _, def) = CreateHarness();
            def.objectives.Add(new ObjectiveDefinition
            {
                id = "tunnel_objective",
                type = ObjectiveType.PassStructure,
                targetId = "tunnel_01"
            });
            m.RequestStart(StartMode.Immediate);

            // Enter the tunnel (tile 4,3 world center 90,70 cm).
            SetSample(m, new Vector3(90, 0, 70));
            Tick(m, c, 5);
            Assert.AreEqual(ObjectiveState.Active, m.Session.Objectives[0].State);

            // Exit the tunnel.
            SetSample(m, new Vector3(50, 0, 50));
            Tick(m, c, 5);
            Assert.AreEqual(ObjectiveState.Passed, m.Session.Objectives[0].State);
            Assert.AreEqual(0, m.Session.Penalties.Count);
        }

        [Test]
        public void PassStructureObjective_FailsOnCollision_WithPerObjectivePenalty()
        {
            var (c, e, m, _, def) = CreateHarness();
            def.objectives.Add(new ObjectiveDefinition
            {
                id = "tunnel_objective",
                type = ObjectiveType.PassStructure,
                targetId = "tunnel_01",
                failurePenalty = 15f // per-objective override (Step 10.19)
            });
            m.RequestStart(StartMode.Immediate);

            SetSample(m, new Vector3(90, 0, 70));
            Tick(m, c, 5);
            Assert.AreEqual(ObjectiveState.Active, m.Session.Objectives[0].State);

            e.Publish(new VehicleCollisionEvent("tunnel_01", 12f, c.Time, c.Tick));

            Assert.AreEqual(ObjectiveState.Failed, m.Session.Objectives[0].State);
            Assert.AreEqual(15f, m.Session.Objectives[0].Penalty, 1e-4f);
            Assert.AreEqual(15f, m.Session.Penalties[0].Value, 1e-4f);
        }

        [Test]
        public void AvoidObjectObjective_FailsOnCollision()
        {
            var (c, e, m, _, def) = CreateHarness();
            def.objectives.Add(new ObjectiveDefinition
            {
                id = "obstacle_section",
                type = ObjectiveType.AvoidObject,
                targetId = "obstacle_01"
            });
            m.RequestStart(StartMode.Immediate);

            e.Publish(new VehicleCollisionEvent("obstacle_01", 15f, c.Time, c.Tick));

            Assert.AreEqual(ObjectiveState.Failed, m.Session.Objectives[0].State);
        }

        [Test]
        public void AvoidObjectObjective_PassesAtFinish_WhenEnteredWithoutCollision()
        {
            var (c, e, m, _, def) = CreateHarness();
            def.objectives.Add(new ObjectiveDefinition
            {
                id = "obstacle_section",
                type = ObjectiveType.AvoidObject,
                targetId = "obstacle_01"
            });
            m.RequestStart(StartMode.Immediate);

            // Vehicle footprint enters the obstacle's tile (6,5) world (130,110).
            SetSample(m, new Vector3(130, 0, 110));
            Tick(m, c, 5);
            Assert.AreEqual(ObjectiveState.Active, m.Session.Objectives[0].State);

            e.Publish(new TriggerEnteredEvent(default, TriggerType.Finish, "finish_line"));

            Assert.AreEqual(ObjectiveState.Passed, m.Session.Objectives[0].State);
        }

        [Test]
        public void AvoidObjectObjective_Skipped_WhenNeverEntered()
        {
            var (c, e, m, _, def) = CreateHarness();
            def.objectives.Add(new ObjectiveDefinition
            {
                id = "obstacle_section",
                type = ObjectiveType.AvoidObject,
                targetId = "obstacle_01",
                required = false
            });
            m.RequestStart(StartMode.Immediate);

            e.Publish(new TriggerEnteredEvent(default, TriggerType.Finish, "finish_line"));

            Assert.AreEqual(ObjectiveState.Skipped, m.Session.Objectives[0].State);
        }

        [Test]
        public void RequiredObjective_NotAttempted_FailsAtFinish()
        {
            var (c, e, m, _, def) = CreateHarness();
            def.objectives.Add(new ObjectiveDefinition
            {
                id = "tunnel_objective",
                type = ObjectiveType.PassStructure,
                targetId = "tunnel_01",
                required = true,
                failurePenalty = 10f
            });
            m.RequestStart(StartMode.Immediate);

            e.Publish(new TriggerEnteredEvent(default, TriggerType.Finish, "finish_line"));

            Assert.AreEqual(ObjectiveState.Failed, m.Session.Objectives[0].State);
            Assert.AreEqual(10f, m.Session.Objectives[0].Penalty, 1e-4f);
        }

        [Test]
        public void ObjectiveSuccess_RecordedInJson()
        {
            var (c, e, m, _, def) = CreateHarness();
            def.objectives.Add(new ObjectiveDefinition
            {
                id = "start_signal",
                type = ObjectiveType.Trigger,
                targetId = "start_line"
            });
            m.RequestStart(StartMode.Immediate);
            e.Publish(new TriggerEnteredEvent(default, TriggerType.Start, "start_line"));
            e.Publish(new TriggerEnteredEvent(default, TriggerType.Finish, "finish_line"));

            var json = m.BuildResultJson();
            Assert.AreEqual(1, json.objectives.Length);
            Assert.AreEqual("start_signal", json.objectives[0].id);
            Assert.IsTrue(json.objectives[0].passed);
            Assert.AreEqual("passed", json.objectives[0].status);
        }
    }
}
