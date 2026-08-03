using System;
using System.Collections.Generic;
using JajuchaSim.Core;
using JajuchaSim.Sensors;
using JajuchaSim.Vehicle;
using UnityEngine;

namespace JajuchaSim.Bridge
{
    /// <summary>
    /// Main-thread command dispatcher that consumes <see cref="BridgeMessage"/>s
    /// from the incoming queue and routes them to the appropriate system.
    ///
    /// This class is the sole bridge component that knows about:
    ///   - VehicleSystem (set_motor)
    ///   - SimulationManager (sim_start, sim_pause, sim_step, sim_reset)
    ///   - Protocol commands and their policies
    ///
    /// It does NOT know about sockets, threads, or JSON parsing.
    /// </summary>
    public sealed class CommandDispatcher
    {
        private readonly SimulationManager _simulation;
        private readonly VehicleSystem _vehicle;
        private readonly CameraSensorSystem _sensors;
        private readonly BridgeConnection _connection;
        private readonly int _protocolVersion;
        private readonly float _commandTimeoutSec;

        private bool _handshakeComplete;
        private int _nextId;

        // Latest-wins motor tracking
        private int _lastMotorCommandId = -1;

        // Watchdog timing (wall time)
        private float _lastMotorCommandRealtime = float.PositiveInfinity;

        public CommandDispatcher(
            SimulationManager simulation,
            VehicleSystem vehicle,
            CameraSensorSystem sensors,
            BridgeConnection connection,
            int protocolVersion,
            float commandTimeoutSec)
        {
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _vehicle = vehicle ?? throw new ArgumentNullException(nameof(vehicle));
            _sensors = sensors; // May be null if sensors not yet initialized
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _protocolVersion = protocolVersion;
            _commandTimeoutSec = commandTimeoutSec > 0 ? commandTimeoutSec : 1f;
        }

        public bool HandshakeComplete => _handshakeComplete;

        /// <summary>
        /// Called once per simulation tick (or per Update) to consume pending
        /// messages and enforce the motor watchdog.
        /// </summary>
        public void ProcessQueue()
        {
            // Consume all available messages
            while (_connection.TryDequeueLine(out string line))
            {
                var msg = BridgeProtocol.Deserialize(line);
                if (msg == null)
                {
                    SendError(0, "INVALID_MESSAGE", "Failed to parse JSON");
                    continue;
                }

                ProcessMessage(msg);
            }

            // Motor watchdog: if no motor command for timeout, set speed to 0
            if (_handshakeComplete && _vehicle.CurrentCommand.Speed != 0)
            {
                if (float.IsFinite(_lastMotorCommandRealtime))
                {
                    float elapsed = Time.realtimeSinceStartup - _lastMotorCommandRealtime;
                    if (elapsed >= _commandTimeoutSec)
                    {
                        SimLog.Info("[BRIDGE] Motor watchdog: speed → 0 (no command received for " +
                                    $"{elapsed:F1}s, timeout={_commandTimeoutSec:F1}s)");
                        EnforceZeroSpeed();
                    }
                }
            }
        }

        private void ProcessMessage(BridgeMessage msg)
        {
            switch (msg.Type)
            {
                case "hello":
                    HandleHello(msg);
                    break;

                case "command":
                    if (!_handshakeComplete)
                    {
                        SendError(msg.Id, "NOT_READY", "Handshake not complete");
                        return;
                    }
                    HandleCommand(msg);
                    break;

                default:
                    SendError(msg.Id, "INVALID_MESSAGE", $"Unknown message type: {msg.Type}");
                    break;
            }
        }

        private void HandleHello(BridgeMessage msg)
        {
            if (msg.Protocol != _protocolVersion)
            {
                var errMsg = new BridgeMessage
                {
                    Type = "error",
                    Error = new BridgeErrorDetail
                    {
                        Code = "PROTOCOL_VERSION_MISMATCH",
                        Message = $"Expected protocol v{_protocolVersion}, got v{msg.Protocol}"
                    }
                };
                _connection.Send(BridgeProtocol.Serialize(errMsg));
                SimLog.Warning($"[BRIDGE] Protocol version mismatch: expected {_protocolVersion}, got {msg.Protocol}");
                return;
            }

            _handshakeComplete = true;
            _lastMotorCommandRealtime = Time.realtimeSinceStartup;

            var ack = new BridgeMessage
            {
                Type = "hello_ack",
                Protocol = _protocolVersion,
                Simulator = "JajuchaSim"
            };
            _connection.Send(BridgeProtocol.Serialize(ack));
            SimLog.Info($"[BRIDGE] Handshake complete protocol={_protocolVersion}");
        }

