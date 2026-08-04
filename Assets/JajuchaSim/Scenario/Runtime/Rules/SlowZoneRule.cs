using System;
using System.Collections.Generic;
using JajuchaSim.Course;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Evaluates vehicle speed inside slow zones (Step 8.13–8.16).
    ///
    /// On TriggerEntered (SlowZone) it opens a per-zone measurement; each tick
    /// while inside it samples the Rigidbody-derived forward speed and updates
    /// max/average/time-above-limit; on exit (or finalize) it produces the
    /// measurement and applies the configured violation mode.
    ///
    /// Multiple overlapping slow zones are tracked independently by id
    /// (Step 8.52) — no global isInSlowZone flag.
    /// </summary>
    public sealed class SlowZoneRule : RuleEvaluator
    {
        private readonly Dictionary<string, SlowZoneMeasurement> _active =
            new Dictionary<string, SlowZoneMeasurement>(StringComparer.Ordinal);

        public override void Initialize(ScenarioContext context)
        {
            base.Initialize(context);
        }

        public override void OnRunStart()
        {
            _active.Clear();
        }

        public override void OnTriggerEntered(TriggerEnteredEvent e)
        {
            if (e.Type != TriggerType.SlowZone || string.IsNullOrEmpty(e.TriggerId)) return;
            if (Ctx.Definition == null) return;
            var cfg = Ctx.Definition.FindSlowZone(e.TriggerId);
            if (cfg == null) return;

            if (_active.ContainsKey(e.TriggerId)) return;

            var m = new SlowZoneMeasurement
            {
                TriggerId = e.TriggerId,
                AllowedMaxCmS = cfg.maxSpeedCmS,
                EntryTime = Ctx.Time,
                ViolationMode = cfg.violationMode
            };
            _active[e.TriggerId] = m;

            Ctx.Session.Events.Add(new ScenarioEvent(Ctx.Tick, Ctx.Time, $"{e.TriggerId} ENTER"));
        }

        public override void OnTriggerExited(TriggerExitedEvent e)
        {
            if (e.Type != TriggerType.SlowZone || string.IsNullOrEmpty(e.TriggerId)) return;
            if (!_active.TryGetValue(e.TriggerId, out var m)) return;
            _active.Remove(e.TriggerId);
            m.ExitTime = Ctx.Time;
            FinalizeMeasurement(m);
            Ctx.Session.Events.Add(new ScenarioEvent(Ctx.Tick, Ctx.Time, $"{e.TriggerId} EXIT"));
        }

        public override void OnTick(float deltaTime)
        {
            if (_active.Count == 0) return;
            float speed = Math.Abs(Ctx.Telemetry.ForwardSpeedCmS);

            foreach (var m in _active.Values)
            {
                if (speed > m.MaxSpeedCmS)
                    m.MaxSpeedCmS = speed;
                m.SumSpeed += speed;
                m.SampleCount++;
                if (speed > m.AllowedMaxCmS + 0.001f)
                    m.TimeAboveLimitSec += deltaTime;
            }
        }

        public override void Finalize()
        {
            // Any zone still active when the run ends: close it at current time.
            foreach (var kv in _active)
            {
                kv.Value.ExitTime = Ctx.Time;
                FinalizeMeasurement(kv.Value);
            }
            _active.Clear();
        }

        private void FinalizeMeasurement(SlowZoneMeasurement m)
        {
            m.AverageSpeedCmS = m.SampleCount > 0 ? m.SumSpeed / m.SampleCount : 0f;
            m.Passed = m.MaxSpeedCmS <= m.AllowedMaxCmS + 0.001f;

            Ctx.Session.SlowZones.Add(m);

            if (m.Passed || m.ViolationMode == ViolationMode.Informational)
                return;

            if (m.ViolationMode == ViolationMode.Penalty)
            {
                var cfg = Ctx.Definition?.FindSlowZone(m.TriggerId);
                float penalty = cfg != null ? cfg.penalty : 0f;
                Ctx.Score.AddPenalty(new PenaltyRecord(
                    RuleId,
                    $"Slow zone {m.TriggerId} exceeded {m.AllowedMaxCmS:0.#} cm/s (max {m.MaxSpeedCmS:0.#})",
                    penalty,
                    Ctx.Tick,
                    Ctx.Time,
                    "speed_violation",
                    m.TriggerId));
            }
            // Fail mode: recorded as FAIL in the result (no immediate abort).
        }
    }
}
