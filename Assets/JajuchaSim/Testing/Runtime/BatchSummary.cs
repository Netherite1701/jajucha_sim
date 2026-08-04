using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JajuchaSim.Scenario;

namespace JajuchaSim.Testing
{
    /// <summary>
    /// Batch summary statistics (Step 10.26):
    ///   Runs, Average/Best/Worst Score, Perfect Runs, Completed, Timeouts,
    ///   Line Violations, Collisions, Objective Failures.
    /// </summary>
    public sealed class BatchSummary
    {
        public int Runs;

        public float AverageScore;
        public float BestScore;
        public float WorstScore;

        /// <summary>Runs that completed with zero penalties.</summary>
        public int PerfectRuns;

        /// <summary>Runs with status completed.</summary>
        public int Completed;

        /// <summary>Runs with status timeout.</summary>
        public int Timeouts;

        /// <summary>Runs that passed the automated pass criteria.</summary>
        public int PassedTests;

        public int LineViolations;
        public int Collisions;
        public int ObjectiveFailures;

        /// <summary>Per-run results (official RunResultJson), in run order.</summary>
        public readonly List<TestRunResult> Results = new List<TestRunResult>();

        public double CompletionRatePercent => Runs > 0 ? (double)Completed / Runs * 100.0 : 0.0;
        public double PassRatePercent => Runs > 0 ? (double)PassedTests / Runs * 100.0 : 0.0;

        /// <summary>Human-readable BATCH summary block (item 26).</summary>
        public string ToSummaryText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("BATCH");
            sb.AppendLine();
            sb.AppendLine($"Runs                 {Runs}");
            sb.AppendLine();
            sb.AppendLine($"Average Score       {AverageScore:0.0}");
            sb.AppendLine($"Best Score           {BestScore:0}");
            sb.AppendLine($"Worst Score          {WorstScore:0}");
            sb.AppendLine();
            sb.AppendLine($"Perfect Runs          {PerfectRuns}");
            sb.AppendLine($"Completed             {Completed}");
            sb.AppendLine($"Timeouts              {Timeouts}");
            sb.AppendLine();
            sb.AppendLine($"Line Violations       {LineViolations}");
            sb.AppendLine($"Collisions            {Collisions}");
            sb.AppendLine($"Objective Failures    {ObjectiveFailures}");
            return sb.ToString();
        }

        /// <summary>
        /// Batch CSV (Step 10.31):
        /// run,status,score,time,line_contacts,collisions,objective_failures
        /// </summary>
        public string ToCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("run,status,score,time,line_contacts,collisions,objective_failures");
            for (int i = 0; i < Results.Count; i++)
            {
                var r = Results[i];
                if (r.Result == null)
                {
                    sb.AppendLine($"{i + 1},no_result,,,0,0,0");
                    continue;
                }
                string status = r.Result.status ?? "none";
                string score = r.Result.completed || r.Result.timedOut ? r.Result.score.ToString("0.##", CultureInfo.InvariantCulture) : "";
                string time = r.Result.completed ? r.Result.elapsedSec.ToString("0.0", CultureInfo.InvariantCulture) : "";
                sb.AppendLine($"{i + 1},{status},{score},{time},{r.Result.lineContacts},{r.Result.collisions},{ObjectiveFailureCount(r.Result)}");
            }
            return sb.ToString();
        }

        /// <summary>JSON payload for the whole batch (per-run results included).</summary>
        public string ToJson(bool pretty = true)
        {
            var batchJson = new BatchJson
            {
                runs = Runs,
                averageScore = AverageScore,
                bestScore = BestScore,
                worstScore = WorstScore,
                perfectRuns = PerfectRuns,
                completed = Completed,
                timeouts = Timeouts,
                passedTests = PassedTests,
                lineViolations = LineViolations,
                collisions = Collisions,
                objectiveFailures = ObjectiveFailures,
                results = Results.ConvertAll(r => r.Result).ToArray()
            };
            return UnityEngine.JsonUtility.ToJson(batchJson, pretty);
        }

        internal static int ObjectiveFailureCount(RunResultJson json)
        {
            if (json?.objectives == null) return 0;
            int n = 0;
            foreach (var o in json.objectives)
                if (!string.IsNullOrEmpty(o.status) && o.status == "failed")
                    n++;
            return n;
        }
    }

    /// <summary>Serializable batch export shape (per-run JSON array included).</summary>
    [Serializable]
    public sealed class BatchJson
    {
        public int runs;
        public float averageScore;
        public float bestScore;
        public float worstScore;
        public int perfectRuns;
        public int completed;
        public int timeouts;
        public int passedTests;
        public int lineViolations;
        public int collisions;
        public int objectiveFailures;
        public RunResultJson[] results = Array.Empty<RunResultJson>();
    }
}
