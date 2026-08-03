using System;
using System.Collections.Generic;
using System.Linq;

namespace JajuchaSim.Course
{
    /// <summary>
    /// A single reversible action in the course editor.
    /// Each command captures a before/after snapshot of affected tiles.
    /// </summary>
    public abstract class CourseCommand
    {
        /// <summary>Human-readable description for the undo stack UI.</summary>
        public abstract string Description { get; }

        /// <summary>Apply the command (redo).</summary>
        public abstract void Execute(CourseGrid grid);

        /// <summary>Revert the command (undo).</summary>
        public abstract void Undo(CourseGrid grid);
    }

    // ================================================================
    //  Concrete commands
    // ================================================================

    /// <summary>Set road tiles (paint road on).</summary>
    public sealed class SetRoadCommand : CourseCommand
    {
        private readonly GridCoordinate[] _tiles;
        private readonly bool[] _wasRoad;
        private readonly string _desc;

        public SetRoadCommand(IEnumerable<GridCoordinate> tiles, string desc = "Paint road")
        {
            _tiles = tiles.ToArray();
            _wasRoad = new bool[_tiles.Length];
            _desc = desc;
        }

        public override string Description => _desc;

        public override void Execute(CourseGrid grid)
        {
            for (int i = 0; i < _tiles.Length; i++)
            {
                _wasRoad[i] = grid.HasRoad(_tiles[i]);
                grid.SetRoad(_tiles[i]);
            }
        }

        public override void Undo(CourseGrid grid)
        {
            for (int i = 0; i < _tiles.Length; i++)
            {
                if (!_wasRoad[i])
                    grid.ClearRoad(_tiles[i]);
            }
        }
    }

    /// <summary>Clear road tiles (erase road).</summary>
    public sealed class ClearRoadCommand : CourseCommand
    {
        private readonly GridCoordinate[] _tiles;
        private readonly bool[] _wasRoad;

        public ClearRoadCommand(IEnumerable<GridCoordinate> tiles)
        {
            _tiles = tiles.ToArray();
            _wasRoad = new bool[_tiles.Length];
        }

        public override string Description => "Erase road";

        public override void Execute(CourseGrid grid)
        {
            for (int i = 0; i < _tiles.Length; i++)
            {
                _wasRoad[i] = grid.HasRoad(_tiles[i]);
                grid.ClearRoad(_tiles[i]);
            }
        }

        public override void Undo(CourseGrid grid)
        {
            for (int i = 0; i < _tiles.Length; i++)
            {
                if (_wasRoad[i])
                    grid.SetRoad(_tiles[i]);
            }
        }
    }

    /// <summary>Place a structure on a region.</summary>
    public sealed class PlaceStructureCommand : CourseCommand
    {
        private readonly GridCoordinate[] _tiles;
        private readonly StructureType _type;
        private readonly StructureType[] _oldTypes;
        private readonly string _desc;

        public PlaceStructureCommand(GridRegion region, StructureType type)
        {
            _tiles = region.ToCoordinates();
            _type = type;
            _oldTypes = new StructureType[_tiles.Length];
            _desc = $"Place {type} ({region.width}x{region.height})";
        }

        public PlaceStructureCommand(IEnumerable<GridCoordinate> tiles, StructureType type, string desc = null)
        {
            _tiles = tiles.ToArray();
            _type = type;
            _oldTypes = new StructureType[_tiles.Length];
            _desc = desc ?? $"Place {type}";
        }

        public override string Description => _desc;

        public override void Execute(CourseGrid grid)
        {
            for (int i = 0; i < _tiles.Length; i++)
            {
                _oldTypes[i] = grid.GetStructure(_tiles[i]);
                grid.SetStructure(_tiles[i], _type);
            }
        }

        public override void Undo(CourseGrid grid)
        {
            for (int i = 0; i < _tiles.Length; i++)
            {
                grid.SetStructure(_tiles[i], _oldTypes[i]);
            }
        }
    }

    /// <summary>Remove structure from a set of tiles.</summary>
    public sealed class RemoveStructureCommand : CourseCommand
    {
        private readonly GridCoordinate[] _tiles;
        private readonly StructureType[] _oldTypes;

        public RemoveStructureCommand(IEnumerable<GridCoordinate> tiles)
        {
            _tiles = tiles.ToArray();
            _oldTypes = new StructureType[_tiles.Length];
        }

