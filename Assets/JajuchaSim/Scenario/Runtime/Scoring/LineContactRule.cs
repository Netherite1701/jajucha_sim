using System;
using JajuchaSim.Course;
using UnityEngine;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Line-contact scoring (Step 10.2/10.3).
    ///
    /// Samples the full vehicle footprint (centre + four corners, supplied by
    /// ground-truth telemetry), never just the centre point. Touching a
    /// boundary-line tile starts a violation episode; staying on the line is
    /// the same violation; leaving ends the episode. A later touch starts a
    /// new violation — no penalty per simulation tick.
    /// </summary>
    public sealed class LineContactRule : RuleEvaluator
    {
        private bool _touching;
        private int _violationCount;

        public int ViolationCount => _violationCount;
        public bool IsTouching => _touching;

        public override void OnRunStart()
        {
            _touching = false;
            _violationCount = 0;
            Ctx.Session.LineContactCount = 0;
            Ctx.Score.Result.LineContactCount = 0;
        }

        public override void OnTick(float deltaTime)
        {
            if (Ctx.Document == null || Ctx.Definition == null) return;
            var cfg = Ctx.Definition.scoring;
            if (cfg == null || !Ctx.Score.ScoringEnabled) return;

            // No boundary lines on this map → nothing to violate.
            if (Ctx.Document.Grid.LineTileCount == 0) return;

            bool touching = FootprintTouchesLine();

            if (touching && !_touching)
            {
                // Episode begins → one LineViolation (Step 10.3).
                _touching = true;
                _violationCount++;
                Ctx.Session.LineContactCount = _violationCount;
                Ctx.Score.Result.LineContactCount = _violationCount;

                string target = CurrentLineTargetId();
                Ctx.Session.Events.Add(new ScenarioEvent(Ctx.Tick, Ctx.Time, "LINE_CONTACT"));
                Ctx.Score.AddPenalty(new PenaltyRecord(
                    RuleId,
                    $"Vehicle footprint touched boundary line {target}",
                    cfg.lineContactPenalty,
                    Ctx.Tick,
                    Ctx.Time,
                    "line_contact",
                    target));
            }
            else if (!touching && _touching)
            {
                // Episode ends.
                _touching = false;
            }
        }

        public override void Finalize()
        {
            Ctx.Session.LineContactCount = _violationCount;
            Ctx.Score.Result.LineContactCount = _violationCount;
        }

        private bool FootprintTouchesLine()
        {
            var pts = Ctx.Telemetry.SamplePoints;
            if (pts == null || pts.Length == 0)
                pts = new[] { Ctx.Telemetry.Position };

            float ts = Ctx.Document.Grid.TileSizeCm;
            foreach (var p in pts)
            {
                var c = new GridCoordinate(
                    Mathf.FloorToInt(p.x / ts),
                    Mathf.FloorToInt(p.z / ts));
                if (Ctx.Document.Grid.HasLine(c))
                    return true;
            }
            return false;
        }

        private string CurrentLineTargetId()
        {
            var pts = Ctx.Telemetry.SamplePoints;
            if (pts == null || pts.Length == 0)
                pts = new[] { Ctx.Telemetry.Position };

            float ts = Ctx.Document.Grid.TileSizeCm;
            foreach (var p in pts)
            {
                var c = new GridCoordinate(
                    Mathf.FloorToInt(p.x / ts),
                    Mathf.FloorToInt(p.z / ts));
                if (Ctx.Document.Grid.HasLine(c))
                    return $"line_{c.X}_{c.Z}";
            }
            return "road_boundary";
        }
    }
}
