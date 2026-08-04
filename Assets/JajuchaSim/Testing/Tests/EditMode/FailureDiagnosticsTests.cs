using System.IO;
using JajuchaSim.Core;
using JajuchaSim.Course;
using JajuchaSim.Scenario;
using JajuchaSim.Vehicle;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Testing.Tests
{
    /// <summary>
    /// Failure diagnostics (Step 10.32): event log, penalty log, motor trace,
    /// final pose, objective states; JSON save/load.
    /// </summary>
    public class FailureDiagnosticsTests
    {
        private static (ScenarioManager manager, CommandRecorder recorder) RunFinishedScenario()
        {
            var clock = new SimulationClock(0.01f);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));

            var doc = new CourseDocument(20f);
            doc.PlaceTrigger(TriggerType.Finish, new GridRegion(0, 10, 2, 1), id: "finish_line");

            var def = ScenarioDefinition.Default();
            def.finishTriggerId = "finish_line";
            def.objectives.Add(new ObjectiveDefinition
            {
                id = "speed_test",
                type = ObjectiveType.SpeedPair,
                pairId = "speed_pair_01",
                maxSpeedCmS = 20f,
                failurePenalty = 5f
            });
            manager.PrepareRun(def, doc);
            manager.RequestStart(StartMode.Immediate);

            manager.GetTelemetry = () => VehicleTelemetry.At(new Vector3(50, 0, 50), 15f);
            events.Publish(new VehicleCollisionEvent("obstacle_01", 12f, clock.Time, clock.Tick));
            events.Publish(new TriggerEnteredEvent(default, TriggerType.Finish, "finish_line"));

            var recorder = new CommandRecorder();
            recorder.Record(new MotorCommand(0, 0, 15), clock.Tick, clock.Time);
            return (manager, recorder);
        }

        [Test]
        public void Capture_IncludesEventPenaltyObjectiveAndMotorTrace()
        {
            var (manager, recorder) = RunFinishedScenario();
            var json = manager.BuildResultJson();

            var diag = FailureDiagnostics.Capture(json, manager, recorder.Records);
            diag.SetFinalPose(new Vector3(50, 0, 50), Vector3.forward);

            Assert.IsNotEmpty(diag.eventLog);
            Assert.AreEqual(1, diag.penaltyLog.Length);
            // Collision is informational by default; the penalty comes from the
            // failed speed objective (missing terminal measurement).
            Assert.AreEqual("speed_test", diag.penaltyLog[0].targetId);
            Assert.AreEqual("objective_failure", diag.penaltyLog[0].eventType);
            Assert.IsNotEmpty(diag.objectiveStates);
            Assert.IsTrue(diag.motorTrace.Contains("set_motor"));
            Assert.AreEqual("50.0,0.0,50.0", diag.finalPosition);
        }

        [Test]
        public void Capture_WithoutManager_IsEmptySafe()
        {
            var diag = FailureDiagnostics.Capture(null, null, null);
            Assert.IsNotNull(diag);
            Assert.AreEqual(0, diag.eventLog.Length);
            Assert.AreEqual("", diag.motorTrace);
        }

        [Test]
        public void Save_Load_RoundTrips()
        {
            var (manager, recorder) = RunFinishedScenario();
            var json = manager.BuildResultJson();
            var diag = FailureDiagnostics.Capture(json, manager, recorder.Records);

            string path = Path.Combine(Path.GetTempPath(), $"jajucha_diag_{System.Guid.NewGuid():N}.json");
            try
            {
                string written = diag.Save(path);
                Assert.IsNotNull(written);
                Assert.IsTrue(File.Exists(written));

                var loaded = FailureDiagnostics.Load(written);
                Assert.IsNotNull(loaded);
                Assert.AreEqual(diag.runId, loaded.runId);
                Assert.AreEqual(diag.status, loaded.status);
                Assert.AreEqual(diag.score, loaded.score, 1e-3f);
                Assert.AreEqual(diag.penaltyLog.Length, loaded.penaltyLog.Length);
                Assert.IsTrue(loaded.motorTrace.Contains("set_motor"));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
