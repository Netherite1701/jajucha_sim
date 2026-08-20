using System.Collections.Generic;
using JajuchaSim.Course;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Records paired speed-gate measurements into the run session
    /// (Step 8.21/8.22). The official v = d / (t2 - t1) computation is performed
    /// by <see cref="SpeedTerminalPairRule"/> (Step 8); this rule subscribes to
    /// <see cref="SpeedMeasuredEvent"/> and stores the snapshot.
    /// </summary>
    public sealed class SpeedGateRule : RuleEvaluator
    {
        private readonly List<GateMeasurement> _measurements = new List<GateMeasurement>();

        public IReadOnlyList<GateMeasurement> Measurements => _measurements;

        /// <summary>Most recent completed measurement, or null.</summary>
        public GateMeasurement Latest { get; private set; }

        public override void OnRunStart()
        {
            _measurements.Clear();
            Latest = null;
        }

        public override void OnSpeedMeasured(SpeedMeasuredEvent e)
        {
            var m = GateTimer.FromSpeedMeasurement(e.Result);
            _measurements.Add(m);
            Latest = m;

            Ctx.Session.Events.Add(new ScenarioEvent(
                Ctx.Tick,
                e.T2,
                $"{m.FirstGate}->{m.SecondGate} SPEED = {m.AverageSpeedCmS:0.00} cm/s"));
        }

        public override void OnSpeedTerminalCrossed(SpeedTerminalCrossedEvent e)
        {
            Ctx.Session.Events.Add(new ScenarioEvent(
                Ctx.Tick,
                e.SimTime,
                $"{e.TerminalId} CROSS"));
        }

        public override void FinalizeRule()
        {
            foreach (var m in _measurements)
                Ctx.Session.Measurements.Add(m);
            Ctx.Score.Result.SpeedGates.Clear();
            Ctx.Score.Result.SpeedGates.AddRange(_measurements);
        }
    }
}
