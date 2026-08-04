using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JajuchaSim.Course
{
    /// <summary>
    /// Serializable coordinate pair for JSON save/load.
    /// JsonUtility does not support nested List/arrays with complex types,
    /// so we use a simple class.
    /// </summary>
    [Serializable]
    public sealed class CoordPair
    {
        public int x;
        public int z;

        public CoordPair() { }
        public CoordPair(int x, int z) { this.x = x; this.z = z; }
        public GridCoordinate ToGrid() => new GridCoordinate(x, z);
        public static CoordPair FromGrid(GridCoordinate c) => new CoordPair(c.X, c.Z);
    }

    // ------------------------------------------------------------------
    //  NEW FORMAT — each structure/object/trigger is an individual entry
    //  with an ID, region/tile, and type-specific parameters.
    //
    //  The old format (grouped by type) is still loadable for backward
    //  compatibility (see CourseSerializerV0).
    // ------------------------------------------------------------------

    /// <summary>
    /// Serializable representation of a course map in the current format.
    ///
    /// Schema overview:
    /// <code>
    /// {
    ///   "tileSizeCm": 20,
    ///   "road": [{"x":10,"z":10}, ...],
    ///   "structures": [
    ///     {
    ///       "id": "tunnel_01",
    ///       "type": "tunnel",
    ///       "region": {"x":20,"z":30,"width":4,"height":8},
    ///       "heightCm": 55
    ///     }
    ///   ],
    ///   "objects": [
    ///     {
    ///       "id": "slow_sign_01",
    ///       "type": "slow_sign",
    ///       "tile": {"x":18,"z":29},
    ///       "rotationDeg": 90
    ///     }
    ///   ],
    ///   "triggers": [
    ///     {
    ///       "id": "slow_zone_01",
    ///       "type": "slow_zone",
    ///       "region": {"x":20,"z":40,"width":4,"height":10}
    ///     }
    ///   ]
    /// }
    /// </code>
    /// </summary>
    [Serializable]
    public sealed class CourseData
    {
        public float tileSizeCm = 20f;
        public CoordPair[] road = Array.Empty<CoordPair>();

        /// <summary>Boundary-line tiles painted on the road (Step 10).</summary>
        public CoordPair[] lines = Array.Empty<CoordPair>();

        public StructureEntry[] structures = Array.Empty<StructureEntry>();
        public ObjectEntry[] objects = Array.Empty<ObjectEntry>();
        public TriggerEntry[] triggers = Array.Empty<TriggerEntry>();

        // ---- Structure entry ------------------------------------------

        [Serializable]
        public sealed class StructureEntry
        {
            // Common
            public string id;
            public string type;          // "tunnel" | "ramp"
            public GridRegion region;    // footprint

            // Tunnel-specific
            public float heightCm;        // tunnel height or ramp rise
            public float wallThicknessCm; // tunnel wall thickness (default 2 cm)

            // Ramp-specific
            public string direction;      // "north" | "south" | "east" | "west"
            public float riseCm;          // same as heightCm, alias for clarity
        }

        // ---- Object entry ---------------------------------------------

        [Serializable]
        public sealed class ObjectEntry
        {
            public string id;
            public string type;          // "obstacle" | "slow_sign" | "start_signal"
            public CoordPair tile;       // anchor tile
            public int rotationDeg;      // 0, 90, 180, 270
            public string footprint;     // "1x1", "2x1", "3x1" (optional, default "1x1")
        }

        // ---- Trigger entry --------------------------------------------

        [Serializable]
        public sealed class TriggerEntry
        {
            public string id;
            public string type;          // "slow_zone" | "start" | "finish" | "speed_terminal" | "speed_gate" | "event"

            // Region triggers (slow_zone, start, finish, event)
            public GridRegion region;

            // Event trigger specific
            public string eventId;

            // Speed terminal specific (edge-snapped line)
            public int cellX;
            public int cellZ;
            public string edge;          // "north" | "south" | "east" | "west"
            public string pairId;        // shared pair id (e.g. "speed_zone_01")
            public string terminal;      // "A" | "B"
            public int widthTiles;       // tiles across the road (≥1); 0 = default 1
        }
    }

    // ------------------------------------------------------------------
    //  ID generator
    // ------------------------------------------------------------------

    /// <summary>
    /// Simple auto-ID generator for course features.
    /// Produces IDs like "tunnel_001", "obstacle_002", etc.
    /// </summary>
    public static class FeatureIdGenerator
    {
        private static readonly Dictionary<string, int> _counters = new Dictionary<string, int>();

        /// <summary>Generate a new auto-incremented ID for the given feature type.</summary>
        public static string NextId(string typePrefix)
        {
            if (!_counters.TryGetValue(typePrefix, out int count))
                count = 0;
            count++;
            _counters[typePrefix] = count;
            return $"{typePrefix}_{count:D3}";
        }

        /// <summary>Reset all counters (for testing or new map).</summary>
        public static void Reset()
        {
            _counters.Clear();
        }

        /// <summary>
        /// Parse a free-form type string into a short prefix suitable for IDs.
        /// </summary>
        public static string TypeToPrefix(string type)
        {
            return type.ToLowerInvariant()
                .Replace(" ", "_")
                .Replace("-", "_");
        }
    }

    // ------------------------------------------------------------------
    //  Serializer
    // ------------------------------------------------------------------

    /// <summary>
    /// Converts between <see cref="CourseGrid"/> runtime data and
    /// <see cref="CourseData"/> (serializable JSON model).
    ///
    /// Also supports loading legacy (Step-6) format for backward compatibility.
    /// </summary>
    public static class CourseSerializer
    {
        /// <summary>
        /// Export a <see cref="CourseGrid"/> into a <see cref="CourseData"/>
        /// suitable for JSON serialization.
        /// </summary>
        public static CourseData ToData(CourseGrid grid)
        {
            var data = new CourseData
            {
                tileSizeCm = grid.TileSizeCm
            };

            // Road tiles
            data.road = grid.AllRoadTiles().Select(CoordPair.FromGrid).ToArray();

            // Boundary-line tiles (Step 10)
            data.lines = grid.AllLineTiles().Select(CoordPair.FromGrid).ToArray();

            // Structures — individual entries with auto-generated IDs
            var structList = new List<CourseData.StructureEntry>();
            foreach (var kv in grid.AllStructures())
            {
                // Group contiguous tiles of same type into minimal regions.
                // For now treat each tile as its own 1×1 region.
                // The CourseGrid only stores per-tile types, not region data.
                // A proper map-editor saves the original region; here we
                // produce individual entries per tile.
                var entry = new CourseData.StructureEntry
                {
                    id = FeatureIdGenerator.NextId(kv.Value.ToString().ToLowerInvariant()),
                    type = kv.Value.ToString().ToLowerInvariant(),
                    region = new GridRegion(kv.Key.X, kv.Key.Z, 1, 1),
                    heightCm = 30f,
                    wallThicknessCm = 2f
                };
                structList.Add(entry);
            }
            data.structures = structList.ToArray();

            // Objects — individual entries
            var objList = new List<CourseData.ObjectEntry>();
            foreach (var kv in grid.AllObjects())
            {
                var entry = new CourseData.ObjectEntry
                {
                    id = FeatureIdGenerator.NextId(kv.Value.ToString().ToLowerInvariant()),
                    type = kv.Value.ToString().ToLowerInvariant(),
                    tile = CoordPair.FromGrid(kv.Key),
                    rotationDeg = 0
                };
                objList.Add(entry);
            }
            data.objects = objList.ToArray();

            // Triggers — individual entries
            var trigList = new List<CourseData.TriggerEntry>();
            foreach (var kv in grid.AllTriggers())
            {
                bool isTerminal = kv.Value == TriggerType.SpeedTerminal;
                var entry = new CourseData.TriggerEntry
                {
                    id = FeatureIdGenerator.NextId(isTerminal ? "speed_terminal" : kv.Value.ToString().ToLowerInvariant()),
                    type = isTerminal ? "speed_terminal" : kv.Value.ToString().ToLowerInvariant(),
                    region = new GridRegion(kv.Key.X, kv.Key.Z, 1, 1),
                    cellX = kv.Key.X,
                    cellZ = kv.Key.Z,
                    edge = isTerminal ? "north" : null,
                    widthTiles = isTerminal ? 1 : 0
                };
                trigList.Add(entry);
            }
            data.triggers = trigList.ToArray();

            return data;
        }

        /// <summary>
        /// Import a <see cref="CourseData"/> (current format) into a new <see cref="CourseGrid"/>.
        /// Also attempts to import legacy format by falling back to
        /// <see cref="FromDataV0"/>.
        /// </summary>
        public static CourseGrid ToGrid(CourseData data)
        {
            var grid = new CourseGrid(data.tileSizeCm);

            // Road tiles
            if (data.road != null)
            {
                foreach (var pair in data.road)
                    grid.SetRoad(pair.ToGrid());
            }

            // Boundary-line tiles (Step 10)
            if (data.lines != null)
            {
                foreach (var pair in data.lines)
                    grid.SetLine(pair.ToGrid());
            }

            // Structures — individual entries with region
            if (data.structures != null)
            {
                foreach (var entry in data.structures)
                {
                    var type = ParseStructureType(entry.type);
                    if (entry.region.IsValid)
                    {
                        // Place structure across the entire region
                        foreach (var c in entry.region.ToCoordinates())
                            grid.SetStructure(c, type);
                    }
                }
            }

            // Objects
            if (data.objects != null)
            {
                foreach (var entry in data.objects)
                {
                    if (entry.tile != null)
                    {
                        var type = ParseObjectType(entry.type);
                        grid.SetObject(entry.tile.ToGrid(), type);
                    }
                }
            }

            // Triggers
            if (data.triggers != null)
            {
                foreach (var entry in data.triggers)
                {
                    var type = ParseTriggerType(entry.type);
                    if (type == TriggerType.SpeedTerminal)
                    {
                        // Document format: cellX/cellZ + edge (+ optional width).
                        // Grid-only / legacy entries may only have a region.
                        bool hasCellFields = !string.IsNullOrEmpty(entry.edge)
                            || entry.widthTiles > 0
                            || !string.IsNullOrEmpty(entry.pairId)
                            || !string.IsNullOrEmpty(entry.terminal);

                        if (!hasCellFields && entry.region.IsValid)
                        {
                            foreach (var c in entry.region.ToCoordinates())
                                grid.SetTrigger(c, TriggerType.SpeedTerminal);
                        }
                        else
                        {
                            int baseX = entry.cellX;
                            int baseZ = entry.cellZ;
                            // If cell fields are zero but region is offset, prefer region origin.
                            if (baseX == 0 && baseZ == 0 && entry.region.IsValid
                                && (entry.region.x != 0 || entry.region.z != 0))
                            {
                                baseX = entry.region.x;
                                baseZ = entry.region.z;
                            }

                            int w = entry.widthTiles > 0 ? entry.widthTiles : 1;
                            var edge = GridOrientationUtil.ParseEdge(entry.edge);
                            for (int i = 0; i < w; i++)
                            {
                                int cx = baseX;
                                int cz = baseZ;
                                if (edge == GridEdge.North || edge == GridEdge.South)
                                    cx += i;
                                else
                                    cz += i;
                                grid.SetTrigger(new GridCoordinate(cx, cz), TriggerType.SpeedTerminal);
                            }
                        }
                    }
                    else if (entry.region.IsValid)
                    {
                        foreach (var c in entry.region.ToCoordinates())
                            grid.SetTrigger(c, type);
                    }
                }
            }

            return grid;
        }

        /// <summary>
        /// Serialize a <see cref="CourseGrid"/> to a JSON string using the current format.
        /// </summary>
        public static string ToJson(CourseGrid grid, bool pretty = true)
        {
            var data = ToData(grid);
            return JsonUtility.ToJson(data, pretty);
        }

        /// <summary>
        /// Deserialize a <see cref="CourseGrid"/> from a JSON string.
        /// Supports both the current (Step-7) format and the legacy (Step-6) format.
        /// Returns null on parse failure.
        /// </summary>
        public static CourseGrid FromJson(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json)) return null;

                // Legacy Step-6 format uses per-entry "tiles" arrays and has no "region".
                // Prefer it when detected so JsonUtility doesn't silently drop those fields
                // while parsing into the new CourseData shape.
                if (IsLegacyFormat(json))
                {
                    var legacy = FromJsonV0(json);
                    if (legacy != null) return legacy;
                }

                var data = JsonUtility.FromJson<CourseData>(json);
                if (data != null)
                {
                    var grid = ToGrid(data);

                    // Safety net: if the payload still looks tile-based and the new
                    // parse produced no features, fall back to legacy.
                    if (json.IndexOf("\"tiles\"", StringComparison.Ordinal) >= 0 &&
                        grid.StructureTileCount == 0 && grid.TriggerTileCount == 0)
                    {
                        var legacy = FromJsonV0(json);
                        if (legacy != null &&
                            (legacy.StructureTileCount + legacy.TriggerTileCount +
                             legacy.ObjectTileCount) >
                            (grid.StructureTileCount + grid.TriggerTileCount +
                             grid.ObjectTileCount))
                        {
                            return legacy;
                        }
                    }

                    return grid;
                }

                return FromJsonV0(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CourseSerializer] Failed to parse course JSON: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Heuristic: legacy format has "tiles" arrays and no "region" objects.
        /// </summary>
        internal static bool IsLegacyFormat(string json)
        {
            if (string.IsNullOrEmpty(json)) return false;
            bool hasTiles = json.IndexOf("\"tiles\"", StringComparison.Ordinal) >= 0;
            bool hasRegion = json.IndexOf("\"region\"", StringComparison.Ordinal) >= 0;
            return hasTiles && !hasRegion;
        }

        // ================================================================
        //  Legacy (Step-6) format support
        // ================================================================

        /// <summary>
        /// Old format used in Step 6 — grouped entries by type.
        /// Schema:
        /// <code>
        /// {
        ///   "tileSizeCm": 20,
        ///   "road": [{"x":10,"z":10}, ...],
        ///   "structures": [{"type":"tunnel","tiles":[{"x":10,"z":11},...]}, ...],
        ///   "objects": [{"type":"obstacle","tile":{"x":9,"z":11}}, ...],
        ///   "triggers": [{"type":"slow_zone","tiles":[{"x":5,"z":5},...]}, ...]
        /// }
        /// </code>
        /// </summary>
        [Serializable]
        private sealed class CourseDataV0
        {
            public float tileSizeCm = 20f;
            public CoordPair[] road;
            public StructureEntryV0[] structures;
            public ObjectEntryV0[] objects;
            public TriggerEntryV0[] triggers;

            [Serializable]
            public sealed class StructureEntryV0
            {
                public string type;
                public CoordPair[] tiles;
            }

            [Serializable]
            public sealed class ObjectEntryV0
            {
                public string type;
                public CoordPair tile;
            }

            [Serializable]
            public sealed class TriggerEntryV0
            {
                public string type;
                public CoordPair[] tiles;
            }
        }

        /// <summary>Try to parse JSON as legacy (Step-6) format.</summary>
        private static CourseGrid FromJsonV0(string json)
        {
            try
            {
                var data = JsonUtility.FromJson<CourseDataV0>(json);
                if (data == null) return null;

                var grid = new CourseGrid(data.tileSizeCm);

                if (data.road != null)
                {
                    foreach (var pair in data.road)
                        grid.SetRoad(pair.ToGrid());
                }

                if (data.structures != null)
                {
                    foreach (var entry in data.structures)
                    {
                        var type = ParseStructureType(entry.type);
                        if (entry.tiles != null)
                        {
                            foreach (var pair in entry.tiles)
                                grid.SetStructure(pair.ToGrid(), type);
                        }
                    }
                }

                if (data.objects != null)
                {
                    foreach (var entry in data.objects)
                    {
                        if (entry.tile != null)
                        {
                            var type = ParseObjectType(entry.type);
                            grid.SetObject(entry.tile.ToGrid(), type);
                        }
                    }
                }

                if (data.triggers != null)
                {
                    foreach (var entry in data.triggers)
                    {
                        var type = ParseTriggerType(entry.type);
                        if (entry.tiles != null)
                        {
                            foreach (var pair in entry.tiles)
                                grid.SetTrigger(pair.ToGrid(), type);
                        }
                    }
                }

                return grid;
            }
            catch
            {
                return null;
            }
        }

        // ---- Enum parsers (case-insensitive) ---------------------------

        internal static StructureType ParseStructureType(string s)
        {
            if (string.IsNullOrEmpty(s)) return StructureType.None;
            s = s.ToLowerInvariant();
            if (s == "tunnel") return StructureType.Tunnel;
            if (s == "ramp") return StructureType.Ramp;
            return StructureType.None;
        }

        internal static ObjectType ParseObjectType(string s)
        {
            if (string.IsNullOrEmpty(s)) return ObjectType.None;
            s = s.ToLowerInvariant();
            if (s == "obstacle") return ObjectType.Obstacle;
            if (s == "sign" || s == "slow_sign") return ObjectType.Sign;
            if (s == "startsignal" || s == "start_signal") return ObjectType.StartSignal;
            return ObjectType.None;
        }

        internal static TriggerType ParseTriggerType(string s)
        {
            if (string.IsNullOrEmpty(s)) return TriggerType.None;
            s = s.ToLowerInvariant();
            if (s == "slowzone" || s == "slow_zone") return TriggerType.SlowZone;
            if (s == "speedgate" || s == "speed_gate"
                || s == "speedterminal" || s == "speed_terminal")
                return TriggerType.SpeedTerminal;
            if (s == "eventtrigger" || s == "event_trigger" || s == "event") return TriggerType.EventTrigger;
            if (s == "start") return TriggerType.Start;
            if (s == "finish") return TriggerType.Finish;
            return TriggerType.None;
        }
    }
}
