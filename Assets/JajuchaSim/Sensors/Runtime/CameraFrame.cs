using System;

namespace JajuchaSim.Sensors
{
    /// <summary>
    /// Represents a single captured camera frame with metadata.
    ///
    /// This is the neutral representation that crosses the sensor → bridge
    /// boundary. Neither Unity textures nor RenderTexture handles are exposed
    /// here; the transport layer only sees raw bytes and metadata.
    /// </summary>
    public sealed class CameraFrame
    {
        /// <summary>Which camera captured this frame.</summary>
        public CameraLocation Location { get; }

        /// <summary>Monotonically increasing frame identifier.</summary>
        public long FrameId { get; }

        /// <summary>Simulation tick at which the frame was captured.</summary>
        public long SimulationTick { get; }

        /// <summary>Simulation time (seconds) at which the frame was captured.</summary>
        public double SimulationTime { get; }

        /// <summary>Image width in pixels.</summary>
        public int Width { get; }

        /// <summary>Image height in pixels.</summary>
        public int Height { get; }

        /// <summary>Raw pixel data (RGB24 order, tightly packed).</summary>
        public byte[] Data { get; }

        /// <summary>Pixel format of <see cref="Data"/>.</summary>
        public CameraOutputFormat Format { get; }

        public CameraFrame(
            CameraLocation location,
            long frameId,
            long simulationTick,
            double simulationTime,
            int width,
            int height,
            byte[] data,
            CameraOutputFormat format)
        {
            Location = location;
            FrameId = frameId;
            SimulationTick = simulationTick;
            SimulationTime = simulationTime;
            Width = width;
            Height = height;
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Format = format;
        }

        /// <summary>Returns the total number of bytes in the pixel data.</summary>
        public int DataLength => Data.Length;
    }
}
