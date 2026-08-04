using JajuchaSim.Core;
using UnityEngine;

namespace JajuchaSim.Vehicle
{
    /// <summary>
    /// Scene-compatible <see cref="SimulationSystemBehaviour"/> wrapper around
    /// <see cref="VehicleSystem"/>. Place this component in the scene so the
    /// bridge and other systems can discover the vehicle through the normal
    /// Unity component pipeline.
    ///
    /// The wrapped <see cref="VehicleSystem"/> is created on initialization and
    /// exposed via the <see cref="VehicleSystem"/> property for downstream code
    /// (bridge, debug HUD, etc.) that needs direct access.
    /// </summary>
    public sealed class VehicleSystemBehaviour : SimulationSystemBehaviour
    {
        [SerializeField] private VehicleConfig vehicleConfig;

        /// <summary>The underlying vehicle system instance.</summary>
        public VehicleSystem VehicleSystem { get; private set; }

        /// <summary>The vehicle's root GameObject.</summary>
        public GameObject VehicleRoot => VehicleSystem?.VehicleRoot;

        public override void SimulationTick(float deltaTime)
        {
            // The VehicleSystem is ticked through ISimulationSystem.
            // This behaviour delegates to the wrapped system.
            VehicleSystem?.SimulationTick(deltaTime);
        }

        public override void ResetSimulation()
        {
            VehicleSystem?.ResetSimulation();
        }

        public override void Shutdown()
        {
            VehicleSystem?.Shutdown();
        }

        protected override void OnInitialize(SimulationContext context)
        {
            if (vehicleConfig == null)
            {
                vehicleConfig = Resources.Load<VehicleConfig>("DefaultVehicleConfig");
                if (vehicleConfig == null)
                {
                    vehicleConfig = ScriptableObject.CreateInstance<VehicleConfig>();
                    Debug.LogWarning("[Vehicle] No VehicleConfig assigned or found in Resources; using defaults.");
                }
            }

            VehicleSystem = new VehicleSystem(vehicleConfig, gameObject);
            VehicleSystem.Initialize(context);
        }

        /// <summary>
        /// Applies a motor command. Delegates to the underlying system.
        /// </summary>
        public void SetMotorCommand(MotorCommand command)
        {
            VehicleSystem?.SetMotorCommand(command);
        }
    }
}
