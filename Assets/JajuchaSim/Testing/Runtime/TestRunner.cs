using System;
using System.Collections.Generic;
using JajuchaSim.Course;
using JajuchaSim.Scenario;
using JajuchaSim.Vehicle;

namespace JajuchaSim.Testing
{
    /// <summary>
    /// Result of one automated test run (Step 10.25/10.28). The competition
    /// score and the automated TEST PASS/FAIL verdict are kept separate.
    /// </summary>
    public sealed class TestRunResult
    {
        /// <summary>Official run id (e.g. "run_0001").</summary>
        public string RunId = "";

        /// <summary>Official competition result (RunResultJson).</summary>
        public RunResultJson Result;

        /// <summary>Automated-test verdict (separate from the score).</summary>
        public bool Passed;

        /// <summary>Human-readable reasons when the test failed.</summary>
        public List<string> FailureReasons = new List<string>();

        public double ElapsedSec => Result != null ? Result.elapsedSec : 0.0;
        public float Score => Result != null ? Result.score : 0f;
    }

    /// <summary>
    /// Single automated test (Step 10.25):
    ///   Reset → Start → external controller drives → Scenario ends →
    ///   ScoreManager finalizes → RunResult → TEST PASS/FAIL (pass criteria).
    ///
    /// Uses the exact same scoring system as manual runs.
    /// </summary>
    public static class TestRunner
    {
        /// <summary>
        /// Run one scenario with an external controller and evaluate the
        /// scenario's pass criteria.
        /// </summary>
        public static TestRunResult RunSingle(
            ScenarioDefinition definition,
            CourseDocument document,
            ulong seed = 1UL,
            Func<long, double, MotorCommand?> controller = null,
            long maxTicks = 1_000_000,
            Action<ScenarioRunDriver> configure = null)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (document == null) throw new ArgumentNullException(nameof(document));

            var driver = new ScenarioRunDriver(definition, document, seed);
            configure?.Invoke(driver);
            driver.Controller = controller;
            driver.RequestStart();

            var json = driver.RunToCompletion(maxTicks);
            var result = new TestRunResult
            {
                RunId = driver.Manager.Session?.RunId ?? "",
                Result = json
            };

            var criteria = definition.passCriteria ?? new PassCriteria();
            result.Passed = criteria.Evaluate(json, result.FailureReasons);
            return result;
        }
    }
}
