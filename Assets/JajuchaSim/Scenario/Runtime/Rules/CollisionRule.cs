using System.Collections.Generic;
using JajuchaSim.Course;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Records debounced collision incidents from
    /// <see cref="VehicleCollisionEvent"/>s (Step 8.20). Baseline policy is
    /// intentionally simple: one collision session = one incident.
    /// </summary>
    public sealed class CollisionRule : RuleEvaluator
    {
        private readonly List<CollisionIncident> _incidents = new List<CollisionIncident>();

        public IReadOnlyList<CollisionIncident> Incidents => _incidents;

        public override void OnRunStart()
        {
            _incidents.Clear();
        }

        public override void OnVehicleCollision(VehicleCollisionEvent e)
        {
            _incidents.Add(new CollisionIncident
            {
                ObjectId = e.ObjectId,
                RelativeVelocityCmS = e.RelativeVelocityCmS,
                SimulationTime = e.SimulationTime,
                SimulationTick = e.SimulationTick
            });

            Ctx.Session.Events.Add(new ScenarioEvent(e.SimulationTick, e.SimulationTime, $"{e.ObjectId} COLLISION"));

            var cfg = Ctx.Definition?.collisions;
            if (cfg != null && cfg.violationMode == ViolationMode.Penalty)
            {
                Ctx.Score.AddPenalty(new PenaltyRecord(
                    RuleId,
                    $"Collision with {e.ObjectId}",
                    cfg.penalty,
                    e.SimulationTick,
                    e.SimulationTime));
            }
        }

        public override void Finalize()
        {
            foreach (var i in _incidents)
                Ctx.Session.Collisions.Add(i);
            Ctx.Score.Result.CollisionCount = _incidents.Count;
        }
    }
}
