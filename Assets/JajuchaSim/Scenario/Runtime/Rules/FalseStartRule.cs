using JajuchaSim.Course;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Optional false-start rule (Step 8.25).
    ///
    /// If the vehicle crosses the start trigger while the signal is not GREEN
    /// (still RED/YELLOW during countdown, or before the run), a FalseStart is
    /// recorded. With violationMode Fail the run is aborted with the
    /// <see cref="RunResultStatus.FalseStart"/> status.
    /// </summary>
    public sealed class FalseStartRule : RuleEvaluator
    {
        public override void OnRunStart()
        {
            Ctx.Session.FalseStart = false;
        }

        public override void OnTriggerEntered(TriggerEnteredEvent e)
        {
            if (e.Type != TriggerType.Start) return;
            var cfg = Ctx.Definition?.falseStart;
            if (cfg == null || !cfg.enabled) return;
            if (Ctx.State == ScenarioState.Running) return; // signal is GREEN

            // Signal is not GREEN → false start.
            Ctx.Session.FalseStart = true;
            Ctx.Score.Result.FalseStart = true;
            Ctx.Session.Events.Add(new ScenarioEvent(Ctx.Tick, Ctx.Time, "FALSE START"));

            if (cfg.violationMode == ViolationMode.Penalty)
            {
                Ctx.Score.AddPenalty(new PenaltyRecord(
                    RuleId,
                    "Crossed start line before GREEN",
                    cfg.penalty,
                    Ctx.Tick,
                    Ctx.Time));
            }
            else if (cfg.violationMode == ViolationMode.Fail)
            {
                Ctx.RequestAbort?.Invoke(RunResultStatus.FalseStart);
            }
        }
    }
}
