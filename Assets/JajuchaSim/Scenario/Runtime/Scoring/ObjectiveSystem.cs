using System;
using System.Collections.Generic;
using JajuchaSim.Course;
using UnityEngine;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Kind of objective (Step 10.4). The scorer only cares about simulator-
    /// observable outcomes — it never inspects the user's controller internals.
    /// </summary>
    public enum ObjectiveType
    {
        /// <summary>Passed when the target trigger is entered (start/finish/event).</summary>
        Trigger = 0,

        /// <summary>Passed when the vehicle enters and correctly exits a structure (e.g. tunnel).</summary>
        PassStructure,

        /// <summary>Passed when the vehicle navigates an object's region without colliding.</summary>
        AvoidObject,

        /// <summary>Passed/failed from the slow-zone measurement for the target trigger.</summary>
        SlowZone,

        /// <summary>Passed when a two-terminal speed measurement for the pair stays within the limit.</summary>
        SpeedPair,

        /// <summary>Passed when the finish is reached.</summary>
        Finish
    }

    /// <summary>Objective lifecycle states (Step 10.5).</summary>
    public enum ObjectiveState
    {
        /// <summary>Not started yet.</summary>
        Pending = 0,

        /// <summary>The vehicle is currently interacting with the objective feature.</summary>
        Active,

        /// <summary>The objective was completed correctly.</summary>
        Passed,

        /// <summary>The objective failed (penalty may apply).</summary>
        Failed,

        /// <summary>The objective was never attempted (e.g. obstacle never entered).</summary>
        Skipped
    }

    /// <summary>
    /// One configured objective (Step 10.4/10.19). References a course feature
    /// by id. Failure penalty is per-objective; when negative it falls back to
    /// <see cref="ScoringConfig.objectiveFailurePenalty"/>.
    /// </summary>
    [Serializable]
    public sealed class ObjectiveDefinition
    {
        /// <summary>Unique objective id (e.g. "tunnel_01", "speed_test_01").</summary>
        public string id = "";

        public ObjectiveType type = ObjectiveType.Trigger;

        /// <summary>
        /// Target course feature id: trigger id (Trigger/Finish/SlowZone),
        /// structure id (PassStructure), or object id (AvoidObject).
        /// </summary>
        public string targetId = "";

        /// <summary>Terminal pair id for <see cref="ObjectiveType.SpeedPair"/>.</summary>
        public string pairId = "";

        /// <summary>
        /// Allowed maximum official speed for <see cref="ObjectiveType.SpeedPair"/>
        /// (cm/s). 0 = any measurement passes.
        /// </summary>
        public float maxSpeedCmS = 0f;

        /// <summary>
        /// Penalty on failure. Negative (default) → use the global
        /// <see cref="ScoringConfig.objectiveFailurePenalty"/>.
        /// </summary>
        public float failurePenalty = -1f;

        /// <summary>
        /// Required objectives that remain unpassed are failed at
        /// finish/timeout (Step 10.14/10.15).
        /// </summary>
        public bool required = true;

        public ObjectiveDefinition Clone() => new ObjectiveDefinition
        {
            id = id,
            type = type,
            targetId = targetId,
            pairId = pairId,
            maxSpeedCmS = maxSpeedCmS,
            failurePenalty = failurePenalty,
            required = required
        };
    }

    /// <summary>
    /// Live result of one objective for a run (Step 10.6/10.37). Successes are
    /// recorded too, not only failures, so batch statistics can show e.g.
    /// "tunnel success 98%" without inspecting controller internals.
    /// </summary>
    public sealed class ObjectiveResult
    {
        public string Id = "";
        public ObjectiveType Type;
        public string TargetId = "";
        public ObjectiveState State = ObjectiveState.Pending;

        /// <summary>Penalty applied when the objective failed (positive number, 0 when none).</summary>
        public float Penalty;

        public bool Passed => State == ObjectiveState.Passed;
        public bool Failed => State == ObjectiveState.Failed;

        public string StatusText => State.ToString().ToUpperInvariant();
    }

    /// <summary>
    /// Evaluates configured objectives from simulator-observable events and
    /// telemetry (Step 10.4–10.7, 10.13–10.15, 10.19). Failures create
    /// structured <see cref="PenaltyRecord"/>s through the <see cref="ScoreManager"/>.
    /// </summary>
    public sealed class ObjectiveRule : RuleEvaluator
    {
        private readonly List<ObjectiveResult> _results = new List<ObjectiveResult>();
        private readonly Dictionary<string, ObjectiveResult> _byId =
            new Dictionary<string, ObjectiveResult>(StringComparer.Ordinal);
        private readonly HashSet<string> _activeStructureIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _enteredObjectIds = new HashSet<string>(StringComparer.Ordinal);

        public IReadOnlyList<ObjectiveResult> Results => _results;

        public override void OnRunStart()
        {
            _results.Clear();
            _byId.Clear();
            _activeStructureIds.Clear();
            _enteredObjectIds.Clear();
            Ctx.Session.Objectives.Clear();
            Ctx.Score.Result.Objectives.Clear();

            if (Ctx.Definition == null) return;
            foreach (var d in Ctx.Definition.objectives)
            {
                if (string.IsNullOrEmpty(d.id)) continue;
                var r = new ObjectiveResult
                {
                    Id = d.id,
                    Type = d.type,
                    TargetId = d.type == ObjectiveType.SpeedPair ? d.pairId : d.targetId
                };
                _results.Add(r);
                _byId[d.id] = r;

                // Mirror live state into the session so the runtime scoring
                // panel and callers can read objective states during the run
                // (Step 10.20) without waiting for finalize.
                Ctx.Session.Objectives.Add(r);
            }
            Ctx.Score.Result.Objectives.AddRange(_results);
        }

        public override void OnTick(float deltaTime)
        {
            if (Ctx.Document == null || Ctx.Definition == null) return;

            var pts = Ctx.Telemetry.SamplePoints;
            if (pts == null || pts.Length == 0) return;

            foreach (var d in Ctx.Definition.objectives)
            {
                if (string.IsNullOrEmpty(d.id)) continue;
                if (!_byId.TryGetValue(d.id, out var r)) continue;
                if (r.State != ObjectiveState.Pending && r.State != ObjectiveState.Active) continue;

                switch (d.type)
                {
                    case ObjectiveType.PassStructure:
                        TickPassStructure(d, r, pts);
                        break;
                    case ObjectiveType.AvoidObject:
                        TickAvoidObject(d, r, pts);
                        break;
                }
            }
        }

        public override void OnTriggerEntered(TriggerEnteredEvent e)
        {
            if (Ctx.Definition == null) return;
            foreach (var d in Ctx.Definition.objectives)
            {
                if (string.IsNullOrEmpty(d.id)) continue;
                if (!_byId.TryGetValue(d.id, out var r)) continue;
                if (r.State != ObjectiveState.Pending && r.State != ObjectiveState.Active) continue;

                switch (d.type)
                {
                    case ObjectiveType.Trigger:
                        if (string.Equals(d.targetId, e.TriggerId, StringComparison.Ordinal))
                            Pass(r);
                        break;
                    case ObjectiveType.Finish:
                        if (e.Type == TriggerType.Finish)
                            Pass(r);
                        break;
                }
            }
        }

        public override void OnTriggerExited(TriggerExitedEvent e)
        {
            if (e.Type != TriggerType.SlowZone || Ctx.Definition == null) return;
            foreach (var d in Ctx.Definition.objectives)
            {
                if (d.type != ObjectiveType.SlowZone || string.IsNullOrEmpty(d.id)) continue;
                if (!_byId.TryGetValue(d.id, out var r)) continue;
                if (r.State != ObjectiveState.Pending) continue;
                if (!string.Equals(d.targetId, e.TriggerId, StringComparison.Ordinal)) continue;

                var m = FindSlowZoneMeasurement(e.TriggerId);
                if (m == null) continue;
                if (m.Passed) Pass(r);
                else Fail(r);
            }
        }

        public override void OnSpeedMeasured(SpeedMeasuredEvent e)
        {
            if (Ctx.Definition == null) return;
            foreach (var d in Ctx.Definition.objectives)
            {
                if (d.type != ObjectiveType.SpeedPair || string.IsNullOrEmpty(d.id)) continue;
                if (!_byId.TryGetValue(d.id, out var r)) continue;
                if (r.State != ObjectiveState.Pending) continue;
                if (!string.Equals(d.pairId, e.PairId, StringComparison.Ordinal)) continue;

                if (d.maxSpeedCmS <= 0f || e.SpeedCmS <= d.maxSpeedCmS + 0.001f)
                    Pass(r);
                else
                    Fail(r);
            }
        }

        public override void OnVehicleCollision(VehicleCollisionEvent e)
        {
            if (Ctx.Definition == null) return;
            foreach (var d in Ctx.Definition.objectives)
            {
                if (string.IsNullOrEmpty(d.id)) continue;
                if (!_byId.TryGetValue(d.id, out var r)) continue;
                if (r.State != ObjectiveState.Pending && r.State != ObjectiveState.Active) continue;

                switch (d.type)
                {
                    case ObjectiveType.PassStructure:
                    case ObjectiveType.AvoidObject:
                        if (string.Equals(d.targetId, e.ObjectId, StringComparison.Ordinal))
                            Fail(r);
                        break;
                }
            }
        }

        public override void FinalizeRule()
        {
            // Resolve anything still pending (Step 10.14/10.15): incomplete
            // required objectives fail; untouched non-required objectives are
            // skipped; slow-zone objectives resolve from the final measurement.
            if (Ctx.Definition != null)
            {
                foreach (var d in Ctx.Definition.objectives)
                {
                    if (string.IsNullOrEmpty(d.id)) continue;
                    if (!_byId.TryGetValue(d.id, out var r)) continue;
                    if (r.State != ObjectiveState.Pending && r.State != ObjectiveState.Active) continue;

                    switch (d.type)
                    {
                        case ObjectiveType.SlowZone:
                            var m = FindSlowZoneMeasurement(d.targetId);
                            if (m != null) { if (m.Passed) Pass(r); else Fail(r); }
                            else if (d.required) Fail(r);
                            else r.State = ObjectiveState.Skipped;
                            break;

                        case ObjectiveType.SpeedPair:
                            // Missing terminal measurement (Step 10.13): A crossed,
                            // B never crossed → objective FAILED, not silently dropped.
                            if (d.required) Fail(r); else r.State = ObjectiveState.Skipped;
                            break;

                        case ObjectiveType.AvoidObject:
                            // Entered (and never collided) → pass, even if still
                            // inside the region when the run ends.
                            if (_enteredObjectIds.Contains(d.targetId)) Pass(r);
                            else if (d.required) Fail(r);
                            else r.State = ObjectiveState.Skipped;
                            break;

                        case ObjectiveType.Finish:
                        case ObjectiveType.Trigger:
                        case ObjectiveType.PassStructure:
                            if (d.required) Fail(r); else r.State = ObjectiveState.Skipped;
                            break;
                    }
                }
            }

            // Record successes/failures into the session + score result. The
            // results were already mirrored into Session.Objectives at run start;
            // finalize just re-syncs the score result and fills any missing
            // penalty values.
            foreach (var r in _results)
            {
                if (r.State == ObjectiveState.Failed && r.Penalty == 0f)
                    r.Penalty = EffectivePenalty(r);
            }
            Ctx.Score.Result.Objectives.Clear();
            Ctx.Score.Result.Objectives.AddRange(_results);
        }

        // ================================================================
        //  Internals
        // ================================================================

        private void TickPassStructure(ObjectiveDefinition d, ObjectiveResult r, Vector3[] pts)
        {
            var structure = Ctx.Document.FindStructure(d.targetId);
            if (structure == null) return;

            bool inside = AnyPointInRegion(pts, structure.Region);
            if (inside)
            {
                if (r.State == ObjectiveState.Pending)
                {
                    r.State = ObjectiveState.Active;
                    _activeStructureIds.Add(d.targetId);
                    Ctx.Session.Events.Add(new ScenarioEvent(Ctx.Tick, Ctx.Time, $"{d.id} ENTER"));
                }
            }
            else if (r.State == ObjectiveState.Active && _activeStructureIds.Contains(d.targetId))
            {
                // Exited correctly (a collision would have failed it already).
                _activeStructureIds.Remove(d.targetId);
                Pass(r);
            }
        }

        private void TickAvoidObject(ObjectiveDefinition d, ObjectiveResult r, Vector3[] pts)
        {
            var obj = Ctx.Document.FindObject(d.targetId);
            if (obj == null) return;

            bool inside = false;
            foreach (var tile in obj.OccupiedTiles())
            {
                if (AnyPointInRegion(pts, new GridRegion(tile.X, tile.Z, 1, 1)))
                {
                    inside = true;
                    break;
                }
            }

            if (inside)
            {
                _enteredObjectIds.Add(d.targetId);
                if (r.State == ObjectiveState.Pending)
                {
                    r.State = ObjectiveState.Active;
                    Ctx.Session.Events.Add(new ScenarioEvent(Ctx.Tick, Ctx.Time, $"{d.id} ENTER"));
                }
            }
        }

        private void Pass(ObjectiveResult r)
        {
            if (r.State == ObjectiveState.Passed) return;
            r.State = ObjectiveState.Passed;
            r.Penalty = 0f;
            Ctx.Session.Events.Add(new ScenarioEvent(Ctx.Tick, Ctx.Time, $"{r.Id} PASS"));
        }

        private void Fail(ObjectiveResult r)
        {
            if (r.State == ObjectiveState.Failed) return;
            r.State = ObjectiveState.Failed;
            float penalty = EffectivePenalty(r);
            r.Penalty = penalty;
            if (penalty > 0f && Ctx.Score.ScoringEnabled)
            {
                Ctx.Score.AddPenalty(new PenaltyRecord(
                    RuleId,
                    $"Objective {r.Id} failed",
                    penalty,
                    Ctx.Tick,
                    Ctx.Time,
                    "objective_failure",
                    r.Id));
            }
            Ctx.Session.Events.Add(new ScenarioEvent(Ctx.Tick, Ctx.Time, $"{r.Id} FAIL"));
        }

        private float EffectivePenalty(ObjectiveResult r)
        {
            ObjectiveDefinition def = null;
            if (Ctx.Definition != null)
            {
                foreach (var d in Ctx.Definition.objectives)
                    if (string.Equals(d.id, r.Id, StringComparison.Ordinal)) { def = d; break; }
            }
            float penalty = def != null && def.failurePenalty >= 0f
                ? def.failurePenalty
                : (Ctx.Definition?.scoring?.objectiveFailurePenalty ?? 0f);
            return penalty > 0f ? penalty : 0f;
        }

        private SlowZoneMeasurement FindSlowZoneMeasurement(string triggerId)
        {
            foreach (var m in Ctx.Session.SlowZones)
                if (string.Equals(m.TriggerId, triggerId, StringComparison.Ordinal))
                    return m;
            return null;
        }

        private bool AnyPointInRegion(Vector3[] pts, GridRegion region)
        {
            float ts = Ctx.Document.Grid.TileSizeCm;
            foreach (var p in pts)
            {
                var c = new GridCoordinate(
                    Mathf.FloorToInt(p.x / ts),
                    Mathf.FloorToInt(p.z / ts));
                if (region.Contains(c))
                    return true;
            }
            return false;
        }
    }
}
