using System;

namespace JajuchaSim.Core
{
    /// <summary>
    /// Deterministic pseudo-random number source for all simulation logic.
    ///
    /// Uses a SplitMix64-based generator so the sequence is identical across
    /// .NET runtime versions and across editor/player builds. Core simulation
    /// code must never call <c>UnityEngine.Random</c>; it must draw randomness
    /// from <see cref="SimulationContext.Random"/>.
    ///
    /// Classification: SIMULATOR_ONLY. The real vehicle is not a PRNG; this
    /// abstraction exists purely to make noisy simulator effects deterministic.
    /// </summary>
    public sealed class SimulationRandom
    {
        private ulong _state;

        public ulong Seed { get; }

        public SimulationRandom(ulong seed)
        {
            Seed = seed;
            _state = seed;
        }

        /// <summary>Returns the next 64-bit unsigned integer.</summary>
        public ulong NextUInt64()
        {
            // SplitMix64 (by Sebastiano Vigna). Stable, well-known algorithm.
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public int NextInt()
        {
            // Collapse to signed 31-bit without bias.
            return (int)(NextUInt64() >> 33);
        }

        /// <summary>Returns an int in [minValue, maxValue].</summary>
        public int NextInt(int minValue, int maxValue)
        {
            if (maxValue < minValue)
                throw new ArgumentOutOfRangeException(nameof(maxValue), "maxValue must be >= minValue.");
            if (maxValue == minValue)
                return minValue;
            // Rejection-free unbiased range using 64-bit.
            ulong range = (ulong)(maxValue - minValue);
            ulong r = NextUInt64() % range;
            return minValue + (int)r;
        }

        /// <summary>Returns a float in [0.0, 1.0).</summary>
        public float NextFloat()
        {
            // 24 bits of mantissa for a uniform float in [0,1).
            return (NextUInt64() >> 40) * (1.0f / (1 << 24));
        }

        /// <summary>Resets the generator to its initial seed.</summary>
        public void Reset() => _state = Seed;
    }
}