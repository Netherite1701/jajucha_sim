using JajuchaSim.Core;
using JajuchaSim.Sensors;
using JajuchaSim.Vehicle;
using UnityEngine;

namespace JajuchaSim.Bridge
{
    /// <summary>
    /// Main MonoBehaviour for the Python bridge. Wires together the TCP
    /// connection, protocol handling, and command dispatch.
    ///
    /// Lifecycle:
    ///   Awake → StartListening (if autoStart)
    ///   Update → ProcessQueue (dequeue + dispatch)
    ///   OnDestroy → Stop
    ///
    /// This component must exist in the scene alongside SimulationManager
    /// and the VehicleSystem to function.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JajuchaBridgeServer : MonoBehaviour
    {
        [SerializeField] private BridgeConfig config;
        [SerializeField] private SimulationManager simulationManager;
        [SerializeField] private SimulationSystemBehaviour vehicleBehaviour;
        [SerializeField] private CameraSensorSystemBehaviour cameraBehaviour;

        private BridgeConnection _connection;
        private CommandDispatcher _dispatcher;
        private VehicleSystem _vehicle;
        private CameraSensorSystem _sensors;

        public BridgeConnection Connection => _connection;
        public CommandDispatcher Dispatcher => _dispatcher;
        public bool IsConnected => _connection != null &&
            _connection.State == BridgeConnection.ConnectionState.Connected;

        private void Awake()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BridgeConfig>();
                Debug.Log("[BRIDGE] No BridgeConfig assigned; using default values.", this);
            }

            if (simulationManager == null)
                simulationManager = FindFirstObjectByType<SimulationManager>();
            if (simulationManager == null)
            {
                Debug.LogError("[BRIDGE] No SimulationManager found. Bridge cannot function without it.");
                return;
            }

            if (vehicleBehaviour == null)
                vehicleBehaviour = FindFirstObjectByType<VehicleSystemBehaviour>();
            if (vehicleBehaviour == null)
            {
                Debug.LogError("[BRIDGE] No VehicleSystemBehaviour found. Bridge cannot function without it.");
                return;
            }

            // Extract VehicleSystem from the behaviour
            if (vehicleBehaviour is VehicleSystemBehaviour vsb)
            {
                _vehicle = vsb.VehicleSystem;
            }
            else
            {
                Debug.LogError("[BRIDGE] Vehicle behaviour is not a VehicleSystemBehaviour.");
                return;
            }

            if (_vehicle == null)
            {
                Debug.LogError("[BRIDGE] VehicleSystem is null. Cannot proceed.");
                return;
            }

            // Find CameraSensorSystem
            if (cameraBehaviour == null)
                cameraBehaviour = FindFirstObjectByType<CameraSensorSystemBehaviour>();
            if (cameraBehaviour != null)
            {
                _sensors = cameraBehaviour.SensorSystem;
            }
            else
            {
                SimLog.Info("[BRIDGE] No CameraSensorSystemBehaviour found. Camera commands will return errors.");
            }

            // Create the connection
            _connection = new BridgeConnection(config.host, config.port, config.maxMessageBytes);

            // Create the dispatcher
            _dispatcher = new CommandDispatcher(
                simulationManager,
                _vehicle,
                _sensors,
                _connection,
                config.protocolVersion,
                config.commandTimeoutMs / 1000f);

            // Wire events
            _connection.ClientConnected += OnClientConnected;
            _connection.ClientDisconnected += OnClientDisconnected;

            if (config.autoStart)
            {
                _connection.StartListening();
            }
        }

        private void Update()
        {
            if (_dispatcher != null)
            {
                _dispatcher.ProcessQueue();
            }
        }

        private void OnDestroy()
        {
            if (_connection != null)
            {
                _connection.ClientConnected -= OnClientConnected;
                _connection.ClientDisconnected -= OnClientDisconnected;
                _connection.Stop();
                _connection.Dispose();
            }
        }

        private void OnClientConnected()
        {
            _dispatcher?.OnConnect();
        }

        private void OnClientDisconnected()
        {
            _dispatcher?.OnDisconnect();
        }

        // --- Public API for testing / jchm_sim ---

        public void StartBridge()
        {
            if (_connection != null && _connection.State == BridgeConnection.ConnectionState.Disconnected)
                _connection.StartListening();
        }

        public void StopBridge()
        {
            _connection?.Stop();
        }

        public bool SendMessage(BridgeMessage msg)
        {
            if (_connection == null) return false;
            string json = BridgeProtocol.Serialize(msg);
            return _connection.Send(json);
        }
    }
}
