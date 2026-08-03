using System.Collections.Generic;
using JajuchaSim.Core;
using JajuchaSim.Sensors;
using JajuchaSim.Vehicle;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Bridge.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="CommandDispatcher"/> dispatch logic.
    /// 
    /// These tests verify the mapping from protocol messages to vehicle/simulation
    /// commands without requiring an actual TCP connection. We provide a fake
    /// <see cref="BridgeConnection"/> substitute (or use a real one in a controlled
    /// manner) and inspect the vehicle state after dispatch.
    /// </summary>
    public class CommandDispatcherTests
    {
        private const float FixedDeltaTime = 0.01f;

        private SimulationManager _simulation;
        private VehicleSystem _vehicle;
        private FakeBridgeConnection _connection;
        private CommandDispatcher _dispatcher;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestRoot");
            _simulation = _go.AddComponent<SimulationManager>();
            var cfg = ScriptableObject.CreateInstance<SimulationConfig>();
            cfg.fixedDeltaTime = FixedDeltaTime;
            cfg.defaultTimeScale = 1f;
            cfg.randomSeed = 12345L;
            cfg.maxTicksPerFrame = 100;
            cfg.autoStart = false;
            _simulation.SetConfigForTesting(cfg);
            _simulation.Initialize();

            var vehConfig = ScriptableObject.CreateInstance<VehicleConfig>();
            _vehicle = new VehicleSystem(vehConfig);
            _simulation.RegisterSystem(_vehicle);

            _connection = new FakeBridgeConnection();
            _dispatcher = new CommandDispatcher(_simulation, _vehicle, null, _connection, 1, 1f);

            // Complete handshake by sending a hello
            var helloMsg = new BridgeMessage
            {
                Type = "hello",
                Protocol = 1,
                Client = "jchm-sim"
            };
            _connection.InjectMessage(BridgeProtocol.Serialize(helloMsg));
            _dispatcher.ProcessQueue();
            Assert.IsTrue(_dispatcher.HandshakeComplete);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
            _connection?.Dispose();
        }

        // --- Ping ---

        [Test]
        public void Ping_ReturnsOk()
        {
            var cmd = new BridgeMessage
            {
                Type = "command",
                Id = 1,
                Name = "ping",
                Payload = new Dictionary<string, object>()
            };
            _connection.InjectMessage(BridgeProtocol.Serialize(cmd));
            _dispatcher.ProcessQueue();

            Assert.That(_connection.SentCount, Is.GreaterThanOrEqualTo(1));
            string sentJson = _connection.LastSent;
            var response = BridgeProtocol.Deserialize(sentJson);
            Assert.IsNotNull(response);
            Assert.AreEqual("response", response.Type);
            Assert.AreEqual(1, response.Id);
            Assert.IsTrue(response.Ok);
        }

        // --- SetMotor dispatch ---

        [Test]
        public void SetMotor_DispatchesToVehicle()
        {
            var cmd = new BridgeMessage
            {
                Type = "command",
                Id = 10,
                Name = "set_motor",
                Payload = new Dictionary<string, object>
                {
                    ["left"] = -5,
                    ["right"] = -5,
                    ["speed"] = 3
                }
            };
            _connection.InjectMessage(BridgeProtocol.Serialize(cmd));
            _dispatcher.ProcessQueue();

            Assert.AreEqual(-5, _vehicle.CurrentCommand.Left);
            Assert.AreEqual(-5, _vehicle.CurrentCommand.Right);
            Assert.AreEqual(3, _vehicle.CurrentCommand.Speed);
        }

        [Test]
        public void SetMotor_ZeroSpeedInvariant_SteeringPreserved()
        {
            var cmd = new BridgeMessage
            {
                Type = "command",
                Id = 11,
                Name = "set_motor",
                Payload = new Dictionary<string, object>
                {
                    ["left"] = -10,
                    ["right"] = 10,
                    ["speed"] = 0
                }
            };
            _connection.InjectMessage(BridgeProtocol.Serialize(cmd));
            _dispatcher.ProcessQueue();

            Assert.AreEqual(-10, _vehicle.CurrentCommand.Left);
            Assert.AreEqual(10, _vehicle.CurrentCommand.Right);
            Assert.AreEqual(0, _vehicle.CurrentCommand.Speed);
        }

        [Test]
        public void SetMotor_MissingPayload_ReturnsError()
        {
            var cmd = new BridgeMessage
            {
                Type = "command",
                Id = 12,
                Name = "set_motor",
                Payload = null
            };
            _connection.InjectMessage(BridgeProtocol.Serialize(cmd));
            _dispatcher.ProcessQueue();

            string sentJson = _connection.LastSent;
            var response = BridgeProtocol.Deserialize(sentJson);
            Assert.IsNotNull(response);
            Assert.IsFalse(response.Ok);
            Assert.AreEqual("INVALID_ARGUMENT", response.Error?.Code);
        }

        [Test]
        public void SetMotor_ClampsValues()
        {
            // Values outside valid range should be clamped by MotorCommand
            var cmd = new BridgeMessage
            {
                Type = "command",
                Id = 13,
                Name = "set_motor",
                Payload = new Dictionary<string, object>
                {
                    ["left"] = -50,
                    ["right"] = 50,
                    ["speed"] = 100
                }
            };
            _connection.InjectMessage(BridgeProtocol.Serialize(cmd));
            _dispatcher.ProcessQueue();

            Assert.AreEqual(-10, _vehicle.CurrentCommand.Left);
            Assert.AreEqual(10, _vehicle.CurrentCommand.Right);
            Assert.AreEqual(30, _vehicle.CurrentCommand.Speed);
        }

        // --- Latest-wins semantic via queue ---

        [Test]
        public void SetMotor_LatestWins_OnlyLastApplied()
        {
            _connection.InjectMessage(BridgeProtocol.Serialize(new BridgeMessage
            {
                Type = "command", Id = 1, Name = "set_motor",
                Payload = new Dictionary<string, object> { ["left"] = 0, ["right"] = 0, ["speed"] = 1 }
            }));
            _connection.InjectMessage(BridgeProtocol.Serialize(new BridgeMessage
            {
                Type = "command", Id = 2, Name = "set_motor",
                Payload = new Dictionary<string, object> { ["left"] = 0, ["right"] = 0, ["speed"] = 2 }
            }));
            _connection.InjectMessage(BridgeProtocol.Serialize(new BridgeMessage
            {
                Type = "command", Id = 3, Name = "set_motor",
                Payload = new Dictionary<string, object> { ["left"] = 0, ["right"] = 0, ["speed"] = 3 }
            }));

            _dispatcher.ProcessQueue();

            // All three are applied in order, but since there's no tick in between,
            // the VehicleSystem applies the last one as the current command.
            Assert.AreEqual(3, _vehicle.CurrentCommand.Speed);
        }

        // --- Unknown command ---

        [Test]
        public void UnknownCommand_ReturnsError()
        {
            var cmd = new BridgeMessage
            {
                Type = "command",
                Id = 99,
                Name = "explode_car",
                Payload = new Dictionary<string, object>()
            };
            _connection.InjectMessage(BridgeProtocol.Serialize(cmd));
            _dispatcher.ProcessQueue();

            string sentJson = _connection.LastSent;
            var response = BridgeProtocol.Deserialize(sentJson);
            Assert.IsNotNull(response);
            Assert.IsFalse(response.Ok);
            Assert.AreEqual("UNKNOWN_COMMAND", response.Error?.Code);
        }

        // --- Invalid message ---

        [Test]
        public void InvalidJson_ReturnsError()
        {
            _connection.InjectMessage("{\"type\":\"command\",");
            _dispatcher.ProcessQueue();

            string sentJson = _connection.LastSent;
            var response = BridgeProtocol.Deserialize(sentJson);
            Assert.IsNotNull(response);
            Assert.IsFalse(response.Ok);
            Assert.AreEqual("INVALID_MESSAGE", response.Error?.Code);
        }

        // --- get_status ---

        [Test]
        public void GetStatus_ReturnsVehicleCommand()
        {
            // Set a motor command first
            _vehicle.SetMotorCommand(new MotorCommand(3, -3, 10));

            var cmd = new BridgeMessage
            {
                Type = "command",
                Id = 20,
                Name = "get_status",
                Payload = new Dictionary<string, object>()
            };
            _connection.InjectMessage(BridgeProtocol.Serialize(cmd));
            _dispatcher.ProcessQueue();

            string sentJson = _connection.LastSent;
            var response = BridgeProtocol.Deserialize(sentJson);
            Assert.IsNotNull(response);
            Assert.IsTrue(response.Ok);
            Assert.IsNotNull(response.Payload);
            Assert.That(response.Payload, Does.ContainKey("vehicle"));
        }

        // --- sim_start ---

        [Test]
        public void SimStart_StartsSimulation()
        {
            var cmd = new BridgeMessage
            {
                Type = "command",
                Id = 30,
                Name = "sim_start",
                Payload = new Dictionary<string, object>()
            };
            _connection.InjectMessage(BridgeProtocol.Serialize(cmd));
            _dispatcher.ProcessQueue();

            Assert.AreEqual(SimulationState.Running, _simulation.State);
            string sentJson = _connection.LastSent;
            var response = BridgeProtocol.Deserialize(sentJson);
            Assert.IsTrue(response.Ok);
        }

        // --- sim_pause ---

        [Test]
        public void SimPause_PausesSimulation()
        {
            _simulation.StartSimulation();

            var cmd = new BridgeMessage
            {
                Type = "command",
                Id = 31,
                Name = "sim_pause",
                Payload = new Dictionary<string, object>()
            };
            _connection.InjectMessage(BridgeProtocol.Serialize(cmd));
            _dispatcher.ProcessQueue();

            Assert.AreEqual(SimulationState.Paused, _simulation.State);
        }

        // --- sim_step ---

        [Test]
        public void SimStep_AdvancesOneTick()
        {
            _simulation.StartSimulation();
            _simulation.Pause();
            long beforeTick = _simulation.Clock.Tick;

            var cmd = new BridgeMessage
            {
                Type = "command",
                Id = 32,
                Name = "sim_step",
                Payload = new Dictionary<string, object>()
            };
            _connection.InjectMessage(BridgeProtocol.Serialize(cmd));
            _dispatcher.ProcessQueue();

            Assert.AreEqual(beforeTick + 1, _simulation.Clock.Tick);
        }

        // --- sim_reset ---

        [Test]
        public void SimReset_ResetsSimulation()
        {
            _simulation.StartSimulation();
            _simulation.Advance(50);
            Assert.Greater(_simulation.Clock.Tick, 0);

            var cmd = new BridgeMessage
            {
                Type = "command",
                Id = 33,
                Name = "sim_reset",
                Payload = new Dictionary<string, object>()
            };
            _connection.InjectMessage(BridgeProtocol.Serialize(cmd));
            _dispatcher.ProcessQueue();

            Assert.AreEqual(0, _simulation.Clock.Tick);
            Assert.AreEqual(SimulationState.Ready, _simulation.State);
        }

        // --- No handshake ---

        [Test]
        public void CommandsBeforeHandshake_AreRejected()
        {
            var freshDispatcher = new CommandDispatcher(_simulation, _vehicle, null, new FakeBridgeConnection(), 1, 1f);

            var cmd = new BridgeMessage
            {
                Type = "command",
                Id = 1,
                Name = "set_motor",
                Payload = new Dictionary<string, object> { ["left"] = 0, ["right"] = 0, ["speed"] = 5 }
            };
            var fakeConn = new FakeBridgeConnection();
            freshDispatcher = new CommandDispatcher(_simulation, _vehicle, null, fakeConn, 1, 1f);
            fakeConn.InjectMessage(BridgeProtocol.Serialize(cmd));
            freshDispatcher.ProcessQueue();

            // Should not be applied without handshake
            Assert.AreEqual(0, _vehicle.CurrentCommand.Speed);
        }

        // --- Disconnect safety (3.53) ---

        [Test]
        public void Disconnect_SetsSpeedToZero_PreservesSteering()
        {
            // Set a motor command with speed and steering
            var cmd = new BridgeMessage
            {
                Type = "command",
                Id = 1,
                Name = "set_motor",
                Payload = new Dictionary<string, object>
                {
                    ["left"] = -5,
                    ["right"] = 5,
                    ["speed"] = 10
                }
            };
            _connection.InjectMessage(BridgeProtocol.Serialize(cmd));
            _dispatcher.ProcessQueue();

            Assert.AreEqual(10, _vehicle.CurrentCommand.Speed);
            Assert.AreEqual(-5, _vehicle.CurrentCommand.Left);
            Assert.AreEqual(5, _vehicle.CurrentCommand.Right);

            // Simulate disconnect
            _dispatcher.OnDisconnect();

            // Speed should be 0, steering preserved
            Assert.AreEqual(0, _vehicle.CurrentCommand.Speed);
            Assert.AreEqual(-5, _vehicle.CurrentCommand.Left);
            Assert.AreEqual(5, _vehicle.CurrentCommand.Right);
        }

        [Test]
        public void Disconnect_WhenSpeedAlreadyZero_NoChange()
        {
            // Set motor with speed=0 but steering
            var cmd = new BridgeMessage
            {
                Type = "command",
                Id = 1,
                Name = "set_motor",
                Payload = new Dictionary<string, object>
                {
                    ["left"] = -10,
                    ["right"] = 10,
                    ["speed"] = 0
                }
            };
            _connection.InjectMessage(BridgeProtocol.Serialize(cmd));
            _dispatcher.ProcessQueue();

            Assert.AreEqual(0, _vehicle.CurrentCommand.Speed);

            // Disconnect
            _dispatcher.OnDisconnect();

            // Should remain unchanged
            Assert.AreEqual(0, _vehicle.CurrentCommand.Speed);
            Assert.AreEqual(-10, _vehicle.CurrentCommand.Left);
            Assert.AreEqual(10, _vehicle.CurrentCommand.Right);
        }

        [Test]
        public void Reconnect_AfterDisconnect_ResetsHandshake()
        {
            // First connection completes handshake
            Assert.IsTrue(_dispatcher.HandshakeComplete);

            // Disconnect
            _dispatcher.OnDisconnect();
            Assert.IsFalse(_dispatcher.HandshakeComplete);

            // New connection sends hello
            var helloMsg = new BridgeMessage
            {
                Type = "hello",
                Protocol = 1,
                Client = "jchm-sim"
            };
            _connection.InjectMessage(BridgeProtocol.Serialize(helloMsg));
            _dispatcher.ProcessQueue();

            // Handshake should complete again
            Assert.IsTrue(_dispatcher.HandshakeComplete);
        }

        // --- Watchdog timeout (3.52) ---

        [Test]
        public void Watchdog_Timeout_SetsSpeedToZero()
        {
            // Create dispatcher with very short timeout for testing
            var shortTimeoutDispatcher = new CommandDispatcher(
                _simulation, _vehicle, null, _connection, 1, 0.1f); // 100ms timeout

            // Complete handshake
            var helloMsg = new BridgeMessage
            {
                Type = "hello",
                Protocol = 1,
                Client = "jchm-sim"
            };
            _connection.InjectMessage(BridgeProtocol.Serialize(helloMsg));
            shortTimeoutDispatcher.ProcessQueue();
            Assert.IsTrue(shortTimeoutDispatcher.HandshakeComplete);

            // Send motor command with speed > 0
            var cmd = new BridgeMessage
            {
                Type = "command",
                Id = 1,
                Name = "set_motor",
                Payload = new Dictionary<string, object>
                {
                    ["left"] = -5,
                    ["right"] = 5,
                    ["speed"] = 10
                }
            };
            _connection.InjectMessage(BridgeProtocol.Serialize(cmd));
            shortTimeoutDispatcher.ProcessQueue();

            Assert.AreEqual(10, _vehicle.CurrentCommand.Speed);
            Assert.AreEqual(-5, _vehicle.CurrentCommand.Left);
            Assert.AreEqual(5, _vehicle.CurrentCommand.Right);

            // Wait for timeout
            System.Threading.Thread.Sleep(150); // Wait 150ms (timeout is 100ms)

            // Process queue again to trigger watchdog
            shortTimeoutDispatcher.ProcessQueue();

            // Speed should be 0, steering preserved
            Assert.AreEqual(0, _vehicle.CurrentCommand.Speed, "Watchdog should set speed to 0 after timeout");
            Assert.AreEqual(-5, _vehicle.CurrentCommand.Left, "Watchdog should preserve left steering");
            Assert.AreEqual(5, _vehicle.CurrentCommand.Right, "Watchdog should preserve right steering");
        }

        [Test]
        public void Watchdog_NoTimeout_WhenSpeedAlreadyZero()
        {
            // Create dispatcher with very short timeout
            var shortTimeoutDispatcher = new CommandDispatcher(
                _simulation, _vehicle, null, _connection, 1, 0.1f);

            // Complete handshake
            var helloMsg = new BridgeMessage
            {
                Type = "hello",
                Protocol = 1,
                Client = "jchm-sim"
            };
            _connection.InjectMessage(BridgeProtocol.Serialize(helloMsg));
            shortTimeoutDispatcher.ProcessQueue();

            // Send motor command with speed=0 but steering
            var cmd = new BridgeMessage
            {
                Type = "command",
                Id = 1,
                Name = "set_motor",
                Payload = new Dictionary<string, object>
                {
                    ["left"] = -10,
                    ["right"] = 10,
                    ["speed"] = 0
                }
            };
            _connection.InjectMessage(BridgeProtocol.Serialize(cmd));
            shortTimeoutDispatcher.ProcessQueue();

            Assert.AreEqual(0, _vehicle.CurrentCommand.Speed);

            // Wait for potential timeout
            System.Threading.Thread.Sleep(150);

            // Process queue
            shortTimeoutDispatcher.ProcessQueue();

            // Should remain unchanged (watchdog only triggers if speed != 0)
            Assert.AreEqual(0, _vehicle.CurrentCommand.Speed);
            Assert.AreEqual(-10, _vehicle.CurrentCommand.Left);
            Assert.AreEqual(10, _vehicle.CurrentCommand.Right);
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

        /// <summary>
        /// Override to record binary-sent JSON headers (ignore the binary payload).
        /// </summary>
        public override bool SendJsonWithBinary(string jsonHeader, byte[] binaryPayload)
        {
            if (!string.IsNullOrEmpty(jsonHeader))
            {
                _sent.Add(jsonHeader);
            }
            return true;
        }

    }
}
