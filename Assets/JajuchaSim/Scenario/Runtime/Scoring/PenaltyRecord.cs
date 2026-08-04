using System;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// A single structured penalty record (Step 10.16). Every deduction creates
    /// one of these so results are auditable:
    ///   RuleId, EventType, Points, SimulationTime, TargetId, Description
    /// </summary>
    public readonly struct PenaltyRecord
    {
        /// <summary>Rule that produced the penalty (e.g. "LineContactRule").</summary>
        public string RuleId { get; }

        /// <summary>Human-readable description of the deduction.</summary>
        public string Reason { get; }

        /// <summary>Deduction magnitude (positive number).</summary>
        public float Value { get; }

        public long SimulationTick { get; }
        public double SimulationTime { get; }

        /// <summary>Event category: line_contact, collision, course_departure,
        /// false_start, objective_failure, speed_violation, timeout, …</summary>
        public string EventType { get; }

        /// <summary>Course feature the penalty refers to (object/structure/trigger id).</summary>
        public string TargetId { get; }

        public PenaltyRecord(string ruleId, string reason, float value, long simulationTick, double simulationTime)
            : this(ruleId, reason, value, simulationTick, simulationTime, "", "")
        {
        }

        public PenaltyRecord(string ruleId, string reason, float value, long simulationTick, double simulationTime, string eventType)
            : this(ruleId, reason, value, simulationTick, simulationTime, eventType, "")
        {
        }

        public PenaltyRecord(
            string ruleId,
            string reason,
            float value,
            long simulationTick,
            double simulationTime,
            string eventType,
            string targetId)
        {
            RuleId = ruleId ?? string.Empty;
            Reason = reason ?? string.Empty;
            Value = value;
            SimulationTick = simulationTick;
            SimulationTime = simulationTime;
            EventType = eventType ?? string.Empty;
            TargetId = targetId ?? string.Empty;
        }

        public override string ToString()
            => $"[{SimulationTime:0.000}] {RuleId}: {Reason} ({Value:0.##})";
    }
}