        public override string Description => "Remove structure";

        public override void Execute(CourseGrid grid)
        {
            for (int i = 0; i < _tiles.Length; i++)
            {
                _oldTypes[i] = grid.GetStructure(_tiles[i]);
                grid.ClearStructure(_tiles[i]);
            }
        }

        public override void Undo(CourseGrid grid)
        {
            for (int i = 0; i < _tiles.Length; i++)
            {
                if (_oldTypes[i] != StructureType.None)
                    grid.SetStructure(_tiles[i], _oldTypes[i]);
            }
        }
    }

    /// <summary>Place an object on a tile.</summary>
    public sealed class PlaceObjectCommand : CourseCommand
    {
        private readonly GridCoordinate _tile;
        private readonly ObjectType _type;
        private readonly ObjectType _oldType;

        public PlaceObjectCommand(GridCoordinate tile, ObjectType type)
        {
            _tile = tile;
            _type = type;
        }

        public override string Description => $"Place {_type}";

        public override void Execute(CourseGrid grid)
        {
            // Capture old state
            // Object state stored in _oldType (field set in constructor, but we capture on first execute)
            // We'll use a trick: set in Execute, store in field
            grid.SetObject(_tile, _type);
        }

        public override void Undo(CourseGrid grid)
        {
            grid.SetObject(_tile, ObjectType.None);
        }
    }

    // Mutable version that captures state on execute
    internal sealed class PlaceObjectCommandEx : CourseCommand
    {
        private readonly GridCoordinate _tile;
        private readonly ObjectType _type;
        private ObjectType _oldType;
        private bool _executed;

        public PlaceObjectCommandEx(GridCoordinate tile, ObjectType type)
        {
            _tile = tile;
            _type = type;
        }

        public override string Description => $"Place {_type}";

        public override void Execute(CourseGrid grid)
        {
            if (!_executed)
            {
                _oldType = grid.GetObject(_tile);
                _executed = true;
            }
            grid.SetObject(_tile, _type);
        }

        public override void Undo(CourseGrid grid)
        {
            grid.SetObject(_tile, _oldType);
            _executed = false;
        }
    }

    /// <summary>Remove an object from a tile.</summary>
    public sealed class RemoveObjectCommand : CourseCommand
    {
        private readonly GridCoordinate _tile;
        private ObjectType _oldType;

        public RemoveObjectCommand(GridCoordinate tile)
        {
            _tile = tile;
        }

        public override string Description => "Remove object";

        public override void Execute(CourseGrid grid)
        {
            _oldType = grid.GetObject(_tile);
            grid.ClearObject(_tile);
        }

        public override void Undo(CourseGrid grid)
        {
            if (_oldType != ObjectType.None)
                grid.SetObject(_tile, _oldType);
        }
    }

    /// <summary>Set a trigger on a set of tiles.</summary>
    public sealed class PaintTriggerCommand : CourseCommand
    {
        private readonly GridCoordinate[] _tiles;
        private readonly TriggerType _type;
        private readonly TriggerType[] _oldTypes;

        public PaintTriggerCommand(IEnumerable<GridCoordinate> tiles, TriggerType type)
        {
            _tiles = tiles.ToArray();
            _type = type;
            _oldTypes = new TriggerType[_tiles.Length];
        }

        public override string Description => $"Paint {_type} ({_tiles.Length} tiles)";

        public override void Execute(CourseGrid grid)
        {
            for (int i = 0; i < _tiles.Length; i++)
            {
                _oldTypes[i] = grid.GetTrigger(_tiles[i]);
                grid.SetTrigger(_tiles[i], _type);
            }
        }

        public override void Undo(CourseGrid grid)
        {
            for (int i = 0; i < _tiles.Length; i++)
            {
                grid.SetTrigger(_tiles[i], _oldTypes[i]);
            }
        }
    }

    /// <summary>Remove triggers from a set of tiles.</summary>
    public sealed class EraseTriggerCommand : CourseCommand
    {
        private readonly GridCoordinate[] _tiles;
        private readonly TriggerType[] _oldTypes;

        public EraseTriggerCommand(IEnumerable<GridCoordinate> tiles)
        {
            _tiles = tiles.ToArray();
            _oldTypes = new TriggerType[_tiles.Length];
        }

