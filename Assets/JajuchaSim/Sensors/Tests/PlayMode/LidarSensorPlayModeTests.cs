using System.Collections;
using JajuchaSim.Core;
using JajuchaSim.Vehicle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JajuchaSim.Sensors.Tests
{
    public class LidarSensorPlayModeTests
    {
        private GameObject _managerObject;
        private GameObject _obstacle;
        private SimulationManager _manager;
        private VehicleSystem _vehicle;
        private LidarSensorSystem _lidar;

        [SetUp]
        public void SetUp()
        {
            _managerObject = new GameObject("LidarPlayModeManager");
            _manager = _managerObject.AddComponent<SimulationManager>();
            var simulationConfig = ScriptableObject.CreateInstance<SimulationConfig>();
            simulationConfig.fixedDeltaTime = 0.01f;
            simulationConfig.defaultTimeScale = 1f;
            simulationConfig.maxTicksPerFrame = 100;
            simulationConfig.autoStart = false;
            _manager.SetConfigForTesting(simulationConfig);
            _manager.Initialize();

            _vehicle = new VehicleSystem(ScriptableObject.CreateInstance<VehicleConfig>());
            _manager.RegisterSystem(_vehicle);

            var lidarConfig = ScriptableObject.CreateInstance<LidarConfig>();
            lidarConfig.rayCount = 360;
            lidarConfig.horizontalFovDeg = 360f;
            lidarConfig.mountPosition = new Vector3(0f, 6f, 10f);
            lidarConfig.scanRateHz = 20f;
            _lidar = new LidarSensorSystem(_vehicle, lidarConfig);
            _manager.RegisterSystem(_lidar);

            _obstacle = new GameObject("LidarObstacle");
            _obstacle.transform.position = new Vector3(0f, 9.1f, 50f);
            var collider = _obstacle.AddComponent<BoxCollider>();
            collider.size = new Vector3(10f, 10f, 1f);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (_obstacle != null) Object.DestroyImmediate(_obstacle);
            if (_vehicle != null && _vehicle.VehicleRoot != null) Object.DestroyImmediate(_vehicle.VehicleRoot);
            if (_managerObject != null) Object.DestroyImmediate(_managerObject);
        }

        [UnityTest]
        public IEnumerator FullCircleScan_ReportsObstacleAndManualAngles()
        {
            _manager.StartSimulation();
            _manager.Pause();
            _manager.Advance(1);

            var scan = _lidar.LatestScan;
            Assert.IsNotNull(scan);
            Assert.AreEqual(360, scan.RayCount);
            Assert.AreEqual(0f, scan.AngleMinDeg, 0.0001f);
            Assert.Greater(scan.AngleMaxDeg, 350f);
            Assert.AreEqual(1f, scan.AngleIncrementDeg, 0.0001f);
            Assert.That(scan.DistancesCm[0], Is.InRange(39f, 41f));
            Assert.That(scan.DistancesCm[90], Is.EqualTo(1000f).Within(0.01f));
            Assert.Greater(scan.FrameId, 0);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Reset_ClearsScanAndNextTickRecreatesIt()
        {
            _manager.StartSimulation();
            _manager.Pause();
            _manager.Advance(1);
            Assert.IsNotNull(_lidar.LatestScan);
            _manager.ResetSimulation();
            Assert.IsNull(_lidar.LatestScan);

            _manager.StartSimulation();
            _manager.Pause();
            _manager.Advance(1);
            Assert.IsNotNull(_lidar.LatestScan);
            Assert.That(_lidar.LatestScan.DistancesCm[0], Is.InRange(39f, 41f));
            yield return null;
        }
    }
}
