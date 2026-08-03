using UnityEngine;

namespace JajuchaSim.Core
{
    /// <summary>
    /// Camera layer configuration so sensor/ANN cameras never see debug overlays
    /// (trigger colors, selection outlines, grid, structure IDs).
    /// Observer cameras keep the full culling mask.
    /// </summary>
    public class CameraLayerConfig : MonoBehaviour
    {
        [Header("Layer Settings")]
        [Tooltip("Layer used for debug overlays (trigger regions, structure IDs, etc.)")]
        public int debugOverlayLayer = SimLayers.SimulatorDebug;

        [Tooltip("Layer used for observer camera (informational)")]
        public int observerCameraLayer = 0;

        [Header("Camera References")]
        [Tooltip("Observer camera that SHOULD see debug overlays")]
        public Camera ObserverCamera;

        [Tooltip("Sensor/ANN cameras that should NOT see debug overlays")]
        public Camera[] SensorCameras;

        private void Start()
        {
            ConfigureCameras();
        }

        /// <summary>Apply culling masks to observer and sensor cameras.</summary>
        public void ConfigureCameras()
        {
            if (ObserverCamera != null)
            {
                ObserverCamera.cullingMask = SimLayers.ObserverCullingMask;
                // Ensure the configured debug layer is included
                ObserverCamera.cullingMask |= (1 << debugOverlayLayer);
            }

            if (SensorCameras != null)
            {
                foreach (var cam in SensorCameras)
                {
                    if (cam == null) continue;
                    cam.cullingMask &= ~(1 << debugOverlayLayer);
                    // Also drop UI layer for cleanliness
                    cam.cullingMask &= ~(1 << 5);
                }
            }
        }

        /// <summary>Assign an object to the debug overlay layer.</summary>
        public static void SetDebugOverlayLayer(GameObject obj, int layer)
        {
            if (obj != null)
                obj.layer = layer;
        }
    }
}