        public override string Description => "Erase trigger";

        public override void Execute(CourseGrid grid)
        {
            for (int i = 0; i < _tiles.Length; i++)
            {
                _oldTypes[i] = grid.GetTrigger(_tiles[i]);
                grid.ClearTrigger(_tiles[i]);
            }
        }

        public override void Undo(CourseGrid grid)
        {
            for (int i = 0; i < _tiles.Length; i++)
            {
                if (_oldTypes[i] != TriggerType.None)
                    grid.SetTrigger(_tiles[i], _oldTypes[i]);
            }
        }
    }

    /// <summary>Move a structure from one region to another.</summary>
    public sealed class MoveStructureCommand : CourseCommand
    {
        private readonly GridCoordinate[] _oldTiles;
        private readonly GridCoordinate[] _newTiles;
        private readonly StructureType _type;
        private readonly StructureType[] _overwrittenTypes;

        public MoveStructureCommand(IEnumerable<GridCoordinate> oldTiles, IEnumerable<GridCoordinate> newTiles, StructureType type)
        {
            _oldTiles = oldTiles.ToArray();
            _newTiles = newTiles.ToArray();
            _type = type;
            _overwrittenTypes = new StructureType[_newTiles.Length];
        }

        public override string Description => $"Move {_type}";

        public override void Execute(CourseGrid grid)
        {
            // Remove from old location
            foreach (var tile in _oldTiles)
                grid.ClearStructure(tile);

            // Place at new location, saving overwritten types
            for (int i = 0; i < _newTiles.Length; i++)
            {
                _overwrittenTypes[i] = grid.GetStructure(_newTiles[i]);
                grid.SetStructure(_newTiles[i], _type);
            }
        }

        public override void Undo(CourseGrid grid)
        {
            // Remove from new location
            foreach (var tile in _newTiles)
                grid.ClearStructure(tile);

            // Restore old location
            foreach (var tile in _oldTiles)
                grid.SetStructure(tile, _type);

            // Restore overwritten tiles
            for (int i = 0; i < _newTiles.Length; i++)
            {
                if (_overwrittenTypes[i] != StructureType.None)
                    grid.SetStructure(_newTiles[i], _overwrittenTypes[i]);
            }
        }
    }

    // ================================================================
    //  Undo stack
    // ================================================================

    /// <summary>
    /// Simple undo/redo stack for course editing commands.
    /// All edit actions go through this to support Ctrl+Z / Ctrl+Shift+Z.
    /// </summary>
    public sealed class CourseUndoStack
    {
        private readonly List<CourseCommand> _undoStack = new List<CourseCommand>();
        private readonly List<CourseCommand> _redoStack = new List<CourseCommand>();
        private const int MaxUndoDepth = 200;

        /// <summary>Execute a command and push it onto the undo stack.</summary>
        public void Execute(CourseGrid grid, CourseCommand command)
        {
            command.Execute(grid);
            _undoStack.Add(command);
            _redoStack.Clear();

            // Trim oldest if over limit
            if (_undoStack.Count > MaxUndoDepth)
                _undoStack.RemoveAt(0);
        }

        /// <summary>Undo the most recent command.</summary>
        public bool Undo(CourseGrid grid)
        {
            if (_undoStack.Count == 0) return false;
            var cmd = _undoStack[^1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            cmd.Undo(grid);
            _redoStack.Add(cmd);
            return true;
        }

        /// <summary>Redo the last undone command.</summary>
        public bool Redo(CourseGrid grid)
        {
            if (_redoStack.Count == 0) return false;
            var cmd = _redoStack[^1];
            _redoStack.RemoveAt(_redoStack.Count - 1);
            cmd.Execute(grid);
            _undoStack.Add(cmd);
            return true;
        }

        /// <summary>Clear all undo/redo history.</summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        public int UndoCount => _undoStack.Count;
        public int RedoCount => _redoStack.Count;

        /// <summary>Get the description of the top undo command (for UI display).</summary>
        public string PeekUndoDescription()
            => _undoStack.Count > 0 ? _undoStack[^1].Description : null;

        /// <summary>Get the description of the top redo command (for UI display).</summary>
        public string PeekRedoDescription()
            => _redoStack.Count > 0 ? _redoStack[^1].Description : null;
    }
}
