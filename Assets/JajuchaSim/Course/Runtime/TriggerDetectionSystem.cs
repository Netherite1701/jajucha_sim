using System.Collections.Generic;
using JajuchaSim.Core;
using UnityEngine;

namespace JajuchaSim.Course
{
    /// <summary>
    /// Runtime system that detects vehicle interaction with course triggers.
    ///
    /// Responsibilities:
    ///   - Track entered/exited state for each trigger instance (by id).
    ///   - Publish TriggerEnteredEvent / TriggerExitedEvent on transitions.
    ///   - Detect speed-terminal crossings via segment-through-line (not colliders).
    ///   - Publish <see cref="SpeedTerminalCrossedEvent"/> (and legacy
    ///     <see cref="SpeedGateCrossedEvent"/>) when a terminal line is crossed.
    ///   - Publish CourseEventTriggeredEvent for generic event triggers.
    ///
    /// Detection is purely geometric / grid-based (no physics triggers).
    /// Official competition speed is computed by <see cref="SpeedTerminalPairRule"/>.
    /// </summary>
    public sealed class TriggerDetectionSystem : ISimulationSystem
    {
        private CourseGrid _grid;
        private CourseDocument _document;

        /// <summary>
        /// Delegate that returns the vehicle's current world pose.
        /// Set by the vehicle system (or tests) during init.
        /// </summary>
        public System.Func<VehiclePose> GetVehiclePose;

        private VehiclePose _prevPose;
        private bool _hasPrevPose;

        // Trigger ids the vehicle was inside on the previous tick.
        private readonly HashSet<string> _wasInside = new HashSet<string>();

        private SimulationEventBus _events;
        private SimulationClock _clock;

        // Cached speed-terminal line segments.
        private readonly List<TerminalLine> _terminals = new List<TerminalLine>();
        private bool _gatesDirty = true;

        // Active region triggers (id → instance snapshot).
        private readonly List<TriggerInstance> _regionTriggers = new List<TriggerInstance>();

        public TriggerDetectionSystem(CourseGrid grid)
        {
            _grid = grid;
        }

        public TriggerDetectionSystem(CourseDocument document)
        {
            _document = document;
            _grid = document?.Grid;
        }

        /// <summary>Replace the active course (e.g. after load / editor change).</summary>
        public void SetCourse(CourseDocument document)
        {
            _document = document;
            _grid = document?.Grid;
            _gatesDirty = true;
            _wasInside.Clear();
            _hasPrevPose = false;
        }

        public void SetGrid(CourseGrid grid)
        {
            _grid = grid;
            if (_document == null || _document.Grid != grid)
                _document = null;
            _gatesDirty = true;
            _wasInside.Clear();
            _hasPrevPose = false;
        }

        public void Initialize(SimulationContext context)
        {
            _events = context?.Events;
            _clock = context?.Clock;
            _gatesDirty = true;
        }

        public void SimulationTick(float deltaTime)
        {
            if (_events == null) return;
            if (_grid == null && _document == null) return;

            RefreshTriggerCache();

            if (GetVehiclePose == null)
            {
                _hasPrevPose = false;
                return;
            }

            var pose = GetVehiclePose();
            var currentIds = GetOccupiedTriggerIds(pose);

            DetectRegionTransitions(currentIds);

            if (_hasPrevPose)
                DetectGateCrossings(_prevPose, pose);

            _prevPose = pose;
            _hasPrevPose = true;
        }

        public void ResetSimulation()
        {
            _wasInside.Clear();
            _hasPrevPose = false;
            _prevPose = default;
            _gatesDirty = true;
            _terminals.Clear();
            _regionTriggers.Clear();
        }

        public void Shutdown()
        {
            _events = null;
            _clock = null;
            GetVehiclePose = null;
            _wasInside.Clear();
            _terminals.Clear();
            _regionTriggers.Clear();
            _hasPrevPose = false;
        }

        // ================================================================
        //  Cache
        // ================================================================

