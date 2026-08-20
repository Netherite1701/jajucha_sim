using JajuchaSim.Course;

namespace JajuchaSim.Scenario
{
    public readonly struct StartLightSnapshot
    {
        public StartSignalState Phase { get; }
        public int LitLampCount { get; }
        public bool Released { get; }
        public bool BuzzerActive { get; }

        public StartLightSnapshot(StartSignalState phase, int litLampCount, bool buzzerActive)
        {
            Phase = phase;
            LitLampCount = litLampCount < 0 ? 0 : litLampCount > 4 ? 4 : litLampCount;
            Released = phase == StartSignalState.Released;
            BuzzerActive = buzzerActive;
        }
    }

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

    /// <summary>Published when the 2026 lamp count, release, or buzzer state changes.</summary>
    public readonly struct ScenarioSignalChangedEvent
    {
        public StartSignalState Signal { get; }
        public StartLightSnapshot Snapshot { get; }
        public ScenarioSignalChangedEvent(StartSignalState signal)
            : this(new StartLightSnapshot(signal, SignalLampCount(signal), false)) { }
        public ScenarioSignalChangedEvent(StartLightSnapshot snapshot)
        {
            Signal = snapshot.Phase;
            Snapshot = snapshot;
        }

        private static int SignalLampCount(StartSignalState state)
            => (int)state >= (int)StartSignalState.Lamp1 && (int)state <= (int)StartSignalState.Lamp4 ? (int)state : 0;
    }

    /// <summary>Published when a run finishes/aborts; carries the final session.</summary>
    public readonly struct ScenarioRunFinishedEvent
    {
        public RunSession Session { get; }
        public ScenarioRunFinishedEvent(RunSession session) => Session = session;
    }
}
