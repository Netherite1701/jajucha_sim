using UnityEngine;

namespace JajuchaSim.Sensors
{
    /// <summary>
    /// Configuration for a single Jajucha camera sensor.
    ///
    /// World scale: 1 Unity unit = 1 cm, so all length/clip values use centimetres.
    ///
    /// APPROXIMATION NOTES:
    ///   - Resolution defaults to 640x480 as a temporary development default.
    ///     The real Jajucha resolution is not yet known — measure with
    ///     image = jchm.camera.get_image("center"); print(image.shape)
    ///   - verticalFov defaults to 60°. Needs physical calibration.
    ///   - Physical mount position/rotation is in the scene Transform, not here.
    /// </summary>
    [CreateAssetMenu(fileName = "CameraConfig", menuName = "JajuchaSim/Camera Config", order = 10)]
    public sealed class CameraConfig : ScriptableObject
    {
        [Header("Resolution")]
        [Tooltip("Image width in pixels. APPROXIMATION / CONFIGURATION DEFAULT — measure real hardware.")]
        public int width = 640;

        [Tooltip("Image height in pixels. APPROXIMATION / CONFIGURATION DEFAULT — measure real hardware.")]
        public int height = 480;

        [Header("Optics")]
        [Tooltip("Vertical field of view in degrees. Needs physical calibration.")]
        [Range(1f, 179f)]
        public float verticalFov = 60f;

        [Tooltip("Near clipping plane distance in cm.")]
        public float nearClipCm = 1f;

        [Tooltip("Far clipping plane distance in cm.")]
        public float farClipCm = 1000f;

        [Header("Timing")]
        [Tooltip("Capture frame rate in frames per second.")]
        [Range(1f, 120f)]
        public float frameRate = 30f;

        [Header("Output")]
        [Tooltip("Pixel format for Unity render output. The Python side converts RGB→BGR.")]
        public CameraOutputFormat outputFormat = CameraOutputFormat.RGB24;

        [Header("Calibration")]
        [Tooltip("Lateral spacing between the left and right camera mount centers in cm. Existing model is 10 cm; provisional until the array is measured directly.")]
        public float arrayWidthCm = 10f;

        [Tooltip("Camera-array mount height above the vehicle reference plane in cm. Existing model is 4 cm; provisional.")]
        public float mountHeightCm = 4f;

        [Tooltip("Camera-array forward offset from the vehicle reference origin in cm. Existing model is 12 cm; provisional.")]
        public float mountForwardOffsetCm = 12f;

        [Tooltip("Lateral position of this camera in the array, in cm: left=-5, center=0, right=5 in the current provisional model.")]
        public float lateralOffsetCm;

        [Tooltip("True if the physical camera has been measured and this config reflects real values.")]
        public bool calibrated = false;

        [Tooltip("Free-text notes about this camera's calibration status.")]
        public string calibrationNotes = "APPROXIMATION — needs physical calibration";

        /// <summary>
        /// Frame interval in seconds derived from <see cref="frameRate"/>.
        /// </summary>
        public float FrameIntervalSec => 1f / Mathf.Max(frameRate, 0.001f);
    }

    /// <summary>
    /// Pixel format for camera output.
    /// </summary>
    public enum CameraOutputFormat
    {
        RGB24,
        Gray8
    }
}
