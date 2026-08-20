namespace JajuchaSim.Core
{
    /// <summary>
    /// Optional second phase for systems that own Unity physics objects.
    /// SimulationManager invokes this immediately after Physics.Simulate so
    /// post-physics invariants are visible to TickCompleted/state tracing.
    /// </summary>
    public interface IPostPhysicsSimulationSystem
    {
        void PostPhysicsStep(float deltaTime);
    }
}
