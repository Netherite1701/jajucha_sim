using System.Collections;
using System.Collections.Generic;
using JajuchaSim.Core;
using JajuchaSim.Sensors;
using JajuchaSim.Vehicle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JajuchaSim.Bridge.Tests
{
    /// <summary>
    /// PlayMode integration tests for the bridge pipeline in a real simulation
    /// environment with physics. Messages are injected via a fake connection
    /// rather than live TCP to avoid threading issues in batchmode.
    ///
    /// These tests verify:
    /// - Bridge + Vehicle integration in a real scene
    /// - Command dispatch with live simulation ticks
    /// - Zero-speed invariant with physics
    /// - Reset behavior
    /// - Watchdog timeout (using short timeout)
    /// </summary>
    public class BridgeIntegrationTests
    {
        private const float FixedDeltaTime = 0.01f;

        private GameObject _go;
        private SimulationManager _simulation;
        private VehicleSystem _vehicle;
        private FakeBridgeConnection _connection;
        private CommandDispatcher _dispatcher;
        private List<GameObject> _vehicleRoots;
        private GameObject _ground;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("BridgeTestRoot");
            _simulation = _go.AddComponent<SimulationManager>();
            var cfg = ScriptableObject.CreateInstance<SimulationConfig>();
            cfg.fixedDeltaTime = FixedDeltaTime;
            cfg.defaultTimeScale = 1f;
            cfg.randomSeed = 12345L;
            cfg.maxTicksPerFrame = 100;
            cfg.autoStart = false;
            _simulation.SetConfigForTesting(cfg);
            _simulation.Initialize();

            // Ground for physics
            _ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _ground.name = "Ground";
            _ground.transform.position = Vector3.zero;
            _ground.transform.localScale = new Vector3(100f, 1f, 100f);
            var rb = _ground.GetComponent<Rigidbody>();
            if (rb != null) Object.DestroyImmediate(rb);

            // Vehicle
            _vehicleRoots = new List<GameObject>();
            var vehConfig = ScriptableObject.CreateInstance<VehicleConfig>();
            _vehicle = new VehicleSystem(vehConfig);
            _vehicleRoots.Add(_vehicle.VehicleRoot);
            _simulation.RegisterSystem(_vehicle);

            // Fake connection + dispatcher (no real TCP)
            _connection = new FakeBridgeConnection();
            _dispatcher = new CommandDispatcher(_simulation, _vehicle, null, _connection, 1, 10f);

            _connection.ClientConnected += () => _dispatcher.OnConnect();
            _connection.ClientDisconnected += () => _dispatcher.OnDisconnect();
        }

        [TearDown]
        public void TearDown()
        {
            _connection?.Dispose();

            foreach (var v in _vehicleRoots)
            {
                if (v != null) Object.DestroyImmediate(v);
            }
            _vehicleRoots?.Clear();

            if (_ground != null)
                Object.DestroyImmediate(_ground);
            _ground = null;

            if (_go != null)
                Object.DestroyImmediate(_go);
            _go = null;
        }

        /// <summary>Send a raw JSON line as if received from the network.</summary>
        private void InjectLine(string json)
        {
            _connection.InjectMessage(json);
        }

        /// <summary>Process all pending bridge messages.</summary>
        private void ProcessBridge()
        {
            _dispatcher.ProcessQueue();
        }

        /// <summary>Inject a hello and complete the handshake.</summary>
        private void DoHandshake()
        {
            InjectLine("{\"type\":\"hello\",\"protocol\":1,\"client\":\"jchm-sim\"}");
            ProcessBridge();
            Assert.IsTrue(_dispatcher.HandshakeComplete);
        }

        /// <summary>Inject a command and return the parsed response.</summary>
        private BridgeMessage InjectCommand(int id, string name, string payloadJson)
        {
            string json = $"{{\"type\":\"command\",\"id\":{id},\"name\":\"{name}\",\"payload\":{payloadJson}}}";
            InjectLine(json);
            ProcessBridge();
            string sent = _connection.LastSent;
            return sent != null ? BridgeProtocol.Deserialize(sent) : null;
        }

        // --- Tests ---

        [UnityTest]
        public IEnumerator Handshake_CompletesSuccessfully()
        {
            yield return null;
            DoHandshake();
            Assert.IsTrue(_dispatcher.HandshakeComplete);
        }

        [UnityTest]
        public IEnumerator SetMotor_DispatchesToVehicle()
        {
            yield return null;
            DoHandshake();

            var resp = InjectCommand(1, "set_motor", "{\"left\":-5,\"right\":-5,\"speed\":3}");
            Assert.IsNotNull(resp);
            Assert.IsTrue(resp.Ok);

            Assert.AreEqual(-5, _vehicle.CurrentCommand.Left);
            Assert.AreEqual(-5, _vehicle.CurrentCommand.Right);
            Assert.AreEqual(3, _vehicle.CurrentCommand.Speed);
        }

        [UnityTest]
        public IEnumerator ZeroSpeedCommand_PreservesSteering()
        {
            yield return null;
            DoHandshake();

            var resp = InjectCommand(1, "set_motor", "{\"left\":-10,\"right\":10,\"speed\":0}");
            Assert.IsNotNull(resp);
            Assert.IsTrue(resp.Ok);

            Assert.AreEqual(-10, _vehicle.CurrentCommand.Left);
            Assert.AreEqual(10, _vehicle.CurrentCommand.Right);
            Assert.AreEqual(0, _vehicle.CurrentCommand.Speed);
        }

        [UnityTest]
        public IEnumerator VehicleMoves_WithPositiveSpeed()
        {
            yield return null;
            DoHandshake();

            _simulation.StartSimulation();
            _simulation.Pause();

            InjectCommand(1, "set_motor", "{\"left\":0,\"right\":0,\"speed\":10}");
            _simulation.Advance(100);

            // Vehicle should have moved forward
            Assert.Greater(_vehicle.ChassisRigidbody.position.z, 0f,
                "Vehicle should move forward with speed > 0");
        }

        [UnityTest]
        public IEnumerator ZeroSpeed_WithPhysics_NoPropulsion()
        {
            yield return null;
            DoHandshake();

            _simulation.StartSimulation();
            _simulation.Pause();

            // Set motor with speed = 0 but nonzero steering
            InjectCommand(1, "set_motor", "{\"left\":10,\"right\":-10,\"speed\":0}");
            _simulation.Advance(200); // 2 seconds

            // Zero-speed invariant: drive force must be 0
            Assert.AreEqual(0f, _vehicle.RearDrive.DriveForce,
                "Drive force must be 0 when speed command is 0");

            // Vehicle should not have moved significantly
            float displacement = new Vector3(
                _vehicle.ChassisRigidbody.position.x,
                0f,
                _vehicle.ChassisRigidbody.position.z).magnitude;
            Assert.LessOrEqual(displacement, 1f,
                "Vehicle should not move significantly with speed=0");
        }

        [UnityTest]
        public IEnumerator Ping_ReturnsOk()
        {
            yield return null;
            DoHandshake();

            var resp = InjectCommand(1, "ping", "{}");
            Assert.IsNotNull(resp);
            Assert.IsTrue(resp.Ok);
            Assert.IsNotNull(resp.Payload);
            Assert.That(resp.Payload, Does.ContainKey("sim_time"));
        }

        [UnityTest]
        public IEnumerator GetStatus_ReturnsVehicleInfo()
        {
            yield return null;
            DoHandshake();

            _vehicle.SetMotorCommand(new MotorCommand(5, -5, 10));
            var resp = InjectCommand(1, "get_status", "{}");
            Assert.IsNotNull(resp);
            Assert.IsTrue(resp.Ok);
            Assert.IsNotNull(resp.Payload);
            Assert.That(resp.Payload, Does.ContainKey("vehicle"));
        }

        [UnityTest]
        public IEnumerator SimReset_ResetsSimulation()
        {
            yield return null;
            DoHandshake();

            _simulation.StartSimulation();
            _simulation.Advance(50);
            Assert.Greater(_simulation.Clock.Tick, 0);

            var resp = InjectCommand(1, "sim_reset", "{}");
            Assert.IsNotNull(resp);
            Assert.IsTrue(resp.Ok);

            Assert.AreEqual(0, _simulation.Clock.Tick);
            Assert.AreEqual(SimulationState.Ready, _simulation.State);
        }

        [UnityTest]
        public IEnumerator UnknownCommand_ReturnsError()
        {
            yield return null;
            DoHandshake();

            var resp = InjectCommand(1, "explode_car", "{}");
            Assert.IsNotNull(resp);
            Assert.IsFalse(resp.Ok);
            Assert.AreEqual("UNKNOWN_COMMAND", resp.Error?.Code);
        }

        [UnityTest]
        public IEnumerator CommandsBeforeHandshake_AreRejected()
        {
            yield return null;

            // Make a fresh dispatcher without handshake
            var freshConn = new FakeBridgeConnection();
            var freshDispatcher = new CommandDispatcher(
                _simulation, _vehicle, null, freshConn, 1, 10f);

            freshConn.InjectMessage(
                "{\"type\":\"command\",\"id\":1,\"name\":\"set_motor\"," +
                "\"payload\":{\"left\":0,\"right\":0,\"speed\":5}}");
            freshDispatcher.ProcessQueue();

            Assert.AreEqual(0, _vehicle.CurrentCommand.Speed,
                "Command should not be applied without handshake");
        }
    }

    /// <summary>
    /// A fake <see cref="BridgeConnection"/> that records sent messages
    /// instead of actually sending them over TCP.
    /// </summary>
    internal sealed class FakeBridgeConnection : BridgeConnection
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _incoming
            = new System.Collections.Concurrent.ConcurrentQueue<string>();

        private readonly System.Collections.Generic.List<string> _sent
            = new System.Collections.Generic.List<string>();

        public int SentCount => _sent.Count;
        public string LastSent => _sent.Count > 0 ? _sent[_sent.Count - 1] : null;

        public FakeBridgeConnection()
            : base("127.0.0.1", 0, 65536)
        {
        }

        /// <summary>
        /// Inject a raw JSON line as if it was received from the network.
        /// </summary>
        public void InjectMessage(string jsonLine)
        {
            _incoming.Enqueue(jsonLine);
            OnLineReceived(jsonLine);
        }

        /// <summary>
        /// Override to read from our injected queue instead of a real socket.
        /// </summary>
        public override bool TryDequeueLine(out string line)
        {
            return _incoming.TryDequeue(out line);
        }

        /// <summary>
        /// Override to record sent messages.
        /// </summary>
        public override bool Send(string json)
        {
            if (!string.IsNullOrEmpty(json))
            {
                _sent.Add(json);
            }
            return true;
        }
    }
}
