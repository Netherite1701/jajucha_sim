using System.Collections.Generic;

namespace JajuchaSim.Bridge
{
    /// <summary>
    /// Represents a parsed protocol message from the Python jchm client.
    /// This is the internal representation used within the bridge, separate
    /// from the network/JSON representation.
    ///
    /// Messages types:
    ///   hello        — handshake from client
    ///   hello_ack    — handshake response from server
    ///   command      — named command with optional payload
    ///   response     — reply to a specific command id
    ///   error        — protocol-level error (no matching id)
    /// </summary>
    public sealed class BridgeMessage
    {
        /// <summary>Message type: "hello", "hello_ack", "command", "response", "error".</summary>
        public string Type { get; set; }

        /// <summary>Request id for matching replies (0 for fire-and-forget).</summary>
        public int Id { get; set; }

        /// <summary>For "command" messages: the command name (e.g. "set_motor").</summary>
        public string Name { get; set; }

        /// <summary>For "response" messages: success indicator.</summary>
        public bool Ok { get; set; }

        /// <summary>For "command" messages: the command payload as key-value pairs.</summary>
        public Dictionary<string, object> Payload { get; set; }

        /// <summary>For "response"/"error" messages: error details (null on success).</summary>
        public BridgeErrorDetail Error { get; set; }

        // --- Handshake fields ---

        /// <summary>Protocol version from client hello.</summary>
        public int Protocol { get; set; }

        /// <summary>Client identifier from hello.</summary>
        public string Client { get; set; }

        /// <summary>Simulator identifier in hello_ack.</summary>
        public string Simulator { get; set; }

        // --- Image response fields ---

        /// <summary>For image responses: "image" for RGB, "depth" for grayscale.</summary>
        public string PayloadType { get; set; }

        /// <summary>Image width in pixels (for binary payload responses).</summary>
        public int ImageWidth { get; set; }

        /// <summary>Image height in pixels (for binary payload responses).</summary>
        public int ImageHeight { get; set; }

        /// <summary>Pixel format string: "rgb24" or "gray8".</summary>
        public string ImageFormat { get; set; }

        /// <summary>Binary payload length in bytes.</summary>
        public int ImageLength { get; set; }
    }

    /// <summary>
    /// Structured error information sent in error responses.
    /// </summary>
    public sealed class BridgeErrorDetail
    {
        public string Code { get; set; }
        public string Message { get; set; }
    }
}
