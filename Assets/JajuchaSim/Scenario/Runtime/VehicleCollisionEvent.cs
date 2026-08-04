using System;
using System.Collections.Generic;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Published when the vehicle collides with a course structure/obstacle
    /// (Step 8.18). Relative velocity is the physics-reported relative speed at
    /// contact (cm/s); simulation time/tick come from the SimulationClock.
    /// </summary>
    public readonly struct VehicleCollisionEvent
    {
        public string ObjectId { get; }
        public float RelativeVelocityCmS { get; }
        public double SimulationTime { get; }
        public long SimulationTick { get; }

        public VehicleCollisionEvent(string objectId, float relativeVelocityCmS, double simulationTime, long simulationTick)
        {
            ObjectId = objectId ?? string.Empty;
            RelativeVelocityCmS = relativeVelocityCmS;
            SimulationTime = simulationTime;
            SimulationTick = simulationTick;
        }

        public override string ToString() => $"{ObjectId} COLLISION {RelativeVelocityCmS:0.#} cm/s";
    }

    /// <summary>
    /// Pure per-object collision debouncer (Step 8.19).
    ///
    /// A car resting against one obstacle generates many physics callbacks; we
    /// count one incident per collision session:
    ///   begin   → one incident (if not already active)
    ///   stay    → nothing new
    ///   end     → session closes; a later begin is a new incident
    /// </summary>
    public sealed class CollisionSessionTracker
    {
        private readonly HashSet<string> _active = new HashSet<string>(StringComparer.Ordinal);

        public int IncidentCount { get; private set; }
        public IReadOnlyCollection<string> ActiveObjects => _active;

        /// <summary>
        /// Begin a collision session. Returns true when this begins a new
        /// incident (i.e. a real collision to record).
        /// </summary>
        public bool OnCollisionBegin(string objectId)
        {
            if (string.IsNullOrEmpty(objectId)) return false;
            if (!_active.Add(objectId)) return false; // already touching → debounce
            IncidentCount++;
            return true;
        }

        /// <summary>End a collision session (object separated).</summary>
        public void OnCollisionEnd(string objectId)
        {
            _active.Remove(objectId);
        }

        public void Reset()
        {
            _active.Clear();
            IncidentCount = 0;
        }
    }
}
