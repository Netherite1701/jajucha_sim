using System;
using System.Collections.Generic;
using System.Linq;

namespace JajuchaSim.Course
{
    /// <summary>High-level editor mode.</summary>
    public enum MapEditorMode
    {
        Edit,
        Drive
    }

    /// <summary>Whether the active document is an official course or a user copy.</summary>
    public enum CourseEditOrigin
    {
        OfficialReadOnly,
        PracticeCopy
    }

    /// <summary>Active placement/editing tool.</summary>
    public enum MapEditorTool
    {
        None = 0,
        // Road
        PaintRoad,
        EraseRoad,
        // Structures
        PlaceTunnel,
        PlaceRamp,
        // Objects
        PlaceObstacle,
        PlaceSlowSign,
        PlaceStartSignal,
        // Triggers
        PaintSlowZone,
        PlaceStartTrigger,
        PlaceFinishTrigger,
        PlaceSpeedGate,
        /// <summary>Place speed measurement Terminal A (pair-aware).</summary>
        PlaceSpeedTerminalA,
        /// <summary>Place speed measurement Terminal B (pair-aware).</summary>
        PlaceSpeedTerminalB,
        PlaceEventTrigger,
        // Select / manipulate
        Select
    }

    /// <summary>Tile selection method.</summary>
    public enum SelectionMethod
    {
        Click,
        Rectangle,
        Paint
    }

    /// <summary>
    /// Pure-logic runtime map editor session.
    /// Handles tool state, placement, move/resize/delete, preview, validation,
    /// undo/redo, layer visibility, and save/load — without requiring the Unity Editor.
    /// UI layers (HUD) drive this session.
    /// </summary>
    public sealed class MapEditorSession
    {
        public CourseDocument Document { get; private set; }

        /// <summary>Official course documents remain selectable but cannot be mutated.</summary>
        public bool IsReadOnly { get; set; }

        public CourseEditOrigin EditOrigin => IsReadOnly
            ? CourseEditOrigin.OfficialReadOnly
            : CourseEditOrigin.PracticeCopy;

        /// <summary>Replace document without clearing the undo stack (used by snapshot undo/redo).</summary>
        internal void SetDocumentQuiet(CourseDocument document)
        {
            Document = document ?? new CourseDocument(20f);
            ClearSelection();
        }
        public CourseUndoStack Undo { get; } = new CourseUndoStack();

        public MapEditorMode Mode { get; set; } = MapEditorMode.Edit;
        public MapEditorTool Tool { get; set; } = MapEditorTool.PaintRoad;
        public SelectionMethod SelectionMethod { get; set; } = SelectionMethod.Click;

        // Layer visibility (edit mode)
        public bool ShowRoad { get; set; } = true;
        public bool ShowStructures { get; set; } = true;
        public bool ShowObjects { get; set; } = true;
        public bool ShowTriggers { get; set; } = true;

        // Drive-mode debug overlays
        public bool ShowTriggerOverlay { get; set; }
        public bool ShowStructureIds { get; set; }

        // Placement parameters
        public float TunnelHeightCm { get; set; } = 55f;
        public float TunnelWallThicknessCm { get; set; } = 2f;
        public float RampRiseCm { get; set; } = 30f;
        public GridDirection RampDirection { get; set; } = GridDirection.North;
        public int ObjectRotationDeg { get; set; }
        public ObstacleFootprint ObstacleFootprint { get; set; } = ObstacleFootprint.Small;
        public GridEdge SpeedGateEdge { get; set; } = GridEdge.North;
        /// <summary>Shared pair id for the next speed terminals placed.</summary>
        public string SpeedPairId { get; set; } = "speed_zone_01";
        /// <summary>Width of the terminal line across the road (tiles).</summary>
        public int SpeedTerminalWidthTiles { get; set; } = 1;
        public string EventTriggerId { get; set; } = "event";

        // Selection state
        public string SelectedStructureId { get; private set; }
        public string SelectedObjectId { get; private set; }
        public string SelectedTriggerId { get; private set; }

        // Drag / rectangle state
        public bool IsDragging { get; private set; }
        public GridCoordinate DragStart { get; private set; }
        public GridCoordinate DragCurrent { get; private set; }

