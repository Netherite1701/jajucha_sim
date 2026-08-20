using System;
using System.Collections.Generic;
using System.Linq;

namespace JajuchaSim.Course
{
    /// <summary>
    /// Authoritative course document holding feature instances (structures,
    /// objects, triggers) plus a compact <see cref="CourseGrid"/> used for
    /// fast per-tile lookups at runtime.
    ///
    /// Layers may overlap: a tunnel does not replace the road underneath it.
    /// The grid stores IDs/types compactly; full parameters live on instances.
    /// </summary>
    public sealed class CourseDocument
    {
        private readonly List<StructureInstance> _structures = new List<StructureInstance>();
        private readonly List<CourseObjectInstance> _objects = new List<CourseObjectInstance>();
        private readonly List<TriggerInstance> _triggers = new List<TriggerInstance>();
        private readonly HashSet<string> _ids = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Lookup grid kept in sync with instances + road tiles.</summary>
        public CourseGrid Grid { get; }

        /// <summary>2026 competition metadata for shipped competition courses.</summary>
        public Competition2026Data Competition2026 { get; private set; }

        public IReadOnlyList<StructureInstance> Structures => _structures;
        public IReadOnlyList<CourseObjectInstance> Objects => _objects;
        public IReadOnlyList<TriggerInstance> Triggers => _triggers;

        public CourseDocument(float tileSizeCm = 20f)
        {
            Grid = new CourseGrid(tileSizeCm);
        }

        public CourseDocument(CourseGrid grid)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        // ================================================================
        //  Road
        // ================================================================

        public void SetRoad(GridCoordinate coord) => Grid.SetRoad(coord);
        public void SetRoad(IEnumerable<GridCoordinate> coords) => Grid.SetRoad(coords);
        public void ClearRoad(GridCoordinate coord) => Grid.ClearRoad(coord);
        public bool HasRoad(GridCoordinate coord) => Grid.HasRoad(coord);

        // ================================================================
        //  Boundary lines (Step 10 — road-surface markings)
        // ================================================================

        public void SetLine(GridCoordinate coord) => Grid.SetLine(coord);
        public void SetLine(IEnumerable<GridCoordinate> coords) => Grid.SetLine(coords);
        public void ClearLine(GridCoordinate coord) => Grid.ClearLine(coord);
        public bool HasLine(GridCoordinate coord) => Grid.HasLine(coord);

        // ================================================================
        //  Structures
        // ================================================================

        /// <summary>
        /// Place a tunnel covering <paramref name="region"/>.
        /// Returns the created instance, or null if the id collides.
        /// </summary>
        public StructureInstance PlaceTunnel(GridRegion region, float heightCm = 55f, float wallThicknessCm = 2f, string id = null)
        {
            id = EnsureUniqueId(id, "tunnel");
            var inst = new StructureInstance(id, StructureType.Tunnel, region)
            {
                HeightCm = heightCm,
                WallThicknessCm = wallThicknessCm
            };
            AddStructureInternal(inst);
            return inst;
        }

        /// <summary>
        /// Place a ramp covering <paramref name="region"/>.
        /// </summary>
        public StructureInstance PlaceRamp(GridRegion region, GridDirection direction, float riseCm = 30f, string id = null)
        {
            id = EnsureUniqueId(id, "ramp");
            var inst = new StructureInstance(id, StructureType.Ramp, region)
            {
                Direction = direction,
                RiseCm = riseCm,
                HeightCm = riseCm
            };
            AddStructureInternal(inst);
            return inst;
        }

        public bool RemoveStructure(string id)
        {
            var inst = FindStructure(id);
            if (inst == null) return false;
            RemoveStructureInternal(inst);
            return true;
        }

        public StructureInstance FindStructure(string id)
            => _structures.FirstOrDefault(s => s.Id == id);

        public StructureInstance FindStructureAt(GridCoordinate tile)
            => _structures.FirstOrDefault(s => s.Region.Contains(tile));

