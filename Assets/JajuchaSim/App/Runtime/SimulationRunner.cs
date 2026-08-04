using JajuchaSim.Core;
using UnityEngine;

namespace JajuchaSim.App
{
    /// <summary>
    /// Thin, explicit wrapper around the <see cref="SimulationManager"/>
    /// lifecycle for the authoritative scene (Step 11.3 "_Core/SimulationRunner").
    /// UI, tests, and automation call this instead of poking the manager
    /// directly; the manager remains the single owner of simulation state.
    /// </summary>
    public sealed class SimulationRunner : MonoBehaviour
    {
        [SerializeField] private SimulationManager manager;

        public SimulationManager Manager => manager;

        private void Awake()
        {
            if (manager == null)
                manager = FindFirstObjectByType<SimulationManager>();
        }

        public void StartSimulation()
        {
            if (manager != null && manager.State == SimulationState.Ready)
                manager.StartSimulation();
        }

        public void Pause()
        {
            manager?.Pause();
        }

        public void Resume()
        {
            manager?.Resume();
        }

        /// <summary>Advance exactly one simulation tick (valid while paused/running).</summary>
        public void Step()
        {
            manager?.Step();
        }

        public void ResetSimulation()
        {
            manager?.ResetSimulation();
        }

        public void SetTimeScale(float scale)
        {
            manager?.SetTimeScale(scale);
        }

        public SimulationState State => manager != null ? manager.State : SimulationState.Uninitialized;

        public long Tick => manager?.Clock != null ? manager.Clock.Tick : 0L;

        public double Time => manager?.Clock != null ? manager.Clock.Time : 0.0;
    }
}
