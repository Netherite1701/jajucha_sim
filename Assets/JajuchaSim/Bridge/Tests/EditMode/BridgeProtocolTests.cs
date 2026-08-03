using System.Collections.Generic;
using NUnit.Framework;

namespace JajuchaSim.Bridge.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="BridgeProtocol"/> serialization/deserialization
    /// and <see cref="BridgeMessage"/> parsing.
    /// </summary>
    public class BridgeProtocolTests
    {
        // --- Serialization ---

        [Test]
        public void Serialize_Hello_ProducesValidJson()
        {
            var msg = new BridgeMessage
            {
                Type = "hello",
                Protocol = 1,
                Client = "jchm-sim"
            };
            string json = BridgeProtocol.Serialize(msg);
            Assert.That(json, Does.Contain("\"type\":\"hello\""));
            Assert.That(json, Does.Contain("\"protocol\":1"));
            Assert.That(json, Does.Contain("\"client\":\"jchm-sim\""));
        }

        [Test]
        public void Serialize_HelloAck_ProducesValidJson()
        {
            var msg = new BridgeMessage
            {
                Type = "hello_ack",
                Protocol = 1,
                Simulator = "JajuchaSim"
            };
            string json = BridgeProtocol.Serialize(msg);
            Assert.That(json, Does.Contain("\"type\":\"hello_ack\""));
            Assert.That(json, Does.Contain("\"protocol\":1"));
            Assert.That(json, Does.Contain("\"simulator\":\"JajuchaSim\""));
        }

        [Test]
        public void Serialize_SetMotorCommand_ProducesValidJson()
        {
            var msg = new BridgeMessage
            {
                Type = "command",
                Id = 17,
                Name = "set_motor",
                Payload = new Dictionary<string, object>
                {
                    ["left"] = -5,
                    ["right"] = -5,
                    ["speed"] = 3
                }
            };
            string json = BridgeProtocol.Serialize(msg);
            Assert.That(json, Does.Contain("\"type\":\"command\""));
            Assert.That(json, Does.Contain("\"id\":17"));
            Assert.That(json, Does.Contain("\"name\":\"set_motor\""));
            Assert.That(json, Does.Contain("\"left\":-5"));
            Assert.That(json, Does.Contain("\"right\":-5"));
            Assert.That(json, Does.Contain("\"speed\":3"));
        }

        [Test]
        public void Serialize_ResponseOk_ProducesValidJson()
        {
            var msg = new BridgeMessage
            {
                Type = "response",
                Id = 17,
                Ok = true
            };
            string json = BridgeProtocol.Serialize(msg);
            Assert.That(json, Does.Contain("\"type\":\"response\""));
            Assert.That(json, Does.Contain("\"id\":17"));
            Assert.That(json, Does.Contain("\"ok\":true"));
        }

        [Test]
        public void Serialize_ResponseError_ProducesValidJson()
        {
            var msg = new BridgeMessage
            {
                Type = "response",
                Id = 12,
                Ok = false,
                Error = new BridgeErrorDetail
                {
                    Code = "INVALID_ARGUMENT",
                    Message = "speed must be between -30 and 30"
                }
            };
            string json = BridgeProtocol.Serialize(msg);
            Assert.That(json, Does.Contain("\"type\":\"response\""));
            Assert.That(json, Does.Contain("\"id\":12"));
            Assert.That(json, Does.Contain("\"ok\":false"));
            Assert.That(json, Does.Contain("\"error\""));
            Assert.That(json, Does.Contain("\"INVALID_ARGUMENT\""));
        }

        // --- Deserialization ---

        [Test]
        public void Deserialize_Hello_ParsesCorrectly()
        {
            string json = "{\"type\":\"hello\",\"protocol\":1,\"client\":\"jchm-sim\"}";
            var msg = BridgeProtocol.Deserialize(json);
            Assert.IsNotNull(msg);
            Assert.AreEqual("hello", msg.Type);
            Assert.AreEqual(1, msg.Protocol);
            Assert.AreEqual("jchm-sim", msg.Client);
        }

        [Test]
        public void Deserialize_SetMotorCommand_ParsesCorrectly()
        {
            string json = "{\"type\":\"command\",\"id\":17,\"name\":\"set_motor\",\"payload\":{\"left\":-5,\"right\":-5,\"speed\":3}}";
            var msg = BridgeProtocol.Deserialize(json);
            Assert.IsNotNull(msg);
            Assert.AreEqual("command", msg.Type);
            Assert.AreEqual(17, msg.Id);
            Assert.AreEqual("set_motor", msg.Name);
            Assert.IsNotNull(msg.Payload);
            Assert.AreEqual(-5, msg.Payload["left"]);
            Assert.AreEqual(-5, msg.Payload["right"]);
            Assert.AreEqual(3, msg.Payload["speed"]);
        }

        [Test]
        public void Deserialize_ResponseOk_ParsesCorrectly()
        {
            string json = "{\"type\":\"response\",\"id\":17,\"ok\":true}";
            var msg = BridgeProtocol.Deserialize(json);
            Assert.IsNotNull(msg);
            Assert.AreEqual("response", msg.Type);
            Assert.AreEqual(17, msg.Id);
            Assert.IsTrue(msg.Ok);
        }

        [Test]
        public void Deserialize_ResponseError_ParsesCorrectly()
        {
            string json = "{\"type\":\"response\",\"id\":12,\"ok\":false,\"error\":{\"code\":\"INVALID_ARGUMENT\",\"message\":\"speed must be between -30 and 30\"}}";
            var msg = BridgeProtocol.Deserialize(json);
            Assert.IsNotNull(msg);
            Assert.AreEqual("response", msg.Type);
            Assert.AreEqual(12, msg.Id);
            Assert.IsFalse(msg.Ok);
            Assert.IsNotNull(msg.Error);
            Assert.AreEqual("INVALID_ARGUMENT", msg.Error.Code);
            Assert.AreEqual("speed must be between -30 and 30", msg.Error.Message);
        }

        [Test]
        public void Deserialize_PingCommand_ParsesCorrectly()
        {
            string json = "{\"type\":\"command\",\"id\":1,\"name\":\"ping\",\"payload\":{}}";
            var msg = BridgeProtocol.Deserialize(json);
            Assert.IsNotNull(msg);
            Assert.AreEqual("command", msg.Type);
            Assert.AreEqual(1, msg.Id);
            Assert.AreEqual("ping", msg.Name);
            Assert.IsNotNull(msg.Payload);
        }

        // --- Edge cases ---

        [Test]
        public void Deserialize_EmptyString_ReturnsNull()
        {
            Assert.IsNull(BridgeProtocol.Deserialize(""));
        }

        [Test]
        public void Deserialize_NullString_ReturnsNull()
        {
            Assert.IsNull(BridgeProtocol.Deserialize(null));
        }

        [Test]
        public void Deserialize_InvalidJson_ReturnsNull()
        {
            string json = "{\"type\":\"command\",";
            var msg = BridgeProtocol.Deserialize(json);
            Assert.IsNull(msg);
        }

        [Test]
        public void Deserialize_UnknownCommand_StillParses()
        {
            string json = "{\"type\":\"command\",\"id\":1,\"name\":\"explode_car\",\"payload\":{}}";
            var msg = BridgeProtocol.Deserialize(json);
            Assert.IsNotNull(msg);
            Assert.AreEqual("command", msg.Type);
            Assert.AreEqual("explode_car", msg.Name);
        }

        [Test]
        public void RoundTrip_Hello_IsSymmetric()
        {
            var original = new BridgeMessage
            {
                Type = "hello",
                Protocol = 1,
                Client = "jchm-sim"
            };
            string json = BridgeProtocol.Serialize(original);
            var deserialized = BridgeProtocol.Deserialize(json);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.Type, deserialized.Type);
            Assert.AreEqual(original.Protocol, deserialized.Protocol);
            Assert.AreEqual(original.Client, deserialized.Client);
        }

        [Test]
        public void RoundTrip_SetMotor_IsSymmetric()
        {
            var original = new BridgeMessage
            {
                Type = "command",
                Id = 42,
                Name = "set_motor",
                Payload = new Dictionary<string, object>
                {
                    ["left"] = 10,
                    ["right"] = -10,
                    ["speed"] = 30
                }
            };
            string json = BridgeProtocol.Serialize(original);
            var deserialized = BridgeProtocol.Deserialize(json);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.Type, deserialized.Type);
            Assert.AreEqual(original.Id, deserialized.Id);
            Assert.AreEqual(original.Name, deserialized.Name);
            Assert.AreEqual(original.Payload["left"], deserialized.Payload["left"]);
            Assert.AreEqual(original.Payload["right"], deserialized.Payload["right"]);
            Assert.AreEqual(original.Payload["speed"], deserialized.Payload["speed"]);
        }

        [Test]
        public void Deserialize_WithExtraWhitespace_StillParses()
        {
            string json = "  {  \"type\" :  \"hello\" ,  \"protocol\" :  1 ,  \"client\" :  \"test\"  }  ";
            var msg = BridgeProtocol.Deserialize(json);
            Assert.IsNotNull(msg);
            Assert.AreEqual("hello", msg.Type);
            Assert.AreEqual(1, msg.Protocol);
        }

        [Test]
        public void Serialize_NullMessage_Throws()
        {
            Assert.That(() => BridgeProtocol.Serialize(null), Throws.ArgumentNullException);
        }
    }
}
