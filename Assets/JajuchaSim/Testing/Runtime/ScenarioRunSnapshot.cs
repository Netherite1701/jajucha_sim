using System;
using JajuchaSim.Course;
using JajuchaSim.Scenario;
using UnityEngine;

namespace JajuchaSim.Testing
{
    /// <summary>
    /// Captures everything needed for a debug re-run (Step 10.33): the same
    /// course, the same scenario, and the same random seed. The simulator then
    /// re-runs at 1× speed with the full runtime UI while the external
    /// controller drives normally.
    /// </summary>
    [Serializable]
    public sealed class ScenarioRunSnapshot
    {
        public string runId = "";
        public string courseJson = "";
        public string scenarioJson = "";
        public ulong seed = 1UL;

        /// <summary>Capture the current prepared run.</summary>
        public static ScenarioRunSnapshot Capture(ScenarioManager manager, ulong seed = 1UL)
        {
            var snap = new ScenarioRunSnapshot
            {
                runId = manager?.Session?.RunId ?? "",
                courseJson = manager?.Document != null ? manager.Document.ToJson() : "",
                scenarioJson = manager?.Definition != null ? manager.Definition.ToJson() : "",
                seed = seed
            };
            return snap;
        }

        /// <summary>Restore the captured course document, or null on failure.</summary>
        public CourseDocument RestoreCourse()
        {
            if (string.IsNullOrEmpty(courseJson)) return null;
            try { return CourseDocument.FromJson(courseJson); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Testing] Failed to restore course: {ex.Message}");
                return null;
            }
        }

        /// <summary>Restore the captured scenario definition, or null on failure.</summary>
        public ScenarioDefinition RestoreScenario()
        {
            if (string.IsNullOrEmpty(scenarioJson)) return null;
            return ScenarioDefinition.FromJson(scenarioJson);
        }
    }
}
