using System;
using JajuchaSim.Scenario;
using JajuchaSim.Vehicle;

namespace JajuchaSim.Testing
{
    /// <summary>
    /// Debug re-run (Step 10.33): re-runs a finished run with the SAME course,
    /// SAME scenario and SAME seed at 1× speed with the full runtime UI, while
    /// the external controller drives normally. Purely uses the captured
    /// snapshot — no knowledge of the controller internals.
    /// </summary>
    public static class DebugReRun
    {
        /// <summary>
        /// Capture the current prepared/finished run and re-run it from scratch.
        /// Returns the new run result, or null when the snapshot cannot be
        /// restored (no course/scenario available).
        /// </summary>
        public static TestRunResult ReRun(
            ScenarioManager manager,
            ulong seed = 1UL,
            Func<long, double, MotorCommand?> controller = null,
            long maxTicks = 1_000_000)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));

            var snap = ScenarioRunSnapshot.Capture(manager, seed);
            var doc = snap.RestoreCourse();
            var def = snap.RestoreScenario();
            if (doc == null || def == null)
            {
                return new TestRunResult
                {
                    RunId = snap.runId,
                    Result = null,
                    Passed = false,
                    FailureReasons = new System.Collections.Generic.List<string>
                    {
                        "Debug re-run failed: could not restore course/scenario snapshot"
                    }
                };
            }

            return TestRunner.RunSingle(def, doc, seed, controller, maxTicks);
        }
    }
}
