using JajuchaSim.Core;
using JajuchaSim.Vehicle;
using UnityEngine;

namespace JajuchaSim.Sensors
{
    /// <summary>
    /// MonoBehaviour wrapper for <see cref="CameraSensorSystem"/> that lives in the
    /// Unity scene and registers as a <see cref="SimulationSystemBehaviour"/>.
    ///
    /// Inspector slots:
    ///   - vehicleBehaviour: the VehicleSystemBehaviour in the scene
    ///   - leftCameraConfig, centerCameraConfig, rightCameraConfig: camera configs
    ///
    /// On initialization, this creates the three camera sensors (left, center, right)
    /// as children of the vehicle root, each with its own mount transform for
    /// independent calibration.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraSensorSystemBehaviour : SimulationSystemBehaviour
    {
        [SerializeField] private VehicleSystemBehaviour vehicleBehaviour;
        [SerializeField] private CameraConfig leftCameraConfig;
        [SerializeField] private CameraConfig centerCameraConfig;
        [SerializeField] private CameraConfig rightCameraConfig;

        private CameraSensorSystem _sensorSystem;

        /// <summary>
        /// The underlying <see cref="CameraSensorSystem"/> instance.
        /// Null until <see cref="Initialize"/> is called.
        /// </summary>
        public CameraSensorSystem SensorSystem => _sensorSystem;

        protected override void OnInitialize(SimulationContext context)
        {
            // Find vehicle if not wired
            if (vehicleBehaviour == null)
                vehicleBehaviour = FindFirstObjectByType<VehicleSystemBehaviour>();

            if (vehicleBehaviour == null || vehicleBehaviour.VehicleSystem == null)
            {
                SimLog.Error("[SENSOR] CameraSensorSystemBehaviour: No VehicleSystemBehaviour found.");
                return;
            }

            // Create configs with defaults if not assigned
            if (leftCameraConfig == null)
            {
                leftCameraConfig = ScriptableObject.CreateInstance<CameraConfig>();
                leftCameraConfig.name = "LeftCameraConfig (default)";
            }
            if (centerCameraConfig == null)
            {
                centerCameraConfig = ScriptableObject.CreateInstance<CameraConfig>();
                centerCameraConfig.name = "CenterCameraConfig (default)";
            }
            if (rightCameraConfig == null)
            {
                rightCameraConfig = ScriptableObject.CreateInstance<CameraConfig>();
                rightCameraConfig.name = "RightCameraConfig (default)";
            }

            _sensorSystem = new CameraSensorSystem(
                vehicleBehaviour.VehicleSystem,
                leftCameraConfig,
                centerCameraConfig,
                rightCameraConfig);

            _sensorSystem.Initialize(context);
            SimLog.Info("[SENSOR] CameraSensorSystemBehaviour initialized");
        }

        public override void SimulationTick(float deltaTime)
        {
            _sensorSystem?.SimulationTick(deltaTime);
        }

        public override void ResetSimulation()
        {
            _sensorSystem?.ResetSimulation();
        }

        public override void Shutdown()
        {
            _sensorSystem?.Shutdown();
            _sensorSystem = null;
        }
    }
}
