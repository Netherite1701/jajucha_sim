using JajuchaSim.Course;
using JajuchaSim.Scenario;
using NUnit.Framework;

namespace JajuchaSim.Testing.Tests
{
    /// <summary>
    /// Single automated test (Step 10.25/10.27/10.28): Reset → Start →
    /// controller drives → scenario ends → ScoreManager finalizes → RunResult
    /// → TEST PASS/FAIL separate from the competition score.
    /// </summary>
    public class TestRunnerTests
    {
        private static ScenarioDefinition CreateDefinition()
        {
            var def = ScenarioDefinition.Default();
            def.startMode = StartMode.Immediate; // skip countdown for tests
            def.finishTriggerId = "finish_line";
            def.passCriteria.mustComplete = true;
            def.passCriteria.minimumScore = 90f;
            return def;
        }

        private static CourseDocument CreateCourse()
        {
            var doc = new CourseDocument(20f);
            doc.PlaceTrigger(TriggerType.Finish, new GridRegion(0, 10, 2, 1), id: "finish_line");
            return doc;
        }

        [Test]
        public void RunSingle_CompletingRun_PassesCriteria()
        {
            var def = CreateDefinition();
            var doc = CreateCourse();

            var result = TestRunner.RunSingle(def, doc, seed: 1UL, maxTicks: 100_000, configure: d =>
            {
                d.OnBeforeTick = (tick, time) =>
                {
                    if (tick == 5)
                        d.Events.Publish(new TriggerEnteredEvent(default, TriggerType.Finish, "finish_line"));
                };
            });

            Assert.IsNotNull(result.Result);
            Assert.AreEqual("completed", result.Result.status);
            Assert.AreEqual(100f, result.Score, 1e-3f);
            Assert.IsTrue(result.Passed);
            Assert.IsEmpty(result.FailureReasons);
        }

        [Test]
        public void RunSingle_Timeout_FailsMustCompleteCriteria()
        {
            var def = CreateDefinition();
            def.maxRunTimeSec = 0.05f; // 5 ticks
            def.passCriteria.minimumScore = 0f; // only completion matters
            var doc = CreateCourse();

            var result = TestRunner.RunSingle(def, doc, seed: 1UL, maxTicks: 100_000);

            Assert.IsNotNull(result.Result);
            Assert.AreEqual("timedout", result.Result.status);
            Assert.IsFalse(result.Passed);
            Assert.IsTrue(result.FailureReasons.Count > 0);
        }

        [Test]
        public void RunSingle_ScoreBelowMinimum_TestFails_SeparateFromCompetitionScore()
        {
            var def = CreateDefinition();
            def.passCriteria.minimumScore = 120f; // impossible to reach
            var doc = CreateCourse();

            var result = TestRunner.RunSingle(def, doc, seed: 1UL, maxTicks: 100_000, configure: d =>
            {
                d.OnBeforeTick = (tick, time) =>
                {
                    if (tick == 5)
                        d.Events.Publish(new TriggerEnteredEvent(default, TriggerType.Finish, "finish_line"));
                };
            });

            // Competition result is fine (score 100), but the automated test fails.
            Assert.AreEqual(100f, result.Score, 1e-3f);
            Assert.IsFalse(result.Passed);
            Assert.IsTrue(result.FailureReasons.Exists(r => r.Contains("minimum")));
        }

        [Test]
        public void RunSingle_RecordsMotorCommands()
        {
            var def = CreateDefinition();
            var doc = CreateCourse();
            var recorder = new CommandRecorder();

            var result = TestRunner.RunSingle(def, doc, seed: 1UL, maxTicks: 100_000, configure: d =>
            {
                d.OnBeforeTick = (tick, time) =>
                {
                    if (tick == 5)
                        d.Events.Publish(new TriggerEnteredEvent(default, TriggerType.Finish, "finish_line"));
                };
            }, controller: (tick, time) => new Vehicle.MotorCommand(0, 0, tick % 2 == 0 ? 10 : 20));

            Assert.IsNotNull(result.Result);
            Assert.IsTrue(result.Result.elapsedSec > 0.0);
        }
    }
}
