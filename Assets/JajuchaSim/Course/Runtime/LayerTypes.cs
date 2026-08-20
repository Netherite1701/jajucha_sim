namespace JajuchaSim.Course
{
    /// <summary>
    /// Types of structures that can be placed on the Structure layer.
    /// Structures span one or more tiles on the shared grid.
    /// </summary>
    public enum StructureType
    {
        /// <summary>No structure.</summary>
        None = 0,
        /// <summary>Tunnel structure that passes over road tiles.</summary>
        Tunnel,
        /// <summary>Ramp that modifies the drivable surface elevation.</summary>
        Ramp
    }

    /// <summary>
    /// Types of objects that can be placed on the Object layer.
    /// Objects typically occupy a single tile.
    /// </summary>
    public enum ObjectType
    {
        /// <summary>No object.</summary>
        None = 0,
        /// <summary>Static obstacle blocking a tile.</summary>
        Obstacle,
        /// <summary>Warning or informational sign.</summary>
        Sign,
        /// <summary>2026 four-red-lamp start signal.</summary>
        StartSignal,
        /// <summary>2026 yellow-flag sign.</summary>
        YellowFlag,
        /// <summary>2026 PIT barrier sign.</summary>
        PitBarrier,
        /// <summary>2026 moving emergency-worker obstacle.</summary>
        DynamicObstacle
    }

    /// <summary>
    /// Types of trigger zones on the Trigger layer.
    /// Trigger zones span one or more tiles.
    /// </summary>
    public enum TriggerType
    {
        /// <summary>No trigger.</summary>
        None = 0,
        /// <summary>Slow zone — vehicle should reduce speed.</summary>
        SlowZone,
        /// <summary>
        /// Speed measurement terminal (paired A/B lines).
        /// Competition speed is d/(t2-t1) between a pair, not Rigidbody velocity.
        /// </summary>
        SpeedTerminal = 2,
        /// <summary>Obsolete alias for <see cref="SpeedTerminal"/> (same value).</summary>
        SpeedGate = SpeedTerminal,
        /// <summary>Generic event trigger for custom scenarios.</summary>
        EventTrigger,
        /// <summary>Start line / race start trigger.</summary>
        Start,
        /// <summary>Finish line / race end trigger.</summary>
        Finish
    }

    /// <summary>
    /// Predefined obstacle footprint sizes.
    /// </summary>
    public enum ObstacleFootprint
    {
        /// <summary>1x1 tile (default small obstacle)</summary>
        Small = 0,
        /// <summary>2x1 tiles (wide obstacle)</summary>
        Wide,
        /// <summary>3x1 tiles (barrier)</summary>
        Barrier
    }
}
