namespace JajuchaSim.Core
{
    /// <summary>
    /// Every major runtime subsystem implements this interface so the
    /// <see cref="SimulationManager"/> can coordinate them generically.
    ///
    /// Responsibilities:
    ///  - <see cref="Initialize"/>: receive context once.
    ///  - <see cref="SimulationTick"/>: advance exactly one fixed-timestep tick.
    ///  - <see cref="ResetSimulation"/>: clear internal state back to initial conditions.
    ///  - <see cref="Shutdown"/>: release resources when stopping/resetting to uninitialized.
    ///
    /// Implementations must be deterministic given the same context and inputs.
    /// </summary>
    public interface ISimulationSystem
    {
        void Initialize(SimulationContext context);
        void SimulationTick(float deltaTime);
        void ResetSimulation();
        void Shutdown();
    }
}