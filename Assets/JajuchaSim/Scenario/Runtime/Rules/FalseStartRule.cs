using JajuchaSim.Course;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Optional false-start rule (Step 8.25).
    ///
    /// If the vehicle crosses the start trigger before release
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
            if (Ctx.State == ScenarioState.Running) return; // released

            // Release has not occurred → false start.
            Ctx.Session.FalseStart = true;
            Ctx.Score.Result.FalseStart = true;
            Ctx.Session.Events.Add(new ScenarioEvent(Ctx.Tick, Ctx.Time, "FALSE START"));

            if (cfg.violationMode == ViolationMode.Penalty)
            {
                Ctx.Score.AddPenalty(new PenaltyRecord(
                    RuleId,
                    "Crossed start line before 2026 light release/buzzer",
                    cfg.penalty,
                    Ctx.Tick,
                    Ctx.Time,
                    "false_start",
                    ""));
            }
            else if (cfg.violationMode == ViolationMode.Fail)
            {
                Ctx.RequestAbort?.Invoke(RunResultStatus.FalseStart);
            }
        }
    }
}
