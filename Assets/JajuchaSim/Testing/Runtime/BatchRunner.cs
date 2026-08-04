using System;
using System.Collections.Generic;
using JajuchaSim.Course;
using JajuchaSim.Scenario;
using JajuchaSim.Vehicle;

namespace JajuchaSim.Testing
{
    /// <summary>
    /// Batch runner (Step 10.26). Runs the same scenario multiple times with
    /// deterministic seeds and aggregates the OFFICIAL RunResults — the same
    /// scoring path manual runs use. No separate scoring model here.
    ///
    /// Each run gets its own competition score AND its own automated
    /// TEST PASS/FAIL verdict from the pass criteria (Step 10.27/10.28).
    /// </summary>
    public static class BatchRunner
    {
        /// <summary>
        /// Run <paramref name="runCount"/> identical scenarios with seeds
        /// <c>baseSeed, baseSeed+1, …</c> and collect the summary.
        /// </summary>
        /// <param name="definition">Scenario (includes pass criteria).</param>
        /// <param name="document">Course document (shared across runs).</param>
        /// <param name="runCount">Number of runs.</param>
        /// <param name="baseSeed">First run seed; each subsequent run adds 1.</param>
        /// <param name="controller">External controller (optional).</param>
        /// <param name="maxTicks">Per-run tick budget.</param>
        /// <param name="onRun">Optional per-run callback (e.g. export).</param>
        public static BatchSummary Run(
            ScenarioDefinition definition,
            CourseDocument document,
            int runCount,
            ulong baseSeed = 1UL,
            Func<long, double, MotorCommand?> controller = null,
            long maxTicks = 1_000_000,
            Action<int, TestRunResult> onRun = null,
            Action<ScenarioRunDriver> configure = null)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (runCount < 0) throw new ArgumentOutOfRangeException(nameof(runCount), "runCount must be >= 0");

            var results = new List<TestRunResult>(runCount);
            for (int i = 0; i < runCount; i++)
            {
                var r = TestRunner.RunSingle(definition, document, baseSeed + (ulong)i, controller, maxTicks, configure);
                results.Add(r);
                onRun?.Invoke(i, r);
            }
            return Aggregate(results);
        }

        /// <summary>
        /// Aggregate pre-collected per-run results into a <see cref="BatchSummary"/>.
        /// Pure logic — usable for regression comparison and for tests.
        /// </summary>
        public static BatchSummary Aggregate(IReadOnlyList<TestRunResult> results)
        {
            var summary = new BatchSummary();
            if (results == null) return summary;

            summary.Runs = results.Count;
            if (results.Count == 0) return summary;

            float total = 0f;
            bool first = true;
            foreach (var r in results)
            {
                summary.Results.Add(r);

                var json = r.Result;
                float score = json != null ? json.score : 0f;
                total += score;
                if (first || score > summary.BestScore) summary.BestScore = score;
                if (first || score < summary.WorstScore) summary.WorstScore = score;
                first = false;

                if (json != null)
                {
                    if (json.completed)
                    {
                        summary.Completed++;
                        if (json.totalPenalty <= 0f && json.collisions == 0 && json.lineContacts == 0)
                            summary.PerfectRuns++;
                    }
                    if (json.timedOut) summary.Timeouts++;
                    summary.LineViolations += json.lineContacts;
                    summary.Collisions += json.collisions;
                    summary.ObjectiveFailures += BatchSummary.ObjectiveFailureCount(json);
                }

                if (r.Passed) summary.PassedTests++;
            }

            summary.AverageScore = results.Count > 0 ? total / results.Count : 0f;
            return summary;
        }
    }
}
