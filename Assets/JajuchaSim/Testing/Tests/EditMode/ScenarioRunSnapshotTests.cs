using JajuchaSim.Core;
using JajuchaSim.Course;
using JajuchaSim.Scenario;
using NUnit.Framework;

namespace JajuchaSim.Testing.Tests
{
    /// <summary>
    /// Debug re-run snapshot (Step 10.33): same course + same scenario + same
    /// seed, restored later for a 1× speed re-run with full runtime UI.
    /// </summary>
    public class ScenarioRunSnapshotTests
    {
        [Test]
        public void Capture_Restore_PreservesCourseScenarioAndSeed()
        {
            var clock = new SimulationClock(0.01f);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));

            var doc = new CourseDocument(20f);
            doc.PlaceTrigger(TriggerType.Finish, new GridRegion(0, 10, 2, 1), id: "finish_line");
            var def = ScenarioDefinition.Default();
            def.name = "Debug Run Scenario";
            def.finishTriggerId = "finish_line";
            manager.PrepareRun(def, doc);

            var snap = ScenarioRunSnapshot.Capture(manager, seed: 42UL);

            Assert.AreEqual("run_0001", snap.runId);
            Assert.AreEqual(42UL, snap.seed);
            Assert.IsNotEmpty(snap.courseJson);
            Assert.IsNotEmpty(snap.scenarioJson);

            var restoredDoc = snap.RestoreCourse();
            Assert.IsNotNull(restoredDoc);
            Assert.IsNotNull(restoredDoc.FindTrigger("finish_line"));

            var restoredDef = snap.RestoreScenario();
            Assert.IsNotNull(restoredDef);
            Assert.AreEqual("Debug Run Scenario", restoredDef.name);
            Assert.AreEqual("finish_line", restoredDef.finishTriggerId);
        }

        [Test]
        public void Restore_EmptySnapshot_ReturnsNull()
        {
            var snap = new ScenarioRunSnapshot();
            Assert.IsNull(snap.RestoreCourse());
            Assert.IsNull(snap.RestoreScenario());
        }
    }
}
