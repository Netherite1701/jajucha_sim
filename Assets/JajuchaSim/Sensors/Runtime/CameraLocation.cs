namespace JajuchaSim.Sensors
{
    /// <summary>
    /// Identifies which physical camera on the Jajucha vehicle.
    /// Protocol strings are lower-case: "left", "center", "right".
    /// </summary>
    public enum CameraLocation
    {
        Left,
        Center,
        Right
    }

    /// <summary>
    /// Helpers for converting between <see cref="CameraLocation"/> and
    /// protocol string representations.
    /// </summary>
    public static class CameraLocationHelper
    {
        /// <summary>
        /// Returns the lower-case protocol string for a camera location.
        /// </summary>
        public static string ToProtocolString(CameraLocation location)
        {
            switch (location)
            {
                case CameraLocation.Left: return "left";
                case CameraLocation.Center: return "center";
                case CameraLocation.Right: return "right";
                default: return "unknown";
            }
        }

        /// <summary>
        /// Parses a protocol string into a <see cref="CameraLocation"/>.
        /// Returns null on invalid input.
        /// </summary>
        public static CameraLocation? FromProtocolString(string value)
        {
            switch (value)
            {
                case "left": return CameraLocation.Left;
                case "center": return CameraLocation.Center;
                case "right": return CameraLocation.Right;
                default: return null;
            }
        }
    }
}
