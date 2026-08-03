using JajuchaSim.Core;

namespace JajuchaSim.Core.Tests
{
    /// <summary>
    /// Pure-C# deterministic counter system used for the defining Step 1
    /// deterministic-tick test. Each tick increments by exactly one. Lives in the
    /// shared test-support assembly so both EditMode and PlayMode tests can use it.
    /// </summary>
    public sealed class CounterSimulationSystem : ISimulationSystem
    {
        public int Value { get; private set; }
        public int InitializeCalls { get; private set; }
        public int TickCalls { get; private set; }
        public int ResetCalls { get; private set; }
        public int ShutdownCalls { get; private set; }

        public void Initialize(SimulationContext context) => InitializeCalls++;

        public void SimulationTick(float deltaTime)
        {
            TickCalls++;
            Value++;
        }

        public void ResetSimulation()
        {
            ResetCalls++;
            Value = 0;
            TickCalls = 0;
        }

        public void Shutdown() => ShutdownCalls++;
    }
}