        /// <summary>Move a structure by a whole-tile delta.</summary>
        public bool MoveStructure(string id, int dx, int dz)
        {
            var inst = FindStructure(id);
            if (inst == null) return false;

            // Clear old footprint from grid
            foreach (var c in inst.Region.ToCoordinates())
                Grid.ClearStructure(c);

            inst.Region = new GridRegion(inst.Region.x + dx, inst.Region.z + dz, inst.Region.width, inst.Region.height);

            foreach (var c in inst.Region.ToCoordinates())
                Grid.SetStructure(c, inst.Type);
            return true;
        }

        /// <summary>Resize a structure to a new region (snapped to whole tiles).</summary>
        public bool ResizeStructure(string id, GridRegion newRegion)
        {
            var inst = FindStructure(id);
            if (inst == null || !newRegion.IsValid) return false;

            foreach (var c in inst.Region.ToCoordinates())
                Grid.ClearStructure(c);

            inst.Region = newRegion;

            foreach (var c in inst.Region.ToCoordinates())
                Grid.SetStructure(c, inst.Type);
            return true;
        }

        /// <summary>Rotate a tunnel/ramp 90° about its region centre (swaps w/h).</summary>
        public bool RotateStructure(string id)
        {
            var inst = FindStructure(id);
            if (inst == null) return false;

            foreach (var c in inst.Region.ToCoordinates())
                Grid.ClearStructure(c);

            var r = inst.Region;
            // Keep origin, swap width/height
            inst.Region = new GridRegion(r.x, r.z, r.height, r.width);

            if (inst.Type == StructureType.Ramp)
                inst.Direction = (GridDirection)(((int)inst.Direction + 1) % 4);

            foreach (var c in inst.Region.ToCoordinates())
                Grid.SetStructure(c, inst.Type);
            return true;
        }

        // ================================================================
        //  Objects
        // ================================================================

        public CourseObjectInstance PlaceObject(
            ObjectType type,
            GridCoordinate tile,
            int rotationDeg = 0,
            ObstacleFootprint footprint = ObstacleFootprint.Small,
            string id = null)
        {
            string prefix = type switch
            {
                ObjectType.Obstacle => "obstacle",
                ObjectType.Sign => "slow_sign",
                ObjectType.StartSignal => "start_signal",
                ObjectType.YellowFlag => "yellow_flag",
                ObjectType.PitBarrier => "pit_barrier",
                ObjectType.DynamicObstacle => "dynamic_obstacle",
                _ => "object"
            };
            id = EnsureUniqueId(id, prefix);

            var inst = new CourseObjectInstance(id, type, tile)
            {
                RotationDeg = GridOrientationUtil.NormalizeRotation(rotationDeg),
                Footprint = footprint
            };
            AddObjectInternal(inst);
            return inst;
        }

        public bool RemoveObject(string id)
        {
            var inst = FindObject(id);
            if (inst == null) return false;
            RemoveObjectInternal(inst);
            return true;
        }

        public CourseObjectInstance FindObject(string id)
            => _objects.FirstOrDefault(o => o.Id == id);

        public CourseObjectInstance FindObjectAt(GridCoordinate tile)
            => _objects.FirstOrDefault(o =>
            {
                foreach (var t in o.OccupiedTiles())
                    if (t.Equals(tile)) return true;
                return false;
            });

        public bool MoveObject(string id, GridCoordinate newTile)
        {
            var inst = FindObject(id);
            if (inst == null) return false;

            foreach (var t in inst.OccupiedTiles())
                Grid.ClearObject(t);

            inst.Tile = newTile;

            foreach (var t in inst.OccupiedTiles())
                Grid.SetObject(t, inst.Type);
            return true;
        }

