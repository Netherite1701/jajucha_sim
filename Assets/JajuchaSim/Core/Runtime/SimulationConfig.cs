using UnityEngine;

namespace JajuchaSim.Core
{
    /// <summary>
    /// Configuration for the simulation kernel. This is a ScriptableObject so
    /// values are not scattered as hard-coded constants across the project.
    ///
    /// World scale: 1 Unity unit = 1 centimeter. Gravity default = -981 cm/s^2
    /// (see project convention; not a value stored here).
    /// </summary>
    [CreateAssetMenu(fileName = "SimulationConfig", menuName = "JajuchaSim/Simulation Config", order = 0)]
    public sealed class SimulationConfig : ScriptableObject
    {
        [Tooltip("Fixed simulation timestep in seconds. 100 Hz = 0.01. Must be > 0.")]
        public float fixedDeltaTime = 0.01f;

        [Tooltip("Default simulation speed multiplier applied to wall-clock time.")]
        [Range(0f, 64f)]
        public float defaultTimeScale = 1f;

        [Tooltip("Seed for the deterministic PRNG. Stored as long because Unity's serializer does not serialize ulong.")]
        public long randomSeed = 12345L;

        [Tooltip("Hard cap on ticks processed in a single frame to prevent a spiral-of-death.")]
        [Min(1)]
        public int maxTicksPerFrame = 100;

        [Tooltip("If true, the manager will attempt to start the simulation immediately after initialize.")]
        public bool autoStart = false;
    }
}