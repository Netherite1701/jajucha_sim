using System.Collections.Generic;
using JajuchaSim.Core;
using JajuchaSim.Sensors;
using JajuchaSim.Vehicle;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Bridge.Tests
{
    /// <summary>
    /// EditMode tests for the camera-related bridge commands (get_image, get_depth).
    /// </summary>
    public class CameraBridgeTests
    {
        private const float FixedDeltaTime = 0.01f;

        private GameObject _go;
        private SimulationManager _simulation;
        private VehicleSystem _vehicle;
        private FakeBridgeConnection _connection;
        private CommandDispatcher _dispatcher;
        private FakeCameraSensorSystem _fakeSensors;

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

            _fakeSensors = new FakeCameraSensorSystem();
            _connection = new FakeBridgeConnection();
            _dispatcher = new CommandDispatcher(
                _simulation, _vehicle, _fakeSensors, _connection, 1, 1f);

            // Complete handshake
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

        // --- get_image ---

        [Test]
        public void GetImage_Center_ReturnsOkWithImageHeader()
        {
            var cmd = CreateGetImageCmd(1, "center");
            _connection.InjectMessage(BridgeProtocol.Serialize(cmd));
            _dispatcher.ProcessQueue();

            Assert.That(_connection.SentCount, Is.GreaterThanOrEqualTo(1));
            var response = BridgeProtocol.Deserialize(_connection.LastSent);

            Assert.IsNotNull(response);
            Assert.AreEqual("response", response.Type);
            Assert.AreEqual(1, response.Id);
            Assert.IsTrue(response.Ok);
            Assert.AreEqual("image", response.PayloadType);
            Assert.AreEqual(640, response.ImageWidth);
            Assert.AreEqual(480, response.ImageHeight);
            Assert.AreEqual("rgb24", response.ImageFormat);
            Assert.Greater(response.ImageLength, 0);
            Assert.AreEqual(640 * 480 * 3, response.ImageLength);
        }

        [Test]
        public void GetImage_Left_ReturnsImage()
        {
            var cmd = CreateGetImageCmd(2, "left");
            _connection.InjectMessage(BridgeProtocol.Serialize(cmd));
            _dispatcher.ProcessQueue();

            var response = BridgeProtocol.Deserialize(_connection.LastSent);
            Assert.IsNotNull(response);
            Assert.IsTrue(response.Ok);
            Assert.AreEqual("image", response.PayloadType);
            Assert.Greater(response.ImageLength, 0);
        }

        [Test]
        public void GetImage_Right_ReturnsImage()
        {
            var cmd = CreateGetImageCmd(3, "right");
            _connection.InjectMessage(BridgeProtocol.Serialize(cmd));
            _dispatcher.ProcessQueue();

            var response = BridgeProtocol.Deserialize(_connection.LastSent);
            Assert.IsNotNull(response);
            Assert.IsTrue(response.Ok);
            Assert.AreEqual("image", response.PayloadType);
            Assert.Greater(response.ImageLength, 0);
        }

        [Test]
        public void GetImage_InvalidLocation_ReturnsError()
        {
            var cmd = CreateGetImageCmd(4, "rear");
            _connection.InjectMessage(BridgeProtocol.Serialize(cmd));
            _dispatcher.ProcessQueue();

            string sentJson = _connection.LastSent;
            var response = BridgeProtocol.Deserialize(sentJson);
            Assert.IsNotNull(response);
            Assert.IsFalse(response.Ok);
            Assert.AreEqual("INVALID_ARGUMENT", response.Error?.Code);
        }

        [Test]
        public void GetImage_MissingLocation_ReturnsError()
        {
            var cmd = new BridgeMessage
            {
                Type = "command",
                Id = 5,
                Name = "get_image",
                Payload = new Dictionary<string, object>()
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
        public void GetImage_NoSensors_ReturnsError()
        {
            var dispatcher = new CommandDispatcher(
                _simulation, _vehicle, null, _connection, 1, 1f);

            // Re-do handshake
            _connection = new FakeBridgeConnection();
            dispatcher = new CommandDispatcher(
                _simulation, _vehicle, null, _connection, 1, 1f);

            var helloMsg = new BridgeMessage
            {
                Type = "hello",
                Protocol = 1,
                Client = "jchm-sim"
            };
            _connection.InjectMessage(BridgeProtocol.Serialize(helloMsg));
            dispatcher.ProcessQueue();

            var cmd = CreateGetImageCmd(6, "center");
            _connection.InjectMessage(BridgeProtocol.Serialize(cmd));
            dispatcher.ProcessQueue();

            string sentJson = _connection.LastSent;
            var response = BridgeProtocol.Deserialize(sentJson);
            Assert.IsNotNull(response);
            Assert.IsFalse(response.Ok);
            Assert.AreEqual("SENSOR_NOT_AVAILABLE", response.Error?.Code);
        }

        // --- get_depth ---

        [Test]
        public void GetDepth_ReturnsGray8Image()
        {
            var cmd = new BridgeMessage
            {
                Type = "command",
                Id = 10,
                Name = "get_depth",
                Payload = new Dictionary<string, object>()
            };
            _connection.InjectMessage(BridgeProtocol.Serialize(cmd));
            _dispatcher.ProcessQueue();

            var response = BridgeProtocol.Deserialize(_connection.LastSent);

            Assert.IsNotNull(response);
            Assert.AreEqual("response", response.Type);
            Assert.IsTrue(response.Ok);
            Assert.AreEqual("depth", response.PayloadType);
            Assert.AreEqual("gray8", response.ImageFormat);
            Assert.Greater(response.ImageLength, 0);
            Assert.AreEqual(640 * 480, response.ImageLength);
        }

        [Test]
        public void GetDepth_NoSensors_ReturnsError()
        {
            var conn = new FakeBridgeConnection();
            var dispatcher = new CommandDispatcher(
                _simulation, _vehicle, null, conn, 1, 1f);

            var helloMsg = new BridgeMessage
            {
                Type = "hello",
                Protocol = 1,
                Client = "jchm-sim"
            };
            conn.InjectMessage(BridgeProtocol.Serialize(helloMsg));
            dispatcher.ProcessQueue();

            var cmd = new BridgeMessage
            {
                Type = "command",
                Id = 11,
                Name = "get_depth",
                Payload = new Dictionary<string, object>()
            };
            conn.InjectMessage(BridgeProtocol.Serialize(cmd));
            dispatcher.ProcessQueue();

            string sentJson = conn.LastSent;
            var response = BridgeProtocol.Deserialize(sentJson);
            Assert.IsNotNull(response);
            Assert.IsFalse(response.Ok);
            Assert.AreEqual("SENSOR_NOT_AVAILABLE", response.Error?.Code);
        }

        // --- Helpers ---

        private static BridgeMessage CreateGetImageCmd(int id, string location)
        {
            return new BridgeMessage
            {
                Type = "command",
                Id = id,
                Name = "get_image",
                Payload = new Dictionary<string, object>
                {
                    ["location"] = location
                }
            };
        }
    }

    /// <summary>
    /// Fake camera sensor system that returns synthetic frames for testing.
    /// </summary>
    internal sealed class FakeCameraSensorSystem : CameraSensorSystem
    {
        public FakeCameraSensorSystem()
        {
        }

        public override CameraFrame GetLatestFrame(CameraLocation location)
        {
            // Return a synthetic 640x480 RGB24 frame
            int width = 640;
            int height = 480;
            byte[] data = new byte[width * height * 3];

            // Fill with a simple pattern matching the location
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = (y * width + x) * 3;
                    // Different color per camera location
                    switch (location)
                    {
                        case CameraLocation.Left:
                            data[idx] = 0;       // R
                            data[idx + 1] = 0;   // G
                            data[idx + 2] = 255; // B
                            break;
                        case CameraLocation.Center:
                            data[idx] = 0;       // R
                            data[idx + 1] = 255; // G
                            data[idx + 2] = 0;   // B
                            break;
                        case CameraLocation.Right:
                            data[idx] = 255;     // R
                            data[idx + 1] = 0;   // G
                            data[idx + 2] = 0;   // B
                            break;
                    }
                }
            }

            return new CameraFrame(
                location,
                (long)location + 1,
                100,
                1.0,
                width,
                height,
                data,
                CameraOutputFormat.RGB24);
        }

        public override CameraFrame GetLatestDepthFrame()
        {
            // Return a synthetic 640x480 Gray8 depth frame
            // Distance gradient: top of image = near (bright), bottom = far (dark)
            int width = 640;
            int height = 480;
            byte[] data = new byte[width * height];

            for (int y = 0; y < height; y++)
            {
                // nearer = brighter, farther = darker
                byte depthValue = (byte)(255 - (y * 255 / height));
                for (int x = 0; x < width; x++)
                {
                    data[y * width + x] = depthValue;
                }
            }

            return new CameraFrame(
                CameraLocation.Center,
                1,
                100,
                1.0,
                width,
                height,
                data,
                CameraOutputFormat.Gray8);
        }
    }
}