        public bool RotateObject(string id, int deltaDeg = 90)
        {
            var inst = FindObject(id);
            if (inst == null) return false;

            foreach (var t in inst.OccupiedTiles())
                Grid.ClearObject(t);

            inst.RotationDeg = GridOrientationUtil.NormalizeRotation(inst.RotationDeg + deltaDeg);

            foreach (var t in inst.OccupiedTiles())
                Grid.SetObject(t, inst.Type);
            return true;
        }

        // ================================================================
        //  Triggers
        // ================================================================

        public TriggerInstance PlaceTrigger(TriggerType type, GridRegion region, string eventId = null, string id = null)
        {
            string prefix = type switch
            {
                TriggerType.SlowZone => "slow_zone",
                TriggerType.Start => "start",
                TriggerType.Finish => "finish",
                TriggerType.EventTrigger => "event",
                TriggerType.SpeedTerminal => "speed_terminal",
                _ => "trigger"
            };
            id = EnsureUniqueId(id, prefix);

            var inst = new TriggerInstance(id, type, region)
            {
                EventId = eventId
            };
            AddTriggerInternal(inst);
            return inst;
        }

        /// <summary>
        /// Place a competition-style speed measurement terminal (edge-snapped line).
        /// Pair with a second terminal sharing <paramref name="pairId"/> (roles A and B).
        /// Distance is derived from world positions at measurement time.
        /// </summary>
        public TriggerInstance PlaceSpeedTerminal(
            int cellX,
            int cellZ,
            GridEdge edge,
            string pairId,
            SpeedTerminalRole role,
            int widthTiles = 1,
            string id = null)
        {
            string prefix = role == SpeedTerminalRole.B ? "speed_b" : "speed_a";
            id = EnsureUniqueId(id, prefix);
            if (string.IsNullOrEmpty(pairId))
                pairId = "speed_zone_01";

            var inst = TriggerInstance.SpeedTerminal(id, cellX, cellZ, edge, pairId, role, widthTiles);
            AddTriggerInternal(inst);
            return inst;
        }

        /// <summary>Backward-compatible single-terminal placement (role A, default pair).</summary>
        public TriggerInstance PlaceSpeedGate(int cellX, int cellZ, GridEdge edge, string id = null)
        {
            return PlaceSpeedTerminal(cellX, cellZ, edge, pairId: null, SpeedTerminalRole.A, 1, id);
        }

        public bool RemoveTrigger(string id)
        {
            var inst = FindTrigger(id);
            if (inst == null) return false;
            RemoveTriggerInternal(inst);
            return true;
        }

        public TriggerInstance FindTrigger(string id)
            => _triggers.FirstOrDefault(t => t.Id == id);

