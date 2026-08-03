namespace JajuchaSim.Core
{
    /// <summary>Typed lifecycle/state events for the simulation kernel.</summary>
    public readonly struct SimulationStartedEvent
    {
        public double StartTime { get; }
        public SimulationStartedEvent(double startTime) => StartTime = startTime;
    }

    public readonly struct SimulationPausedEvent
    {
        public long Tick { get; }
        public double Time { get; }
        public SimulationPausedEvent(long tick, double time)
        {
            Tick = tick;
            Time = time;
        }
    }

    public readonly struct SimulationResumedEvent
    {
        public long Tick { get; }
        public double Time { get; }
        public SimulationResumedEvent(long tick, double time)
        {
            Tick = tick;
            Time = time;
        }
    }

    public readonly struct SimulationStoppedEvent
    {
        public long Tick { get; }
        public double Time { get; }
        public SimulationStoppedEvent(long tick, double time)
        {
            Tick = tick;
            Time = time;
        }
    }

    public readonly struct SimulationResetEvent
    {
        public long Tick { get; }
        public SimulationResetEvent(long tick) => Tick = tick;
    }
}