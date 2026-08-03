namespace JajuchaSim.Core
{
    /// <summary>
    /// Well-known Unity layers used by the simulator.
    ///
    /// Layer 6 = SimulatorDebug — overlays, trigger regions, selection outlines,
    /// grid lines, structure IDs. Visible only to the observer camera.
    /// Sensor cameras (Jajucha left/center/right) MUST exclude this layer so the
    /// ANN never sees debug decorations.
    /// </summary>
    public static class SimLayers
    {
        /// <summary>Unity layer index for simulator debug overlays.</summary>
        public const int SimulatorDebug = 6;

        /// <summary>Layer mask containing only the SimulatorDebug layer.</summary>
        public const int SimulatorDebugMask = 1 << SimulatorDebug;

        /// <summary>
        /// Default culling mask for sensor cameras: everything except
        /// SimulatorDebug and UI (layer 5).
        /// </summary>
        public const int SensorCullingMask = ~((1 << SimulatorDebug) | (1 << 5));

        /// <summary>
        /// Default culling mask for the observer/free camera: everything.
        /// </summary>
        public const int ObserverCullingMask = ~0;
    }
}
