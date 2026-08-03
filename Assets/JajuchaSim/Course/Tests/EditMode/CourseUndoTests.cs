using NUnit.Framework;

namespace JajuchaSim.Course.Tests
{
    public class CourseUndoTests
    {
        private CourseGrid _grid;
        private CourseUndoStack _undo;

        [SetUp]
        public void SetUp()
        {
            _grid = new CourseGrid(20f);
            _undo = new CourseUndoStack();
        }

        // ---- SetRoadCommand -------------------------------------------

        [Test]
        public void SetRoadCommand_Undo_RestoresState()
        {
            var coord = new GridCoordinate(0, 0);
            var cmd = new SetRoadCommand(new[] { coord });

            cmd.Execute(_grid);
            Assert.IsTrue(_grid.HasRoad(coord));

            cmd.Undo(_grid);
            Assert.IsFalse(_grid.HasRoad(coord));
        }

        [Test]
        public void SetRoadCommand_Undo_OnlyRemovesNewRoad()
        {
            var coord = new GridCoordinate(0, 0);
            _grid.SetRoad(coord); // Pre-existing road

            var cmd = new SetRoadCommand(new[] { coord });
            cmd.Execute(_grid);
            Assert.IsTrue(_grid.HasRoad(coord));

            cmd.Undo(_grid);
            Assert.IsTrue(_grid.HasRoad(coord)); // Should still have road (was there before)
        }

        // ---- ClearRoadCommand -----------------------------------------

        [Test]
        public void ClearRoadCommand_Undo_RestoresRoad()
        {
            var coord = new GridCoordinate(0, 0);
            _grid.SetRoad(coord);

            var cmd = new ClearRoadCommand(new[] { coord });
            cmd.Execute(_grid);
            Assert.IsFalse(_grid.HasRoad(coord));

            cmd.Undo(_grid);
            Assert.IsTrue(_grid.HasRoad(coord));
        }

        // ---- PlaceStructureCommand ------------------------------------

        [Test]
        public void PlaceStructureCommand_Undo_RemovesStructure()
        {
            var coord = new GridCoordinate(0, 0);
            var cmd = new PlaceStructureCommand(new[] { coord }, StructureType.Tunnel);

            cmd.Execute(_grid);
            Assert.AreEqual(StructureType.Tunnel, _grid.GetStructure(coord));

            cmd.Undo(_grid);
            Assert.AreEqual(StructureType.None, _grid.GetStructure(coord));
        }

        [Test]
        public void PlaceStructureCommand_Undo_RestoresPreviousStructure()
        {
            var coord = new GridCoordinate(0, 0);
            _grid.SetStructure(coord, StructureType.Ramp);

            var cmd = new PlaceStructureCommand(new[] { coord }, StructureType.Tunnel);
            cmd.Execute(_grid);
            Assert.AreEqual(StructureType.Tunnel, _grid.GetStructure(coord));

            cmd.Undo(_grid);
            Assert.AreEqual(StructureType.Ramp, _grid.GetStructure(coord));
        }

        // ---- RemoveStructureCommand -----------------------------------

        [Test]
        public void RemoveStructureCommand_Undo_RestoresStructure()
        {
            var coord = new GridCoordinate(0, 0);
            _grid.SetStructure(coord, StructureType.Tunnel);

            var cmd = new RemoveStructureCommand(new[] { coord });
            cmd.Execute(_grid);
            Assert.AreEqual(StructureType.None, _grid.GetStructure(coord));

            cmd.Undo(_grid);
            Assert.AreEqual(StructureType.Tunnel, _grid.GetStructure(coord));
        }

        // ---- PaintTriggerCommand --------------------------------------

        [Test]
        public void PaintTriggerCommand_Undo_RemovesTrigger()
        {
            var coord = new GridCoordinate(0, 0);
            var cmd = new PaintTriggerCommand(new[] { coord }, TriggerType.SlowZone);

            cmd.Execute(_grid);
            Assert.AreEqual(TriggerType.SlowZone, _grid.GetTrigger(coord));

            cmd.Undo(_grid);
            Assert.AreEqual(TriggerType.None, _grid.GetTrigger(coord));
        }

        [Test]
        public void PaintTriggerCommand_Undo_RestoresPreviousTrigger()
        {
            var coord = new GridCoordinate(0, 0);
            _grid.SetTrigger(coord, TriggerType.SpeedGate);

            var cmd = new PaintTriggerCommand(new[] { coord }, TriggerType.SlowZone);
            cmd.Execute(_grid);
            Assert.AreEqual(TriggerType.SlowZone, _grid.GetTrigger(coord));

            cmd.Undo(_grid);
            Assert.AreEqual(TriggerType.SpeedGate, _grid.GetTrigger(coord));
        }

        // ---- MoveStructureCommand -------------------------------------

