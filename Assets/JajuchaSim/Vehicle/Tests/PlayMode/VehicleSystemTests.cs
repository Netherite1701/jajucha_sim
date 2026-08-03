using System.Collections;
using System.Collections.Generic;
using JajuchaSim.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JajuchaSim.Vehicle.Tests
{
    /// <summary>
    /// PlayMode integration tests for <see cref="VehicleSystem"/> with
    /// real Unity physics (WheelCollider, Rigidbody).
    /// </summary>
    public class VehicleSystemTests
    {
        private const float FixedDeltaTime = 0.01f;

        private GameObject _go;
        private SimulationManager _manager;
        private GameObject _ground;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("SimManager_VehicleTest");
            _manager = _go.AddComponent<SimulationManager>();
            var cfg = ScriptableObject.CreateInstance<SimulationConfig>();
            cfg.fixedDeltaTime = FixedDeltaTime;
            cfg.defaultTimeScale = 1f;
            cfg.randomSeed = 12345L;
            cfg.maxTicksPerFrame = 100;
            cfg.autoStart = false;
            _manager.SetConfigForTesting(cfg);
            _manager.Initialize();

            CreateGround();
        }

        private readonly List<GameObject> _vehicleRoots = new List<GameObject>();

        private void CreateGround()
        {
            // Use a Plane primitive — its MeshCollider is compatible with
            // WheelCollider raycasting (WheelColliders only detect MeshColliders,
            // TerrainColliders, and other WheelColliders; NOT BoxColliders).
            _ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _ground.name = "Ground";
            _ground.transform.position = new Vector3(0f, 0f, 0f);
            _ground.transform.localScale = new Vector3(100f, 1f, 100f);
            var rb = _ground.GetComponent<Rigidbody>();
            if (rb != null) Object.DestroyImmediate(rb);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var v in _vehicleRoots)
            {
                if (v != null)
                    Object.DestroyImmediate(v);
            }
            _vehicleRoots.Clear();

            if (_ground != null)
                Object.DestroyImmediate(_ground);
            _ground = null;

            if (_go != null)
                Object.DestroyImmediate(_go);
            _go = null;
        }

        private VehicleSystem CreateVehicle()
        {
            var vehConfig = ScriptableObject.CreateInstance<VehicleConfig>();
            var vehicle = new VehicleSystem(vehConfig);
            _vehicleRoots.Add(vehicle.VehicleRoot);
            _manager.RegisterSystem(vehicle);
            Assert.IsNotNull(vehicle.ChassisRigidbody,
                "Vehicle should have a Rigidbody after initialization.");
            Assert.IsNotNull(vehicle.Steering,
                "Steering model should be initialized.");
            Assert.IsNotNull(vehicle.RearDrive,
                "RearDrive model should be initialized.");
            return vehicle;
        }

        [UnityTest]
        public IEnumerator Default_Command_Zero_No_Movement()
        {
            var vehicle = CreateVehicle();
            _manager.StartSimulation();
            _manager.Pause();

            _manager.Advance(100);

            Vector3 pos = vehicle.ChassisRigidbody.position;
            float displacement = new Vector3(pos.x, 0f, pos.z).magnitude;
            Assert.LessOrEqual(displacement, 0.5f,
                "Vehicle should not move with zero command.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Positive_Speed_Moves_Forward()
        {
            var vehicle = CreateVehicle();
            _manager.StartSimulation();
            _manager.Pause();

            vehicle.SetMotorCommand(new MotorCommand(0, 0, 10));
            _manager.Advance(100);

            Vector3 pos = vehicle.ChassisRigidbody.position;
            Assert.Greater(pos.z, 0f,
                "Vehicle should move forward (positive Z) with positive speed command.");
            Assert.Less(pos.z, 80f,
                "Forward displacement should be reasonable for 1s at speed 10.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Negative_Speed_Moves_Backward()
        {
            var vehicle = CreateVehicle();
            _manager.StartSimulation();
            _manager.Pause();

            vehicle.SetMotorCommand(new MotorCommand(0, 0, -10));
            _manager.Advance(100);

            Vector3 pos = vehicle.ChassisRigidbody.position;
            Assert.Less(pos.z, 0f,
                "Vehicle should move backward (negative Z) with negative speed command.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Steering_Affects_Heading()
        {
            var vehicle = CreateVehicle();
            _manager.StartSimulation();
            _manager.Pause();

            vehicle.SetMotorCommand(new MotorCommand(-10, -10, 10));
            _manager.Advance(200);

            Quaternion rot = vehicle.ChassisRigidbody.rotation;
            Vector3 forward = rot * Vector3.forward;

            Assert.That(Mathf.Abs(forward.x), Is.GreaterThan(0.01f),
                "Steering should cause heading change.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetMotorCommand_Updates_Command()
        {
            var vehicle = CreateVehicle();
            var cmd = new MotorCommand(5, -3, 10);
            vehicle.SetMotorCommand(cmd);
            Assert.AreEqual(cmd, vehicle.CurrentCommand);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Reset_Stops_Vehicle()
        {
            var vehicle = CreateVehicle();
            _manager.StartSimulation();
            _manager.Pause();

            vehicle.SetMotorCommand(new MotorCommand(0, 0, 10));
            _manager.Advance(50);
            Assert.Greater(vehicle.ChassisRigidbody.position.z, 0f);

            vehicle.ResetSimulation();
            Assert.AreEqual(MotorCommand.Zero, vehicle.CurrentCommand);
            Assert.AreEqual(new Vector3(0f, 3.1f, 0f),
                vehicle.ChassisRigidbody.position);
            Assert.AreEqual(Vector3.zero, vehicle.ChassisRigidbody.linearVelocity);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DriveForce_Mirrors_RearDriveModel()
        {
            var vehicle = CreateVehicle();
            _manager.StartSimulation();
            _manager.Pause();

            vehicle.SetMotorCommand(new MotorCommand(0, 0, 0));
            _manager.Advance(1);
            Assert.AreEqual(0f, vehicle.RearDrive.DriveForce);

            vehicle.SetMotorCommand(new MotorCommand(0, 0, 10));
            _manager.Advance(1);
            Assert.Greater(vehicle.RearDrive.DriveForce, 0f);

            vehicle.SetMotorCommand(new MotorCommand(0, 0, -10));
            _manager.Advance(1);
            Assert.Less(vehicle.RearDrive.DriveForce, 0f);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ZeroSpeed_WithVariousSteering_NoPropulsion()
        {
            var vehicle = CreateVehicle();
            _manager.StartSimulation();
            _manager.Pause();

            int[][] cases = new[]
            {
                new[] { -10, -10 },
                new[] { -10,   0 },
                new[] { -10,  10 },
                new[] {   0,  10 },
                new[] {  10,  10 },
            };

            foreach (var c in cases)
            {
                int left = c[0];
                int right = c[1];
                vehicle.SetMotorCommand(new MotorCommand(left, right, 0));
                _manager.Advance(100);

                Assert.AreEqual(0f, vehicle.RearDrive.DriveForce,
                    $"DriveForce must be 0 for ({left}, {right}, 0)");

                vehicle.ResetSimulation();
                _manager.Advance(10);
            }

            yield return null;
        }
    }
}
