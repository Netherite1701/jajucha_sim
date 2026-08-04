using System.Collections.Generic;
using JajuchaSim.Course;
using JajuchaSim.Scenario;
using NUnit.Framework;

namespace JajuchaSim.Testing.Tests
{
    /// <summary>
    /// Batch runs (Step 10.26/10.27/10.29/10.31): summary statistics, CSV
    /// export, pass-rate, and regression comparison — all from the official
    /// RunResults.
    /// </summary>
    public class BatchRunnerTests
    {
        private static TestRunResult MakeResult(string runId, string status, float score, int collisions = 0, int lineContacts = 0, int objectiveFailures = 0, bool completed = true, bool passed = true)
        {
            var json = new RunResultJson
            {
                runId = runId,
                status = status,
                completed = completed,
                timedOut = status == "timedout",
                collisions = collisions,
                lineContacts = lineContacts,
                baseScore = 100f,
                totalPenalty = 100f - score,
                score = score,
                elapsedSec = completed ? 55.2 : 0.0,
                objectives = new ObjectiveJson[objectiveFailures > 0 ? objectiveFailures : 0]
            };
            for (int i = 0; i < objectiveFailures; i++)
            {
                json.objectives[i] = new ObjectiveJson { id = $"obj_{i}", status = "failed", passed = false, penalty = 10f };
            }
            return new TestRunResult { RunId = runId, Result = json, Passed = passed };
        }

        [Test]
        public void Aggregate_ComputesSummary()
        {
            var results = new List<TestRunResult>
            {
                MakeResult("run_0001", "completed", 100f),
                MakeResult("run_0002", "completed", 95f, lineContacts: 1),
                MakeResult("run_0003", "completed", 80f, collisions: 1, objectiveFailures: 1),
                MakeResult("run_0004", "timedout", 40f, collisions: 2, objectiveFailures: 3, completed: false)
            };

            var summary = BatchRunner.Aggregate(results);

            Assert.AreEqual(4, summary.Runs);
            Assert.AreEqual(78.75f, summary.AverageScore, 1e-3f);
            Assert.AreEqual(100f, summary.BestScore, 1e-3f);
            Assert.AreEqual(40f, summary.WorstScore, 1e-3f);
            Assert.AreEqual(3, summary.Completed);
            Assert.AreEqual(1, summary.Timeouts);
            Assert.AreEqual(1, summary.LineViolations);
            Assert.AreEqual(3, summary.Collisions);
            Assert.AreEqual(4, summary.ObjectiveFailures);
            Assert.AreEqual(1, summary.PerfectRuns);
        }

        [Test]
        public void BatchCsv_HasHeaderAndRows()
        {
            var results = new List<TestRunResult>
            {
                MakeResult("run_0001", "completed", 100f),
                MakeResult("run_0002", "completed", 95f, lineContacts: 1),
                MakeResult("run_0003", "timedout", 40f, completed: false)
            };

            var summary = BatchRunner.Aggregate(results);
            string csv = summary.ToCsv();

            var lines = csv.Split('\n');
            Assert.AreEqual("run,status,score,time,line_contacts,collisions,objective_failures", lines[0].TrimEnd('\r'));
            Assert.IsTrue(lines[1].StartsWith("1,completed,100,55.2,0,0,0"));
            Assert.IsTrue(lines[2].StartsWith("2,completed,95,55.2,1,0,0"));
            Assert.IsTrue(lines[3].StartsWith("3,timedout,40,,0,0,0"));
        }

        [Test]
        public void BatchRun_WithScriptedController_ProducesSummary()
        {
            var def = ScenarioDefinition.Default();
            def.startMode = StartMode.Immediate;
            def.finishTriggerId = "finish_line";
            def.passCriteria.mustComplete = true;

            var doc = new CourseDocument(20f);
            doc.PlaceTrigger(TriggerType.Finish, new GridRegion(0, 10, 2, 1), id: "finish_line");

            var summary = BatchRunner.Run(def, doc, runCount: 3, baseSeed: 7UL, maxTicks: 100_000, configure: d =>
            {
                d.OnBeforeTick = (tick, time) =>
                {
                    if (tick == 5)
                        d.Events.Publish(new TriggerEnteredEvent(default, TriggerType.Finish, "finish_line"));
                };
            });

            Assert.AreEqual(3, summary.Runs);
            Assert.AreEqual(3, summary.Completed);
            Assert.AreEqual(0, summary.Timeouts);
            Assert.AreEqual(100f, summary.AverageScore, 1e-3f);
            Assert.AreEqual(100f, summary.BestScore, 1e-3f);
            Assert.AreEqual(100f, summary.WorstScore, 1e-3f);
            Assert.AreEqual(3, summary.PassedTests);
            Assert.AreEqual(3, summary.PerfectRuns);
            Assert.AreEqual(3, summary.Results.Count);
        }

        [Test]
        public void RegressionReport_DetectsWorseController()
        {
            var baseline = BatchRunner.Aggregate(new List<TestRunResult>
            {
                MakeResult("b1", "completed", 96f),
                MakeResult("b2", "completed", 96f)
            });
            var current = BatchRunner.Aggregate(new List<TestRunResult>
            {
                MakeResult("c1", "completed", 82f),
                MakeResult("c2", "timedout", 40f, completed: false)
            });

            var report = new RegressionReport(baseline, current);
            Assert.IsTrue(report.IsRegression);
            Assert.IsTrue(report.ToReportText().Contains("REGRESSION DETECTED"));
        }

        [Test]
        public void RegressionReport_NoRegression_WhenEqual()
        {
            var a = BatchRunner.Aggregate(new List<TestRunResult> { MakeResult("a1", "completed", 96f) });
            var b = BatchRunner.Aggregate(new List<TestRunResult> { MakeResult("b1", "completed", 96f) });

            var report = new RegressionReport(a, b);
            Assert.IsFalse(report.IsRegression);
            Assert.IsTrue(report.ToReportText().Contains("NO REGRESSION"));
        }

        [Test]
        public void BatchSummary_ToSummaryText_IsInformative()
        {
            var summary = BatchRunner.Aggregate(new List<TestRunResult> { MakeResult("r1", "completed", 90f) });
            string text = summary.ToSummaryText();
            Assert.IsTrue(text.Contains("BATCH"));
            Assert.IsTrue(text.Contains("Average Score"));
            Assert.IsTrue(text.Contains("Perfect Runs"));
        }
    }
}
