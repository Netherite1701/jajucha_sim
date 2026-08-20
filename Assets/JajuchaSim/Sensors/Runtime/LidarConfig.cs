using UnityEngine;

namespace JajuchaSim.Sensors
{
    /// <summary>
    /// Runtime configuration for the horizontal vehicle lidar.
    ///
    /// Distances use the simulator's centimetre world convention (1 Unity
    /// unit = 1 cm).  The defaults are simulator training values and are
    /// intentionally exposed so a measured vehicle profile can replace them.
    /// </summary>
    [CreateAssetMenu(fileName = "LidarConfig", menuName = "JajuchaSim/Lidar Config", order = 11)]
    public sealed class LidarConfig : ScriptableObject
    {
        [Header("Scan geometry")]
        [Min(3)]
        // The physical Jajucha API exposes a full horizontal scan.  360 rays
        // keeps the simulator deterministic while matching the manual's
        // 0..360 degree indexing convention.
        public int rayCount = 360;

        [Range(1f, 360f)]
        public float horizontalFovDeg = 360f;

        [Min(1f)]
        public float maxDistanceCm = 1000f;

        [Min(0f)]
        public float minDistanceCm = 1f;

        [Header("Mount (vehicle-local cm)")]
        public Vector3 mountPosition = new Vector3(0f, 6f, 10f);

        [Header("Timing")]
        [Range(1f, 120f)]
        public float scanRateHz = 20f;

        [Tooltip("Optional physics layer mask. Default 0 means all layers.")]
        public LayerMask layerMask;

        public float FrameIntervalSec => 1f / Mathf.Max(scanRateHz, 0.001f);

        public int ClampedRayCount => Mathf.Clamp(rayCount, 3, 4096);

        public float ClampedFovDeg => Mathf.Clamp(horizontalFovDeg, 1f, 360f);

        public float ClampedMaxDistanceCm => Mathf.Max(minDistanceCm + 0.001f, maxDistanceCm);

        public float ClampedMinDistanceCm => Mathf.Clamp(minDistanceCm, 0f, ClampedMaxDistanceCm - 0.001f);

        public int EffectiveLayerMask => layerMask.value == 0 ? Physics.DefaultRaycastLayers : layerMask.value;
    }
}
