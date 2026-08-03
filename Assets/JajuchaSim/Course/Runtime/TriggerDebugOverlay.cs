using UnityEngine;

namespace JajuchaSim.Course
{
    /// <summary>
    /// Settings + colour helpers for trigger-region debug overlays.
    /// Actual mesh rendering is handled by <see cref="CourseOverlayRenderer"/>;
    /// this component exposes tunables and layer assignment for the observer view.
    /// </summary>
    public class TriggerDebugOverlay : MonoBehaviour
    {
        [Header("References")]
        public MapEditorSession Session;
        public CourseGrid Grid;

        [Header("Visual Settings")]
        public Color slowZoneColor = new Color(1f, 1f, 0f, 0.3f);
        public Color startColor = new Color(0f, 1f, 0f, 0.3f);
        public Color finishColor = new Color(1f, 0f, 0f, 0.3f);
        public Color eventColor = new Color(0f, 0f, 1f, 0.3f);
        public Color speedGateColor = new Color(1f, 0f, 1f, 0.5f);

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
            if (Grid == null && Session != null)
                Grid = Session.Document.Grid;

            gameObject.layer = overlayLayer;
        }

        /// <summary>Get the color for a trigger type.</summary>
        public Color GetTriggerColor(TriggerType type)
        {
            switch (type)
            {
                case TriggerType.SlowZone: return slowZoneColor;
                case TriggerType.Start: return startColor;
                case TriggerType.Finish: return finishColor;
                case TriggerType.EventTrigger: return eventColor;
                case TriggerType.SpeedTerminal: return speedGateColor;
                default: return Color.clear;
            }
        }
    }
}
