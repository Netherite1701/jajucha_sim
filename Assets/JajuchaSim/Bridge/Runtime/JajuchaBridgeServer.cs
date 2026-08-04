using JajuchaSim.Core;
using JajuchaSim.Scenario;
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
            }

            if (vehicleBehaviour == null)
                vehicleBehaviour = FindFirstObjectByType<VehicleSystemBehaviour>();
            if (vehicleBehaviour == null)
            {
                Debug.LogWarning("[BRIDGE] No VehicleSystemBehaviour found yet; binding deferred.");
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

            // Wire events
            _connection.ClientConnected += OnClientConnected;
            _connection.ClientDisconnected += OnClientDisconnected;

            // Best-effort early bind; the ApplicationBootstrap calls
            // TryBindSystems() again once the simulation kernel has spawned
            // the vehicle, so system initialization ordering is explicit and
            // does not depend on MonoBehaviour Awake order.
            TryBindSystems();

            if (config.autoStart)
            {
                _connection.StartListening();
            }
        }

        /// <summary>
        /// Assign a bridge configuration before the component is activated
        /// (used by tests and automated harnesses that must avoid port
        /// conflicts). Must be called before Awake runs.
        /// </summary>
        public void SetBridgeConfig(BridgeConfig cfg)
        {
            config = cfg;
        }

        /// <summary>
        /// Resolve the vehicle/sensor systems and create the command dispatcher
        /// once the simulation kernel is initialized. Safe to call repeatedly;
        /// returns true when the dispatcher is ready. The ApplicationBootstrap
        /// calls this explicitly during ordered startup (Step 11.4).
        /// </summary>
        public bool TryBindSystems()
        {
            if (_dispatcher != null)
                return true;

            if (simulationManager == null)
                simulationManager = FindFirstObjectByType<SimulationManager>();
            if (simulationManager == null)
                return false;

            if (vehicleBehaviour == null)
                vehicleBehaviour = FindFirstObjectByType<VehicleSystemBehaviour>();
            if (vehicleBehaviour == null)
                return false;

            if (_vehicle == null && vehicleBehaviour is VehicleSystemBehaviour vsb)
                _vehicle = vsb.VehicleSystem;
            if (_vehicle == null)
                return false; // vehicle not spawned yet (kernel not initialized)

            // Re-read sensor system if it became available after kernel init.
            if (cameraBehaviour == null)
                cameraBehaviour = FindFirstObjectByType<CameraSensorSystemBehaviour>();
            if (cameraBehaviour != null && _sensors == null)
                _sensors = cameraBehaviour.SensorSystem;

            if (_connection == null)
                _connection = new BridgeConnection(config.host, config.port, config.maxMessageBytes);

            _dispatcher = new CommandDispatcher(
                simulationManager,
                _vehicle,
                _sensors,
                _connection,
                config.protocolVersion,
                config.commandTimeoutMs / 1000f);

            return true;
        }

        private void Update()
        {
            TryBindSystems();

            // Lazily bind the scenario manager once a ScenarioPanel exists
            // (panel may create its manager in Start, after this Awake).
            if (_dispatcher != null && _dispatcher.Scenario == null)
            {
                var panel = FindFirstObjectByType<ScenarioPanel>();
                if (panel != null && panel.Manager != null)
                    _dispatcher.Scenario = panel.Manager;
            }

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
