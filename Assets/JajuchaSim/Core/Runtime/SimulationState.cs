namespace JajuchaSim.Core
{
    /// <summary>
    /// Lifecycle states of the simulation kernel.
    ///
    /// Transition diagram:
    ///   Uninitialized -> Initialize()  -> Ready
    ///   Ready        -> StartSimulation() -> Running
    ///   Running     -> Pause()        -> Paused
    ///   Paused      -> Resume()       -> Running
    ///   Running     -> Stop()         -> Stopped
    ///   Stopped     -> ResetSimulation() -> Ready
    ///   anything    -> ResetSimulation() -> Ready
    ///
    /// <see cref="SimulationManager"/> owns these transitions.
    /// </summary>
    public enum SimulationState
    {
        /// <summary>The kernel has not yet been initialized.</summary>
        Uninitialized = 0,

        /// <summary>Initialized and ready to start. Tick = 0, time = 0.</summary>
        Ready,

        /// <summary>Simulation is auto-ticking from real time using a fixed timestep.</summary>
        Running,

        /// <summary>Simulation is frozen; <see cref="SimulationManager.Step"/> advances a single tick.</summary>
        Paused,

        /// <summary>Running was stopped; final state preserved for inspection until reset.</summary>
        Stopped
    }
}