        private void RefreshTriggerCache()
        {
            if (!_gatesDirty) return;
            _gatesDirty = false;
            _terminals.Clear();
            _regionTriggers.Clear();

            float ts = _grid != null ? _grid.TileSizeCm : 20f;

            if (_document != null)
            {
                foreach (var t in _document.Triggers)
                {
                    if (t.IsSpeedTerminal)
                    {
                        _terminals.Add(BuildTerminalLine(t, ts));
                    }
                    else
                    {
                        _regionTriggers.Add(t);
                    }
                }
                return;
            }

            // Grid-only fallback: synthesize 1×1 triggers from tile layer.
            if (_grid == null) return;
            foreach (var kv in _grid.AllTriggers())
            {
                if (kv.Value == TriggerType.SpeedTerminal)
                {
                    var synthetic = TriggerInstance.SpeedTerminal(
                        $"speed_terminal_{kv.Key.X}_{kv.Key.Z}",
                        kv.Key.X, kv.Key.Z, GridEdge.North,
                        pairId: null, SpeedTerminalRole.A, 1);
                    _terminals.Add(BuildTerminalLine(synthetic, ts));
                }
                else
                {
                    _regionTriggers.Add(new TriggerInstance(
                        $"{kv.Value.ToString().ToLowerInvariant()}_{kv.Key.X}_{kv.Key.Z}",
                        kv.Value,
                        new GridRegion(kv.Key.X, kv.Key.Z, 1, 1)));
                }
            }
        }

        private static TerminalLine BuildTerminalLine(TriggerInstance t, float tileSizeCm)
        {
            SpeedTerminalGeometry.GetLineEndpoints(t, tileSizeCm, out var p0, out var p1);
            return new TerminalLine
            {
                Id = t.Id,
                PairId = t.PairId,
                Role = t.TerminalRole,
                P0 = p0,
                P1 = p1
            };
        }

        // ================================================================
        //  Region enter/exit
        // ================================================================

        private HashSet<string> GetOccupiedTriggerIds(VehiclePose pose)
        {
            var ids = new HashSet<string>();
            var tiles = GetOccupiedTiles(pose);

            foreach (var trigger in _regionTriggers)
            {
                foreach (var tile in trigger.OccupiedTiles())
                {
                    if (tiles.Contains(tile))
                    {
                        ids.Add(trigger.Id);
                        break;
                    }
                }
            }
            return ids;
        }

        private void DetectRegionTransitions(HashSet<string> nowInside)
        {
            // Enter
            foreach (var id in nowInside)
            {
                if (_wasInside.Contains(id)) continue;

                var trigger = FindRegionTrigger(id);
                if (trigger == null) continue;

                var tile = trigger.OccupiedTiles().Length > 0
                    ? trigger.OccupiedTiles()[0]
                    : default;

                _events.Publish(new TriggerEnteredEvent(tile, trigger.Type, trigger.Id));

                if (trigger.Type == TriggerType.EventTrigger && !string.IsNullOrEmpty(trigger.EventId))
                {
                    _events.Publish(new CourseEventTriggeredEvent(trigger.EventId, trigger.Id, true));
                }
            }

            // Exit
            foreach (var id in _wasInside)
            {
                if (nowInside.Contains(id)) continue;

                var trigger = FindRegionTrigger(id);
                if (trigger == null) continue;

                var tile = trigger.OccupiedTiles().Length > 0
                    ? trigger.OccupiedTiles()[0]
                    : default;

                _events.Publish(new TriggerExitedEvent(tile, trigger.Type, trigger.Id));

                if (trigger.Type == TriggerType.EventTrigger && !string.IsNullOrEmpty(trigger.EventId))
                {
                    _events.Publish(new CourseEventTriggeredEvent(trigger.EventId, trigger.Id, false));
                }
            }

            _wasInside.Clear();
            foreach (var id in nowInside)
                _wasInside.Add(id);
        }

        private TriggerInstance FindRegionTrigger(string id)
        {
            foreach (var t in _regionTriggers)
                if (t.Id == id) return t;
            return null;
        }

        // ================================================================
        //  Speed terminals (line crossing)
        // ================================================================

