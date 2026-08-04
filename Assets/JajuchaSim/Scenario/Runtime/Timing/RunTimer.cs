using JajuchaSim.Core;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Official run timer driven by <see cref="SimulationClock"/> (Step 8.10/8.11).
    /// At 0.5×, 2×, 8× simulation speed the measured course time stays
    /// physically correct in simulated seconds because it is derived from ticks.
    /// </summary>
    public sealed class RunTimer
    {
        private readonly SimulationClock _clock;

        public long StartTick { get; private set; }
        public long EndTick { get; private set; }
        public double StartTime { get; private set; }
        public double EndTime { get; private set; }
        public bool IsRunning { get; private set; }

        public RunTimer(SimulationClock clock)
        {
            _clock = clock;
        }

        /// <summary>Start the timer at the current clock position.</summary>
        public void Start()
        {
            StartTick = _clock?.Tick ?? 0;
            StartTime = _clock?.Time ?? 0.0;
            EndTick = StartTick;
            EndTime = StartTime;
            IsRunning = true;
        }

        /// <summary>Stop the timer at the current clock position.</summary>
        public void Stop()
        {
            if (!IsRunning) return;
            EndTick = _clock?.Tick ?? StartTick;
            EndTime = _clock?.Time ?? StartTime;
            IsRunning = false;
        }

        /// <summary>
        /// Elapsed simulated seconds derived from ticks: <c>(EndTick - StartTick)
        /// * FixedDeltaTime</c>. While running, the live clock tick is used.
        ///
        /// Deriving from ticks (rather than subtracting clock times) is exact
        /// and deterministic at any simulation speed: each tick is one fixed
        /// timestep of simulated time regardless of wall-clock rate (Step 8.10).
        ///
        /// The product is computed in double and explicitly rounded to float32
        /// (the FixedDeltaTime precision) so tick multiples like 5000 × 0.01 s
        /// yield exactly 50.00 s across JITs that otherwise keep extended float
        /// precision (Mono/Unity, Step 8.57).
        /// </summary>
        public double ElapsedSimulationTime
        {
            get
            {
                long endTick = IsRunning ? (_clock?.Tick ?? EndTick) : EndTick;
                float dt = _clock?.FixedDeltaTime ?? 0f;
                return (float)((endTick - StartTick) * (double)dt);
            }
        }

        public void Reset()
        {
            StartTick = 0;
            EndTick = 0;
            StartTime = 0.0;
            EndTime = 0.0;
            IsRunning = false;
        }
    }
}
