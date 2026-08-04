using System;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// A single scored penalty (Step 8.43). Scenario files define the actual
    /// numbers; the scoring engine just records them.
    /// </summary>
    public readonly struct PenaltyRecord
    {
        public string RuleId { get; }
        public string Reason { get; }
        public float Value { get; }
        public long SimulationTick { get; }
        public double SimulationTime { get; }

        public PenaltyRecord(string ruleId, string reason, float value, long simulationTick, double simulationTime)
        {
            RuleId = ruleId ?? string.Empty;
            Reason = reason ?? string.Empty;
            Value = value;
            SimulationTick = simulationTick;
            SimulationTime = simulationTime;
        }

        public override string ToString()
            => $"[{SimulationTime:0.000}] {RuleId}: {Reason} ({Value:0.##})";
    }
}