        private void HandleCommand(BridgeMessage msg)
        {
            switch (msg.Name)
            {
                case "ping":
                    HandlePing(msg);
                    break;

                case "set_motor":
                    HandleSetMotor(msg);
                    break;

                case "get_image":
                    HandleGetImage(msg);
                    break;

                case "get_depth":
                    HandleGetDepth(msg);
                    break;

                case "get_status":
                    HandleGetStatus(msg);
                    break;

                case "sim_start":
                    HandleSimStart(msg);
                    break;

                case "sim_pause":
                    HandleSimPause(msg);
                    break;

                case "sim_step":
                    HandleSimStep(msg);
                    break;

                case "sim_reset":
                    HandleSimReset(msg);
                    break;

                default:
                    SendError(msg.Id, "UNKNOWN_COMMAND", $"Unknown command: {msg.Name}");
                    break;
            }
        }

        private void HandlePing(BridgeMessage msg)
        {
            var response = new BridgeMessage
            {
                Type = "response",
                Id = msg.Id,
                Ok = true,
                Payload = new Dictionary<string, object>
                {
                    ["sim_time"] = _simulation.Clock.Time
                }
            };
            _connection.Send(BridgeProtocol.Serialize(response));
        }

        private void HandleSetMotor(BridgeMessage msg)
        {
            var payload = msg.Payload;
            if (payload == null)
            {
                SendError(msg.Id, "INVALID_ARGUMENT", "Missing payload");
                return;
            }

            if (!TryGetInt(payload, "left", out int left) ||
                !TryGetInt(payload, "right", out int right) ||
                !TryGetInt(payload, "speed", out int speed))
            {
                SendError(msg.Id, "INVALID_ARGUMENT",
                    "Payload must contain integer fields: left, right, speed");
                return;
            }

            // Latest-wins semantic: only apply if this command is newer
            // (we always apply since the network thread processes in order,
            // but we skip sending a response for older commands that got
            // overtaken — actually, for set_motor we apply all and respond to all)
            var cmd = new MotorCommand(left, right, speed);
            _vehicle.SetMotorCommand(cmd);

            // Update watchdog timer
            _lastMotorCommandRealtime = Time.realtimeSinceStartup;
            _lastMotorCommandId = msg.Id;

            SimLog.Info($"[BRIDGE] set_motor left={left} right={right} speed={speed}");

            // Acknowledge
            var response = new BridgeMessage
            {
                Type = "response",
                Id = msg.Id,
                Ok = true
            };
            _connection.Send(BridgeProtocol.Serialize(response));
        }

        private void HandleGetStatus(BridgeMessage msg)
        {
            var mc = _vehicle.CurrentCommand;
            var response = new BridgeMessage
            {
                Type = "response",
                Id = msg.Id,
                Ok = true,
                Payload = new Dictionary<string, object>
                {
                    ["state"] = _simulation.State.ToString(),
                    ["tick"] = _simulation.Clock.Tick,
                    ["sim_time"] = _simulation.Clock.Time,
                    ["vehicle"] = new Dictionary<string, object>
                    {
                        ["command"] = new Dictionary<string, object>
                        {
                            ["left"] = mc.Left,
                            ["right"] = mc.Right,
                            ["speed"] = mc.Speed
                        }
                    }
                }
            };
            _connection.Send(BridgeProtocol.Serialize(response));
        }

        private void HandleSimStart(BridgeMessage msg)
        {
            try
            {
                _simulation.StartSimulation();
                SendOk(msg.Id);
                SimLog.Info("[BRIDGE] sim_start");
            }
            catch (Exception ex)
            {
                SendError(msg.Id, "INTERNAL_ERROR", ex.Message);
            }
        }

        private void HandleSimPause(BridgeMessage msg)
        {
            try
            {
                _simulation.Pause();
                SendOk(msg.Id);
                SimLog.Info("[BRIDGE] sim_pause");
            }
            catch (Exception ex)
            {
                SendError(msg.Id, "INTERNAL_ERROR", ex.Message);
            }
        }

        private void HandleSimStep(BridgeMessage msg)
        {
            try
            {
                _simulation.Step();
                SendOk(msg.Id);
                SimLog.Info("[BRIDGE] sim_step");
            }
            catch (Exception ex)
            {
                SendError(msg.Id, "INTERNAL_ERROR", ex.Message);
            }
        }

        private void HandleSimReset(BridgeMessage msg)
        {
            try
            {
                _simulation.ResetSimulation();
                SendOk(msg.Id);
                _lastMotorCommandRealtime = float.PositiveInfinity;
                SimLog.Info("[BRIDGE] sim_reset");
            }
            catch (Exception ex)
            {
                SendError(msg.Id, "INTERNAL_ERROR", ex.Message);
            }
        }

        // --- Camera commands ---

