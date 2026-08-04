using System.Text;

namespace JajuchaSim.Testing
{
    /// <summary>
    /// Regression comparison (Step 10.29): compares a known-good baseline batch
    /// against a new controller's batch purely from official RunResults —
    /// without knowing anything about controller internals.
    /// </summary>
    public sealed class RegressionReport
    {
        /// <summary>Known-good baseline batch summary.</summary>
        public BatchSummary Baseline;

        /// <summary>Batch summary of the new controller under test.</summary>
        public BatchSummary Current;

        /// <summary>
        /// True when the current batch is worse than the baseline on average
        /// score or completion rate (a regression).
        /// </summary>
        public bool IsRegression =>
            Current != null && Baseline != null &&
            (Current.AverageScore < Baseline.AverageScore - 1e-4f ||
             Current.CompletionRatePercent < Baseline.CompletionRatePercent - 1e-4f);

        public RegressionReport(BatchSummary baseline, BatchSummary current)
        {
            Baseline = baseline;
            Current = current;
        }

        /// <summary>REGRESSION report block (item 29).</summary>
        public string ToReportText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("REGRESSION");
            sb.AppendLine();
            if (Baseline == null || Current == null)
            {
                sb.AppendLine("(baseline or current batch missing)");
                return sb.ToString();
            }
            sb.AppendLine($"{"",-18} {"Baseline",-12} {"Current",-12}");
            sb.AppendLine($"{"Runs",-18} {Baseline.Runs,-12} {Current.Runs,-12}");
            sb.AppendLine($"{"Average Score",-18} {Baseline.AverageScore,10:0.0}   {Current.AverageScore,10:0.0}");
            sb.AppendLine($"{"Completion %",-18} {Baseline.CompletionRatePercent,10:0.0}%  {Current.CompletionRatePercent,10:0.0}%");
            sb.AppendLine($"{"Line Violations",-18} {Baseline.LineViolations,-12} {Current.LineViolations,-12}");
            sb.AppendLine($"{"Collisions",-18} {Baseline.Collisions,-12} {Current.Collisions,-12}");
            sb.AppendLine($"{"Objective Failures",-18} {Baseline.ObjectiveFailures,-12} {Current.ObjectiveFailures,-12}");
            sb.AppendLine();
            sb.AppendLine(IsRegression ? "RESULT: REGRESSION DETECTED" : "RESULT: NO REGRESSION");
            return sb.ToString();
        }
    }
}
