namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Explicit scenario state machine (Step 8).
    ///
    /// Flow:
    ///   Idle → (prepare/reset) → Ready → (start requested) → Countdown
    ///       → (start signal GREEN) → Running → (finish) → Finished
    ///   Running → (abort) → Aborted
    ///
    /// The scenario state is separate from the kernel state
    /// (<see cref="JajuchaSim.Core.SimulationState"/>). The kernel may be
    /// running/paused while the scenario waits for a start.
    /// </summary>
    public enum ScenarioState
    {
        /// <summary>No scenario configured / before first prepare.</summary>
        Idle = 0,

        /// <summary>Run prepared; waiting for start request.</summary>
        Ready,

        /// <summary>Start sequence in progress (RED → YELLOW → GREEN).</summary>
        Countdown,

        /// <summary>Run is active; timer may be running.</summary>
        Running,

        /// <summary>Run completed (finish crossed or time limit reached).</summary>
        Finished,

        /// <summary>Run aborted by operator or rule.</summary>
        Aborted
    }
}
