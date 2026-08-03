using System.Linq;
using NUnit.Framework;

namespace JajuchaSim.Course.Tests
{
    public class CourseValidatorTests
    {
        // ---- Validate grid --------------------------------------------

        [Test]
        public void Validate_NullGrid_ReturnsError()
        {
            var results = CourseValidator.Validate(null);
            Assert.IsTrue(results.Any(r => r.IsError));
            Assert.IsTrue(results.Any(r => r.Message.Contains("null")));
        }

        [Test]
        public void Validate_EmptyGrid_ReturnsWarning()
        {
            var grid = new CourseGrid(20f);
            var results = CourseValidator.Validate(grid);
            Assert.IsTrue(results.Any(r => r.IsWarning));
            Assert.IsTrue(results.Any(r => r.Message.Contains("no road")));
        }

        [Test]
        public void Validate_GridWithRoad_NoErrors()
        {
            var grid = new CourseGrid(20f);
            grid.SetRoad(new GridCoordinate(0, 0));
            grid.SetRoad(new GridCoordinate(1, 0));

            var results = CourseValidator.Validate(grid);
            Assert.IsFalse(results.Any(r => r.IsError));
        }

        [Test]
        public void Validate_StructureWithoutRoad_Warns()
        {
            var grid = new CourseGrid(20f);
            grid.SetRoad(new GridCoordinate(0, 0));
            grid.SetStructure(new GridCoordinate(1, 0), StructureType.Tunnel);

            var results = CourseValidator.Validate(grid);
            Assert.IsTrue(results.Any(r => r.IsWarning && r.Message.Contains("no road")));
        }

        [Test]
        public void Validate_ObjectWithoutRoad_Warns()
        {
            var grid = new CourseGrid(20f);
            grid.SetRoad(new GridCoordinate(0, 0));
            grid.SetObject(new GridCoordinate(1, 0), ObjectType.Obstacle);

            var results = CourseValidator.Validate(grid);
            Assert.IsTrue(results.Any(r => r.IsWarning && r.Message.Contains("not on a road")));
        }

        // ---- ValidateStructurePlacement -------------------------------

        [Test]
        public void ValidateStructurePlacement_InvalidRegion_ReturnsError()
        {
            var grid = new CourseGrid(20f);
            var region = new GridRegion(0, 0, 0, 1);
            var results = CourseValidator.ValidateStructurePlacement(grid, region, StructureType.Tunnel);
            Assert.IsTrue(results.Any(r => r.IsError && r.Message.Contains("invalid")));
        }

        [Test]
        public void ValidateStructurePlacement_NoRoadUnderneath_ReturnsError()
        {
            var grid = new CourseGrid(20f);
            var region = new GridRegion(0, 0, 2, 2);
            var results = CourseValidator.ValidateStructurePlacement(grid, region, StructureType.Tunnel);
            Assert.IsTrue(results.Any(r => r.IsError && r.Message.Contains("no road")));
        }

        [Test]
        public void ValidateStructurePlacement_PartialRoad_Warns()
        {
            var grid = new CourseGrid(20f);
            grid.SetRoad(new GridCoordinate(0, 0));
            grid.SetRoad(new GridCoordinate(1, 0));
            // Region 2x2 has 2 road tiles, 2 non-road
            var region = new GridRegion(0, 0, 2, 2);
            var results = CourseValidator.ValidateStructurePlacement(grid, region, StructureType.Tunnel);
            Assert.IsTrue(results.Any(r => r.IsWarning && r.Message.Contains("do not contain road")));
        }

        [Test]
        public void ValidateStructurePlacement_RampFullRoadCoverageRequired()
        {
            var grid = new CourseGrid(20f);
            grid.SetRoad(new GridCoordinate(0, 0));
            // Region 2x1: only 1 of 2 tiles has road
            var region = new GridRegion(0, 0, 2, 1);
            var results = CourseValidator.ValidateStructurePlacement(grid, region, StructureType.Ramp);
            Assert.IsTrue(results.Any(r => r.IsError && r.Message.Contains("road coverage")));
        }

        [Test]
        public void ValidateStructurePlacement_RampFullCoverage_OK()
        {
            var grid = new CourseGrid(20f);
            grid.SetRoad(new GridCoordinate(0, 0));
            grid.SetRoad(new GridCoordinate(1, 0));
            var region = new GridRegion(0, 0, 2, 1);
            var results = CourseValidator.ValidateStructurePlacement(grid, region, StructureType.Ramp);
            // Both tiles are road, so no road-coverage error
            Assert.IsFalse(results.Any(r => r.IsError && r.Message.Contains("road coverage")));
        }