        /// <summary>Last validation results from the most recent place attempt.</summary>
        public IReadOnlyList<ValidationResult> LastValidation { get; private set; }
            = Array.Empty<ValidationResult>();

        /// <summary>Raised when the document changes (place/move/delete/undo/load).</summary>
        public event Action DocumentChanged;

        public MapEditorSession(CourseDocument document = null)
        {
            Document = document ?? new CourseDocument(20f);
        }

        public void SetDocument(CourseDocument document)
        {
            Document = document ?? new CourseDocument(20f);
            Undo.Clear();
            ClearSelection();
            DocumentChanged?.Invoke();
        }

        // ================================================================
        //  Selection
        // ================================================================

        public void ClearSelection()
        {
            SelectedStructureId = null;
            SelectedObjectId = null;
            SelectedTriggerId = null;
        }

        public void SelectAt(GridCoordinate tile)
        {
            ClearSelection();
            var s = Document.FindStructureAt(tile);
            if (s != null) { SelectedStructureId = s.Id; return; }
            var o = Document.FindObjectAt(tile);
            if (o != null) { SelectedObjectId = o.Id; return; }
            var t = Document.FindTriggersAt(tile).FirstOrDefault();
            if (t != null) { SelectedTriggerId = t.Id; }
        }

        // ================================================================
        //  Drag / rectangle
        // ================================================================

        public void BeginDrag(GridCoordinate start)
        {
            if (IsReadOnly || Mode != MapEditorMode.Edit) return;
            IsDragging = true;
            DragStart = start;
            DragCurrent = start;
        }

        public void UpdateDrag(GridCoordinate current)
        {
            if (!IsDragging) return;
            DragCurrent = current;
        }

