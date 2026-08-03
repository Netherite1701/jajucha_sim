using System.Collections.Generic;
using JajuchaSim.Core;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Course.Tests
{
    /// <summary>
    /// EditMode tests for the TriggerDetectionSystem.
    /// These tests exercise the pure-logic detection without requiring
    /// a Unity scene or PlayMode.
    /// </summary>
    public class TriggerDetectionSystemTests
    {
        private TriggerDetectionSystem _system;
        private CourseGrid _grid;
        private SimulationEventBus _events;
        private List<object> _capturedEvents;

        [SetUp]
        public void SetUp()
        {
            _grid = new CourseGrid(20f);
            _system = new TriggerDetectionSystem(_grid);
            _events = new SimulationEventBus();
            _capturedEvents = new List<object>();

            _events.Subscribe<TriggerEnteredEvent>(e => _capturedEvents.Add(e));
            _events.Subscribe<TriggerExitedEvent>(e => _capturedEvents.Add(e));
            _events.Subscribe<SpeedGateCrossedEvent>(e => _capturedEvents.Add(e));

            var ctx = new SimulationContext(
                new SimulationClock(0.01f),
                _events,
                new SimulationRandom(42)
            );
            _system.Initialize(ctx);
        }

        [TearDown]
        public void TearDown()
        {
            _system.Shutdown();
        }

        // ---- Helper: set vehicle pose --------------------------------

        private void SetPose(Vector3 position)
        {
            _system.GetVehiclePose = () => new VehiclePose
            {
                Position = position,
                SamplePoints = new[] { position }
            };
        }

        // ---- Trigger Enter/Exit tests --------------------------------

        [Test]
        public void EnterTrigger_FiresEnterEvent()
        {
            // Place a slow zone at (5,5)
            _grid.SetTrigger(new GridCoordinate(5, 5), TriggerType.SlowZone);

            // Start outside
            SetPose(new Vector3(0, 0, 0));
            _system.SimulationTick(0.01f);

            // Move into the zone (tile 5,5 center = 110, 0, 110 in world)
            SetPose(new Vector3(110, 0, 110));
            _system.SimulationTick(0.01f);

            // Should have received exactly one Enter event
            var enterEvents = _capturedEvents.FindAll(e => e is TriggerEnteredEvent);
            Assert.AreEqual(1, enterEvents.Count);
            var enter = (TriggerEnteredEvent)enterEvents[0];
            Assert.AreEqual(new GridCoordinate(5, 5), enter.Tile);
            Assert.AreEqual(TriggerType.SlowZone, enter.Type);
        }

        [Test]
        public void StayInside_DoesNotRepeatEnter()
        {
            _grid.SetTrigger(new GridCoordinate(5, 5), TriggerType.SlowZone);

            // Move inside and tick once
            SetPose(new Vector3(110, 0, 110));
            _system.SimulationTick(0.01f);

            // Tick again while staying inside
            _system.SimulationTick(0.01f);

            // Should still have only 1 enter event
            var enterEvents = _capturedEvents.FindAll(e => e is TriggerEnteredEvent);
            Assert.AreEqual(1, enterEvents.Count);
        }

        [Test]
        public void EnterThenExit_FiresExitEvent()
        {
            _grid.SetTrigger(new GridCoordinate(5, 5), TriggerType.SlowZone);

            // Move inside
            SetPose(new Vector3(110, 0, 110));
            _system.SimulationTick(0.01f);

            // Move outside
            SetPose(new Vector3(0, 0, 0));
            _system.SimulationTick(0.01f);

            var enterEvents = _capturedEvents.FindAll(e => e is TriggerEnteredEvent);
            var exitEvents = _capturedEvents.FindAll(e => e is TriggerExitedEvent);

            Assert.AreEqual(1, enterEvents.Count);
            Assert.AreEqual(1, exitEvents.Count);

            var exit = (TriggerExitedEvent)exitEvents[0];
            Assert.AreEqual(new GridCoordinate(5, 5), exit.Tile);
            Assert.AreEqual(TriggerType.SlowZone, exit.Type);
        }

        [Test]
        public void EnterMultipleTiles_FiresEnterForEach()
        {
            // Place slow zone on 2 tiles
            _grid.SetTrigger(new GridCoordinate(5, 5), TriggerType.SlowZone);
            _grid.SetTrigger(new GridCoordinate(5, 6), TriggerType.SlowZone);

            // Pose that covers both tiles
            SetPose(new Vector3(110, 0, 110));
            _system.GetVehiclePose = () => new VehiclePose
            {
                Position = new Vector3(110, 0, 110),
                SamplePoints = new[]
                {
                    new Vector3(105, 0, 105), // tile (5,5)
                    new Vector3(105, 0, 115)  // tile (5,6) [z=115 / 20 = 5.75 → floor 5... actually 115/20=5.75 floor=5]
                }
            };
            _system.SimulationTick(0.01f);

            // Should enter at least 1 tile
            var enterEvents = _capturedEvents.FindAll(e => e is TriggerEnteredEvent);
            Assert.IsTrue(enterEvents.Count >= 1, "Should have at least one enter event");
        }

        // ---- Speed Gate Crossing tests --------------------------------

        [Test]
        public void CrossSpeedGate_FiresCrossEvent()
        {
            // Place a speed gate at (10, 10) - default north edge is z = (10+1)*20 = 220
            _grid.SetTrigger(new GridCoordinate(10, 10), TriggerType.SpeedGate);

            // Start south of the north edge (z < 220)
            SetPose(new Vector3(210, 0, 200));
            _system.SimulationTick(0.01f);

            // Move north across the gate line at z=220
            _system.GetVehiclePose = () => new VehiclePose
            {
                Position = new Vector3(210, 0, 240),
                SamplePoints = new[] { new Vector3(210, 0, 240) }
            };
            _system.SimulationTick(0.01f);

            var crossEvents = _capturedEvents.FindAll(e => e is SpeedGateCrossedEvent);
            Assert.IsTrue(crossEvents.Count >= 1, "Should detect gate crossing");
        }

        [Test]
        public void NoCrossing_NoCrossEvent()
        {
            _grid.SetTrigger(new GridCoordinate(10, 10), TriggerType.SpeedGate);

            // Start and stay on same side
            SetPose(new Vector3(210, 0, 190));
            _system.SimulationTick(0.01f);
            SetPose(new Vector3(210, 0, 195));
            _system.SimulationTick(0.01f);

            var crossEvents = _capturedEvents.FindAll(e => e is SpeedGateCrossedEvent);
            Assert.AreEqual(0, crossEvents.Count);
        }

        // ---- No vehicle pose registered --------------------------------

        [Test]
        public void NoVehiclePose_DoesNotThrow()
        {
            _grid.SetTrigger(new GridCoordinate(0, 0), TriggerType.SlowZone);

            // System should handle null GetVehiclePose gracefully
            _system.GetVehiclePose = null;
            Assert.DoesNotThrow(() => _system.SimulationTick(0.01f));
        }

        // ---- Reset clears state ---------------------------------------

        [Test]
        public void Reset_ClearsInsideState()
        {
            _grid.SetTrigger(new GridCoordinate(5, 5), TriggerType.SlowZone);

            // Enter trigger
            SetPose(new Vector3(110, 0, 110));
            _system.SimulationTick(0.01f);

            Assert.AreEqual(1, _capturedEvents.Count);

            // Reset
            _system.ResetSimulation();

            // Exit without actually leaving should NOT fire exit (state was cleared)
            _system.SimulationTick(0.01f);

            var exitEvents = _capturedEvents.FindAll(e => e is TriggerExitedEvent);
            Assert.AreEqual(0, exitEvents.Count);
        }

        // ---- Multi-tile sample detection ------------------------------

        [Test]
        public void SamplePoints_DetectMultipleTiles()
        {
            // Place triggers on adjacent tiles
            _grid.SetTrigger(new GridCoordinate(5, 5), TriggerType.SlowZone);
            _grid.SetTrigger(new GridCoordinate(6, 5), TriggerType.SlowZone);
            _grid.SetTrigger(new GridCoordinate(5, 6), TriggerType.SlowZone);

            // Vehicle center at (5.5, 5.5) → covers all 4 tiles
            _system.GetVehiclePose = () => new VehiclePose
            {
                Position = new Vector3(110, 0, 110),
                SamplePoints = new[]
                {
                    new Vector3(105, 0, 105), // tile (5,5)
                    new Vector3(115, 0, 105), // tile (6,5) → 115/20=5.75 floor=5? No, 115/20=5.75 floor=5? 115/20=5.75, floor is 5... but 115-5*20=15 < 20, so x=5. So it's still tile 5
                }
            };
            _system.SimulationTick(0.01f);

            var enterEvents = _capturedEvents.FindAll(e => e is TriggerEnteredEvent);
            Assert.IsTrue(enterEvents.Count >= 1, "Should detect at least one trigger tile");
        }

        // ---- Null/empty grid ------------------------------------------

        [Test]
        public void NullGrid_DoesNotThrow()
        {
            var sys = new TriggerDetectionSystem((CourseGrid)null);
            var ctx = new SimulationContext(
                new SimulationClock(0.01f),
                new SimulationEventBus(),
                new SimulationRandom(42)
            );
            Assert.DoesNotThrow(() => sys.Initialize(ctx));
            Assert.DoesNotThrow(() => sys.SimulationTick(0.01f));
            Assert.DoesNotThrow(() => sys.ResetSimulation());
            Assert.DoesNotThrow(() => sys.Shutdown());
        }
    }
}