        private void DetectGateCrossings(VehiclePose prevPose, VehiclePose currPose)
        {
            Vector3 p0 = prevPose.Position;
            Vector3 p1 = currPose.Position;
            p0.y = 0f;
            p1.y = 0f;

            // Also try sample-point segments if available (more robust).
            var segments = new List<(Vector3 a, Vector3 b)>();
            segments.Add((p0, p1));

            if (prevPose.SamplePoints != null && currPose.SamplePoints != null &&
                prevPose.SamplePoints.Length == currPose.SamplePoints.Length)
            {
                for (int i = 0; i < prevPose.SamplePoints.Length; i++)
                {
                    var a = prevPose.SamplePoints[i]; a.y = 0f;
                    var b = currPose.SamplePoints[i]; b.y = 0f;
                    segments.Add((a, b));
                }
            }

            // Authoritative SimulationClock time (not wall clock).
            // Manager advances the clock at the start of each tick, so Time is the
            // end of the current tick and motion P0→P1 spans [Time-dt, Time].
            // When Tick==0 (unit tests calling SimulationTick without Advance),
            // fall back to Time as-is.
            double tickEnd = _clock != null ? _clock.Time : 0.0;
            float dt = _clock != null ? _clock.FixedDeltaTime : 0f;
            bool canInterpolate = _clock != null && _clock.Tick > 0 && dt > 0f;

            foreach (var terminal in _terminals)
            {
                foreach (var seg in segments)
                {
                    if (SegmentsIntersect(seg.a, seg.b, terminal.P0, terminal.P1))
                    {
                        float t = EstimateCrossingTime(seg.a, seg.b, terminal.P0, terminal.P1);
                        double simTime = canInterpolate
                            ? tickEnd - (1.0 - t) * dt
                            : tickEnd;

                        _events.Publish(new SpeedTerminalCrossedEvent(
                            terminal.Id,
                            terminal.PairId,
                            terminal.Role,
                            simTime,
                            seg.a,
                            seg.b,
                            t));

                        // Legacy alias for older subscribers / tests.
                        _events.Publish(new SpeedGateCrossedEvent(
                            terminal.Id, seg.a, seg.b, t, simTime, terminal.PairId, terminal.Role));
                        break; // one event per terminal per tick
                    }
                }
            }
        }

        // ================================================================
        //  Sampling
        // ================================================================

        private HashSet<GridCoordinate> GetOccupiedTiles(VehiclePose pose)
        {
            var tiles = new HashSet<GridCoordinate>();
            float ts = _grid != null ? _grid.TileSizeCm : 20f;

            void Add(Vector3 p)
            {
                int x = Mathf.FloorToInt(p.x / ts);
                int z = Mathf.FloorToInt(p.z / ts);
                tiles.Add(new GridCoordinate(x, z));
            }

            if (pose.SamplePoints != null && pose.SamplePoints.Length > 0)
            {
                foreach (var pt in pose.SamplePoints)
                    Add(pt);
            }
            else
            {
                Add(pose.Position);
            }

            return tiles;
        }

        // ================================================================
        //  2D segment intersection (XZ plane)
        // ================================================================

