using UnityEngine;

namespace JajuchaSim.Bridge
{
    /// <summary>
    /// Configuration for the TCP bridge server that communicates with the
    /// Python jchm client.
    ///
    /// Keep network config separate from simulation config — changes to the
    /// bridge (port, timeout) should not require touching simulation settings.
    /// </summary>
    [CreateAssetMenu(fileName = "BridgeConfig", menuName = "JajuchaSim/Bridge Config", order = 100)]
    public sealed class BridgeConfig : ScriptableObject
    {
        [Tooltip("Host address to bind the TCP server. Default is 127.0.0.1 (localhost only).")]
        public string host = "127.0.0.1";

        [Tooltip("TCP port for the bridge server.")]
        public ushort port = 8765;

        [Tooltip("Automatically start the bridge server when the scene loads.")]
        public bool autoStart = true;

        [Tooltip("Timeout in milliseconds after which the motor watchdog sets speed to 0.")]
        public int commandTimeoutMs = 1000;

        [Tooltip("Maximum allowed incoming message size in bytes.")]
        public int maxMessageBytes = 65536;

        [Tooltip("Protocol version that this bridge implements.")]
        public int protocolVersion = 1;
    }
}