        private void HandleGetImage(BridgeMessage msg)
        {
            if (_sensors == null)
            {
                SendError(msg.Id, "SENSOR_NOT_AVAILABLE", "Camera sensor system is not initialized.");
                return;
            }

            var payload = msg.Payload;
            if (payload == null || !payload.TryGetValue("location", out var locObj) || !(locObj is string location))
            {
                SendError(msg.Id, "INVALID_ARGUMENT", "Payload must contain a 'location' string field: 'left', 'center', or 'right'.");
                return;
            }

            CameraLocation camLoc;
            try
            {
                camLoc = CameraSensorSystem.ParseLocation(location);
            }
            catch (ArgumentException ex)
            {
                SendError(msg.Id, "INVALID_ARGUMENT", ex.Message);
                return;
            }

            var frame = _sensors.GetLatestFrame(camLoc);
            if (frame == null)
            {
                SendError(msg.Id, "NO_FRAME", "No camera frame available yet.");
                return;
            }

            // Send JSON header + binary payload
            var headerMsg = new BridgeMessage
            {
                Type = "response",
                Id = msg.Id,
                Ok = true,
                PayloadType = "image",
                ImageWidth = frame.Width,
                ImageHeight = frame.Height,
                ImageFormat = "rgb24",
                ImageLength = frame.Data.Length
            };
            string headerJson = BridgeProtocol.Serialize(headerMsg);

            SimLog.Info($"[BRIDGE] get_image({location}) -> {frame.Width}x{frame.Height} frame #{frame.FrameId}");
            _connection.SendJsonWithBinary(headerJson, frame.Data);
        }

        private void HandleGetDepth(BridgeMessage msg)
        {
            if (_sensors == null)
            {
                SendError(msg.Id, "SENSOR_NOT_AVAILABLE", "Camera sensor system is not initialized.");
                return;
            }

            // Get the actual depth frame from the center camera
            var depthFrame = _sensors.GetLatestDepthFrame();
            if (depthFrame == null)
            {
                SendError(msg.Id, "NO_FRAME", "No depth frame available yet.");
                return;
            }

            // Send JSON header + binary payload
            var headerMsg = new BridgeMessage
            {
                Type = "response",
                Id = msg.Id,
                Ok = true,
                PayloadType = "depth",
                ImageWidth = depthFrame.Width,
                ImageHeight = depthFrame.Height,
                ImageFormat = "gray8",
                ImageLength = depthFrame.Data.Length
            };
            string headerJson = BridgeProtocol.Serialize(headerMsg);

            SimLog.Info($"[BRIDGE] get_depth() -> {depthFrame.Width}x{depthFrame.Height} frame #{depthFrame.FrameId}");
            _connection.SendJsonWithBinary(headerJson, depthFrame.Data);
        }

        // --- Helpers ---

        private void SendOk(int id)
        {
            var response = new BridgeMessage
            {
                Type = "response",
                Id = id,
                Ok = true
            };
            _connection.Send(BridgeProtocol.Serialize(response));
        }

        private void SendError(int id, string code, string message)
        {
            var response = new BridgeMessage
            {
                Type = "response",
                Id = id,
                Ok = false,
                Error = new BridgeErrorDetail
                {
                    Code = code,
                    Message = message
                }
            };
            _connection.Send(BridgeProtocol.Serialize(response));
        }

        private void EnforceZeroSpeed()
        {
            var current = _vehicle.CurrentCommand;
            // Preserve steering, set speed to 0
            _vehicle.SetMotorCommand(new MotorCommand(current.Left, current.Right, 0));
        }

        /// <summary>
        /// Called from the disconnect handler to enforce safety stop.
        /// </summary>
        public void OnDisconnect()
        {
            _handshakeComplete = false;
            if (_vehicle.CurrentCommand.Speed != 0)
            {
                EnforceZeroSpeed();
                SimLog.Info("[BRIDGE] Disconnect safety: speed → 0");
            }
        }

        /// <summary>
        /// Called from the connect handler to reset handshake state.
        /// </summary>
        public void OnConnect()
        {
            // Wait for hello
            _handshakeComplete = false;
        }

        private static bool TryGetInt(Dictionary<string, object> dict, string key, out int value)
        {
            value = 0;
            if (dict.TryGetValue(key, out var obj))
            {
                if (obj is int i)
                {
                    value = i;
                    return true;
                }
                if (obj is long l)
                {
                    value = (int)l;
                    return true;
                }
                if (obj is double d)
                {
                    value = (int)d;
                    return true;
                }
                // Try parsing from string (unlikely but safe)
                if (obj is string s && int.TryParse(s, out int parsed))
                {
                    value = parsed;
                    return true;
                }
                return false;
            }
            return false;
        }
    }
}
