using System.Collections;
using JajuchaSim.App;
using JajuchaSim.Bridge;
using JajuchaSim.Core;
using JajuchaSim.Course;
using JajuchaSim.MapEditor;
using JajuchaSim.Sensors;
using JajuchaSim.Vehicle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JajuchaSim.App.Tests
{
    /// <summary>
    /// End-to-end workflow integration tests (Step 11.51): bootstrap success,
    /// template course loading, vehicle spawn, bridge readiness, first camera
    /// frame, reset lifecycle, and mode transitions. Builds the authoritative
    /// wiring programmatically so each test is isolated.
    /// </summary>
    public class WorkflowIntegrationTests
    {
        private static int _portCounter;

        private GameObject _root;
        private SimulationManager _sim;
        private VehicleSystemBehaviour _vehicle;
        private CameraSensorSystemBehaviour _sensors;
        private MapEditorHud _mapEditor;
        private CourseManager _course;
        private ApplicationBootstrap _bootstrap;
        private JajuchaBridgeServer _bridge;
        private SimulationRunner _runner;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("TestRoot");

            // Simulation kernel
            var simGo = new GameObject("SimulationManager");
            simGo.transform.SetParent(_root.transform, false);
            _sim = simGo.AddComponent<SimulationManager>();
            var cfg = ScriptableObject.CreateInstance<SimulationConfig>();
            cfg.fixedDeltaTime = 0.01f;
            cfg.defaultTimeScale = 1f;
            cfg.randomSeed = 12345L;
            cfg.maxTicksPerFrame = 100;
            cfg.autoStart = false;
            _sim.SetConfigForTesting(cfg);

            // Ground (plane for WheelColliders)
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(_root.transform, false);
            ground.transform.localScale = new Vector3(100f, 1f, 100f);
            var groundRb = ground.GetComponent<Rigidbody>();
            if (groundRb != null)
                Object.Destroy(groundRb);

            // Vehicle
            var vehGo = new GameObject("JajuchaVehicle");
            vehGo.transform.SetParent(_root.transform, false);
            _vehicle = vehGo.AddComponent<VehicleSystemBehaviour>();

            // Sensors
            var sensorGo = new GameObject("SensorRuntimeRoot");
            sensorGo.transform.SetParent(_root.transform, false);
            _sensors = sensorGo.AddComponent<CameraSensorSystemBehaviour>();

            // Runtime UI (map editor)
            var editorGo = new GameObject("MapEditorUI");
            editorGo.transform.SetParent(_root.transform, false);
            _mapEditor = editorGo.AddComponent<MapEditorHud>();

            // Course root + manager
            var courseRoot = new GameObject("CourseRuntimeRoot");
            courseRoot.transform.SetParent(_root.transform, false);
            var courseGo = new GameObject("CourseManager");
            courseGo.transform.SetParent(_root.transform, false);
            _course = courseGo.AddComponent<CourseManager>();

            // Bridge with a unique port so parallel/sequential tests never
            // conflict on the default 8765 listener.
            var bridgeGo = new GameObject("JajuchaBridgeServer");
            bridgeGo.transform.SetParent(_root.transform, false);
            bridgeGo.SetActive(false);
            _bridge = bridgeGo.AddComponent<JajuchaBridgeServer>();
            var bridgeCfg = ScriptableObject.CreateInstance<BridgeConfig>();
            bridgeCfg.host = "127.0.0.1";
            bridgeCfg.port = (ushort)(18000 + (++_portCounter % 4000));
            bridgeCfg.autoStart = true;
            _bridge.SetBridgeConfig(bridgeCfg);
            bridgeGo.SetActive(true);

            // Runner
            var runnerGo = new GameObject("SimulationRunner");
            runnerGo.transform.SetParent(_root.transform, false);
            _runner = runnerGo.AddComponent<SimulationRunner>();

            // Observer camera
            var camGo = new GameObject("ObserverCamera");
            camGo.transform.SetParent(_root.transform, false);
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();

            // Initialize the kernel and register vehicle + sensors so the
            // bootstrap's ordered steps find them ready (mirrors the scene's
            // SimulationManager.simulationSystemBehaviours wiring).
            _sim.Initialize();
            _sim.RegisterSystem(_vehicle);
            _sim.RegisterSystem(_sensors);

            yield return null; // let Awake/Start of map editor run
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_root);
            yield return null;
        }

        private ApplicationBootstrap CreateBootstrap()
        {
            var go = new GameObject("ApplicationBootstrap");
            go.transform.SetParent(_root.transform, false);
            var b = go.AddComponent<ApplicationBootstrap>();
            // Disable auto-Start so the test drives RunBootstrap() explicitly.
            b.enabled = false;
            return b;
        }

        [UnityTest]
        public IEnumerator Bootstrap_Succeeds_AndCourseLoads()
        {
            _bootstrap = CreateBootstrap();
            var result = _bootstrap.RunBootstrap();
            yield return null;

            Assert.IsTrue(result.Success, result.FormatDisplay());
            Assert.IsNotNull(_bootstrap.Course);
            Assert.IsNotNull(_bootstrap.Course.Document);
            Assert.Greater(_bootstrap.Course.Document.Grid.RoadTileCount, 0,
                "template course road must be loaded");
        }

        [UnityTest]
        public IEnumerator Bootstrap_MissingCourse_FailsReadably()
        {
            _bootstrap = CreateBootstrap();
            var result = _bootstrap.LoadCourse("no_such_course_abc");
            yield return null;

            Assert.IsFalse(result.Success);
            Assert.AreEqual(BootstrapErrorCode.CourseNotFound, result.ErrorCode);
            Assert.AreEqual("CourseManager", result.FailedSystem);
            StringAssert.Contains("not found", result.Message);
        }

        [UnityTest]
        public IEnumerator Bootstrap_VehicleSpawned()
        {
            _bootstrap = CreateBootstrap();
            var result = _bootstrap.RunBootstrap();
            yield return null;
            Assert.IsTrue(result.Success, result.FormatDisplay());

            Assert.IsNotNull(_vehicle.VehicleSystem, "VehicleSystem must be created.");
            Assert.IsNotNull(_vehicle.VehicleRoot, "Vehicle root GameObject must exist.");
            Assert.IsNotNull(_vehicle.VehicleSystem.ChassisRigidbody,
                "Vehicle must have a Rigidbody after spawn.");
        }

        [UnityTest]
        public IEnumerator Bootstrap_BridgeReady()
        {
            _bootstrap = CreateBootstrap();
            var result = _bootstrap.RunBootstrap();
            yield return null;
            Assert.IsTrue(result.Success, result.FormatDisplay());

            Assert.IsTrue(_bridge.TryBindSystems(), "Bridge systems must be bound after bootstrap.");
            Assert.IsNotNull(_bridge.Connection, "Bridge connection must exist.");
        }

        [UnityTest]
        public IEnumerator Bootstrap_FirstCameraFrame()
        {
            _bootstrap = CreateBootstrap();
            var result = _bootstrap.RunBootstrap();
            yield return null;
            Assert.IsTrue(result.Success, result.FormatDisplay());

            Assert.IsNotNull(_sensors.SensorSystem, "Sensor system must be initialized.");
            // Run a few ticks so the initial frame capture fires.
            _sim.Advance(10);
            yield return null;

            var frame = _sensors.SensorSystem.GetLatestFrame(CameraLocation.Center);
            Assert.IsNotNull(frame, "Center camera must produce a first frame after ticks.");
            Assert.IsNotNull(_sensors.SensorSystem.CenterCamera,
                "Center camera sensor must exist.");
        }

        [UnityTest]
        public IEnumerator ResetLifecycle_ReturnsToReady()
        {
            _bootstrap = CreateBootstrap();
            var result = _bootstrap.RunBootstrap();
            yield return null;
            Assert.IsTrue(result.Success, result.FormatDisplay());

            _sim.Advance(20);
            yield return null;

            _sim.ResetSimulation();
            yield return null;

            Assert.AreEqual(SimulationState.Ready, _sim.State);
            var rb = _vehicle.VehicleSystem.ChassisRigidbody;
            Assert.IsNotNull(rb);
            Assert.Less(rb.linearVelocity.magnitude, 0.1f, "Reset must stop vehicle motion.");
        }

        [UnityTest]
        public IEnumerator ModeTransitions_AreExplicit()
        {
            _bootstrap = CreateBootstrap();
            var result = _bootstrap.RunBootstrap();
            yield return null;
            Assert.IsTrue(result.Success, result.FormatDisplay());

            // Drive mode starts the simulation.
            Assert.AreEqual(ApplicationMode.Drive, _bootstrap.Mode);
            Assert.AreEqual(SimulationState.Running, _sim.State);

            // MapEditor pauses and enters edit mode.
            _bootstrap.SetMode(ApplicationMode.MapEditor);
            yield return null;
            Assert.AreEqual(ApplicationMode.MapEditor, _bootstrap.Mode);
            Assert.AreEqual(SimulationState.Paused, _sim.State);
            Assert.AreEqual(MapEditorMode.Edit, _mapEditor.Session.Mode);

            // Back to Drive resumes.
            _bootstrap.SetMode(ApplicationMode.Drive);
            yield return null;
            Assert.AreEqual(ApplicationMode.Drive, _bootstrap.Mode);
            Assert.AreEqual(SimulationState.Running, _sim.State);
        }

        [UnityTest]
        public IEnumerator SceneValidator_PassesOnWiredScene()
        {
            _bootstrap = CreateBootstrap();
            var result = _bootstrap.RunBootstrap();
            yield return null;
            Assert.IsTrue(result.Success, result.FormatDisplay());

            var problems = SceneValidator.ValidateScene();
            Assert.IsEmpty(problems, "Wired test scene should validate: " + string.Join(" | ", problems));
        }
    }
}
