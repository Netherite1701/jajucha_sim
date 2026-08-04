using System;
using System.Collections.Generic;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// How a run is started from the UI / automation.
    /// </summary>
    public enum StartMode
    {
        /// <summary>Execute the normal RED → YELLOW → GREEN countdown.</summary>
        NormalSignal = 0,

        /// <summary>Skip the countdown and go straight to GREEN (development).</summary>
        Immediate
    }

    /// <summary>
    /// What starts the official run timer.
    /// </summary>
    public enum StartTimingMode
    {
        /// <summary>Timer starts when the start signal turns GREEN.</summary>
        SignalGreen = 0,

        /// <summary>Timer starts when the vehicle crosses the start trigger.</summary>
        StartGateCrossing
    }

    /// <summary>
    /// How a rule violation is treated.
    /// </summary>
    public enum ViolationMode
    {
        /// <summary>Violation fails the objective.</summary>
        Fail = 0,

        /// <summary>Violation adds a penalty (value from config).</summary>
        Penalty,

        /// <summary>Violation is recorded but does not fail or penalize.</summary>
        Informational
    }

    /// <summary>Configuration for one slow-zone rule.</summary>
    [Serializable]
    public sealed class SlowZoneConfig
    {
        /// <summary>Trigger id of the slow zone region (must exist on the map).</summary>
        public string triggerId = "slow_zone_01";

        /// <summary>Maximum allowed forward speed inside the zone (cm/s).</summary>
        public float maxSpeedCmS = 20f;

        public ViolationMode violationMode = ViolationMode.Fail;

        /// <summary>Penalty value when <see cref="violationMode"/> is Penalty.</summary>
        public float penalty = 5f;

        public SlowZoneConfig Clone() => new SlowZoneConfig
        {
            triggerId = triggerId,
            maxSpeedCmS = maxSpeedCmS,
            violationMode = violationMode,
            penalty = penalty
        };
    }

    /// <summary>Configuration for the collision rule.</summary>
    [Serializable]
    public sealed class CollisionConfig
    {
        public bool enabled = true;
        public ViolationMode violationMode = ViolationMode.Informational;
        public float penalty = 5f;
    }

    /// <summary>Configuration for the (optional) false-start rule.</summary>
    [Serializable]
    public sealed class FalseStartConfig
    {
        public bool enabled = false;
        public ViolationMode violationMode = ViolationMode.Fail;
        public float penalty = 10f;
    }

    /// <summary>
    /// Course-independent scenario rules (Step 8.5). Course geometry and
    /// scenario rules stay separate: the same course can be tested with
    /// different rule sets by swapping this definition.
    ///
    /// JSON shape:
    /// <code>
    /// {
    ///   "scenario": {
    ///     "name": "Competition Run",
    ///     "startTrigger": "start_line",
    ///     "finishTrigger": "finish_line",
    ///     "maxRunTimeSec": 180,
    ///     "slowZones": [ { "triggerId": "slow_zone_01", "maxSpeedCmS": 20 } ]
    ///   }
    /// }
    /// </code>
    /// </summary>
    [Serializable]
    public sealed class ScenarioDefinition
    {
        public string name = "Competition Run";

        /// <summary>Course id used for the run session (from the map / editor).</summary>
        public string courseId = "course";

        /// <summary>Scenario id used for the run session.</summary>
        public string scenarioId = "scenario";

        /// <summary>Object id of the start signal (StartSignal type). Optional.</summary>
        public string startSignalObjectId = "";

        /// <summary>Trigger id that starts the run (start line).</summary>
        public string startTriggerId = "";

        /// <summary>Trigger id that finishes the run (finish line).</summary>
        public string finishTriggerId = "";

        /// <summary>Maximum run time in simulated seconds; exceeded → TIME_LIMIT.</summary>
        public float maxRunTimeSec = 180f;

        /// <summary>When the official run timer starts (see <see cref="StartTimingMode"/>).</summary>
        public StartTimingMode startTimingMode = StartTimingMode.SignalGreen;

        /// <summary>Default start mode used by the UI [Start Run] button.</summary>
        public StartMode startMode = StartMode.NormalSignal;

        // ---- Start signal sequence (Step 8.8) ----
        public float redDurationSec = 2f;
        public float yellowDurationSec = 1f;

        /// <summary>Green stays on indefinitely once reached (typical).</summary>
        public bool greenPersistent = true;

        // ---- Prepared for random start timing (Step 8.47), disabled by default ----
        public bool randomStartDelayEnabled = false;
        public float randomStartDelayMinSec = 0f;
        public float randomStartDelayMaxSec = 0f;

        // ---- Scoring (Step 10.18) ----
        /// <summary>When false: timing/events still work, no final points/penalties.</summary>
        public bool scoringEnabled = true;

        /// <summary>Competition scoring rules (base score + penalty values).</summary>
        public ScoringConfig scoring = new ScoringConfig();

        /// <summary>Objectives evaluated for this scenario (Step 10.4/10.19).</summary>
        public List<ObjectiveDefinition> objectives = new List<ObjectiveDefinition>();

        /// <summary>
        /// Optional automated-test pass criteria (Step 10.27/10.28). Distinct
        /// from the competition score; used by TestRunner/BatchRunner.
        /// </summary>
        public PassCriteria passCriteria = new PassCriteria();

        /// <summary>Optionally require the finish to be crossed in the drive direction.</summary>
        public bool requireFinishDirection = false;

        /// <summary>Optionally require gate pairs to be crossed A → B (already the default).</summary>
        public bool requireGateDirection = false;

        // ---- Result export ----
        public bool autoSaveResults = false;
        public string runsDirectory = "Runs";

        // ---- Rules ----
        public List<SlowZoneConfig> slowZones = new List<SlowZoneConfig>();
        public CollisionConfig collisions = new CollisionConfig();
        public FalseStartConfig falseStart = new FalseStartConfig();

        /// <summary>Default definition with one empty slow-zone slot (id filled later).</summary>
        public static ScenarioDefinition Default()
        {
            return new ScenarioDefinition
            {
                slowZones = new List<SlowZoneConfig>
                {
                    new SlowZoneConfig { triggerId = "slow_zone_01", maxSpeedCmS = 20f }
                }
            };
        }

        public ScenarioDefinition Clone()
        {
            var copy = new ScenarioDefinition
            {
                name = name,
                courseId = courseId,
                scenarioId = scenarioId,
                startSignalObjectId = startSignalObjectId,
                startTriggerId = startTriggerId,
                finishTriggerId = finishTriggerId,
                maxRunTimeSec = maxRunTimeSec,
                startTimingMode = startTimingMode,
                startMode = startMode,
                redDurationSec = redDurationSec,
                yellowDurationSec = yellowDurationSec,
                greenPersistent = greenPersistent,
                randomStartDelayEnabled = randomStartDelayEnabled,
                randomStartDelayMinSec = randomStartDelayMinSec,
                randomStartDelayMaxSec = randomStartDelayMaxSec,
                scoringEnabled = scoringEnabled,
                scoring = scoring?.Clone() ?? new ScoringConfig(),
                requireFinishDirection = requireFinishDirection,
                requireGateDirection = requireGateDirection,
                autoSaveResults = autoSaveResults,
                runsDirectory = runsDirectory,
                collisions = new CollisionConfig
                {
                    enabled = collisions.enabled,
                    violationMode = collisions.violationMode,
                    penalty = collisions.penalty
                },
                falseStart = new FalseStartConfig
                {
                    enabled = falseStart.enabled,
                    violationMode = falseStart.violationMode,
                    penalty = falseStart.penalty
                },
                passCriteria = passCriteria?.Clone() ?? new PassCriteria()
            };
            foreach (var z in slowZones)
                copy.slowZones.Add(z.Clone());
            foreach (var o in objectives)
                copy.objectives.Add(o?.Clone() ?? new ObjectiveDefinition());
            return copy;
        }

        /// <summary>Slow-zone config for a trigger id, or null.</summary>
        public SlowZoneConfig FindSlowZone(string triggerId)
        {
            foreach (var z in slowZones)
                if (string.Equals(z.triggerId, triggerId, StringComparison.Ordinal))
                    return z;
            return null;
        }

        // ================================================================
        //  JSON
        // ================================================================

        [Serializable]
        private sealed class Wrapper
        {
            public ScenarioDefinition scenario;
        }

        public string ToJson(bool pretty = true)
            => UnityEngine.JsonUtility.ToJson(new Wrapper { scenario = this }, pretty);

        public static ScenarioDefinition FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                // Accept both a bare scenario object and a {"scenario": {...}} wrapper.
                if (json.TrimStart().StartsWith("{") && !json.Contains("\"scenario\""))
                {
                    var bare = UnityEngine.JsonUtility.FromJson<ScenarioDefinition>(json);
                    return bare != null && !string.IsNullOrEmpty(bare.name) ? bare : null;
                }
                var wrapper = UnityEngine.JsonUtility.FromJson<Wrapper>(json);
                return wrapper?.scenario;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Scenario] Failed to parse ScenarioDefinition JSON: {ex.Message}");
                return null;
            }
        }
    }
}
