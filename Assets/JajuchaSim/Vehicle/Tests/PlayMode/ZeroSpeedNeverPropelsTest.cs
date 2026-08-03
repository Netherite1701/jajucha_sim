using System.Collections;
using System.Collections.Generic;
using JajuchaSim.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JajuchaSim.Vehicle.Tests
{
    /// <summary>
    /// Mandatory Step 2 test: verifies that when speed command is 0, the
    /// vehicle never propels itself regardless of steering angle.
    ///
    /// Acceptance criteria:
    /// - Drive force reported by RearDriveModel is exactly 0.
    /// - Horizontal displacement after 10 simulated seconds is below a small
    ///   settling tolerance (< 0.5 cm).
    /// - Forward speed after settling is approximately 0.
    ///
    /// Test cases (all with speed=0):
    ///   (-10, -10, 0)
    ///   (-10,   0, 0)
    ///   (-10,  10, 0)
    ///   (  0,  10, 0)
    ///   ( 10,  10, 0)
    /// </summary>
    public class ZeroSpeedNeverPropelsTest
    {
        private const float SimulatedSeconds = 10f;
        private const float FixedDeltaTime = 0.01f;
        private const int TotalTicks = (int)(SimulatedSeconds / FixedDeltaTime); // 1000

        // Maximum allowed horizontal displacement from starting position (cm).
        // Accounts for tiny settling from physics (suspension, gravity, etc.).
        private const float MaxDisplacementCm = 0.5f;

        // Maximum allowed forward speed after settling (cm/s).
        private const float MaxSpeedCmS = 0.1f;

        private GameObject _go;
        private SimulationManager _manager;
        private GameObject _ground;
        private readonly List<GameObject> _vehicleRoots = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("SimManager_ZeroSpeedTest");
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

        private void CreateGround()
        {
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

        /// <summary>
        /// Runs the test for a single (left, right, speed=0) combination.
        /// </summary>
        private IEnumerator RunZeroSpeedCase(int left, int right, string label)
        {
            var vehConfig = ScriptableObject.CreateInstance<VehicleConfig>();
            var vehicle = new VehicleSystem(vehConfig);
            _vehicleRoots.Add(vehicle.VehicleRoot);
            _manager.RegisterSystem(vehicle);
            _manager.StartSimulation();
            _manager.Pause();

            vehicle.SetMotorCommand(new MotorCommand(left, right, 0));

            _manager.Advance(TotalTicks);

            // --- Assertions ---

            // 1. Drive force must be exactly 0
            Assert.AreEqual(0f, vehicle.RearDrive.DriveForce,
                $"[{label}] DriveForce must be 0 when speed=0. " +
                $"Command = ({left}, {right}, 0)");

            // 2. Horizontal displacement must be tiny (just settling)
            Vector3 pos = vehicle.ChassisRigidbody.position;
            float displacement = new Vector3(pos.x, 0f, pos.z).magnitude;
            Assert.LessOrEqual(displacement, MaxDisplacementCm,
                $"[{label}] Vehicle moved {displacement:F3} cm from origin " +
                $"despite speed=0. Command = ({left}, {right}, 0). " +
                $"Position = {pos}");

            // 3. Forward speed must be approximately 0 after settling
            Vector3 velocity = vehicle.ChassisRigidbody.linearVelocity;
            float forwardSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
            Assert.LessOrEqual(forwardSpeed, MaxSpeedCmS,
                $"[{label}] Vehicle speed is {forwardSpeed:F4} cm/s " +
                $"despite speed=0. Command = ({left}, {right}, 0). " +
                $"Velocity = {velocity}");

            yield return null;
        }

        [UnityTest]
        public IEnumerator LeftNegative10_RightNegative10_Speed0_Stays_Still()
        {
            yield return RunZeroSpeedCase(-10, -10, "L-10_R-10");
        }

        [UnityTest]
        public IEnumerator LeftNegative10_Right0_Speed0_Stays_Still()
        {
            yield return RunZeroSpeedCase(-10, 0, "L-10_R0");
        }

        [UnityTest]
        public IEnumerator LeftNegative10_Right10_Speed0_Stays_Still()
        {
            yield return RunZeroSpeedCase(-10, 10, "L-10_R10");
        }

        [UnityTest]
        public IEnumerator Left0_Right10_Speed0_Stays_Still()
        {
            yield return RunZeroSpeedCase(0, 10, "L0_R10");
        }

        [UnityTest]
        public IEnumerator Left10_Right10_Speed0_Stays_Still()
        {
            yield return RunZeroSpeedCase(10, 10, "L10_R10");
        }
    }
}
