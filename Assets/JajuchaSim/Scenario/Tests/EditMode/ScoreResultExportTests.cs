using System.IO;
using JajuchaSim.Core;
using JajuchaSim.Course;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Scenario.Tests
{
    /// <summary>
    /// Result export/import (Step 8.34/8.35/8.65), gate measurements
    /// (Step 8.21/8.22), and scenario-definition JSON (Step 8.5).
    /// </summary>
    public class ScoreResultExportTests
    {
        private const float Dt = 0.01f;

        private static RunSession RunScriptedSession(out ScenarioManager manager)
        {
            var clock = new SimulationClock(Dt);
            var events = new SimulationEventBus();
            manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(7UL)));

            var doc = new CourseDocument(20f);
            doc.PlaceTrigger(TriggerType.SlowZone, new GridRegion(0, 3, 2, 1), id: "slow_zone_01");
            doc.PlaceTrigger(TriggerType.Finish, new GridRegion(0, 10, 2, 1), id: "finish_line");
            doc.PlaceSpeedTerminal(0, 5, GridEdge.North, "speed_pair_01", SpeedTerminalRole.A, 2, "speed_a");
            doc.PlaceSpeedTerminal(0, 6, GridEdge.North, "speed_pair_01", SpeedTerminalRole.B, 2, "speed_b");

            var def = ScenarioDefinition.Default();
            def.courseId = "competition_course";
            def.scenarioId = "competition_scenario";
            def.finishTriggerId = "finish_line";
            def.slowZones[0].maxSpeedCmS = 20f;
            manager.PrepareRun(def, doc);
            manager.RequestStart(StartMode.Immediate);

            manager.GetTelemetry = () => VehicleTelemetry.At(new Vector3(50, 0, 50), 18f);
            events.Publish(new TriggerEnteredEvent(default, TriggerType.SlowZone, "slow_zone_01"));
            for (int i = 0; i < 40; i++) { clock.AdvanceOneTick(); manager.SimulationTick(Dt); }
            events.Publish(new TriggerExitedEvent(default, TriggerType.SlowZone, "slow_zone_01"));

            events.Publish(new VehicleCollisionEvent("obstacle_03", 18.2f, clock.Time, clock.Tick));

            // Official speed measurement from paired terminals (Step 8.21).
            var speed = new SpeedMeasurementResult(
                "speed_pair_01", "speed_a", "speed_b",
                clock.Time, clock.Time + 0.81, 20f, 24.69f);
            events.Publish(new SpeedMeasuredEvent(speed));

            events.Publish(new TriggerEnteredEvent(default, TriggerType.Finish, "finish_line"));
            return manager.Session;
        }

        // ---- 8.65: export → reload matches in-memory session ------------

        [Test]
        public void ExportResult_ReloadJson_MatchesInMemorySession()
        {
            var session = RunScriptedSession(out var manager);
            Assert.AreEqual(RunResultStatus.Completed, session.Status);
            Assert.AreEqual(1, session.SlowZones.Count);
            Assert.AreEqual(1, session.Collisions.Count);
            Assert.AreEqual(1, session.Measurements.Count);

            string path = Path.Combine(Path.GetTempPath(), $"jajucha_test_run_{System.Guid.NewGuid():N}.json");
            try
            {
                string written = manager.ExportResult(path);
                Assert.IsNotNull(written);
                Assert.IsTrue(File.Exists(written));

                var loaded = ScenarioManager.LoadResultJson(written);
                Assert.IsNotNull(loaded);

                Assert.AreEqual(session.RunId, loaded.runId);
                Assert.AreEqual(session.CourseId, loaded.course);
                Assert.AreEqual(session.ScenarioId, loaded.scenario);
                Assert.AreEqual(session.Status.ToString().ToLowerInvariant(), loaded.status);
                Assert.AreEqual(session.ElapsedSec, loaded.elapsedSec, 1e-6);
                Assert.IsTrue(loaded.completed);
                Assert.AreEqual(session.Collisions.Count, loaded.collisions);

                Assert.AreEqual(1, loaded.slowZones.Length);
                Assert.AreEqual("slow_zone_01", loaded.slowZones[0].triggerId);
                Assert.IsTrue(loaded.slowZones[0].passed);
                Assert.AreEqual(session.SlowZones[0].MaxSpeedCmS, loaded.slowZones[0].maxSpeedCmS, 1e-4f);

                Assert.AreEqual(1, loaded.speedGates.Length);
                Assert.AreEqual("speed_pair_01", loaded.speedGates[0].pairId);
                Assert.AreEqual(24.69f, loaded.speedGates[0].averageSpeedCmS, 1e-3f);

                Assert.AreEqual(1, loaded.collisionList.Length);
                Assert.AreEqual("obstacle_03", loaded.collisionList[0].objectId);
                Assert.AreEqual(18.2f, loaded.collisionList[0].relativeVelocityCmS, 1e-4f);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void BuildResultJson_IncludesRawMeasurements()
        {
            RunScriptedSession(out var manager);
            var json = manager.BuildResultJson();

            Assert.AreEqual(manager.Session.RunId, json.runId);
            Assert.AreEqual(manager.Session.ElapsedSec, json.elapsedSec, 1e-6);
            Assert.AreEqual(manager.Session.Collisions.Count, json.collisions);
            Assert.AreEqual(manager.Score.Result.TotalPenalty, json.totalPenalty, 1e-4f);
            Assert.AreEqual(manager.Score.Result.Score, json.score, 1e-4f);
        }

        // ---- 8.5: scenario definition JSON round-trip -------------------

        [Test]
        public void ScenarioDefinition_JsonRoundTrip_PreservesConfig()
        {
            var def = ScenarioDefinition.Default();
            def.name = "Competition Run";
            def.courseId = "competition_course";
            def.startTriggerId = "start_line";
            def.finishTriggerId = "finish_line";
            def.maxRunTimeSec = 180f;
            def.redDurationSec = 2f;
            def.yellowDurationSec = 1f;
            def.slowZones[0].maxSpeedCmS = 20f;
            def.slowZones[0].violationMode = ViolationMode.Penalty;
            def.slowZones[0].penalty = 5f;
            def.falseStart.enabled = true;

            string json = def.ToJson();
            var parsed = ScenarioDefinition.FromJson(json);

            Assert.IsNotNull(parsed);
            Assert.AreEqual(def.name, parsed.name);
            Assert.AreEqual(def.startTriggerId, parsed.startTriggerId);
            Assert.AreEqual(def.finishTriggerId, parsed.finishTriggerId);
            Assert.AreEqual(def.maxRunTimeSec, parsed.maxRunTimeSec, 1e-4f);
            Assert.AreEqual(def.redDurationSec, parsed.redDurationSec, 1e-4f);
            Assert.AreEqual(def.yellowDurationSec, parsed.yellowDurationSec, 1e-4f);
            Assert.AreEqual(1, parsed.slowZones.Count);
            Assert.AreEqual(20f, parsed.slowZones[0].maxSpeedCmS, 1e-4f);
            Assert.AreEqual(ViolationMode.Penalty, parsed.slowZones[0].violationMode);
            Assert.AreEqual(5f, parsed.slowZones[0].penalty, 1e-4f);
            Assert.IsTrue(parsed.falseStart.enabled);
        }

        [Test]
        public void ScenarioDefinition_FromJson_Invalid_ReturnsNull()
        {
            Assert.IsNull(ScenarioDefinition.FromJson("{ not json"));
            Assert.IsNull(ScenarioDefinition.FromJson(""));
            Assert.IsNull(ScenarioDefinition.FromJson(null));
        }
    }
}
