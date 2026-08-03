using System.Collections;
using JajuchaSim.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JajuchaSim.Course.Tests
{
    /// <summary>
    /// PlayMode integration tests for the course system.
    /// Verifies that the course system integrates correctly with SimulationManager.
    /// </summary>
    public class CourseSystemPlayModeTests
    {
        private SimulationManager _manager;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("[SimulationManager]");
            _manager = go.AddComponent<SimulationManager>();

            var cfg = ScriptableObject.CreateInstance<SimulationConfig>();
            cfg.fixedDeltaTime = 0.01f;
            cfg.randomSeed = 42L;
            cfg.maxTicksPerFrame = 10;
            cfg.autoStart = false;

            _manager.SetConfigForTesting(cfg);
            _manager.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            if (_manager != null)
            {
                if (_manager.State == SimulationState.Running ||
                    _manager.State == SimulationState.Paused)
                {
                    _manager.Stop();
                }
                Object.DestroyImmediate(_manager.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator CourseSystem_CanBeRegistered()
        {
            // Create a simple grid
            var grid = new CourseGrid(20f);
            grid.SetRoad(new GridCoordinate(0, 0));
            grid.SetRoad(new GridCoordinate(1, 0));
            grid.SetRoad(new GridCoordinate(0, 1));
            grid.SetStructure(new GridCoordinate(0, 0), StructureType.Tunnel);

            // Register as a system
            var courseSystem = new CourseSystem(grid);
            _manager.RegisterSystem(courseSystem);

            Assert.IsNotNull(courseSystem.Grid);
            Assert.IsTrue(courseSystem.Grid.HasRoad(new GridCoordinate(0, 0)));
            Assert.AreEqual(StructureType.Tunnel, courseSystem.Grid.GetStructure(new GridCoordinate(0, 0)));
            Assert.AreEqual(3, courseSystem.Grid.RoadTileCount);

            yield return null;
        }

        [UnityTest]
        public IEnumerator CourseSystem_ResetsCleanly()
        {
            var grid = new CourseGrid(20f);
            grid.SetRoad(new GridCoordinate(5, 5));

            var courseSystem = new CourseSystem(grid);
            _manager.RegisterSystem(courseSystem);

            // Reset
            courseSystem.ResetSimulation();

            // Grid should remain (ResetSimulation does not clear grid data)
            Assert.IsNotNull(courseSystem.Grid);
            Assert.IsTrue(courseSystem.Grid.HasRoad(new GridCoordinate(5, 5)));

            yield return null;
        }

        [UnityTest]
        public IEnumerator CourseSystem_Tick_DoesNotThrow()
        {
            var grid = new CourseGrid(20f);
            var courseSystem = new CourseSystem(grid);
            _manager.RegisterSystem(courseSystem);

            // Should not throw during simulation
            _manager.StartSimulation();
            _manager.Advance(10);

            Assert.Pass("CourseSystem.SimulationTick ran without exceptions");

            yield return null;
        }

        [UnityTest]
        public IEnumerator CourseSystem_Shutdown_ClearsGrid()
        {
            var grid = new CourseGrid(20f);
            var courseSystem = new CourseSystem(grid);
            _manager.RegisterSystem(courseSystem);

            Assert.IsNotNull(courseSystem.Grid);

            courseSystem.Shutdown();
            Assert.IsNull(courseSystem.Grid);

            yield return null;
        }

        [UnityTest]
        public IEnumerator CourseSystem_DefaultConstructor_GridIsNull()
        {
            var courseSystem = new CourseSystem();
            _manager.RegisterSystem(courseSystem);

            Assert.IsNull(courseSystem.Grid);

            yield return null;
        }

        [UnityTest]
        public IEnumerator CourseSystem_SetGrid_ReplacesExistingGrid()
        {
            var grid1 = new CourseGrid(20f);
            grid1.SetRoad(new GridCoordinate(0, 0));

            var courseSystem = new CourseSystem(grid1);
            _manager.RegisterSystem(courseSystem);

            Assert.IsTrue(courseSystem.Grid.HasRoad(new GridCoordinate(0, 0)));

            // Replace with new grid
            var grid2 = new CourseGrid(30f);
            grid2.SetRoad(new GridCoordinate(5, 5));
            courseSystem.SetGrid(grid2);

            Assert.IsFalse(courseSystem.Grid.HasRoad(new GridCoordinate(0, 0)));
            Assert.IsTrue(courseSystem.Grid.HasRoad(new GridCoordinate(5, 5)));
            Assert.AreEqual(30f, courseSystem.Grid.TileSizeCm, 1e-6f);

            yield return null;
        }

        [UnityTest]
        public IEnumerator CourseSystem_SetGrid_ToNull_Works()
        {
            var grid = new CourseGrid(20f);
            var courseSystem = new CourseSystem(grid);
            _manager.RegisterSystem(courseSystem);

            Assert.IsNotNull(courseSystem.Grid);

            courseSystem.SetGrid(null);
            Assert.IsNull(courseSystem.Grid);

            yield return null;
        }

        [UnityTest]
        public IEnumerator CourseSystem_Initialize_DoesNotThrow()
        {
            var courseSystem = new CourseSystem();
            _manager.RegisterSystem(courseSystem);

            Assert.DoesNotThrow(() => courseSystem.Initialize(null));

            yield return null;
        }
    }
}
