using System.Collections.Generic;
using UnityEngine;

namespace JajuchaSim.Course
{
    /// <summary>
    /// The authoritative shared tile grid for all course features.
    ///
    /// Four layers (may overlap on the same tile):
    ///   Road      — boolean: is this tile a drivable road?
    ///   Structure — optional <see cref="StructureType"/> per tile.
    ///   Object    — optional <see cref="ObjectType"/> per tile.
    ///   Trigger   — optional <see cref="TriggerType"/> per tile.
    ///
    /// Example (tile 12,8): Road=true, Structure=Tunnel, Trigger=None.
    ///
    /// Rendering is separate (see course mesh generator). The grid only stores
    /// logical data; the renderer figures out connectivity from neighbour states.
    /// </summary>
    public sealed class CourseGrid
    {
        private readonly HashSet<GridCoordinate> _road = new HashSet<GridCoordinate>();
        private readonly Dictionary<GridCoordinate, StructureType> _structures =
            new Dictionary<GridCoordinate, StructureType>();
        private readonly Dictionary<GridCoordinate, ObjectType> _objects =
            new Dictionary<GridCoordinate, ObjectType>();
        private readonly Dictionary<GridCoordinate, TriggerType> _triggers =
            new Dictionary<GridCoordinate, TriggerType>();

        /// <summary>Tile size in centimetres.</summary>
        public float TileSizeCm { get; }

        public CourseGrid(float tileSizeCm)
        {
            TileSizeCm = tileSizeCm > 0f ? tileSizeCm : 20f;
        }

        // ================================================================
        //  Road layer
        // ================================================================

        /// <summary>Mark one or more tiles as road.</summary>
        public void SetRoad(GridCoordinate coord) => _road.Add(coord);
        public void SetRoad(IEnumerable<GridCoordinate> coords)
        {
            foreach (var c in coords) _road.Add(c);
        }

        /// <summary>Remove road from one or more tiles.</summary>
        public void ClearRoad(GridCoordinate coord) => _road.Remove(coord);
        public void ClearRoad(IEnumerable<GridCoordinate> coords)
        {
            foreach (var c in coords) _road.Remove(c);
        }

        /// <summary>Is the given tile a road?</summary>
        public bool HasRoad(GridCoordinate coord) => _road.Contains(coord);

        /// <summary>All tiles that are road.</summary>
        public IEnumerable<GridCoordinate> AllRoadTiles() => _road;

        public int RoadTileCount => _road.Count;

        // ================================================================
        //  Structure layer
        // ================================================================

        /// <summary>Place a structure on a single tile.</summary>
        public void SetStructure(GridCoordinate coord, StructureType type)
        {
            if (type == StructureType.None)
                _structures.Remove(coord);
            else
                _structures[coord] = type;
        }

        /// <summary>Place a structure on multiple tiles.</summary>
        public void SetStructure(IEnumerable<GridCoordinate> coords, StructureType type)
        {
            foreach (var c in coords) SetStructure(c, type);
        }

        /// <summary>Remove any structure from a tile.</summary>
        public void ClearStructure(GridCoordinate coord) => _structures.Remove(coord);
        public void ClearStructure(IEnumerable<GridCoordinate> coords)
        {
            foreach (var c in coords) _structures.Remove(c);
        }

        /// <summary>Get the structure at a tile (None if absent).</summary>
        public StructureType GetStructure(GridCoordinate coord)
        {
            return _structures.TryGetValue(coord, out var t) ? t : StructureType.None;
        }

        /// <summary>All tiles that have a structure, with their type.</summary>
        public IEnumerable<KeyValuePair<GridCoordinate, StructureType>> AllStructures() => _structures;

        public int StructureTileCount => _structures.Count;

        // ================================================================
        //  Object layer
        // ================================================================

        /// <summary>Place an object on a single tile.</summary>
        public void SetObject(GridCoordinate coord, ObjectType type)
        {
            if (type == ObjectType.None)
                _objects.Remove(coord);
            else
                _objects[coord] = type;
        }

        /// <summary>Place an object on multiple tiles (same type).</summary>
        public void SetObject(IEnumerable<GridCoordinate> coords, ObjectType type)
        {
            foreach (var c in coords) SetObject(c, type);
        }

        /// <summary>Remove any object from a tile.</summary>
        public void ClearObject(GridCoordinate coord) => _objects.Remove(coord);
        public void ClearObject(IEnumerable<GridCoordinate> coords)
        {
            foreach (var c in coords) _objects.Remove(c);
        }

