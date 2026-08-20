using JajuchaSim.Course;
using UnityEngine;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Course-departure scoring (Step 10.8).
    ///
    /// Samples the vehicle footprint against the road layer. When too much of
    /// the footprint is outside the road, a COURSE_DEPARTURE episode begins;
    /// it is debounced like line contact (one episode = one violation).
    /// Severe departures can deduct points; the exact behavior is configured
    /// through <see cref="ScoringConfig.courseDeparturePenalty"/>.
    /// </summary>
    public sealed class CourseDepartureRule : RuleEvaluator
    {
        /// <summary>
        /// Fraction of footprint sample points that must be outside the road
        /// layer for a departure to count ("too much of it is outside").
        /// </summary>
        public float OutsideThreshold { get; set; } = 0.5f;

        private bool _departed;
        private int _departureCount;

        public int DepartureCount => _departureCount;
        public bool IsDeparted => _departed;

        public override void OnRunStart()
        {
            _departed = false;
            _departureCount = 0;
            Ctx.Session.CourseDepartureCount = 0;
            Ctx.Score.Result.CourseDepartureCount = 0;
        }

        public override void OnTick(float deltaTime)
        {
            if (Ctx.Document == null || Ctx.Definition == null) return;
            var cfg = Ctx.Definition.scoring;
            if (cfg == null || !Ctx.Score.ScoringEnabled) return;

            // Road layer not defined on this map → no departure can be judged.
            if (Ctx.Document.Grid.RoadTileCount == 0) return;

            float fractionOutside = FootprintOutsideFraction();
            bool departed = fractionOutside > OutsideThreshold;

            if (departed && !_departed)
            {
                _departed = true;
                _departureCount++;
                Ctx.Session.CourseDepartureCount = _departureCount;
                Ctx.Score.Result.CourseDepartureCount = _departureCount;

                Ctx.Session.Events.Add(new ScenarioEvent(Ctx.Tick, Ctx.Time, "COURSE_DEPARTURE"));
                Ctx.Score.AddPenalty(new PenaltyRecord(
                    RuleId,
                    $"Vehicle footprint outside road ({fractionOutside:P0} outside)",
                    cfg.courseDeparturePenalty,
                    Ctx.Tick,
                    Ctx.Time,
                    "course_departure",
                    ""));
            }
            else if (!departed && _departed)
            {
                _departed = false;
            }
        }

        public override void FinalizeRule()
        {
            Ctx.Session.CourseDepartureCount = _departureCount;
            Ctx.Score.Result.CourseDepartureCount = _departureCount;
        }

        private float FootprintOutsideFraction()
        {
            var pts = Ctx.Telemetry.SamplePoints;
            if (pts == null || pts.Length == 0)
                pts = new[] { Ctx.Telemetry.Position };

            float ts = Ctx.Document.Grid.TileSizeCm;
            int outside = 0;
            foreach (var p in pts)
            {
                var c = new GridCoordinate(
                    Mathf.FloorToInt(p.x / ts),
                    Mathf.FloorToInt(p.z / ts));
                if (!Ctx.Document.Grid.HasRoad(c))
                    outside++;
            }
            return pts.Length == 0 ? 0f : (float)outside / pts.Length;
        }
    }
}
