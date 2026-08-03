using System;
using System.Collections.Generic;

namespace JajuchaSim.Course
{
    /// <summary>
    /// Cardinal direction used by ramps and oriented features.
    /// Grid-aligned only (no free angles in Step 7).
    /// </summary>
    public enum GridDirection
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }

    /// <summary>
    /// Cardinal edge of a grid cell (used by speed terminals).
    /// </summary>
    public enum GridEdge
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }

    /// <summary>
    /// Role of a speed terminal within its pair (A then B is the valid direction).
    /// </summary>
    public enum SpeedTerminalRole
    {
        A = 0,
        B = 1
    }

    /// <summary>
    /// A structure placed on the course (tunnel or ramp).
    /// Structures have a rectangular tile footprint and type-specific parameters.
    /// </summary>
    [Serializable]
    public sealed class StructureInstance
    {
        public string Id;
        public StructureType Type;
        public GridRegion Region;

        // Tunnel
        public float HeightCm = 55f;
        public float WallThicknessCm = 2f;

        // Ramp
        public GridDirection Direction = GridDirection.North;
        public float RiseCm = 30f;

        public StructureInstance() { }

        public StructureInstance(string id, StructureType type, GridRegion region)
        {
            Id = id;
            Type = type;
            Region = region;
        }

        public StructureInstance Clone()
        {
            return new StructureInstance
            {
                Id = Id,
                Type = Type,
                Region = Region,
                HeightCm = HeightCm,
                WallThicknessCm = WallThicknessCm,
                Direction = Direction,
                RiseCm = RiseCm
            };
        }
    }

    /// <summary>
    /// An object placed on the course (obstacle, sign, start signal).
    /// Objects sit at a tile anchor with optional multi-tile footprint and rotation.
    /// </summary>
    [Serializable]
    public sealed class CourseObjectInstance
    {
        public string Id;
        public ObjectType Type;
        public GridCoordinate Tile;
        public int RotationDeg; // 0, 90, 180, 270
        public ObstacleFootprint Footprint = ObstacleFootprint.Small;

        /// <summary>Runtime state for start signals.</summary>
        public StartSignalState SignalState = StartSignalState.Off;

        public CourseObjectInstance() { }

        public CourseObjectInstance(string id, ObjectType type, GridCoordinate tile)
        {
            Id = id;
            Type = type;
            Tile = tile;
        }

        /// <summary>
        /// Tiles occupied by this object given its footprint and rotation.
        /// </summary>
        public GridCoordinate[] OccupiedTiles()
        {
            int w = 1, h = 1;
            switch (Footprint)
            {
                case ObstacleFootprint.Wide: w = 2; h = 1; break;
                case ObstacleFootprint.Barrier: w = 3; h = 1; break;
                default: w = 1; h = 1; break;
            }

            // Rotate footprint 90/270 swaps width/height
            if (RotationDeg == 90 || RotationDeg == 270)
            {
                int tmp = w;
                w = h;
                h = tmp;
            }

            var tiles = new GridCoordinate[w * h];
            int idx = 0;
            for (int dz = 0; dz < h; dz++)
                for (int dx = 0; dx < w; dx++)
                    tiles[idx++] = new GridCoordinate(Tile.X + dx, Tile.Z + dz);
            return tiles;
        }

        public CourseObjectInstance Clone()
        {
            return new CourseObjectInstance
            {
                Id = Id,
                Type = Type,
                Tile = Tile,
                RotationDeg = RotationDeg,
                Footprint = Footprint,
                SignalState = SignalState
            };
        }
    }

    /// <summary>Start-signal lamp state.</summary>
    public enum StartSignalState
    {
        Off = 0,
        Red,
        Yellow,
        Green
    }

    /// <summary>
    /// A trigger placed on the course (region or speed-terminal edge).
    /// </summary>
    [Serializable]
    public sealed class TriggerInstance
    {
        public string Id;
        public TriggerType Type;

        // Region triggers (slow zone, start, finish, event)
        public GridRegion Region;

        // Generic event trigger
        public string EventId;

        // Speed terminal (edge-snapped line)
        public int CellX;
        public int CellZ;
        public GridEdge Edge = GridEdge.North;
        /// <summary>Shared pair identifier linking Terminal A and Terminal B.</summary>
        public string PairId;
        /// <summary>Role within the pair (A → B is the valid measurement direction).</summary>
        public SpeedTerminalRole TerminalRole = SpeedTerminalRole.A;
        /// <summary>How many tiles the terminal line spans across the road (≥ 1).</summary>
        public int WidthTiles = 1;

        public TriggerInstance() { }

        public TriggerInstance(string id, TriggerType type, GridRegion region)
        {
            Id = id;
            Type = type;
            Region = region;
        }

        /// <summary>True when this instance is a speed measurement terminal.</summary>
        public bool IsSpeedTerminal => Type == TriggerType.SpeedTerminal;

        public static TriggerInstance SpeedTerminal(
            string id,
            int cellX,
            int cellZ,
            GridEdge edge,
            string pairId,
            SpeedTerminalRole role,
            int widthTiles = 1)
        {
            int w = widthTiles < 1 ? 1 : widthTiles;
            return new TriggerInstance
            {
                Id = id,
                Type = TriggerType.SpeedTerminal,
                CellX = cellX,
                CellZ = cellZ,
                Edge = edge,
                PairId = pairId,
                TerminalRole = role,
                WidthTiles = w,
                Region = BuildTerminalRegion(cellX, cellZ, edge, w)
            };
        }

        /// <summary>Backward-compatible factory (unpaired / role A).</summary>
        public static TriggerInstance SpeedGate(string id, int cellX, int cellZ, GridEdge edge)
        {
            return SpeedTerminal(id, cellX, cellZ, edge, pairId: null, SpeedTerminalRole.A, 1);
        }

        /// <summary>Tiles this trigger covers (for region triggers / terminal cells).</summary>
        public GridCoordinate[] OccupiedTiles()
        {
            if (IsSpeedTerminal)
            {
                int w = WidthTiles < 1 ? 1 : WidthTiles;
                var tiles = new GridCoordinate[w];
                for (int i = 0; i < w; i++)
                {
                    // Width extends along the edge (east for N/S edges, north for E/W edges).
                    if (Edge == GridEdge.North || Edge == GridEdge.South)
                        tiles[i] = new GridCoordinate(CellX + i, CellZ);
                    else
                        tiles[i] = new GridCoordinate(CellX, CellZ + i);
                }
                return tiles;
            }
            if (Region.IsValid)
                return Region.ToCoordinates();
            return Array.Empty<GridCoordinate>();
        }

        public TriggerInstance Clone()
        {
            return new TriggerInstance
            {
                Id = Id,
                Type = Type,
                Region = Region,
                EventId = EventId,
                CellX = CellX,
                CellZ = CellZ,
                Edge = Edge,
                PairId = PairId,
                TerminalRole = TerminalRole,
                WidthTiles = WidthTiles
            };
        }

        private static GridRegion BuildTerminalRegion(int cellX, int cellZ, GridEdge edge, int widthTiles)
        {
            if (edge == GridEdge.North || edge == GridEdge.South)
                return new GridRegion(cellX, cellZ, widthTiles, 1);
            return new GridRegion(cellX, cellZ, 1, widthTiles);
        }
    }

    /// <summary>
    /// Helpers for direction/edge string conversion used by JSON.
    /// </summary>
    public static class GridOrientationUtil
    {
        public static string DirectionToString(GridDirection d)
        {
            switch (d)
            {
                case GridDirection.North: return "north";
                case GridDirection.East: return "east";
                case GridDirection.South: return "south";
                case GridDirection.West: return "west";
                default: return "north";
            }
        }

        public static GridDirection ParseDirection(string s)
        {
            if (string.IsNullOrEmpty(s)) return GridDirection.North;
            switch (s.ToLowerInvariant())
            {
                case "east": return GridDirection.East;
                case "south": return GridDirection.South;
                case "west": return GridDirection.West;
                default: return GridDirection.North;
            }
        }

        public static string EdgeToString(GridEdge e)
        {
            switch (e)
            {
                case GridEdge.North: return "north";
                case GridEdge.East: return "east";
                case GridEdge.South: return "south";
                case GridEdge.West: return "west";
                default: return "north";
            }
        }

        public static GridEdge ParseEdge(string s)
        {
            if (string.IsNullOrEmpty(s)) return GridEdge.North;
            switch (s.ToLowerInvariant())
            {
                case "east": return GridEdge.East;
                case "south": return GridEdge.South;
                case "west": return GridEdge.West;
                default: return GridEdge.North;
            }
        }

        public static string TerminalRoleToString(SpeedTerminalRole role)
            => role == SpeedTerminalRole.B ? "B" : "A";

        public static SpeedTerminalRole ParseTerminalRole(string s)
        {
            if (string.IsNullOrEmpty(s)) return SpeedTerminalRole.A;
            switch (s.Trim().ToUpperInvariant())
            {
                case "B":
                case "2":
                case "TERMINAL_B":
                case "TERMINALB":
                    return SpeedTerminalRole.B;
                default:
                    return SpeedTerminalRole.A;
            }
        }

        public static int NormalizeRotation(int deg)
        {
            deg %= 360;
            if (deg < 0) deg += 360;
            // Snap to nearest 90
            int q = ((deg + 45) / 90) % 4;
            return q * 90;
        }
    }
}