        /// <summary>Get the object at a tile (None if absent).</summary>
        public ObjectType GetObject(GridCoordinate coord)
        {
            return _objects.TryGetValue(coord, out var t) ? t : ObjectType.None;
        }

        /// <summary>All tiles that have an object, with their type.</summary>
        public IEnumerable<KeyValuePair<GridCoordinate, ObjectType>> AllObjects() => _objects;

        public int ObjectTileCount => _objects.Count;

        // ================================================================
        //  Trigger layer
        // ================================================================

        /// <summary>Place a trigger on a single tile.</summary>
        public void SetTrigger(GridCoordinate coord, TriggerType type)
        {
            if (type == TriggerType.None)
                _triggers.Remove(coord);
            else
                _triggers[coord] = type;
        }

        /// <summary>Place a trigger on multiple tiles (same type).</summary>
        public void SetTrigger(IEnumerable<GridCoordinate> coords, TriggerType type)
        {
            foreach (var c in coords) SetTrigger(c, type);
        }

        /// <summary>Remove any trigger from a tile.</summary>
        public void ClearTrigger(GridCoordinate coord) => _triggers.Remove(coord);
        public void ClearTrigger(IEnumerable<GridCoordinate> coords)
        {
            foreach (var c in coords) _triggers.Remove(c);
        }

        /// <summary>Get the trigger at a tile (None if absent).</summary>
        public TriggerType GetTrigger(GridCoordinate coord)
        {
            return _triggers.TryGetValue(coord, out var t) ? t : TriggerType.None;
        }

        /// <summary>All tiles that have a trigger, with their type.</summary>
        public IEnumerable<KeyValuePair<GridCoordinate, TriggerType>> AllTriggers() => _triggers;

        public int TriggerTileCount => _triggers.Count;

        // ================================================================
        //  Bulk operations
        // ================================================================

        /// <summary>Remove all data from all layers.</summary>
        public void ClearAll()
        {
            _road.Clear();
            _structures.Clear();
            _objects.Clear();
            _triggers.Clear();
        }

        /// <summary>
        /// Enumerate a rectangular region of tiles from (xMin, zMin) to
        /// (xMax, zMax) inclusive.
        /// </summary>
        public static IEnumerable<GridCoordinate> Rectangle(int xMin, int zMin, int xMax, int zMax)
        {
            for (int z = zMin; z <= zMax; z++)
                for (int x = xMin; x <= xMax; x++)
                    yield return new GridCoordinate(x, z);
        }

        // ================================================================
        //  Query helpers
        // ================================================================

        // ================================================================
        //  Coordinate conversion helpers
        // ================================================================

        /// <summary>
        /// Convert a grid coordinate to a world-space position (centre of tile).
        /// 1 Unity unit = 1 cm.
        /// </summary>
        public Vector3 GridToWorld(GridCoordinate coord)
        {
            float half = TileSizeCm * 0.5f;
            return new Vector3(coord.X * TileSizeCm + half, 0f, coord.Z * TileSizeCm + half);
        }

        /// <summary>
        /// Convert a world-space position to the nearest grid coordinate.
        /// </summary>
        public GridCoordinate WorldToGrid(Vector3 worldPos)
        {
            int x = Mathf.FloorToInt(worldPos.x / TileSizeCm);
            int z = Mathf.FloorToInt(worldPos.z / TileSizeCm);
            return new GridCoordinate(x, z);
        }

        /// <summary>
        /// Returns a compact summary of all four layers at the given tile.
        /// </summary>
        public TileInfo GetTileInfo(GridCoordinate coord)
        {
            return new TileInfo(
                coord,
                HasRoad(coord),
                GetStructure(coord),
                GetObject(coord),
                GetTrigger(coord)
            );
        }
    }

    /// <summary>
    /// Snapshot of all four layers at one grid coordinate.
    /// </summary>
    public readonly struct TileInfo
    {
        public GridCoordinate Coordinate { get; }
        public bool Road { get; }
        public StructureType Structure { get; }
        public ObjectType Object { get; }
        public TriggerType Trigger { get; }

        public TileInfo(
            GridCoordinate coordinate,
            bool road,
            StructureType structure,
            ObjectType obj,
            TriggerType trigger)
        {
            Coordinate = coordinate;
            Road = road;
            Structure = structure;
            Object = obj;
            Trigger = trigger;
        }

        public override string ToString()
            => $"Tile{Coordinate}: Road={Road}, Struct={Structure}, Obj={Object}, Trig={Trigger}";
    }
}
