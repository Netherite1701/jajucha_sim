namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Finalizes the course-completion status on the score result
    /// (Step 8.26/8.29 / Step 10.14/10.15). The ScenarioManager performs the
    /// actual state transition / timer stop; this rule mirrors the outcome into
    /// the result object and applies the configurable timeout penalty.
    /// </summary>
    public sealed class CompletionRule : RuleEvaluator
    {
        public override void Finalize()
        {
            var r = Ctx.Score.Result;
            r.Status = Ctx.Session.Status;
            r.Completed = Ctx.Session.Status == RunResultStatus.Completed;
            r.TimedOut = Ctx.Session.Status == RunResultStatus.TimedOut;
            r.Aborted = Ctx.Session.Status == RunResultStatus.Aborted;
            r.FalseStart = Ctx.Session.FalseStart || Ctx.Session.Status == RunResultStatus.FalseStart;
            r.ElapsedSec = Ctx.Session.ElapsedSec;

            // Step 10.15: timeout → timeout penalty.
            if (r.TimedOut && Ctx.Score.ScoringEnabled)
            {
                var cfg = Ctx.Definition?.scoring;
                if (cfg != null && cfg.timeoutPenalty > 0f)
                {
                    Ctx.Score.AddPenalty(new PenaltyRecord(
                        RuleId,
                        "Course not finished within max run time",
                        cfg.timeoutPenalty,
                        Ctx.Tick,
                        Ctx.Time,
                        "timeout",
                        ""));
                }
            }
        }
    }
}