        public IEnumerable<TriggerInstance> FindTriggersAt(GridCoordinate tile)
        {
            foreach (var t in _triggers)
            {
                foreach (var c in t.OccupiedTiles())
                {
                    if (c.Equals(tile))
                    {
                        yield return t;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Paint a trigger type onto individual tiles, merging into an existing
        /// region trigger of the same type when adjacent, otherwise creating one.
        /// For simplicity Step 7 creates/extends a single multi-tile region
        /// covering the painted tiles' bounding box when type matches.
        /// </summary>
        public TriggerInstance PaintTriggerTiles(IEnumerable<GridCoordinate> tiles, TriggerType type, string id = null)
        {
            var list = tiles.ToList();
            if (list.Count == 0) return null;

            int minX = list.Min(t => t.X);
            int maxX = list.Max(t => t.X);
            int minZ = list.Min(t => t.Z);
            int maxZ = list.Max(t => t.Z);
            var region = new GridRegion(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
            return PlaceTrigger(type, region, null, id);
        }

        // ================================================================
        //  Bulk / clear
        // ================================================================

        public void ClearAll()
        {
            _structures.Clear();
            _objects.Clear();
            _triggers.Clear();
            _ids.Clear();
            Grid.ClearAll();
            FeatureIdGenerator.Reset();
        }

        /// <summary>
        /// Rebuild the lookup grid from instances (road tiles preserved).
        /// </summary>
        public void RebuildGridLayers()
        {
            // Preserve road + boundary lines
            var roads = Grid.AllRoadTiles().ToList();
            var lines = Grid.AllLineTiles().ToList();
            Grid.ClearAll();
            foreach (var r in roads) Grid.SetRoad(r);
            foreach (var l in lines) Grid.SetLine(l);

            foreach (var s in _structures)
                foreach (var c in s.Region.ToCoordinates())
                    Grid.SetStructure(c, s.Type);

            foreach (var o in _objects)
                foreach (var c in o.OccupiedTiles())
                    Grid.SetObject(c, o.Type);

            foreach (var t in _triggers)
                foreach (var c in t.OccupiedTiles())
                    Grid.SetTrigger(c, t.Type);
        }

        // ================================================================
        //  Serialization helpers
        // ================================================================

        public CourseData ToData()
        {
            var data = new CourseData
            {
                tileSizeCm = Grid.TileSizeCm,
                competition2026 = Competition2026,
                road = Grid.AllRoadTiles().Select(CoordPair.FromGrid).ToArray(),
                lines = Grid.AllLineTiles().Select(CoordPair.FromGrid).ToArray(),
                structures = _structures.Select(ToStructureEntry).ToArray(),
                objects = _objects.Select(ToObjectEntry).ToArray(),
                triggers = _triggers.Select(ToTriggerEntry).ToArray()
            };
            return data;
        }

        public static CourseDocument FromData(CourseData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var doc = new CourseDocument(data.tileSizeCm);
            doc.Competition2026 = data.competition2026;

            if (data.road != null)
            {
                foreach (var p in data.road)
                    doc.Grid.SetRoad(p.ToGrid());
            }

            if (data.lines != null)
            {
                foreach (var p in data.lines)
                    doc.Grid.SetLine(p.ToGrid());
            }

            if (data.structures != null)
            {
                foreach (var e in data.structures)
                {
                    var type = CourseSerializer.ParseStructureType(e.type);
                    if (type == StructureType.None || !e.region.IsValid) continue;

                    var id = string.IsNullOrEmpty(e.id)
                        ? FeatureIdGenerator.NextId(type.ToString().ToLowerInvariant())
                        : e.id;

                    var inst = new StructureInstance(id, type, e.region)
                    {
                        HeightCm = e.heightCm > 0 ? e.heightCm : 55f,
                        WallThicknessCm = e.wallThicknessCm > 0 ? e.wallThicknessCm : 2f,
                        RiseCm = e.riseCm > 0 ? e.riseCm : (e.heightCm > 0 ? e.heightCm : 30f),
                        Direction = GridOrientationUtil.ParseDirection(e.direction),
                        Profile = string.IsNullOrEmpty(e.profile) ? "rectangular" : e.profile,
                        OpeningWidthCm = e.openingWidthCm > 0 ? e.openingWidthCm : Competition2026Specification.TunnelOpeningWidthCm,
                        RoofLongCm = e.roofLongCm > 0 ? e.roofLongCm : Competition2026Specification.TunnelRoofLongCm,
                        RoofShortCm = e.roofShortCm > 0 ? e.roofShortCm : Competition2026Specification.TunnelRoofShortCm,
                        PathPoints = e.pathPoints ?? Array.Empty<StructurePathPointData>()
                    };
                    if (type == StructureType.Ramp && inst.HeightCm <= 0)
                        inst.HeightCm = inst.RiseCm;
                    doc.AddStructureInternal(inst);
                }
            }

            if (data.objects != null)
            {
                foreach (var e in data.objects)
                {
                    if (e.tile == null) continue;
                    var type = CourseSerializer.ParseObjectType(e.type);
                    if (type == ObjectType.None) continue;

                    var id = string.IsNullOrEmpty(e.id)
                        ? FeatureIdGenerator.NextId(type.ToString().ToLowerInvariant())
                        : e.id;

                    var inst = new CourseObjectInstance(id, type, e.tile.ToGrid())
                    {
                        RotationDeg = GridOrientationUtil.NormalizeRotation(e.rotationDeg),
                        Footprint = ParseFootprint(e.footprint),
                        ObstacleWaitSec = e.obstacleWaitSec > 0f ? e.obstacleWaitSec : 3f,
                        ObstacleExitSec = e.obstacleExitSec > 0f ? e.obstacleExitSec : 1f
                    };
                    doc.AddObjectInternal(inst);
                }
            }

            if (data.triggers != null)
            {
                foreach (var e in data.triggers)
                {
                    var type = CourseSerializer.ParseTriggerType(e.type);
                    if (type == TriggerType.None) continue;

                    var id = string.IsNullOrEmpty(e.id)
                        ? FeatureIdGenerator.NextId(type.ToString().ToLowerInvariant())
                        : e.id;

                    TriggerInstance inst;
                    if (type == TriggerType.SpeedTerminal)
                    {
                        inst = TriggerInstance.SpeedTerminal(
                            id,
                            e.cellX,
                            e.cellZ,
                            GridOrientationUtil.ParseEdge(e.edge),
                            e.pairId,
                            GridOrientationUtil.ParseTerminalRole(e.terminal),
                            e.widthTiles > 0 ? e.widthTiles : 1);
                    }
                    else
                    {
                        if (!e.region.IsValid) continue;
                        inst = new TriggerInstance(id, type, e.region)
                        {
                            EventId = e.eventId
                        };
                    }
                    doc.AddTriggerInternal(inst);
                }
            }

            return doc;
        }

        public string ToJson(bool pretty = true)
            => UnityEngine.JsonUtility.ToJson(ToData(), pretty);

        public static CourseDocument FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                // Prefer full document load via serializer grid path first for
                // legacy, then lift into a document when possible.
                if (IsLegacyJson(json))
                {
                    var grid = CourseSerializer.FromJson(json);
                    if (grid == null) return null;
                    return FromGridLoose(grid);
                }

                var data = UnityEngine.JsonUtility.FromJson<CourseData>(json);
                if (data == null) return null;

                // If structures look empty-region, fall back to legacy.
                if (LooksLikeFailedNewFormat(data, json))
                {
                    var grid = CourseSerializer.FromJson(json);
                    if (grid == null) return null;
                    return FromGridLoose(grid);
                }

                return FromData(data);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[CourseDocument] Failed to parse JSON: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Build a document from a grid by treating each structure/object/trigger
        /// tile as a 1×1 instance (used when loading legacy formats).
        /// </summary>
        public static CourseDocument FromGridLoose(CourseGrid grid)
        {
            var doc = new CourseDocument(grid.TileSizeCm);
            foreach (var r in grid.AllRoadTiles())
                doc.Grid.SetRoad(r);
            foreach (var l in grid.AllLineTiles())
                doc.Grid.SetLine(l);

            foreach (var kv in grid.AllStructures())
            {
                var prefix = kv.Value.ToString().ToLowerInvariant();
                var id = FeatureIdGenerator.NextId(prefix);
                var inst = new StructureInstance(id, kv.Value, new GridRegion(kv.Key.X, kv.Key.Z, 1, 1));
                doc.AddStructureInternal(inst);
            }

            foreach (var kv in grid.AllObjects())
            {
                var prefix = kv.Value.ToString().ToLowerInvariant();
                var id = FeatureIdGenerator.NextId(prefix);
                var inst = new CourseObjectInstance(id, kv.Value, kv.Key);
                doc.AddObjectInternal(inst);
            }

            foreach (var kv in grid.AllTriggers())
            {
                var prefix = kv.Value.ToString().ToLowerInvariant();
                var id = FeatureIdGenerator.NextId(prefix);
                TriggerInstance inst;
                if (kv.Value == TriggerType.SpeedTerminal)
                    inst = TriggerInstance.SpeedTerminal(id, kv.Key.X, kv.Key.Z, GridEdge.North, null, SpeedTerminalRole.A, 1);
                else
                    inst = new TriggerInstance(id, kv.Value, new GridRegion(kv.Key.X, kv.Key.Z, 1, 1));
                doc.AddTriggerInternal(inst);
            }

            return doc;
        }

        // ================================================================
        //  Internals
        // ================================================================

        private void AddStructureInternal(StructureInstance inst)
        {
            RegisterId(inst.Id);
            _structures.Add(inst);
            foreach (var c in inst.Region.ToCoordinates())
                Grid.SetStructure(c, inst.Type);
        }

        private void RemoveStructureInternal(StructureInstance inst)
        {
            _structures.Remove(inst);
            _ids.Remove(inst.Id);
            foreach (var c in inst.Region.ToCoordinates())
            {
                // Only clear if no other structure covers this tile
                bool stillCovered = false;
                foreach (var other in _structures)
                {
                    if (other.Region.Contains(c))
                    {
                        Grid.SetStructure(c, other.Type);
                        stillCovered = true;
                        break;
                    }
                }
                if (!stillCovered)
                    Grid.ClearStructure(c);
            }
        }

        private void AddObjectInternal(CourseObjectInstance inst)
        {
            RegisterId(inst.Id);
            _objects.Add(inst);
            foreach (var c in inst.OccupiedTiles())
                Grid.SetObject(c, inst.Type);
        }

        private void RemoveObjectInternal(CourseObjectInstance inst)
        {
            _objects.Remove(inst);
            _ids.Remove(inst.Id);
            foreach (var c in inst.OccupiedTiles())
            {
                bool still = false;
                foreach (var other in _objects)
                {
                    foreach (var t in other.OccupiedTiles())
                    {
                        if (t.Equals(c))
                        {
                            Grid.SetObject(c, other.Type);
                            still = true;
                            break;
                        }
                    }
                    if (still) break;
                }
                if (!still) Grid.ClearObject(c);
            }
        }

        private void AddTriggerInternal(TriggerInstance inst)
        {
            RegisterId(inst.Id);
            _triggers.Add(inst);
            foreach (var c in inst.OccupiedTiles())
                Grid.SetTrigger(c, inst.Type);
        }

        private void RemoveTriggerInternal(TriggerInstance inst)
        {
            _triggers.Remove(inst);
            _ids.Remove(inst.Id);
            foreach (var c in inst.OccupiedTiles())
            {
                bool still = false;
                foreach (var other in _triggers)
                {
                    foreach (var t in other.OccupiedTiles())
                    {
                        if (t.Equals(c))
                        {
                            Grid.SetTrigger(c, other.Type);
                            still = true;
                            break;
                        }
                    }
                    if (still) break;
                }
                if (!still) Grid.ClearTrigger(c);
            }
        }

        private string EnsureUniqueId(string id, string prefix)
        {
            if (string.IsNullOrEmpty(id))
                id = FeatureIdGenerator.NextId(prefix);
            if (_ids.Contains(id))
            {
                // Auto-suffix until unique
                int n = 2;
                string candidate;
                do { candidate = $"{id}_{n++}"; }
                while (_ids.Contains(candidate));
                id = candidate;
            }
            return id;
        }

        private void RegisterId(string id)
        {
            if (!string.IsNullOrEmpty(id))
                _ids.Add(id);
        }

        private static CourseData.StructureEntry ToStructureEntry(StructureInstance s)
        {
            return new CourseData.StructureEntry
            {
                id = s.Id,
                type = s.Type.ToString().ToLowerInvariant(),
                region = s.Region,
                heightCm = s.Type == StructureType.Tunnel ? s.HeightCm : s.RiseCm,
                wallThicknessCm = s.WallThicknessCm,
                direction = GridOrientationUtil.DirectionToString(s.Direction),
                riseCm = s.RiseCm
                ,profile = s.Profile
                ,openingWidthCm = s.OpeningWidthCm
                ,roofLongCm = s.RoofLongCm
                ,roofShortCm = s.RoofShortCm
                ,pathPoints = s.PathPoints
            };
        }

        private static CourseData.ObjectEntry ToObjectEntry(CourseObjectInstance o)
        {
            string type = o.Type switch
            {
                ObjectType.Sign => "slow_sign",
                ObjectType.StartSignal => "start_signal",
                ObjectType.Obstacle => "obstacle",
                _ => o.Type.ToString().ToLowerInvariant()
            };
            return new CourseData.ObjectEntry
            {
                id = o.Id,
                type = type,
                tile = CoordPair.FromGrid(o.Tile),
                rotationDeg = o.RotationDeg,
                footprint = FootprintToString(o.Footprint),
                obstacleWaitSec = o.Type == ObjectType.DynamicObstacle ? o.ObstacleWaitSec : 0f,
                obstacleExitSec = o.Type == ObjectType.DynamicObstacle ? o.ObstacleExitSec : 0f
            };
        }

        private static CourseData.TriggerEntry ToTriggerEntry(TriggerInstance t)
        {
            string type = t.Type switch
            {
                TriggerType.SlowZone => "slow_zone",
                TriggerType.SpeedTerminal => "speed_terminal",
                TriggerType.EventTrigger => "event",
                TriggerType.Start => "start",
                TriggerType.Finish => "finish",
                _ => t.Type.ToString().ToLowerInvariant()
            };
            return new CourseData.TriggerEntry
            {
                id = t.Id,
                type = type,
                region = t.Region,
                eventId = t.EventId,
                cellX = t.CellX,
                cellZ = t.CellZ,
                edge = GridOrientationUtil.EdgeToString(t.Edge),
                pairId = t.PairId,
                terminal = t.IsSpeedTerminal
                    ? GridOrientationUtil.TerminalRoleToString(t.TerminalRole)
                    : null,
                widthTiles = t.IsSpeedTerminal ? (t.WidthTiles < 1 ? 1 : t.WidthTiles) : 0
            };
        }

        private static ObstacleFootprint ParseFootprint(string s)
        {
            if (string.IsNullOrEmpty(s)) return ObstacleFootprint.Small;
            switch (s.ToLowerInvariant())
            {
                case "2x1":
                case "wide": return ObstacleFootprint.Wide;
                case "3x1":
                case "barrier": return ObstacleFootprint.Barrier;
                default: return ObstacleFootprint.Small;
            }
        }

        private static string FootprintToString(ObstacleFootprint f)
        {
            switch (f)
            {
                case ObstacleFootprint.Wide: return "2x1";
                case ObstacleFootprint.Barrier: return "3x1";
                default: return "1x1";
            }
        }

        internal static bool IsLegacyJson(string json)
        {
            // Legacy Step-6 format uses "tiles":[...] arrays and no "region".
            bool hasTiles = json.Contains("\"tiles\"");
            bool hasRegion = json.Contains("\"region\"");
            return hasTiles && !hasRegion;
        }

        private static bool LooksLikeFailedNewFormat(CourseData data, string json)
        {
            if (!json.Contains("\"tiles\"")) return false;
            if (data.structures != null && data.structures.Length > 0 &&
                data.structures.All(s => !s.region.IsValid))
                return true;
            if (data.triggers != null && data.triggers.Length > 0)
            {
                bool anyNonGate = false;
                bool allInvalid = true;
                foreach (var t in data.triggers)
                {
                    var type = CourseSerializer.ParseTriggerType(t.type);
                    if (type == TriggerType.SpeedTerminal) continue;
                    anyNonGate = true;
                    if (t.region.IsValid) allInvalid = false;
                }
                if (anyNonGate && allInvalid) return true;
            }
            return false;
        }
    }
}
