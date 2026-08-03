namespace JajuchaSim.Core
{
    /// <summary>
    /// Explicit dependency bundle handed to every <see cref="ISimulationSystem"/>
    /// during initialization. Subsystems must not discover dependencies via
    /// <c>GameObject.Find</c>, <c>FindObjectOfType</c>, or globals; they receive
    /// this context once, up front.
    ///
    /// Keep this small. Do not let it grow into a 70-property service locator.
    /// </summary>
    public sealed class SimulationContext
    {
        public SimulationClock Clock { get; }
        public SimulationEventBus Events { get; }
        public SimulationRandom Random { get; }

        public SimulationContext(
            SimulationClock clock,
            SimulationEventBus events,
            SimulationRandom random)
        {
            Clock = clock;
            Events = events;
            Random = random;
        }
    }
}