        [Test]
        public void MoveStructureCommand_Undo_MovesBack()
        {
            var oldTile = new GridCoordinate(0, 0);
            var newTile = new GridCoordinate(1, 0);
            _grid.SetStructure(oldTile, StructureType.Tunnel);

            var cmd = new MoveStructureCommand(new[] { oldTile }, new[] { newTile }, StructureType.Tunnel);
            cmd.Execute(_grid);
            Assert.AreEqual(StructureType.None, _grid.GetStructure(oldTile));
            Assert.AreEqual(StructureType.Tunnel, _grid.GetStructure(newTile));

            cmd.Undo(_grid);
            Assert.AreEqual(StructureType.Tunnel, _grid.GetStructure(oldTile));
            Assert.AreEqual(StructureType.None, _grid.GetStructure(newTile));
        }

        // ---- CourseUndoStack ------------------------------------------

        [Test]
        public void UndoStack_Execute_AddsToUndo()
        {
            Assert.AreEqual(0, _undo.UndoCount);
            _undo.Execute(_grid, new SetRoadCommand(new[] { new GridCoordinate(0, 0) }));
            Assert.AreEqual(1, _undo.UndoCount);
            Assert.AreEqual(0, _undo.RedoCount);
        }

        [Test]
        public void UndoStack_Undo_PopsAndMovesToRedo()
        {
            _undo.Execute(_grid, new SetRoadCommand(new[] { new GridCoordinate(0, 0) }));
            Assert.IsTrue(_grid.HasRoad(new GridCoordinate(0, 0)));

            bool undone = _undo.Undo(_grid);
            Assert.IsTrue(undone);
            Assert.IsFalse(_grid.HasRoad(new GridCoordinate(0, 0)));
            Assert.AreEqual(0, _undo.UndoCount);
            Assert.AreEqual(1, _undo.RedoCount);
        }

        [Test]
        public void UndoStack_Redo_ReappliesCommand()
        {
            _undo.Execute(_grid, new SetRoadCommand(new[] { new GridCoordinate(0, 0) }));
            _undo.Undo(_grid);
            Assert.IsFalse(_grid.HasRoad(new GridCoordinate(0, 0)));

            bool redone = _undo.Redo(_grid);
            Assert.IsTrue(redone);
            Assert.IsTrue(_grid.HasRoad(new GridCoordinate(0, 0)));
            Assert.AreEqual(1, _undo.UndoCount);
            Assert.AreEqual(0, _undo.RedoCount);
        }

        [Test]
        public void UndoStack_Undo_EmptyStack_ReturnsFalse()
        {
            Assert.IsFalse(_undo.Undo(_grid));
            Assert.IsFalse(_undo.Redo(_grid));
        }

        [Test]
        public void UndoStack_Clear_ResetsAll()
        {
            _undo.Execute(_grid, new SetRoadCommand(new[] { new GridCoordinate(0, 0) }));
            _undo.Undo(_grid);
            // After execute + undo: 0 undone + 1 redone = total 1 command tracked
            Assert.AreEqual(1, _undo.UndoCount + _undo.RedoCount);

            _undo.Clear();
            Assert.AreEqual(0, _undo.UndoCount);
            Assert.AreEqual(0, _undo.RedoCount);
        }

        [Test]
        public void UndoStack_NewCommandAfterUndo_ClearsRedo()
        {
            _undo.Execute(_grid, new SetRoadCommand(new[] { new GridCoordinate(0, 0) }));
            _undo.Undo(_grid);
            Assert.AreEqual(1, _undo.RedoCount);

            _undo.Execute(_grid, new SetRoadCommand(new[] { new GridCoordinate(1, 0) }));
            Assert.AreEqual(0, _undo.RedoCount);
            Assert.AreEqual(1, _undo.UndoCount);
        }

        [Test]
        public void UndoStack_PeekDescriptions()
        {
            Assert.IsNull(_undo.PeekUndoDescription());
            Assert.IsNull(_undo.PeekRedoDescription());

            _undo.Execute(_grid, new SetRoadCommand(new[] { new GridCoordinate(0, 0) }, "Paint road"));
            Assert.AreEqual("Paint road", _undo.PeekUndoDescription());

            _undo.Undo(_grid);
            Assert.AreEqual("Paint road", _undo.PeekRedoDescription());
        }

        // ---- EraseTriggerCommand --------------------------------------

        [Test]
        public void EraseTriggerCommand_Undo_RestoresTrigger()
        {
            var coord = new GridCoordinate(0, 0);
            _grid.SetTrigger(coord, TriggerType.SlowZone);

            var cmd = new EraseTriggerCommand(new[] { coord });
            cmd.Execute(_grid);
            Assert.AreEqual(TriggerType.None, _grid.GetTrigger(coord));

            cmd.Undo(_grid);
            Assert.AreEqual(TriggerType.SlowZone, _grid.GetTrigger(coord));
        }

        // ---- RemoveObjectCommand --------------------------------------

        [Test]
        public void RemoveObjectCommand_Undo_RestoresObject()
        {
            var coord = new GridCoordinate(0, 0);
            _grid.SetObject(coord, ObjectType.Obstacle);

            var cmd = new RemoveObjectCommand(coord);
            cmd.Execute(_grid);
            Assert.AreEqual(ObjectType.None, _grid.GetObject(coord));

            cmd.Undo(_grid);
            Assert.AreEqual(ObjectType.Obstacle, _grid.GetObject(coord));
        }
    }
}
