using System;

namespace JajuchaSim.Sensors
{
    /// <summary>One deterministic horizontal lidar scan.</summary>
    public sealed class LidarScan
    {
        public long FrameId { get; }
        public long SimulationTick { get; }
        public double SimulationTime { get; }
        public int RayCount => DistancesCm.Length;
        public float AngleMinDeg { get; }
        public float AngleMaxDeg { get; }
        public float AngleIncrementDeg => RayCount > 1
            ? (AngleMaxDeg - AngleMinDeg) / (RayCount - 1)
            : 0f;
        public float MaxDistanceCm { get; }
        public float[] DistancesCm { get; }

        public LidarScan(long frameId, long simulationTick, double simulationTime,
            float angleMinDeg, float angleMaxDeg, float maxDistanceCm, float[] distancesCm)
        {
            if (distancesCm == null || distancesCm.Length < 1)
                throw new ArgumentException("A lidar scan must contain at least one ray.", nameof(distancesCm));

            FrameId = frameId;
            SimulationTick = simulationTick;
            SimulationTime = simulationTime;
            AngleMinDeg = angleMinDeg;
            AngleMaxDeg = angleMaxDeg;
            MaxDistanceCm = maxDistanceCm;
            DistancesCm = distancesCm;
        }

        public float DistanceAt(int index)
        {
            if (index < 0 || index >= DistancesCm.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return DistancesCm[index];
        }

        /// <summary>Returns tightly packed little-endian IEEE754 float32 data.</summary>
        public byte[] ToFloat32Bytes()
        {
            var data = new byte[DistancesCm.Length * sizeof(float)];
            Buffer.BlockCopy(DistancesCm, 0, data, 0, data.Length);
            return data;
        }
    }
}