        /// <summary>
        /// True if segment A0–A1 properly intersects segment B0–B1 (including touching).
        /// </summary>
        public static bool SegmentsIntersect(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
        {
            Vector2 A0 = new Vector2(a0.x, a0.z);
            Vector2 A1 = new Vector2(a1.x, a1.z);
            Vector2 B0 = new Vector2(b0.x, b0.z);
            Vector2 B1 = new Vector2(b1.x, b1.z);

            float d1 = Cross(B0, B1, A0);
            float d2 = Cross(B0, B1, A1);
            float d3 = Cross(A0, A1, B0);
            float d4 = Cross(A0, A1, B1);

            const float eps = 1e-6f;

            // Proper intersection: endpoints on opposite sides of each segment's line
            bool oppAb = (d1 > eps && d2 < -eps) || (d1 < -eps && d2 > eps);
            bool oppBa = (d3 > eps && d4 < -eps) || (d3 < -eps && d4 > eps);
            if (oppAb && oppBa) return true;

            // Touching / collinear endpoint cases
            if (Mathf.Abs(d1) <= eps && IsBetween(A0, B0, B1)) return true;
            if (Mathf.Abs(d2) <= eps && IsBetween(A1, B0, B1)) return true;
            if (Mathf.Abs(d3) <= eps && IsBetween(B0, A0, A1)) return true;
            if (Mathf.Abs(d4) <= eps && IsBetween(B1, A0, A1)) return true;

            return false;
        }

        private static float Cross(Vector2 p, Vector2 q, Vector2 r)
            => (q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x);

        private static bool IsBetween(Vector2 pt, Vector2 a, Vector2 b)
        {
            float minX = Mathf.Min(a.x, b.x) - 1e-4f;
            float maxX = Mathf.Max(a.x, b.x) + 1e-4f;
            float minY = Mathf.Min(a.y, b.y) - 1e-4f;
            float maxY = Mathf.Max(a.y, b.y) + 1e-4f;
            return pt.x >= minX && pt.x <= maxX && pt.y >= minY && pt.y <= maxY;
        }

        private static float EstimateCrossingTime(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
        {
            Vector2 A0 = new Vector2(a0.x, a0.z);
            Vector2 A1 = new Vector2(a1.x, a1.z);
            Vector2 B0 = new Vector2(b0.x, b0.z);
            Vector2 B1 = new Vector2(b1.x, b1.z);

            Vector2 d = A1 - A0;
            Vector2 e = B1 - B0;
            float denom = d.x * e.y - d.y * e.x;
            if (Mathf.Abs(denom) < 1e-10f) return 0.5f;

            Vector2 r = B0 - A0;
            float t = (r.x * e.y - r.y * e.x) / denom;
            return Mathf.Clamp01(t);
        }
    }

    // ================================================================
    //  Event types
    // ================================================================

    /// <summary>Published when the vehicle enters a trigger region.</summary>
    public readonly struct TriggerEnteredEvent
    {
        public GridCoordinate Tile { get; }
        public TriggerType Type { get; }
        public string TriggerId { get; }

        public TriggerEnteredEvent(GridCoordinate tile, TriggerType type, string triggerId = null)
        {
            Tile = tile;
            Type = type;
            TriggerId = triggerId;
        }

        public override string ToString()
            => $"ENTER {(TriggerId ?? Type.ToString())} at {Tile}";
    }

    /// <summary>Published when the vehicle exits a trigger region.</summary>
    public readonly struct TriggerExitedEvent
    {
        public GridCoordinate Tile { get; }
        public TriggerType Type { get; }
        public string TriggerId { get; }

        public TriggerExitedEvent(GridCoordinate tile, TriggerType type, string triggerId = null)
        {
            Tile = tile;
            Type = type;
            TriggerId = triggerId;
        }

        public override string ToString()
            => $"EXIT {(TriggerId ?? Type.ToString())} at {Tile}";
    }

    /// <summary>
    /// Legacy event published when the vehicle crosses a speed terminal line.
    /// Prefer <see cref="SpeedTerminalCrossedEvent"/> for new code.
    /// </summary>
    public readonly struct SpeedGateCrossedEvent
    {
        public string GateId { get; }
        public Vector3 PreviousPosition { get; }
        public Vector3 CurrentPosition { get; }
        public float CrossingT { get; }
        public double SimTime { get; }
        public string PairId { get; }
        public SpeedTerminalRole Role { get; }

        public SpeedGateCrossedEvent(
            string gateId,
            Vector3 prevPos,
            Vector3 currPos,
            float crossingT,
            double simTime = 0.0,
            string pairId = null,
            SpeedTerminalRole role = SpeedTerminalRole.A)
        {
            GateId = gateId;
            PreviousPosition = prevPos;
            CurrentPosition = currPos;
            CrossingT = crossingT;
            SimTime = simTime;
            PairId = pairId;
            Role = role;
        }

        public override string ToString() => $"CROSS {GateId}";
    }

    /// <summary>
    /// Published for generic event triggers (enter=true) and on exit (enter=false).
    /// Scenario/scoring systems subscribe to <see cref="EventId"/>.
    /// </summary>
    public readonly struct CourseEventTriggeredEvent
    {
        public string EventId { get; }
        public string TriggerId { get; }
        public bool IsEnter { get; }

        public CourseEventTriggeredEvent(string eventId, string triggerId, bool isEnter)
        {
            EventId = eventId;
            TriggerId = triggerId;
            IsEnter = isEnter;
        }

        public override string ToString()
            => $"{(IsEnter ? "ENTER" : "EXIT")} event:{EventId}";
    }

    /// <summary>
    /// Vehicle pose snapshot used by TriggerDetectionSystem.
    /// </summary>
    public struct VehiclePose
    {
        public Vector3 Position;
        /// <summary>
        /// Optional sample points (centre, FL, FR, RL, RR). Falls back to centre.
        /// </summary>
        public Vector3[] SamplePoints;
    }

    internal struct TerminalLine
    {
        public string Id;
        public string PairId;
        public SpeedTerminalRole Role;
        public Vector3 P0;
        public Vector3 P1;
    }
}
