using UnityEngine;

namespace JajuchaSim.Course
{
    /// <summary>
    /// Settings for structure-ID debug labels.
    /// Rendering is performed by <see cref="CourseOverlayRenderer"/> when
    /// <see cref="MapEditorSession.ShowStructureIds"/> is enabled.
    /// Lives on the SimulatorDebug layer (observer only).
    /// </summary>
    public class StructureDebugOverlay : MonoBehaviour
    {
        [Header("References")]
        public MapEditorSession Session;
        public CourseDocument Document;

        [Header("Visual Settings")]
        public float textHeight = 60f; // cm above structure
        public Color textColor = Color.white;
        public float textSize = 5f; // cm

        [Header("Layer")]
        public int overlayLayer = JajuchaSim.Core.SimLayers.SimulatorDebug;

        private void Start()
        {
            if (Session == null)
            {
                var beh = FindFirstObjectByType<MapEditorSessionBehaviour>();
                if (beh != null)
                    Session = beh.Session;
            }
            if (Document == null && Session != null)
                Document = Session.Document;

            gameObject.layer = overlayLayer;
        }
    }
}
