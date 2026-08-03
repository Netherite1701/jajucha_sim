using System;

namespace JajuchaSim.Core
{
    /// <summary>
    /// Authoritative simulation time source.
    ///
    /// The clock is the only thing that defines "simulation time".
    /// Rendering/Unity time (Time.deltaTime, Time.time) is deliberately separate;
    /// the scheduler feeds wall-clock delta time into a fixed-step accumulator
    /// and ticks the simulation at <see cref="FixedDeltaTime"/> resolution.
    ///
    /// 1 Unity unit = 1 centimeter (see project world-scale convention).
    /// Default FixedDeltaTime = 0.01 s (100 Hz).
    /// </summary>
    public sealed class SimulationClock
    {
        /// <summary>Fixed simulation timestep in seconds. Immutable per kernel run.</summary>
        public float FixedDeltaTime { get; }

        /// <summary>Number of simulation ticks that have elapsed.</summary>
        public long Tick { get; private set; }

        /// <summary>Total simulated time in seconds.</summary>
        public double Time { get; private set; }

        /// <summary>Multiplier applied to wall-clock delta time by the scheduler.
        /// Does not affect <see cref="Step"/>/<see cref="Advance"/> which always use real dt.</summary>
        public float TimeScale { get; private set; }

        /// <summary>True while the scheduler should not accumulate wall time.</summary>
        public bool IsPaused { get; private set; }

        public SimulationClock(float fixedDeltaTime)
        {
            if (fixedDeltaTime <= 0f || !float.IsFinite(fixedDeltaTime))
                throw new ArgumentOutOfRangeException(
                    nameof(fixedDeltaTime), "FixedDeltaTime must be a positive, finite value.");
            FixedDeltaTime = fixedDeltaTime;
            TimeScale = 1f;
            Tick = 0;
            Time = 0.0;
            IsPaused = false;
        }

        /// <summary>Advances exactly one tick by <see cref="FixedDeltaTime"/>.</summary>
        public void AdvanceOneTick()
        {
            Tick++;
            Time += FixedDeltaTime;
        }

        /// <summary>Advances <paramref name="tickCount"/> ticks.</summary>
        public void Advance(int tickCount)
        {
            if (tickCount < 0)
                throw new ArgumentOutOfRangeException(nameof(tickCount), "Cannot advance a negative number of ticks.");
            for (int i = 0; i < tickCount; i++)
                AdvanceOneTick();
        }

        public void SetTimeScale(float scale)
        {
            if (scale < 0f || !float.IsFinite(scale))
                throw new ArgumentOutOfRangeException(nameof(scale), "TimeScale must be non-negative and finite.");
            TimeScale = scale;
        }

        public void SetPaused(bool paused) => IsPaused = paused;

        /// <summary>Returns the clock to tick 0, time 0 (does not change time scale).</summary>
        public void Reset()
        {
            Tick = 0;
            Time = 0.0;
            IsPaused = false;
        }
    }
}