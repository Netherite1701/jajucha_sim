using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace JajuchaSim.Course.Tests
{
    public class CourseGridTests
    {
        [Test]
        public void Constructor_SetsTileSize()
        {
            var grid = new CourseGrid(20f);
            Assert.AreEqual(20f, grid.TileSizeCm, 1e-6f);
        }

        [Test]
        public void Constructor_ClampsNonPositiveTileSizeToDefault()
        {
            var grid = new CourseGrid(0f);
            Assert.AreEqual(20f, grid.TileSizeCm, 1e-6f);

            grid = new CourseGrid(-5f);
            Assert.AreEqual(20f, grid.TileSizeCm, 1e-6f);
        }

        [Test]
        public void Road_SetAndQuery()
        {
            var grid = new CourseGrid(20f);
            var coord = new GridCoordinate(10, 10);

            Assert.IsFalse(grid.HasRoad(coord));
            grid.SetRoad(coord);
            Assert.IsTrue(grid.HasRoad(coord));
            Assert.AreEqual(1, grid.RoadTileCount);
        }

        [Test]
        public void Road_Clear()
        {
            var grid = new CourseGrid(20f);
            var coord = new GridCoordinate(5, 5);
            grid.SetRoad(coord);
            Assert.IsTrue(grid.HasRoad(coord));

            grid.ClearRoad(coord);
            Assert.IsFalse(grid.HasRoad(coord));
            Assert.AreEqual(0, grid.RoadTileCount);
        }

        [Test]
        public void Road_SetMultiple()
        {
            var grid = new CourseGrid(20f);
            var coords = new[] { new GridCoordinate(0, 0), new GridCoordinate(1, 0), new GridCoordinate(2, 0) };
            grid.SetRoad(coords);

            Assert.IsTrue(grid.HasRoad(new GridCoordinate(0, 0)));
            Assert.IsTrue(grid.HasRoad(new GridCoordinate(1, 0)));
            Assert.IsTrue(grid.HasRoad(new GridCoordinate(2, 0)));
            Assert.IsFalse(grid.HasRoad(new GridCoordinate(0, 1)));
            Assert.AreEqual(3, grid.RoadTileCount);
        }

        [Test]
        public void Road_ClearMultiple()
        {
            var grid = new CourseGrid(20f);
            var coords = new[] { new GridCoordinate(0, 0), new GridCoordinate(1, 0) };
            grid.SetRoad(coords);
            Assert.AreEqual(2, grid.RoadTileCount);

            grid.ClearRoad(new[] { new GridCoordinate(0, 0) });
            Assert.IsFalse(grid.HasRoad(new GridCoordinate(0, 0)));
            Assert.IsTrue(grid.HasRoad(new GridCoordinate(1, 0)));
            Assert.AreEqual(1, grid.RoadTileCount);
        }

        [Test]
        public void Road_AllRoadTiles_ReturnsAll()
        {
            var grid = new CourseGrid(20f);
            grid.SetRoad(new GridCoordinate(2, 3));
            grid.SetRoad(new GridCoordinate(5, 7));

            var all = grid.AllRoadTiles().ToList();
            Assert.AreEqual(2, all.Count);
            Assert.Contains(new GridCoordinate(2, 3), all);
            Assert.Contains(new GridCoordinate(5, 7), all);
        }

        // ---- Structure layer tests ------------------------------------

        [Test]
        public void Structure_SetAndQuery()
        {
            var grid = new CourseGrid(20f);
            var coord = new GridCoordinate(0, 0);

            Assert.AreEqual(StructureType.None, grid.GetStructure(coord));

            grid.SetStructure(coord, StructureType.Tunnel);
            Assert.AreEqual(StructureType.Tunnel, grid.GetStructure(coord));
            Assert.AreEqual(1, grid.StructureTileCount);
        }

        [Test]
        public void Structure_SetNone_Removes()
        {
            var grid = new CourseGrid(20f);
            var coord = new GridCoordinate(0, 0);
            grid.SetStructure(coord, StructureType.Tunnel);
            Assert.AreEqual(StructureType.Tunnel, grid.GetStructure(coord));

            grid.SetStructure(coord, StructureType.None);
            Assert.AreEqual(StructureType.None, grid.GetStructure(coord));
            Assert.AreEqual(0, grid.StructureTileCount);
        }

        [Test]
        public void Structure_Clear()
        {
            var grid = new CourseGrid(20f);
            grid.SetStructure(new GridCoordinate(0, 0), StructureType.Ramp);
            grid.ClearStructure(new GridCoordinate(0, 0));
            Assert.AreEqual(StructureType.None, grid.GetStructure(new GridCoordinate(0, 0)));
            Assert.AreEqual(0, grid.StructureTileCount);
        }

        [Test]
        public void Structure_SetMultiple()
        {
            var grid = new CourseGrid(20f);
            var coords = new[] { new GridCoordinate(0, 0), new GridCoordinate(0, 1), new GridCoordinate(0, 2) };
            grid.SetStructure(coords, StructureType.Tunnel);

            foreach (var c in coords)
                Assert.AreEqual(StructureType.Tunnel, grid.GetStructure(c));
            Assert.AreEqual(3, grid.StructureTileCount);
        }

        [Test]
        public void Structure_AllStructures_ReturnsAll()
        {
            var grid = new CourseGrid(20f);
            grid.SetStructure(new GridCoordinate(1, 1), StructureType.Tunnel);
            grid.SetStructure(new GridCoordinate(2, 2), StructureType.Ramp);

            var all = grid.AllStructures().ToList();
            Assert.AreEqual(2, all.Count);
        }

        // ---- Object layer tests ---------------------------------------

        [Test]
        public void Object_SetAndQuery()
        {
            var grid = new CourseGrid(20f);
            var coord = new GridCoordinate(5, 5);

            Assert.AreEqual(ObjectType.None, grid.GetObject(coord));

            grid.SetObject(coord, ObjectType.Obstacle);
            Assert.AreEqual(ObjectType.Obstacle, grid.GetObject(coord));
            Assert.AreEqual(1, grid.ObjectTileCount);
        }

        [Test]
        public void Object_SetNone_Removes()
        {
            var grid = new CourseGrid(20f);
            grid.SetObject(new GridCoordinate(0, 0), ObjectType.Sign);
            grid.SetObject(new GridCoordinate(0, 0), ObjectType.None);
            Assert.AreEqual(ObjectType.None, grid.GetObject(new GridCoordinate(0, 0)));
            Assert.AreEqual(0, grid.ObjectTileCount);
        }

        [Test]
        public void Object_Clear()
        {
            var grid = new CourseGrid(20f);
            grid.SetObject(new GridCoordinate(3, 7), ObjectType.StartSignal);
            grid.ClearObject(new GridCoordinate(3, 7));
            Assert.AreEqual(ObjectType.None, grid.GetObject(new GridCoordinate(3, 7)));
        }

        [Test]
        public void Object_SetMultiple()
        {
            var grid = new CourseGrid(20f);
            var coords = new[] { new GridCoordinate(0, 0), new GridCoordinate(1, 0) };
            grid.SetObject(coords, ObjectType.Obstacle);

            foreach (var c in coords)
                Assert.AreEqual(ObjectType.Obstacle, grid.GetObject(c));
            Assert.AreEqual(2, grid.ObjectTileCount);
        }

        [Test]
        public void Object_AllObjects_ReturnsAll()
        {
            var grid = new CourseGrid(20f);
            grid.SetObject(new GridCoordinate(1, 1), ObjectType.Obstacle);
            grid.SetObject(new GridCoordinate(2, 2), ObjectType.Sign);

            var all = grid.AllObjects().ToList();
            Assert.AreEqual(2, all.Count);
        }

        // ---- Trigger layer tests --------------------------------------

        [Test]
        public void Trigger_SetAndQuery()
        {
            var grid = new CourseGrid(20f);
            var coord = new GridCoordinate(0, 0);

            Assert.AreEqual(TriggerType.None, grid.GetTrigger(coord));

            grid.SetTrigger(coord, TriggerType.SlowZone);
            Assert.AreEqual(TriggerType.SlowZone, grid.GetTrigger(coord));
            Assert.AreEqual(1, grid.TriggerTileCount);
        }

        [Test]
        public void Trigger_SetNone_Removes()
        {
            var grid = new CourseGrid(20f);
            grid.SetTrigger(new GridCoordinate(0, 0), TriggerType.SpeedGate);
            grid.SetTrigger(new GridCoordinate(0, 0), TriggerType.None);
            Assert.AreEqual(TriggerType.None, grid.GetTrigger(new GridCoordinate(0, 0)));
            Assert.AreEqual(0, grid.TriggerTileCount);
        }

        [Test]
        public void Trigger_Clear()
        {
            var grid = new CourseGrid(20f);
            grid.SetTrigger(new GridCoordinate(4, 4), TriggerType.EventTrigger);
            grid.ClearTrigger(new GridCoordinate(4, 4));
            Assert.AreEqual(TriggerType.None, grid.GetTrigger(new GridCoordinate(4, 4)));
        }

        [Test]
        public void Trigger_SetMultiple()
        {
            var grid = new CourseGrid(20f);
            var coords = new[] { new GridCoordinate(0, 0), new GridCoordinate(0, 1), new GridCoordinate(0, 2) };
            grid.SetTrigger(coords, TriggerType.SlowZone);

            foreach (var c in coords)
                Assert.AreEqual(TriggerType.SlowZone, grid.GetTrigger(c));
            Assert.AreEqual(3, grid.TriggerTileCount);
        }

        [Test]
        public void Trigger_AllTriggers_ReturnsAll()
        {
            var grid = new CourseGrid(20f);
            grid.SetTrigger(new GridCoordinate(1, 1), TriggerType.SlowZone);
            grid.SetTrigger(new GridCoordinate(2, 2), TriggerType.EventTrigger);

            var all = grid.AllTriggers().ToList();
            Assert.AreEqual(2, all.Count);
        }

        // ---- Layer overlap tests --------------------------------------

        [Test]
        public void Layers_CanOverlap()
        {
            var grid = new CourseGrid(20f);
            var coord = new GridCoordinate(12, 8);

            // Road + Structure + Trigger on the same tile
            grid.SetRoad(coord);
            grid.SetStructure(coord, StructureType.Tunnel);
            grid.SetTrigger(coord, TriggerType.SlowZone);

            Assert.IsTrue(grid.HasRoad(coord));
            Assert.AreEqual(StructureType.Tunnel, grid.GetStructure(coord));
            Assert.AreEqual(ObjectType.None, grid.GetObject(coord));
            Assert.AreEqual(TriggerType.SlowZone, grid.GetTrigger(coord));
        }

        // ---- ClearAll -------------------------------------------------

        [Test]
        public void ClearAll_RemovesEverything()
        {
            var grid = new CourseGrid(20f);
            grid.SetRoad(new GridCoordinate(0, 0));
            grid.SetStructure(new GridCoordinate(0, 1), StructureType.Tunnel);
            grid.SetObject(new GridCoordinate(1, 0), ObjectType.Obstacle);
            grid.SetTrigger(new GridCoordinate(1, 1), TriggerType.SlowZone);

            Assert.AreEqual(1, grid.RoadTileCount);
            Assert.AreEqual(1, grid.StructureTileCount);
            Assert.AreEqual(1, grid.ObjectTileCount);
            Assert.AreEqual(1, grid.TriggerTileCount);

            grid.ClearAll();

            Assert.AreEqual(0, grid.RoadTileCount);
            Assert.AreEqual(0, grid.StructureTileCount);
            Assert.AreEqual(0, grid.ObjectTileCount);
            Assert.AreEqual(0, grid.TriggerTileCount);
        }

        // ---- Rectangle helper -----------------------------------------

        [Test]
        public void Rectangle_EnumeratesInclusive()
        {
            var rect = CourseGrid.Rectangle(1, 2, 3, 4).ToList();

            // Expected: (1,2),(2,2),(3,2),(1,3),(2,3),(3,3),(1,4),(2,4),(3,4)
            Assert.AreEqual(9, rect.Count);
            Assert.Contains(new GridCoordinate(1, 2), rect);
            Assert.Contains(new GridCoordinate(3, 4), rect);
            Assert.IsFalse(rect.Contains(new GridCoordinate(0, 2)));
            Assert.IsFalse(rect.Contains(new GridCoordinate(1, 5)));
        }

        [Test]
        public void Rectangle_SingleTile()
        {
            var rect = CourseGrid.Rectangle(5, 5, 5, 5).ToList();
            Assert.AreEqual(1, rect.Count);
            Assert.AreEqual(new GridCoordinate(5, 5), rect[0]);
        }

        // ---- TileInfo -------------------------------------------------

        [Test]
        public void GetTileInfo_ReturnsSnapshot()
        {
            var grid = new CourseGrid(20f);
            var coord = new GridCoordinate(7, 3);
            grid.SetRoad(coord);
            grid.SetStructure(coord, StructureType.Ramp);
            grid.SetObject(coord, ObjectType.Sign);

            var info = grid.GetTileInfo(coord);
            Assert.AreEqual(coord, info.Coordinate);
            Assert.IsTrue(info.Road);
            Assert.AreEqual(StructureType.Ramp, info.Structure);
            Assert.AreEqual(ObjectType.Sign, info.Object);
            Assert.AreEqual(TriggerType.None, info.Trigger);
        }

        // ---- GridCoordinate tests -------------------------------------

        [Test]
        public void GridCoordinate_Equality()
        {
            var a = new GridCoordinate(3, 5);
            var b = new GridCoordinate(3, 5);
            var c = new GridCoordinate(5, 3);

            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, c);
        }

        [Test]
        public void GridCoordinate_ToString()
        {
            var coord = new GridCoordinate(10, -3);
            Assert.AreEqual("(10, -3)", coord.ToString());
        }

        [Test]
        public void GridCoordinate_OrthogonalNeighbours()
        {
            var coord = new GridCoordinate(5, 5);
            var neighbours = coord.OrthogonalNeighbours();

            Assert.AreEqual(4, neighbours.Length);
            Assert.Contains(new GridCoordinate(5, 4), neighbours); // up
            Assert.Contains(new GridCoordinate(5, 6), neighbours); // down
            Assert.Contains(new GridCoordinate(4, 5), neighbours); // left
            Assert.Contains(new GridCoordinate(6, 5), neighbours); // right
        }

        [Test]
        public void GridCoordinate_AllNeighbours()
        {
            var coord = new GridCoordinate(0, 0);
            var neighbours = coord.AllNeighbours();

            Assert.AreEqual(8, neighbours.Length);
            // Includes diagonals
            Assert.Contains(new GridCoordinate(-1, -1), neighbours);
            Assert.Contains(new GridCoordinate(1, 1), neighbours);
        }

        // ---- Adversarial edge cases -----------------------------------

        [Test]
        public void Road_SetTwice_IsIdempotent()
        {
            var grid = new CourseGrid(20f);
            var coord = new GridCoordinate(5, 5);
            grid.SetRoad(coord);
            grid.SetRoad(coord); // Duplicate
            Assert.AreEqual(1, grid.RoadTileCount);
            Assert.IsTrue(grid.HasRoad(coord));
        }

        [Test]
        public void Structure_OverwriteWithDifferentType()
        {
            var grid = new CourseGrid(20f);
            var coord = new GridCoordinate(3, 3);
            grid.SetStructure(coord, StructureType.Tunnel);
            Assert.AreEqual(StructureType.Tunnel, grid.GetStructure(coord));

            grid.SetStructure(coord, StructureType.Ramp); // Overwrite
            Assert.AreEqual(StructureType.Ramp, grid.GetStructure(coord));
            Assert.AreEqual(1, grid.StructureTileCount);
        }

        [Test]
        public void Object_OverwriteWithDifferentType()
        {
            var grid = new CourseGrid(20f);
            var coord = new GridCoordinate(4, 4);
            grid.SetObject(coord, ObjectType.Obstacle);
            Assert.AreEqual(ObjectType.Obstacle, grid.GetObject(coord));

            grid.SetObject(coord, ObjectType.Sign); // Overwrite
            Assert.AreEqual(ObjectType.Sign, grid.GetObject(coord));
            Assert.AreEqual(1, grid.ObjectTileCount);
        }

        [Test]
        public void Trigger_OverwriteWithDifferentType()
        {
            var grid = new CourseGrid(20f);
            var coord = new GridCoordinate(6, 6);
            grid.SetTrigger(coord, TriggerType.SlowZone);
            Assert.AreEqual(TriggerType.SlowZone, grid.GetTrigger(coord));

            grid.SetTrigger(coord, TriggerType.SpeedGate); // Overwrite
            Assert.AreEqual(TriggerType.SpeedGate, grid.GetTrigger(coord));
            Assert.AreEqual(1, grid.TriggerTileCount);
        }

        [Test]
        public void ClearRoad_NonExistentTile_DoesNotThrow()
        {
            var grid = new CourseGrid(20f);
            var coord = new GridCoordinate(99, 99);
            Assert.DoesNotThrow(() => grid.ClearRoad(coord));
            Assert.IsFalse(grid.HasRoad(coord));
        }

        [Test]
        public void ClearStructure_NonExistentTile_DoesNotThrow()
        {
            var grid = new CourseGrid(20f);
            var coord = new GridCoordinate(99, 99);
            Assert.DoesNotThrow(() => grid.ClearStructure(coord));
            Assert.AreEqual(StructureType.None, grid.GetStructure(coord));
        }

        [Test]
        public void LargeCoordinateValues_Work()
        {
            var grid = new CourseGrid(20f);
            var coord = new GridCoordinate(10000, 10000);
            grid.SetRoad(coord);
            grid.SetStructure(coord, StructureType.Tunnel);
            grid.SetObject(coord, ObjectType.Obstacle);
            grid.SetTrigger(coord, TriggerType.SlowZone);

            Assert.IsTrue(grid.HasRoad(coord));
            Assert.AreEqual(StructureType.Tunnel, grid.GetStructure(coord));
            Assert.AreEqual(ObjectType.Obstacle, grid.GetObject(coord));
            Assert.AreEqual(TriggerType.SlowZone, grid.GetTrigger(coord));
        }

        [Test]
        public void NegativeCoordinateValues_Work()
        {
            var grid = new CourseGrid(20f);
            var coord = new GridCoordinate(-10, -20);
            grid.SetRoad(coord);
            Assert.IsTrue(grid.HasRoad(coord));
            Assert.AreEqual(new GridCoordinate(-10, -20), coord);
        }

        [Test]
        public void Rectangle_InvertedBounds_ReturnsEmpty()
        {
            // xMin > xMax should produce empty enumeration
            var rect = CourseGrid.Rectangle(5, 5, 3, 7).ToList();
            Assert.AreEqual(0, rect.Count);

            // zMin > zMax should produce empty enumeration
            rect = CourseGrid.Rectangle(1, 5, 3, 2).ToList();
            Assert.AreEqual(0, rect.Count);
        }

        [Test]
        public void Constructor_MinTileSize_Works()
        {
            var grid = new CourseGrid(1f);
            Assert.AreEqual(1f, grid.TileSizeCm, 1e-6f);
        }

        [Test]
        public void Constructor_VeryLargeTileSize_Works()
        {
            var grid = new CourseGrid(10000f);
            Assert.AreEqual(10000f, grid.TileSizeCm, 1e-6f);
        }

        [Test]
        public void Structure_ClearMultiple_RemovesAll()
        {
            var grid = new CourseGrid(20f);
            var coords = new[] { new GridCoordinate(0, 0), new GridCoordinate(1, 0), new GridCoordinate(2, 0) };
            grid.SetStructure(coords, StructureType.Tunnel);
            Assert.AreEqual(3, grid.StructureTileCount);

            grid.ClearStructure(new[] { new GridCoordinate(0, 0), new GridCoordinate(1, 0) });
            Assert.AreEqual(1, grid.StructureTileCount);
            Assert.AreEqual(StructureType.None, grid.GetStructure(new GridCoordinate(0, 0)));
            Assert.AreEqual(StructureType.None, grid.GetStructure(new GridCoordinate(1, 0)));
            Assert.AreEqual(StructureType.Tunnel, grid.GetStructure(new GridCoordinate(2, 0)));
        }

        [Test]
        public void Object_ClearMultiple_RemovesAll()
        {
            var grid = new CourseGrid(20f);
            var coords = new[] { new GridCoordinate(0, 0), new GridCoordinate(1, 0) };
            grid.SetObject(coords, ObjectType.Obstacle);
            Assert.AreEqual(2, grid.ObjectTileCount);

            grid.ClearObject(new[] { new GridCoordinate(0, 0) });
            Assert.AreEqual(1, grid.ObjectTileCount);
            Assert.AreEqual(ObjectType.None, grid.GetObject(new GridCoordinate(0, 0)));
            Assert.AreEqual(ObjectType.Obstacle, grid.GetObject(new GridCoordinate(1, 0)));
        }

        [Test]
        public void Trigger_ClearMultiple_RemovesAll()
        {
            var grid = new CourseGrid(20f);
            var coords = new[] { new GridCoordinate(0, 0), new GridCoordinate(1, 0) };
            grid.SetTrigger(coords, TriggerType.SlowZone);
            Assert.AreEqual(2, grid.TriggerTileCount);

            grid.ClearTrigger(new[] { new GridCoordinate(0, 0) });
            Assert.AreEqual(1, grid.TriggerTileCount);
            Assert.AreEqual(TriggerType.None, grid.GetTrigger(new GridCoordinate(0, 0)));
            Assert.AreEqual(TriggerType.SlowZone, grid.GetTrigger(new GridCoordinate(1, 0)));
        }

        [Test]
        public void ClearObject_NonExistentTile_DoesNotThrow()
        {
            var grid = new CourseGrid(20f);
            var coord = new GridCoordinate(99, 99);
            Assert.DoesNotThrow(() => grid.ClearObject(coord));
            Assert.AreEqual(ObjectType.None, grid.GetObject(coord));
        }

        [Test]
        public void ClearTrigger_NonExistentTile_DoesNotThrow()
        {
            var grid = new CourseGrid(20f);
            var coord = new GridCoordinate(99, 99);
            Assert.DoesNotThrow(() => grid.ClearTrigger(coord));
            Assert.AreEqual(TriggerType.None, grid.GetTrigger(coord));
        }

        [Test]
        public void TileInfo_ToString_ContainsAllFields()
        {
            var grid = new CourseGrid(20f);
            var coord = new GridCoordinate(5, 5);
            grid.SetRoad(coord);
            grid.SetStructure(coord, StructureType.Tunnel);
            grid.SetObject(coord, ObjectType.Sign);
            grid.SetTrigger(coord, TriggerType.SlowZone);

            var info = grid.GetTileInfo(coord);
            var str = info.ToString();
            Assert.IsTrue(str.Contains("5, 5"));
            Assert.IsTrue(str.Contains("Road=True"));
            Assert.IsTrue(str.Contains("Tunnel"));
            Assert.IsTrue(str.Contains("Sign"));
            Assert.IsTrue(str.Contains("SlowZone"));
        }
    }
}