        [Test]
        public void ValidateStructurePlacement_OverlappingStructure_Warns()
        {
            var grid = new CourseGrid(20f);
            grid.SetRoad(new GridCoordinate(0, 0));
            grid.SetRoad(new GridCoordinate(1, 0));
            grid.SetStructure(new GridCoordinate(1, 0), StructureType.Tunnel);

            var region = new GridRegion(0, 0, 2, 1);
            var results = CourseValidator.ValidateStructurePlacement(grid, region, StructureType.Ramp);
            Assert.IsTrue(results.Any(r => r.IsWarning && r.Message.Contains("overlap")));
        }

        // ---- ValidateObjectPlacement ----------------------------------

        [Test]
        public void ValidateObjectPlacement_NoRoad_Warns()
        {
            var grid = new CourseGrid(20f);
            var coord = new GridCoordinate(5, 5);
            var results = CourseValidator.ValidateObjectPlacement(grid, coord, ObjectType.Obstacle);
            Assert.IsTrue(results.Any(r => r.IsWarning && r.Message.Contains("not a road")));
        }

        [Test]
        public void ValidateObjectPlacement_OnRoad_OK()
        {
            var grid = new CourseGrid(20f);
            grid.SetRoad(new GridCoordinate(5, 5));
            var results = CourseValidator.ValidateObjectPlacement(grid, new GridCoordinate(5, 5), ObjectType.Sign);
            Assert.IsFalse(results.Any(r => r.IsError));
        }

        [Test]
        public void ValidateObjectPlacement_OverlappingObject_Warns()
        {
            var grid = new CourseGrid(20f);
            grid.SetRoad(new GridCoordinate(5, 5));
            grid.SetObject(new GridCoordinate(5, 5), ObjectType.Obstacle);

            var results = CourseValidator.ValidateObjectPlacement(grid, new GridCoordinate(5, 5), ObjectType.Sign);
            Assert.IsTrue(results.Any(r => r.IsWarning && r.Message.Contains("already has")));
        }

        [Test]
        public void ValidateObjectPlacement_InTunnel_Info()
        {
            var grid = new CourseGrid(20f);
            grid.SetRoad(new GridCoordinate(5, 5));
            grid.SetStructure(new GridCoordinate(5, 5), StructureType.Tunnel);

            var results = CourseValidator.ValidateObjectPlacement(grid, new GridCoordinate(5, 5), ObjectType.StartSignal);
            Assert.IsTrue(results.Any(r => r.Message.Contains("inside a tunnel")));
        }

        // ---- ValidateTriggerPlacement ---------------------------------

        [Test]
        public void ValidateTriggerPlacement_InvalidRegion_ReturnsError()
        {
            var grid = new CourseGrid(20f);
            var region = new GridRegion(0, 0, 0, 1);
            var results = CourseValidator.ValidateTriggerPlacement(grid, region, TriggerType.SlowZone);
            Assert.IsTrue(results.Any(r => r.IsError));
        }

        [Test]
        public void ValidateTriggerPlacement_ValidRegion_NoErrors()
        {
            var grid = new CourseGrid(20f);
            var region = new GridRegion(0, 0, 3, 4);
            var results = CourseValidator.ValidateTriggerPlacement(grid, region, TriggerType.SlowZone);
            Assert.AreEqual(0, results.Count);
        }

        // ---- Severity helpers -----------------------------------------

        [Test]
        public void ValidationResult_ToString_ContainsSeverity()
        {
            var r = new ValidationResult(ValidationResult.Severity.Error, "test error", "id_001", "tunnel");
            var s = r.ToString();
            Assert.IsTrue(s.Contains("ERROR"));
            Assert.IsTrue(s.Contains("test error"));
            Assert.IsTrue(s.Contains("id_001"));
        }

        [Test]
        public void ValidationResult_IsError_TrueForError()
        {
            var r = new ValidationResult(ValidationResult.Severity.Error, "err");
            Assert.IsTrue(r.IsError);
            Assert.IsFalse(r.IsWarning);
        }

        [Test]
        public void ValidationResult_IsWarning_TrueForWarning()
        {
            var r = new ValidationResult(ValidationResult.Severity.Warning, "warn");
            Assert.IsTrue(r.IsWarning);
            Assert.IsFalse(r.IsError);
        }
    }
}