        public GridRegion CurrentDragRegion()
        {
            int x0 = Math.Min(DragStart.X, DragCurrent.X);
            int z0 = Math.Min(DragStart.Z, DragCurrent.Z);
            int x1 = Math.Max(DragStart.X, DragCurrent.X);
            int z1 = Math.Max(DragStart.Z, DragCurrent.Z);
            return new GridRegion(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
        }

        /// <summary>
        /// Preview info for the current drag region (tile count + physical cm).
        /// </summary>
        public (int tilesW, int tilesH, int cmW, int cmH, bool valid) PreviewInfo()
        {
            var region = IsDragging ? CurrentDragRegion() : new GridRegion(0, 0, 0, 0);
            if (!region.IsValid) return (0, 0, 0, 0, false);
            float ts = Document.Grid.TileSizeCm;
            bool valid = true;
            if (Tool == MapEditorTool.PlaceTunnel || Tool == MapEditorTool.PlaceRamp)
            {
                var type = Tool == MapEditorTool.PlaceTunnel ? StructureType.Tunnel : StructureType.Ramp;
                var results = CourseValidator.ValidateStructurePlacement(Document.Grid, region, type);
                valid = !results.Any(r => r.IsError);
                LastValidation = results;
            }
            return (region.width, region.height, region.TileWidthCm(ts), region.TileHeightCm(ts), valid);
        }

        public void CancelDrag()
        {
            IsDragging = false;
        }

        /// <summary>Commit the current drag as a placement for the active tool.</summary>
        public bool EndDrag()
        {
            if (IsReadOnly || !IsDragging) return false;
            IsDragging = false;
            var region = CurrentDragRegion();
            return ApplyToolOnRegion(region);
        }

        // ================================================================
        //  Click placement
        // ================================================================

        public bool Click(GridCoordinate tile)
        {
            if (Mode != MapEditorMode.Edit) return false;

            if (IsReadOnly)
            {
                if (Tool == MapEditorTool.Select)
                {
                    SelectAt(tile);
                    return true;
                }
                return false;
            }

            switch (Tool)
            {
                case MapEditorTool.Select:
                    SelectAt(tile);
                    return true;

                case MapEditorTool.PaintRoad:
                    return ApplyRoad(new[] { tile }, paint: true);

                case MapEditorTool.EraseRoad:
                    return ApplyRoad(new[] { tile }, paint: false);

                case MapEditorTool.PlaceObstacle:
                    return PlaceObjectAt(tile, ObjectType.Obstacle);

                case MapEditorTool.PlaceSlowSign:
                    return PlaceObjectAt(tile, ObjectType.Sign);

                case MapEditorTool.PlaceStartSignal:
                    return PlaceObjectAt(tile, ObjectType.StartSignal);

                case MapEditorTool.PlaceSpeedGate:
                case MapEditorTool.PlaceSpeedTerminalA:
                    return PlaceTerminalAt(tile, SpeedTerminalRole.A);
                case MapEditorTool.PlaceSpeedTerminalB:
                    return PlaceTerminalAt(tile, SpeedTerminalRole.B);

                case MapEditorTool.PaintSlowZone:
                    return PaintTrigger(new[] { tile }, TriggerType.SlowZone);

                case MapEditorTool.PlaceStartTrigger:
                    return PaintTrigger(new[] { tile }, TriggerType.Start);

                case MapEditorTool.PlaceFinishTrigger:
                    return PaintTrigger(new[] { tile }, TriggerType.Finish);

                case MapEditorTool.PlaceEventTrigger:
                    return PlaceEventAt(new GridRegion(tile.X, tile.Z, 1, 1));

                case MapEditorTool.PlaceTunnel:
                case MapEditorTool.PlaceRamp:
                    // Single-click places 1×1; prefer drag for multi-tile
                    return ApplyToolOnRegion(new GridRegion(tile.X, tile.Z, 1, 1));

                default:
                    return false;
            }
        }

        public bool Paint(IEnumerable<GridCoordinate> tiles)
        {
            if (IsReadOnly || Mode != MapEditorMode.Edit) return false;
            var list = tiles.ToList();
            if (list.Count == 0) return false;

            switch (Tool)
            {
                case MapEditorTool.PaintRoad:
                    return ApplyRoad(list, paint: true);
                case MapEditorTool.EraseRoad:
                    return ApplyRoad(list, paint: false);
                case MapEditorTool.PaintSlowZone:
                    return PaintTrigger(list, TriggerType.SlowZone);
                case MapEditorTool.PlaceStartTrigger:
                    return PaintTrigger(list, TriggerType.Start);
                case MapEditorTool.PlaceFinishTrigger:
                    return PaintTrigger(list, TriggerType.Finish);
                case MapEditorTool.PlaceEventTrigger:
                {
                    int minX = list.Min(t => t.X), maxX = list.Max(t => t.X);
                    int minZ = list.Min(t => t.Z), maxZ = list.Max(t => t.Z);
                    return PlaceEventAt(new GridRegion(minX, minZ, maxX - minX + 1, maxZ - minZ + 1));
                }
                default:
                    return false;
            }
        }

        // ================================================================
        //  Edit selected
        // ================================================================

        public bool DeleteSelected()
        {
            if (IsReadOnly) return false;
            if (SelectedStructureId != null)
            {
                var id = SelectedStructureId;
                if (Document.FindStructure(id) == null) return false;
                ExecuteWithSnapshot(() =>
                {
                    Document.RemoveStructure(id);
                    ClearSelection();
                }, $"Delete structure {id}");
                return true;
            }
            if (SelectedObjectId != null)
            {
                var id = SelectedObjectId;
                if (Document.FindObject(id) == null) return false;
                ExecuteWithSnapshot(() =>
                {
                    Document.RemoveObject(id);
                    ClearSelection();
                }, $"Delete object {id}");
                return true;
            }
            if (SelectedTriggerId != null)
            {
                var id = SelectedTriggerId;
                if (Document.FindTrigger(id) == null) return false;
                ExecuteWithSnapshot(() =>
                {
                    Document.RemoveTrigger(id);
                    ClearSelection();
                }, $"Delete trigger {id}");
                return true;
            }
            return false;
        }

        public bool MoveSelected(int dx, int dz)
        {
            if (IsReadOnly || dx == 0 && dz == 0) return false;

            if (SelectedStructureId != null)
            {
                var id = SelectedStructureId;
                if (Document.FindStructure(id) == null) return false;
                bool ok = false;
                ExecuteWithSnapshot(() =>
                {
                    ok = Document.MoveStructure(id, dx, dz);
                }, $"Move structure {id}");
                return ok;
            }
            if (SelectedObjectId != null)
            {
                var id = SelectedObjectId;
                var o = Document.FindObject(id);
                if (o == null) return false;
                var dest = new GridCoordinate(o.Tile.X + dx, o.Tile.Z + dz);
                bool ok = false;
                ExecuteWithSnapshot(() =>
                {
                    ok = Document.MoveObject(id, dest);
                }, $"Move object {id}");
                return ok;
            }
            return false;
        }

        public bool RotateSelected()
        {
            if (IsReadOnly) return false;
            if (SelectedStructureId != null)
            {
                var id = SelectedStructureId;
                if (Document.FindStructure(id) == null) return false;
                bool ok = false;
                ExecuteWithSnapshot(() =>
                {
                    ok = Document.RotateStructure(id);
                }, $"Rotate structure {id}");
                return ok;
            }
            if (SelectedObjectId != null)
            {
                var id = SelectedObjectId;
                if (Document.FindObject(id) == null) return false;
                bool ok = false;
                ExecuteWithSnapshot(() =>
                {
                    ok = Document.RotateObject(id, 90);
                }, $"Rotate object {id}");
                return ok;
            }
            return false;
        }

        // ================================================================
        //  Undo / redo
        // ================================================================

        /// <summary>
        /// Snapshot-based undo: capture full document JSON before a mutating
        /// operation so Ctrl+Z restores exact instance state (not just grid tiles).
        /// </summary>
        public void ExecuteWithSnapshot(Action mutation, string description)
        {
            if (IsReadOnly || mutation == null) return;
            string before = Document.ToJson(pretty: false);
            mutation();
            string after = Document.ToJson(pretty: false);
            if (before == after) return;
            Undo.Execute(Document.Grid, new DocumentSnapshotCommand(this, before, after, description));
            DocumentChanged?.Invoke();
        }

        public bool UndoLast()
        {
            if (IsReadOnly) return false;
            bool ok = Undo.Undo(Document.Grid);
            if (ok) DocumentChanged?.Invoke();
            return ok;
        }

        public bool RedoLast()
        {
            if (IsReadOnly) return false;
            bool ok = Undo.Redo(Document.Grid);
            if (ok) DocumentChanged?.Invoke();
            return ok;
        }

        // ================================================================
        //  Save / load / validate
        // ================================================================

        public string SaveJson(bool pretty = true) => Document.ToJson(pretty);

        public bool LoadJson(string json)
        {
            var doc = CourseDocument.FromJson(json);
            if (doc == null) return false;
            SetDocument(doc);
            return true;
        }

        public List<ValidationResult> Validate()
            => CourseValidator.ValidateDocument(Document);

        // ================================================================
        //  Internals
        // ================================================================

        private bool ApplyToolOnRegion(GridRegion region)
        {
            if (!region.IsValid) return false;

            switch (Tool)
            {
                case MapEditorTool.PlaceTunnel:
                    return PlaceTunnelRegion(region);
                case MapEditorTool.PlaceRamp:
                    return PlaceRampRegion(region);
                case MapEditorTool.PaintRoad:
                    return ApplyRoad(region.ToCoordinates(), paint: true);
                case MapEditorTool.EraseRoad:
                    return ApplyRoad(region.ToCoordinates(), paint: false);
                case MapEditorTool.PaintSlowZone:
                    return PaintTrigger(region.ToCoordinates(), TriggerType.SlowZone);
                case MapEditorTool.PlaceStartTrigger:
                    return PaintTrigger(region.ToCoordinates(), TriggerType.Start);
                case MapEditorTool.PlaceFinishTrigger:
                    return PaintTrigger(region.ToCoordinates(), TriggerType.Finish);
                case MapEditorTool.PlaceEventTrigger:
                    return PlaceEventAt(region);
                default:
                    return false;
            }
        }

        private bool PlaceTunnelRegion(GridRegion region)
        {
            var results = CourseValidator.ValidateStructurePlacement(Document.Grid, region, StructureType.Tunnel);
            LastValidation = results;
            if (results.Any(r => r.IsError)) return false;

            ExecuteWithSnapshot(() =>
            {
                Document.PlaceTunnel(region, TunnelHeightCm, TunnelWallThicknessCm);
            }, $"Place tunnel ({region.width}x{region.height})");
            return true;
        }

        private bool PlaceRampRegion(GridRegion region)
        {
            var results = CourseValidator.ValidateStructurePlacement(Document.Grid, region, StructureType.Ramp);
            LastValidation = results;
            if (results.Any(r => r.IsError)) return false;

            ExecuteWithSnapshot(() =>
            {
                Document.PlaceRamp(region, RampDirection, RampRiseCm);
            }, $"Place ramp ({region.width}x{region.height})");
            return true;
        }

        private bool PlaceObjectAt(GridCoordinate tile, ObjectType type)
        {
            var results = CourseValidator.ValidateObjectPlacement(Document.Grid, tile, type);
            LastValidation = results;
            // Soft: warnings ok, only block on error
            if (results.Any(r => r.IsError)) return false;

            // Occupancy: prevent two large obstacles on same tile
            if (Document.FindObjectAt(tile) != null && type == ObjectType.Obstacle)
            {
                LastValidation = results.Concat(new[]
                {
                    new ValidationResult(ValidationResult.Severity.Error,
                        $"Tile {tile} already occupied by an object.")
                }).ToList();
                return false;
            }

            ExecuteWithSnapshot(() =>
            {
                Document.PlaceObject(type, tile, ObjectRotationDeg,
                    type == ObjectType.Obstacle ? ObstacleFootprint : ObstacleFootprint.Small);
            }, $"Place {type}");
            return true;
        }

        private bool PlaceTerminalAt(GridCoordinate tile, SpeedTerminalRole role)
        {
            string pairId = string.IsNullOrEmpty(SpeedPairId) ? "speed_zone_01" : SpeedPairId;
            int width = SpeedTerminalWidthTiles < 1 ? 1 : SpeedTerminalWidthTiles;
            ExecuteWithSnapshot(() =>
            {
                Document.PlaceSpeedTerminal(
                    tile.X, tile.Z, SpeedGateEdge, pairId, role, width);
            }, $"Place speed terminal {role} ({pairId})");
            return true;
        }

        private bool PlaceEventAt(GridRegion region)
        {
            var results = CourseValidator.ValidateTriggerPlacement(Document.Grid, region, TriggerType.EventTrigger);
            LastValidation = results;
            if (results.Any(r => r.IsError)) return false;

            ExecuteWithSnapshot(() =>
            {
                Document.PlaceTrigger(TriggerType.EventTrigger, region, EventTriggerId);
            }, "Place event trigger");
            return true;
        }

        private bool PaintTrigger(IEnumerable<GridCoordinate> tiles, TriggerType type)
        {
            var list = tiles.ToList();
            if (list.Count == 0) return false;

            ExecuteWithSnapshot(() =>
            {
                Document.PaintTriggerTiles(list, type);
            }, $"Paint {type}");
            return true;
        }

        private bool ApplyRoad(IEnumerable<GridCoordinate> tiles, bool paint)
        {
            var list = tiles.ToList();
            if (list.Count == 0) return false;

            ExecuteWithSnapshot(() =>
            {
                if (paint) Document.SetRoad(list);
                else foreach (var t in list) Document.ClearRoad(t);
            }, paint ? "Paint road" : "Erase road");
            return true;
        }
    }

    /// <summary>
    /// Undo command that restores a full document JSON snapshot.
    /// </summary>
    internal sealed class DocumentSnapshotCommand : CourseCommand
    {
        private readonly MapEditorSession _session;
        private readonly string _before;
        private readonly string _after;
        private readonly string _desc;

        public DocumentSnapshotCommand(MapEditorSession session, string before, string after, string desc)
        {
            _session = session;
            _before = before;
            _after = after;
            _desc = desc;
        }

        public override string Description => _desc;

        public override void Execute(CourseGrid grid)
        {
            // Redo: restore "after"
            var doc = CourseDocument.FromJson(_after);
            if (doc != null)
            {
                // Replace session document content in-place by swapping reference
                _session.SetDocumentQuiet(doc);
            }
        }

        public override void Undo(CourseGrid grid)
        {
            var doc = CourseDocument.FromJson(_before);
            if (doc != null)
                _session.SetDocumentQuiet(doc);
        }
    }

}
