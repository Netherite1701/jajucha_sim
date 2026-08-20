using System;
using System.Collections.Generic;
using JajuchaSim.Course;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Final run result: status, timing, raw rule measurements, objectives and
    /// the competition score (Step 10.14/10.15). Raw data comes first; point
    /// values are configurable via <see cref="ScoringConfig"/>.
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

        /// <summary>Debounced line-contact episodes this run (Step 10.3).</summary>
        public int LineContactCount;

        /// <summary>Debounced course-departure episodes this run (Step 10.8).</summary>
        public int CourseDepartureCount;

        public List<SlowZoneMeasurement> SlowZones = new List<SlowZoneMeasurement>();
        public List<GateMeasurement> SpeedGates = new List<GateMeasurement>();
        public List<CollisionIncident> Collisions = new List<CollisionIncident>();
        public List<PenaltyRecord> Penalties = new List<PenaltyRecord>();
        public List<ObjectiveResult> Objectives = new List<ObjectiveResult>();

        /// <summary>Official two-terminal measurements (Step 10.12), with result.</summary>
        public List<SpeedMeasurementResult> SpeedMeasurements = new List<SpeedMeasurementResult>();

        /// <summary>Sum of penalty values (positive numbers, added as deductions).</summary>
        public float TotalPenalty;

        /// <summary>Configured starting score (Step 10.1).</summary>
        public float BaseScore;

        /// <summary>Final Score = Base Score − Penalties.</summary>
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
            LineContactCount = 0;
            CourseDepartureCount = 0;
            SlowZones.Clear();
            SpeedGates.Clear();
            Collisions.Clear();
            Penalties.Clear();
            Objectives.Clear();
            SpeedMeasurements.Clear();
            TotalPenalty = 0f;
            BaseScore = 0f;
            Score = 0f;
        }
    }

    /// <summary>
    /// JSON-export shape for a finished run (Step 10.30). Detailed rule
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
        public int lineContacts;
        public int courseDepartures;
        public string competitionStage;
        public string additionalMission;
        public string missionCandidateId;
        public ulong missionRandomSeed;
        public float startReleaseDelaySec;
        public bool practiceValuesOfficial = false;
        public string practiceValueLabel = "비공식 연습값";
        public float measuredSpeedCmS;
        public bool dynamicObstacleCollision;
        public bool additionalMissionPassed;

        /// <summary>Nested violation counters (Step 10.30 shape).</summary>
        public ViolationsJson violations = new ViolationsJson();

        public float baseScore;
        public float totalPenalty;
        public float score;
        public SlowZoneJson[] slowZones = Array.Empty<SlowZoneJson>();
        public SpeedGateJson[] speedGates = Array.Empty<SpeedGateJson>();
        public CollisionJson[] collisionList = Array.Empty<CollisionJson>();
        public PenaltyJson[] penalties = Array.Empty<PenaltyJson>();
        public ObjectiveJson[] objectives = Array.Empty<ObjectiveJson>();
        public SpeedMeasurementJson[] speedMeasurements = Array.Empty<SpeedMeasurementJson>();
        public EventJson[] events = Array.Empty<EventJson>();
    }

    [Serializable]
    public sealed class ViolationsJson
    {
        public int lineContacts;
        public int collisions;
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
        public string eventType;
        public string targetId;
    }

    [Serializable]
    public sealed class ObjectiveJson
    {
        public string id;
        public string type;
        public string targetId;
        public string status;
        public bool passed;
        public float penalty;
    }

    [Serializable]
    public sealed class SpeedMeasurementJson
    {
        public string pairId;
        public float distanceCm;
        public double t1;
        public double t2;
        public float speedCmS;
        public string result; // "pass" | "fail"
    }

    [Serializable]
    public sealed class EventJson
    {
        public double time;
        public long tick;
        public string message;
    }
}
