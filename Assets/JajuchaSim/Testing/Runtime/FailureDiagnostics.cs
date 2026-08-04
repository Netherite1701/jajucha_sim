using System;
using System.IO;
using JajuchaSim.Scenario;
using UnityEngine;

namespace JajuchaSim.Testing
{
    /// <summary>
    /// Failure diagnostics snapshot for failed / low-score runs (Step 10.32):
    /// center camera frame (optional), depth image (optional), event log,
    /// penalty log, motor command trace, final vehicle location, objective
    /// states. Never depends on ANN/FSM internals.
    /// </summary>
    [Serializable]
    public sealed class FailureDiagnostics
    {
        public string runId = "";
        public string status = "";
        public float score;
        public double elapsedSec;
        public RunResultJson result;

        public EventJson[] eventLog = Array.Empty<EventJson>();
        public PenaltyJson[] penaltyLog = Array.Empty<PenaltyJson>();
        public ObjectiveJson[] objectiveStates = Array.Empty<ObjectiveJson>();

        public string finalPosition = "";          // "x,y,z" (cm)
        public string finalForward = "";           // "x,y,z" heading (cm)

        /// <summary>Latest center camera frame (JPEG bytes → base64). Optional.</summary>
        public string centerCameraBase64 = "";

        /// <summary>Latest depth frame (grayscale bytes → base64). Optional.</summary>
        public string depthBase64 = "";

        /// <summary>Motor command trace lines ("time set_motor(left=.., right=.., speed=..)").</summary>
        public string motorTrace = "";

        public static FailureDiagnostics Capture(
            RunResultJson result,
            ScenarioManager manager,
            System.Collections.Generic.IReadOnlyList<CommandRecord> motorTrace = null,
            string centerCameraBase64 = "",
            string depthBase64 = "")
        {
            var d = new FailureDiagnostics
            {
                runId = result != null ? result.runId : (manager?.Session?.RunId ?? ""),
                status = result != null ? result.status : (manager?.Session?.Status.ToString().ToLowerInvariant() ?? ""),
                score = result != null ? result.score : 0f,
                elapsedSec = result != null ? result.elapsedSec : 0.0,
                result = result
            };

            if (manager?.Session != null)
            {
                var evts = new System.Collections.Generic.List<EventJson>();
                foreach (var e in manager.Session.Events)
                    evts.Add(new EventJson { time = e.SimulationTime, tick = e.SimulationTick, message = e.Message });
                d.eventLog = evts.ToArray();

                var pens = new System.Collections.Generic.List<PenaltyJson>();
                foreach (var p in manager.Session.Penalties)
                    pens.Add(new PenaltyJson
                    {
                        ruleId = p.RuleId,
                        reason = p.Reason,
                        value = p.Value,
                        simulationTime = p.SimulationTime,
                        eventType = p.EventType,
                        targetId = p.TargetId
                    });
                d.penaltyLog = pens.ToArray();

                var objs = new System.Collections.Generic.List<ObjectiveJson>();
                foreach (var o in manager.Session.Objectives)
                    objs.Add(new ObjectiveJson
                    {
                        id = o.Id,
                        type = o.Type.ToString().ToLowerInvariant(),
                        targetId = o.TargetId,
                        status = o.State.ToString().ToLowerInvariant(),
                        passed = o.Passed,
                        penalty = o.Penalty
                    });
                d.objectiveStates = objs.ToArray();
            }

            if (motorTrace != null)
                d.motorTrace = CommandReplay.Format(motorTrace);

            d.centerCameraBase64 = centerCameraBase64 ?? "";
            d.depthBase64 = depthBase64 ?? "";
            return d;
        }

        /// <summary>Set the final vehicle pose (cm).</summary>
        public void SetFinalPose(Vector3 position, Vector3 forward)
        {
            finalPosition = $"{position.x:0.0},{position.y:0.0},{position.z:0.0}";
            finalForward = $"{forward.x:0.00},{forward.y:0.00},{forward.z:0.00}";
        }

        public string ToJson(bool pretty = true) => JsonUtility.ToJson(this, pretty);

        /// <summary>Write the diagnostics JSON to <paramref name="path"/>.</summary>
        public string Save(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    string dir = Path.Combine(Application.persistentDataPath, "Diagnostics");
                    Directory.CreateDirectory(dir);
                    path = Path.Combine(dir, string.IsNullOrEmpty(runId) ? "diagnostics.json" : runId + "_diag.json");
                }
                File.WriteAllText(path, ToJson(true));
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Testing] Failed to save diagnostics: {ex.Message}");
                return null;
            }
        }

        public static FailureDiagnostics Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                return JsonUtility.FromJson<FailureDiagnostics>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Testing] Failed to load diagnostics: {ex.Message}");
                return null;
            }
        }
    }
}
