using JajuchaSim.Core;
using JajuchaSim.Course;
using JajuchaSim.Scenario;
using NUnit.Framework;

namespace JajuchaSim.Testing.Tests
{
    /// <summary>
    /// Debug re-run (Step 10.33): same course + same scenario + same seed at
    /// 1× speed via the captured snapshot.
    /// </summary>
    public class DebugReRunTests
    {
        private static ScenarioManager PrepareFinishedManager(out CourseDocument docOut)
        {
            var clock = new SimulationClock(0.01f);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));

            var doc = new CourseDocument(20f);
            doc.PlaceTrigger(TriggerType.Finish, new GridRegion(0, 10, 2, 1), id: "finish_line");
            var def = ScenarioDefinition.Default();
            def.courseId = "debug_course";
            def.scenarioId = "debug_scenario";
            def.finishTriggerId = "finish_line";
            // Immediate start + very short max time so the re-run terminates
            // quickly without an external controller publishing the finish.
            def.startMode = StartMode.Immediate;
            def.maxRunTimeSec = 0.05f;
            manager.PrepareRun(def, doc);
            manager.RequestStart(StartMode.Immediate);
            events.Publish(new TriggerEnteredEvent(default, TriggerType.Finish, "finish_line"));
            docOut = doc;
            return manager;
        }

        [Test]
        public void ReRun_UsesSameCourseAndScenario()
        {
            var manager = PrepareFinishedManager(out _);
            Assert.AreEqual(ScenarioState.Finished, manager.State);

            var result = DebugReRun.ReRun(manager, seed: 99UL, maxTicks: 100_000);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Result);
            Assert.AreEqual("debug_course", result.Result.course);
            Assert.AreEqual("debug_scenario", result.Result.scenario);
            Assert.AreEqual("timedout", result.Result.status); // no controller → timeout path
        }

        [Test]
        public void ReRun_NullManager_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => DebugReRun.ReRun(null));
        }
    }
}
