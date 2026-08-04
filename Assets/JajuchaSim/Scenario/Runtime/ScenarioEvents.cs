using JajuchaSim.Course;

namespace JajuchaSim.Scenario
{
    /// <summary>Published when the scenario state machine changes state.</summary>
    public readonly struct ScenarioStateChangedEvent
    {
        public ScenarioState State { get; }
        public StartSignalState Signal { get; }
        public ScenarioStateChangedEvent(ScenarioState state, StartSignalState signal)
        {
            State = state;
            Signal = signal;
        }
    }

    /// <summary>Published when the start signal changes (RED/YELLOW/GREEN/OFF).</summary>
    public readonly struct ScenarioSignalChangedEvent
    {
        public StartSignalState Signal { get; }
        public ScenarioSignalChangedEvent(StartSignalState signal) => Signal = signal;
    }

    /// <summary>Published when a run finishes/aborts; carries the final session.</summary>
    public readonly struct ScenarioRunFinishedEvent
    {
        public RunSession Session { get; }
        public ScenarioRunFinishedEvent(RunSession session) => Session = session;
    }
}
