using JajuchaSim.Core;

namespace JajuchaSim.Core.Tests
{
    /// <summary>
    /// Architecture-test double used to prove ISimulationSystem plugs into the
    /// kernel correctly. Counts lifecycle calls so tests can assert ordering and
    /// tick counts without introducing real subsystem behavior.
    /// </summary>
    public sealed class FakeSimulationSystem : ISimulationSystem
    {
        public int InitializeCalls { get; private set; }
        public int TickCalls { get; private set; }
        public int ResetCalls { get; private set; }
        public int ShutdownCalls { get; private set; }

        public SimulationContext LastContext { get; private set; }
        public float LastDeltaTime { get; private set; }

        public void Initialize(SimulationContext context)
        {
            LastContext = context;
            InitializeCalls++;
        }

        public void SimulationTick(float deltaTime)
        {
            TickCalls++;
            LastDeltaTime = deltaTime;
        }

        public void ResetSimulation()
        {
            ResetCalls++;
        }

        public void Shutdown()
        {
            ShutdownCalls++;
        }
    }
}