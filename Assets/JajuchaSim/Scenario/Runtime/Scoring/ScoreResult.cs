using System;
using System.Collections.Generic;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Final run result: status, timing, and raw rule measurements (Step 8.29).
    /// Raw data comes first; point mapping is intentionally left for later
    /// (Step 8.42) so we do not invent competition rules.
    /// </summary>
    [Serializable]
    public sealed class ScoreResult
    {
        public RunResultStatus Status = RunResultStatus.None;
        public double ElapsedSec;

        public bool Completed;
        public bool TimedOut;
        public bool Aborted;
        public bool FalseStart;

        public int CollisionCount;

        public List<SlowZoneMeasurement> SlowZones = new List<SlowZoneMeasurement>();
        public List<GateMeasurement> SpeedGates = new List<GateMeasurement>();
        public List<CollisionIncident> Collisions = new List<CollisionIncident>();
        public List<PenaltyRecord> Penalties = new List<PenaltyRecord>();

        /// <summary>Sum of penalty values (positive numbers, added as deductions).</summary>
        public float TotalPenalty;

        /// <summary>
        /// Computed score. Since no official competition points are known yet
        /// (Step 8.42), this is 0 minus penalties while scoring is enabled.
        /// </summary>
        public float Score;

        public void Clear()
        {
            Status = RunResultStatus.None;
            ElapsedSec = 0.0;
            Completed = false;
            TimedOut = false;
            Aborted = false;
            FalseStart = false;
            CollisionCount = 0;
            SlowZones.Clear();
            SpeedGates.Clear();
            Collisions.Clear();
            Penalties.Clear();
            TotalPenalty = 0f;
            Score = 0f;
        }
    }

    /// <summary>
    /// JSON-export shape for a finished run (Step 8.34). Detailed rule
    /// measurements are included so automated agents can consume them.
    /// </summary>
    [Serializable]
    public sealed class RunResultJson
    {
        public string runId;
        public string course;
        public string scenario;
        public string status;
        public double elapsedSec;
        public bool completed;
        public bool timedOut;
        public bool aborted;
        public bool falseStart;
        public int collisions;
        public float totalPenalty;
        public float score;
        public SlowZoneJson[] slowZones = Array.Empty<SlowZoneJson>();
        public SpeedGateJson[] speedGates = Array.Empty<SpeedGateJson>();
        public CollisionJson[] collisionList = Array.Empty<CollisionJson>();
        public PenaltyJson[] penalties = Array.Empty<PenaltyJson>();
    }

    [Serializable]
    public sealed class SlowZoneJson
    {
        public string triggerId;
        public float allowedMaxCmS;
        public float maxSpeedCmS;
        public float averageSpeedCmS;
        public float timeAboveLimitSec;
        public bool passed;
    }

    [Serializable]
    public sealed class SpeedGateJson
    {
        public string pairId;
        public string firstGate;
        public string secondGate;
        public float distanceCm;
        public double startTime;
        public double endTime;
        public float averageSpeedCmS;
    }

    [Serializable]
    public sealed class CollisionJson
    {
        public string objectId;
        public float relativeVelocityCmS;
        public double simulationTime;
    }

    [Serializable]
    public sealed class PenaltyJson
    {
        public string ruleId;
        public string reason;
        public float value;
        public double simulationTime;
    }
}
