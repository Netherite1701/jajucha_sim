using System;
using JajuchaSim.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace JajuchaSim.Sensors
{
    /// <summary>
    /// A single camera sensor for the Jajucha vehicle. Owns a Unity Camera
    /// component and its RenderTexture, handles scheduled rendering, and
    /// provides the most recently completed frame via <see cref="LatestFrame"/>.
    ///
    /// Architecture:
    ///   - camera.enabled = false — we call Render() explicitly when a frame is due
    ///   - Uses AsyncGPUReadback for non-blocking GPU readback when available,
    ///     falling back to synchronous ReadPixels for editor/testing
    ///   - Maintains at most 1 in-flight capture per camera
    ///   - Frame metadata is recorded at capture-request time, not readback-completion time
    ///
    /// The sensor outputs raw RGB24 pixel data. The Python bridge layer converts
    /// RGB → BGR for OpenCV compatibility.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class JajuchaCameraSensor : MonoBehaviour
    {
        [SerializeField] private CameraLocation _location = CameraLocation.Center;
        [SerializeField] private CameraConfig _config;

        // Unity camera
        private Camera _camera;
        private RenderTexture _renderTexture;
        private bool _hasCamera;

        // Depth rendering
        private RenderTexture _depthRenderTexture;
        private Material _depthMaterial;
        private Texture2D _depthReadTexture;
        private CameraFrame _latestDepthFrame;
        private readonly object _depthFrameLock = new object();
        private long _depthFrameId;
        private bool _hasPendingDepthReadback;
        private bool _awaitingDepthReadback;
        private bool _depthEnabled;

        // Frame tracking
        private long _frameId;
        private long _captureTick;
        private double _captureTime;
        private CameraFrame _latestFrame;
        private readonly object _frameLock = new object();

        // In-flight capture tracking
        private AsyncGPUReadbackRequest _pendingRequest;
        private bool _hasPendingReadback;
        private bool _awaitingReadback;

        // Dropped frame counter
        private int _droppedFrames;

        // Synchronous fallback texture (for ReadPixels path)
        private Texture2D _readTexture;

        public CameraLocation Location => _location;
        public Camera UnityCamera => _camera;
        public int DroppedFrames => _droppedFrames;
        public long FrameId => _frameId;

        /// <summary>
        /// The most recently completed frame. May be null before first capture.
        /// Thread-safe for reading from the bridge thread.
        /// </summary>
        public CameraFrame LatestFrame
        {
            get
            {
                lock (_frameLock)
                {
                    return _latestFrame;
                }
            }
        }

        /// <summary>
        /// The most recently completed depth frame. May be null before first capture.
        /// Only available when depth rendering is enabled (typically center camera).
        /// Thread-safe for reading from the bridge thread.
        /// </summary>
        public CameraFrame LatestDepthFrame
        {
            get
            {
                lock (_depthFrameLock)
                {
                    return _latestDepthFrame;
                }
            }
        }

        /// <summary>
        /// Whether depth rendering is enabled for this sensor.
        /// </summary>
        public bool DepthEnabled => _depthEnabled;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                SimLog.Error($"[SENSOR] JajuchaCameraSensor at '{gameObject.name}': no Camera component found.");
                return;
            }

            if (_config == null)
            {
                SimLog.Warning($"[SENSOR] JajuchaCameraSensor at '{gameObject.name}': no CameraConfig assigned, using defaults.");
                _config = ScriptableObject.CreateInstance<CameraConfig>();
            }

            SetupCamera();
            _hasCamera = true;
        }

        private void SetupCamera()
        {
            // Create RenderTexture at configured resolution
            _renderTexture = new RenderTexture(_config.width, _config.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = $"{gameObject.name}_RT",
                autoGenerateMips = false,
                useMipMap = false,
                antiAliasing = 1,
                anisoLevel = 0,
                useDynamicScale = false,
                dimension = TextureDimension.Tex2D,
                volumeDepth = 1
            };
            _renderTexture.Create();

            // Configure the Unity Camera
            _camera.enabled = false; // We control rendering manually
            _camera.targetTexture = _renderTexture;
            _camera.rect = new Rect(0f, 0f, 1f, 1f);
            _camera.fieldOfView = _config.verticalFov;
            // The physical cameras look slightly down toward the lane.  The
            // previous zero-pitch pose put the road edge/grass in the middle
            // of the sensor image, which made the feed look as if it were
            // seeing through the road. Keep the calibration configurable but
            // apply a safe downward default from CameraConfig.
            _camera.transform.localRotation = Quaternion.Euler(
                Mathf.Clamp(_config.pitchDownDeg, 0f, 45f), 0f, 0f);
            _camera.nearClipPlane = _config.nearClipCm; // 1 unit = 1 cm
            _camera.farClipPlane = _config.farClipCm;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.allowMSAA = false;
            _camera.allowHDR = false;
            _camera.useOcclusionCulling = false;

            // Sensor cameras must NEVER see simulator debug overlays
            // (trigger colors, selection outlines, grid, structure IDs).
            // Only the observer camera uses the full culling mask.
            _camera.cullingMask = SimLayers.SensorCullingMask;

            // Set rendering path for deterministic output
            _camera.renderingPath = RenderingPath.Forward;
        }

        private void OnDestroy()
        {
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                if (!Application.isPlaying)
                    DestroyImmediate(_renderTexture);
                else
                    Destroy(_renderTexture);
                _renderTexture = null;
            }

            if (_depthRenderTexture != null)
            {
                _depthRenderTexture.Release();
                if (!Application.isPlaying)
                    DestroyImmediate(_depthRenderTexture);
                else
                    Destroy(_depthRenderTexture);
                _depthRenderTexture = null;
            }

            if (_readTexture != null)
            {
                if (!Application.isPlaying)
                    DestroyImmediate(_readTexture);
                else
                    Destroy(_readTexture);
                _readTexture = null;
            }

            if (_depthReadTexture != null)
            {
                if (!Application.isPlaying)
                    DestroyImmediate(_depthReadTexture);
                else
                    Destroy(_depthReadTexture);
                _depthReadTexture = null;
            }

            if (_depthMaterial != null)
            {
                if (!Application.isPlaying)
                    DestroyImmediate(_depthMaterial);
                else
                    Destroy(_depthMaterial);
                _depthMaterial = null;
            }
        }

        /// <summary>
        /// Requests a new frame capture. Called by the sensor system when the
        /// scheduler determines a frame is due.
        ///
        /// If a previous capture is still in-flight (readback pending), this
        /// frame is skipped and <see cref="DroppedFrames"/> is incremented.
        /// </summary>
        public void RequestCapture(long simulationTick, double simulationTime)
        {
            if (!_hasCamera)
                return;

            if (_hasPendingReadback || _awaitingReadback)
            {
                _droppedFrames++;
                return;
            }

            // Record metadata at capture time (not readback time)
            _captureTick = simulationTick;
            _captureTime = simulationTime;

            // Render the camera
            _camera.Render();

            // Editor tests use a synchronous path for deterministic readback;
            // standalone builds use RGBA32 async readback and normalize the
            // rows below without stalling the simulation thread.
            if (Application.isEditor)
            {
                CaptureSync();
            }
            else if (SystemInfo.supportsAsyncGPUReadback)
            {
                _awaitingReadback = true;
                // Read back the native 32-bit render target.  Requesting RGB24
                // directly is not supported consistently by all Windows
                // graphics backends and can return a half-height/strided
                // buffer.  We strip alpha in OnAsyncReadback instead.
                _pendingRequest = AsyncGPUReadback.Request(
                    _renderTexture, 0, TextureFormat.RGBA32, OnAsyncReadback);
            }
            else
            {
                // Synchronous fallback for editor/testing
                CaptureSync();
            }
        }

        /// <summary>
        /// Synchronous readback fallback using ReadPixels.
        /// </summary>
        private void CaptureSync()
        {
            if (_readTexture == null || _readTexture.width != _config.width || _readTexture.height != _config.height)
            {
                if (_readTexture != null)
                {
                    if (Application.isPlaying)
                        Destroy(_readTexture);
                    else
                        DestroyImmediate(_readTexture);
                }
                _readTexture = new Texture2D(_config.width, _config.height, TextureFormat.RGB24, false)
                {
                    name = $"{gameObject.name}_ReadTex"
                };
            }

            RenderTexture previousRT = RenderTexture.active;
            RenderTexture.active = _renderTexture;

            try
            {
                _readTexture.ReadPixels(new Rect(0, 0, _config.width, _config.height), 0, 0);
                _readTexture.Apply();

                byte[] data = _readTexture.GetRawTextureData();
                // Unity Texture2D data is bottom-left origin while the JCHM
                // image API (and OpenCV/manual examples) use top-left origin.
                FlipRows(data, _config.width, _config.height, 3);
                PublishFrame(data);
            }
            finally
            {
                RenderTexture.active = previousRT;
            }
        }

        /// <summary>
        /// Callback for AsyncGPUReadback completion.
        /// </summary>
        private void OnAsyncReadback(AsyncGPUReadbackRequest request)
        {
            _awaitingReadback = false;

            if (request.hasError)
            {
                _droppedFrames++;
                SimLog.Warning($"[SENSOR] AsyncGPUReadback error on '{gameObject.name}' camera");
                return;
            }

            if (request.done && request.layerCount > 0)
            {
                var data = request.GetData<byte>();
                if (data.Length > 0)
                {
                    int pixelCount = _config.width * _config.height;
                    if (data.Length == pixelCount * 4)
                    {
                        byte[] rgba = new byte[data.Length];
                        data.CopyTo(rgba);
                        byte[] rgb = new byte[pixelCount * 3];
                        for (int src = 0, dst = 0; src < rgba.Length; src += 4)
                        {
                            rgb[dst++] = rgba[src];
                            rgb[dst++] = rgba[src + 1];
                            rgb[dst++] = rgba[src + 2];
                        }
                        FlipRows(rgb, _config.width, _config.height, 3);
                        PublishFrame(rgb);
                    }
                    else
                    {
                        byte[] managedData = new byte[data.Length];
                        data.CopyTo(managedData);
                        if (data.Length == _config.width * _config.height * 3)
                            FlipRows(managedData, _config.width, _config.height, 3);
                        PublishFrame(managedData);
                    }
                }
            }
        }

        private static void FlipRows(byte[] data, int width, int height, int bytesPerPixel)
        {
            if (data == null || width <= 0 || height <= 1 || bytesPerPixel <= 0) return;
            int rowBytes = width * bytesPerPixel;
            if (data.Length < rowBytes * height) return;
            var scratch = new byte[rowBytes];
            for (int y = 0; y < height / 2; y++)
            {
                int top = y * rowBytes;
                int bottom = (height - 1 - y) * rowBytes;
                Buffer.BlockCopy(data, top, scratch, 0, rowBytes);
                Buffer.BlockCopy(data, bottom, data, top, rowBytes);
                Buffer.BlockCopy(scratch, 0, data, bottom, rowBytes);
            }
        }

        /// <summary>
        /// Publishes a completed frame, making it available as <see cref="LatestFrame"/>.
        /// </summary>
        private void PublishFrame(byte[] pixelData)
        {
            _frameId++;

            var frame = new CameraFrame(
                _location,
                _frameId,
                _captureTick,
                _captureTime,
                _config.width,
                _config.height,
                pixelData,
                CameraOutputFormat.RGB24
            );

            lock (_frameLock)
            {
                _latestFrame = frame;
            }

            _hasPendingReadback = false;
        }

        /// <summary>
        /// Resets the sensor to initial state.
        /// </summary>
        public void ResetSensor()
        {
            _frameId = 0;
            _depthFrameId = 0;
            _droppedFrames = 0;
            _hasPendingReadback = false;
            _awaitingReadback = false;
            _hasPendingDepthReadback = false;
            _awaitingDepthReadback = false;

            lock (_frameLock)
            {
                _latestFrame = null;
            }

            lock (_depthFrameLock)
            {
                _latestDepthFrame = null;
            }
        }

        /// <summary>
        /// Ensures at least one initial frame exists. Call after the scene
        /// is fully set up to capture the first deterministic frame.
        /// </summary>
        public void CaptureInitialFrame(long simulationTick, double simulationTime)
        {
            if (!_hasCamera)
                return;

            // Force immediate synchronous capture for the first frame
            // so that get_image() never blocks on startup.
            _captureTick = simulationTick;
            _captureTime = simulationTime;
            _camera.Render();
            CaptureSync();

            // Also capture initial depth frame if depth is enabled
            if (_depthEnabled)
            {
                RenderDepthTexture();
                CaptureDepthSync(simulationTick, simulationTime);
            }
        }

        /// <summary>
        /// Enables depth rendering for this sensor.
        /// Creates the depth RenderTexture and material.
        /// </summary>
        public void EnableDepthRendering()
        {
            if (!_hasCamera || _depthEnabled)
                return;

            // Create depth RenderTexture
            _depthRenderTexture = new RenderTexture(_config.width, _config.height, 24, RenderTextureFormat.ARGB32)
            {
                name = $"{gameObject.name}_DepthRT",
                autoGenerateMips = false,
                useMipMap = false,
                antiAliasing = 1,
                anisoLevel = 0
            };
            _depthRenderTexture.Create();

            // Load depth shader
            Shader depthShader = Shader.Find("Hidden/JajuchaSim/DepthGrayscale");
            if (depthShader == null)
            {
                SimLog.Error($"[SENSOR] Depth shader not found at '{gameObject.name}'");
                return;
            }

            _depthMaterial = new Material(depthShader);
            _depthEnabled = true;
        }

        /// <summary>
        /// Captures a depth frame using the depth shader.
        /// </summary>
        public void RequestDepthCapture(long simulationTick, double simulationTime)
        {
            if (!_hasCamera || !_depthEnabled)
                return;

            if (_hasPendingDepthReadback || _awaitingDepthReadback)
            {
                return;
            }

            RenderDepthTexture();

            // Match the camera path: editor tests use synchronous readback;
            // standalone builds use RGBA32 async readback.
            if (Application.isEditor)
            {
                CaptureDepthSync(simulationTick, simulationTime);
            }
            else if (SystemInfo.supportsAsyncGPUReadback)
            {
                _awaitingDepthReadback = true;
                AsyncGPUReadback.Request(_depthRenderTexture, 0, TextureFormat.RGB24, request => OnDepthAsyncReadback(request, simulationTick, simulationTime));
            }
            else
            {
                CaptureDepthSync(simulationTick, simulationTime);
            }
        }

        /// <summary>
        /// Render the replacement depth shader into the depth RenderTexture.
        /// RenderWithShader uses the replacement shader directly, so the
        /// distance parameters must be global shader properties as well as
        /// material properties; otherwise the shader sees the zero vector and
        /// every pixel clamps to black.
        /// </summary>
        private void RenderDepthTexture()
        {
            if (_camera == null || _depthRenderTexture == null || _depthMaterial == null)
                return;

            Vector3 cameraPosition = _camera.transform.position;
            _depthMaterial.SetVector("_CameraWorldPos", cameraPosition);
            _depthMaterial.SetFloat("_NearDistance", _config.nearClipCm);
            _depthMaterial.SetFloat("_FarDistance", _config.farClipCm);
            Shader.SetGlobalVector("_CameraWorldPos", cameraPosition);
            Shader.SetGlobalFloat("_NearDistance", _config.nearClipCm);
            Shader.SetGlobalFloat("_FarDistance", _config.farClipCm);

            RenderTexture previousTarget = _camera.targetTexture;
            _camera.targetTexture = _depthRenderTexture;
            _camera.RenderWithShader(_depthMaterial.shader, "");
            _camera.targetTexture = previousTarget;
        }

        /// <summary>
        /// Synchronous depth readback fallback.
        /// </summary>
        private void CaptureDepthSync(long simulationTick, double simulationTime)
        {
            if (_depthReadTexture == null || _depthReadTexture.width != _config.width || _depthReadTexture.height != _config.height)
            {
                if (_depthReadTexture != null)
                {
                    if (Application.isPlaying)
                        Destroy(_depthReadTexture);
                    else
                        DestroyImmediate(_depthReadTexture);
                }
                _depthReadTexture = new Texture2D(_config.width, _config.height, TextureFormat.RGB24, false)
                {
                    name = $"{gameObject.name}_DepthReadTex"
                };
            }

            RenderTexture previousRT = RenderTexture.active;
            RenderTexture.active = _depthRenderTexture;

            try
            {
                _depthReadTexture.ReadPixels(new Rect(0, 0, _config.width, _config.height), 0, 0);
                _depthReadTexture.Apply();

                // Convert RGB to grayscale (R=G=B in depth shader output)
                Color[] pixels = _depthReadTexture.GetPixels();
                byte[] depthData = new byte[_config.width * _config.height];
                for (int i = 0; i < pixels.Length; i++)
                {
                    depthData[i] = (byte)(pixels[i].r * 255f);
                }

                FlipRows(depthData, _config.width, _config.height, 1);

                PublishDepthFrame(depthData, simulationTick, simulationTime);
            }
            finally
            {
                RenderTexture.active = previousRT;
            }
        }

        /// <summary>
        /// Callback for async depth readback completion.
        /// </summary>
        private void OnDepthAsyncReadback(AsyncGPUReadbackRequest request, long simulationTick, double simulationTime)
        {
            _awaitingDepthReadback = false;

            if (request.hasError)
            {
                SimLog.Warning($"[SENSOR] AsyncGPUReadback error for depth on '{gameObject.name}' camera");
                return;
            }

            if (request.done && request.layerCount > 0)
            {
                var data = request.GetData<byte>();
                if (data.Length > 0)
                {
                    // Convert RGB to grayscale (R=G=B in depth shader output)
                    byte[] depthData = new byte[_config.width * _config.height];
                    for (int i = 0; i < depthData.Length; i++)
                    {
                        int srcIdx = i * 3; // RGB format
                        if (srcIdx < data.Length)
                        {
                            depthData[i] = data[srcIdx]; // R channel (R=G=B)
                        }
                    }

                    FlipRows(depthData, _config.width, _config.height, 1);

                    PublishDepthFrame(depthData, simulationTick, simulationTime);
                }
            }
        }

        /// <summary>
        /// Publishes a completed depth frame.
        /// </summary>
        private void PublishDepthFrame(byte[] depthData, long simulationTick, double simulationTime)
        {
            _depthFrameId++;

            var frame = new CameraFrame(
                _location,
                _depthFrameId,
                simulationTick,
                simulationTime,
                _config.width,
                _config.height,
                depthData,
                CameraOutputFormat.Gray8
            );

            lock (_depthFrameLock)
            {
                _latestDepthFrame = frame;
            }

            _hasPendingDepthReadback = false;
        }
    }
}
