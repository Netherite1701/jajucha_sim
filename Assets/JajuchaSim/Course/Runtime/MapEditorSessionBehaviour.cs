using UnityEngine;

namespace JajuchaSim.Course
{
    /// <summary>
    /// MonoBehaviour wrapper for <see cref="MapEditorSession"/> so the session
    /// participates in the Unity lifecycle and is discoverable at runtime
    /// (including standalone builds).
    /// </summary>
    public class MapEditorSessionBehaviour : MonoBehaviour
    {
        [Header("Configuration")]
        public CourseConfig Config;
        public float tileSizeCm = 20f;

        /// <summary>Active editor session. Created in Awake.</summary>
        public MapEditorSession Session { get; private set; }

        private void Awake()
        {
            if (Config != null)
                tileSizeCm = Config.tileSizeCm;

            Session = new MapEditorSession(new CourseDocument(tileSizeCm));
        }

        private void OnDestroy()
        {
            Session = null;
        }
    }
}
