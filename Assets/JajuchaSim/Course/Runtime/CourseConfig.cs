using UnityEngine;

namespace JajuchaSim.Course
{
    /// <summary>
    /// Configuration for the course tile grid.
    ///
    /// Tile size is stored in physical centimetres (matching the world-scale
    /// convention: 1 Unity unit = 1 cm). All course features snap to this grid.
    /// </summary>
    [CreateAssetMenu(fileName = "CourseConfig", menuName = "JajuchaSim/Course Config", order = 110)]
    public sealed class CourseConfig : ScriptableObject
    {
        [Tooltip("Tile size in centimetres. Every grid cell is tileSizeCm × tileSizeCm. Default 20 cm.")]
        [Min(1f)]
        public float tileSizeCm = 20f;
    }
}
