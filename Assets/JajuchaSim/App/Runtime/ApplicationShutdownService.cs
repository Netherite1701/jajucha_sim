using JajuchaSim.Bridge;
using JajuchaSim.Core;
using UnityEngine;

namespace JajuchaSim.App
{
    /// <summary>
    /// Application-level shutdown (Step 11.3 "_Services/ApplicationShutdownService").
    /// On quit: stops propulsion (jchm.control.set_motor(0,0,0) equivalent),
    /// stops the bridge, flushes file logs.
    /// </summary>
    public sealed class ApplicationShutdownService : MonoBehaviour
    {
        [SerializeField] private SimulationManager manager;
        [SerializeField] private JajuchaBridgeServer bridgeServer;

        private void Awake()
        {
            if (manager == null)
                manager = FindFirstObjectByType<SimulationManager>();
            if (bridgeServer == null)
                bridgeServer = FindFirstObjectByType<JajuchaBridgeServer>();
        }

        /// <summary>
        /// Safe shutdown: stop vehicle propulsion, stop the simulation if
        /// running, stop the bridge, flush logs.
        /// </summary>
        public void Shutdown()
        {
            // Propulsion stop: mirror jchm.control.set_motor(0,0,0).
            var vehicle = FindFirstObjectByType<Vehicle.VehicleSystemBehaviour>();
            if (vehicle != null && vehicle.VehicleSystem != null)
                vehicle.SetMotorCommand(new Vehicle.MotorCommand(0, 0, 0));

            if (manager != null &&
                (manager.State == SimulationState.Running || manager.State == SimulationState.Paused))
            {
                manager.Stop();
            }

            bridgeServer?.StopBridge();
            RuntimeFileLogger.Info("ApplicationShutdown", "Shutdown complete");
            RuntimeFileLogger.Flush();
        }

        private void OnApplicationQuit()
        {
            RuntimeFileLogger.Flush();
        }

        private void OnDestroy()
        {
            RuntimeFileLogger.Flush();
        }
    }
}
