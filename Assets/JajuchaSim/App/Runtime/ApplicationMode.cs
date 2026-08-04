namespace JajuchaSim.App
{
    /// <summary>
    /// Explicit application modes (Step 11.9). Mode switching is explicit and
    /// never inferred from which UI panel happens to be visible.
    /// </summary>
    public enum ApplicationMode
    {
        /// <summary>Normal driving: observer camera follows, bridge active, HUD visible.</summary>
        Drive = 0,

        /// <summary>Map editing: simulation paused, propulsion stopped, editor camera active.</summary>
        MapEditor,

        /// <summary>Single automated test run: course reset, scoring active, result saved.</summary>
        SingleTest,

        /// <summary>Repeated reset/run cycle with automatic result collection.</summary>
        BatchTest
    }
}
