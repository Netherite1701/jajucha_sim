using UnityEngine;

namespace JajuchaSim.Core
{
    /// <summary>
    /// Optional <see cref="MonoBehaviour"/> base class for simulation systems
    /// that need to live in the scene (vehicle, sensors, etc.). Subclasses get
    /// type-safe inspector registration and a single explicit lifecycle surface.
    /// </summary>
    public abstract class SimulationSystemBehaviour : MonoBehaviour, ISimulationSystem
    {
        /// <summary>Context provided after initialization. Null until then.</summary>
        protected SimulationContext Context { get; private set; }

        public void Initialize(SimulationContext context)
        {
            Context = context;
            OnInitialize(context);
        }

        public abstract void SimulationTick(float deltaTime);
        public abstract void ResetSimulation();
        public abstract void Shutdown();

        protected virtual void OnInitialize(SimulationContext context) { }
    }
}