using System;
using System.Collections.Generic;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Final course-completion status of a run (Step 8.26).
    /// Raw measurements are kept per rule; this is the overall outcome.
    /// </summary>
    public enum RunResultStatus
    {
        /// <summary>No result yet.</summary>
        None = 0,

        /// <summary>Finish crossed while running.</summary>
        Completed,

        /// <summary>Max run time exceeded.</summary>
        TimedOut,

        /// <summary>Run aborted by operator or a failing rule.</summary>
        Aborted,

        /// <summary>Run ended because of a false start (rule-driven).</summary>
        FalseStart
    }

    /// <summary>
    /// One timestamped scenario event. Every scenario event carries both the
    /// simulation tick and simulation time so regression testing is exact
    /// (Step 8.33) — never just a formatted string.
    /// </summary>
    public readonly struct ScenarioEvent
    {
        public long SimulationTick { get; }
        public double SimulationTime { get; }
        public string Message { get; }

        public ScenarioEvent(long simulationTick, double simulationTime, string message)
        {
            SimulationTick = simulationTick;
            SimulationTime = simulationTime;
            Message = message ?? string.Empty;
        }

        public override string ToString() => $"{SimulationTime:0.000}  {Message}";
    }

    /// <summary>Raw slow-zone measurement (Step 8.15).</summary>
    public sealed class SlowZoneMeasurement
    {
        public string TriggerId;
        public float AllowedMaxCmS;
        public double EntryTime;
        public double ExitTime;
        public float MaxSpeedCmS;
        public float AverageSpeedCmS;
        public float TimeAboveLimitSec;
        public bool Passed;
        public ViolationMode ViolationMode;

        // accumulation (internal)
        internal float SumSpeed;
        internal int SampleCount;

        public string StatusText => Passed ? "PASS" : "FAIL";
    }

    /// <summary>Raw per-object collision incident (Step 8.20).</summary>
    public sealed class CollisionIncident
    {
        public string ObjectId;
        public float RelativeVelocityCmS;
        public double SimulationTime;
        public long SimulationTick;
    }

    /// <summary>Raw gate measurement (Step 8.22).</summary>
    public sealed class GateMeasurement
    {
        public string PairId;
        public string FirstGate;
        public string SecondGate;
        public float DistanceCm;
        public double StartTime;
        public double EndTime;
        public float AverageSpeedCmS;
    }

    /// <summary>
    /// One run's collected data (Step 8.27). A new session is created on every
    /// reset/prepare; the previous session stays available for inspection.
    /// </summary>
    public sealed class RunSession
    {
        public string RunId { get; set; } = "";
        public string CourseId { get; set; } = "";
        public string ScenarioId { get; set; } = "";

        /// <summary>Simulation time when the run timer started.</summary>
        public double StartTime { get; set; }

        /// <summary>Simulation time when the run ended (timer stop).</summary>
        public double EndTime { get; set; }

        public RunResultStatus Status { get; set; } = RunResultStatus.None;

        public bool FalseStart { get; set; }

        public readonly List<ScenarioEvent> Events = new List<ScenarioEvent>();
        public readonly List<PenaltyRecord> Penalties = new List<PenaltyRecord>();
        public readonly List<GateMeasurement> Measurements = new List<GateMeasurement>();
        public readonly List<SlowZoneMeasurement> SlowZones = new List<SlowZoneMeasurement>();
        public readonly List<CollisionIncident> Collisions = new List<CollisionIncident>();
        public readonly List<ObjectiveResult> Objectives = new List<ObjectiveResult>();

        /// <summary>Debounced line-contact episodes (Step 10.3).</summary>
        public int LineContactCount;

        /// <summary>Debounced course-departure episodes (Step 10.8).</summary>
        public int CourseDepartureCount;

        /// <summary>Elapsed simulated seconds (0 until the run ends).</summary>
        public double ElapsedSec => EndTime >= StartTime ? EndTime - StartTime : 0.0;

        public void Clear()
        {
            RunId = "";
            CourseId = "";
            ScenarioId = "";
            StartTime = 0.0;
            EndTime = 0.0;
            Status = RunResultStatus.None;
            FalseStart = false;
            Events.Clear();
            Penalties.Clear();
            Measurements.Clear();
            SlowZones.Clear();
            Collisions.Clear();
            Objectives.Clear();
            LineContactCount = 0;
            CourseDepartureCount = 0;
        }
    }
}
