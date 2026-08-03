using System;
using System.Collections.Generic;
using JajuchaSim.Core;
using JajuchaSim.Vehicle;
using UnityEngine;

namespace JajuchaSim.Sensors
{
    /// <summary>
    /// Simulation system that owns three physical camera sensors (left, center,
    /// right) on the Jajucha vehicle, manages capture scheduling independently
    /// of the physics tick rate, and provides the most recent frame for each
    /// camera on demand.
    ///
    /// This is the main entry point for the Bridge and test code to retrieve
    /// camera frames.
    /// </summary>
    public class CameraSensorSystem : ISimulationSystem
    {
        private readonly VehicleSystem _vehicleSystem;
        private readonly CameraConfig _leftConfig;
        private readonly CameraConfig _centerConfig;
        private readonly CameraConfig _rightConfig;

        // The three camera sensors
        private JajuchaCameraSensor _leftCamera;
        private JajuchaCameraSensor _centerCamera;
        private JajuchaCameraSensor _rightCamera;

        // Per-camera scheduling
        private CameraCaptureScheduler _leftScheduler;
        private CameraCaptureScheduler _centerScheduler;
        private CameraCaptureScheduler _rightScheduler;

        // Camera mount GameObjects
        private GameObject _leftMount;
        private GameObject _centerMount;
        private GameObject _rightMount;

        // Whether the system has been initialized
        private bool _initialized;
        private SimulationContext _context;

        // Frame tracking
        private bool _initialFramesCaptured;

        public JajuchaCameraSensor LeftCamera => _leftCamera;
        public JajuchaCameraSensor CenterCamera => _centerCamera;
        public JajuchaCameraSensor RightCamera => _rightCamera;

        public CameraSensorSystem(
            VehicleSystem vehicleSystem,
            CameraConfig leftConfig,
            CameraConfig centerConfig,
            CameraConfig rightConfig)
        {
            _vehicleSystem = vehicleSystem ?? throw new ArgumentNullException(nameof(vehicleSystem));
            _leftConfig = leftConfig ?? throw new ArgumentNullException(nameof(leftConfig));
            _centerConfig = centerConfig ?? throw new ArgumentNullException(nameof(centerConfig));
            _rightConfig = rightConfig ?? throw new ArgumentNullException(nameof(rightConfig));
        }

        /// <summary>
        /// Protected parameterless constructor for testing/mocking.
        /// Does not initialize any fields; override <see cref="GetLatestFrame"/> etc.
        /// </summary>
        protected CameraSensorSystem()
        {
        }

        public void Initialize(SimulationContext context)
        {
            _context = context;
            CreateCameras();
            _initialized = true;
        }

        public void SimulationTick(float deltaTime)
        {
            if (!_initialized || _context == null)
                return;

            // Capture initial frames on first tick after scene is ready
            if (!_initialFramesCaptured)
            {
                CaptureInitialFrames();
                _initialFramesCaptured = true;
            }

            // Advance each camera's scheduler and request captures as needed
            if (_leftCamera != null && _leftScheduler.Advance(deltaTime, out _))
            {
                _leftCamera.RequestCapture(_context.Clock.Tick, _context.Clock.Time);
            }

            if (_centerCamera != null && _centerScheduler.Advance(deltaTime, out _))
            {
                _centerCamera.RequestCapture(_context.Clock.Tick, _context.Clock.Time);
                // Also capture depth frame for center camera
                _centerCamera.RequestDepthCapture(_context.Clock.Tick, _context.Clock.Time);
            }

            if (_rightCamera != null && _rightScheduler.Advance(deltaTime, out _))
            {
                _rightCamera.RequestCapture(_context.Clock.Tick, _context.Clock.Time);
            }
        }

        public void ResetSimulation()
        {
            _initialFramesCaptured = false;

            _leftCamera?.ResetSensor();
            _centerCamera?.ResetSensor();
            _rightCamera?.ResetSensor();

            _leftScheduler?.Reset();
            _centerScheduler?.Reset();
            _rightScheduler?.Reset();
        }

        public void Shutdown()
        {
            // Cameras are MonoBehaviour; they are destroyed with their GameObjects.
            _initialized = false;
        }

        /// <summary>
        /// Retrieves the latest completed frame for the given camera location.
        /// Returns null if no frame has been captured yet.
        /// </summary>
        public virtual CameraFrame GetLatestFrame(CameraLocation location)
        {
            switch (location)
            {
                case CameraLocation.Left: return _leftCamera?.LatestFrame;
                case CameraLocation.Center: return _centerCamera?.LatestFrame;
                case CameraLocation.Right: return _rightCamera?.LatestFrame;
                default: return null;
            }
        }

        /// <summary>
        /// Retrieves the latest completed depth frame from the center camera.
        /// Returns null if no depth frame has been captured yet.
        /// </summary>
        public virtual CameraFrame GetLatestDepthFrame()
        {
            return _centerCamera?.LatestDepthFrame;
        }

        /// <summary>
        /// Validates that a camera location string is one of "left", "center", "right".
        /// Throws <see cref="ArgumentException"/> if invalid.
        /// </summary>
        public static void ValidateLocation(string location)
        {
            if (string.IsNullOrEmpty(location))
                throw new ArgumentException("Camera location must not be null or empty.", nameof(location));

            var parsed = CameraLocationHelper.FromProtocolString(location);
            if (parsed == null)
            {
                throw new ArgumentException(
                    $"Camera location must be one of: 'left', 'center', 'right'. Got '{location}'.",
                    nameof(location));
            }
        }

        /// <summary>
        /// Parses a location string to a <see cref="CameraLocation"/>.
        /// Throws if invalid.
        /// </summary>
        public static CameraLocation ParseLocation(string location)
        {
            ValidateLocation(location);
            return CameraLocationHelper.FromProtocolString(location).Value;
        }

        // --- Private helpers ---

        private void CreateCameras()
        {
            var vehicleRoot = _vehicleSystem.VehicleRoot;
            if (vehicleRoot == null)
            {
                SimLog.Error("[SENSOR] CameraSensorSystem: VehicleSystem has no root GameObject.");
                return;
            }

            // Create a Sensors container under the vehicle
            var sensorsRoot = new GameObject("Sensors");
            sensorsRoot.transform.SetParent(vehicleRoot.transform, false);

            // Create mounts with independently editable transforms
            _leftMount = CreateCameraMount(sensorsRoot, "CameraLeftMount", new Vector3(-10f, 5f, 5f));
            _rightMount = CreateCameraMount(sensorsRoot, "CameraRightMount", new Vector3(10f, 5f, 5f));
            _centerMount = CreateCameraMount(sensorsRoot, "CameraCenterMount", new Vector3(0f, 5f, 10f));

            // Create camera sensors as children of their mounts
            _leftCamera = CreateCameraSensor(_leftMount, "CameraLeft", CameraLocation.Left, _leftConfig);
            _centerCamera = CreateCameraSensor(_centerMount, "CameraCenter", CameraLocation.Center, _centerConfig);
            _rightCamera = CreateCameraSensor(_rightMount, "CameraRight", CameraLocation.Right, _rightConfig);

            // Create schedulers
            _leftScheduler = new CameraCaptureScheduler(_leftConfig.FrameIntervalSec);
            _centerScheduler = new CameraCaptureScheduler(_centerConfig.FrameIntervalSec);
            _rightScheduler = new CameraCaptureScheduler(_rightConfig.FrameIntervalSec);

            // Enable depth rendering on center camera
            if (_centerCamera != null)
            {
                _centerCamera.EnableDepthRendering();
            }

            SimLog.Info($"[SENSOR] Camera sensors created (left={_leftConfig.width}x{_leftConfig.height}, " +
                        $"center={_centerConfig.width}x{_centerConfig.height}, " +
                        $"right={_rightConfig.width}x{_rightConfig.height})");
        }

        private static GameObject CreateCameraMount(GameObject parent, string name, Vector3 localPosition)
        {
            var mount = new GameObject(name);
            mount.transform.SetParent(parent.transform, false);
            mount.transform.localPosition = localPosition;
            mount.transform.localRotation = Quaternion.identity;
            return mount;
        }

        private static JajuchaCameraSensor CreateCameraSensor(GameObject mount, string name, CameraLocation location, CameraConfig config)
        {
            var go = new GameObject(name);
            go.transform.SetParent(mount.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            // Add Unity Camera
            var unityCamera = go.AddComponent<Camera>();
            // We'll configure it in JajuchaCameraSensor.Awake

            // Add our sensor component
            var sensor = go.AddComponent<JajuchaCameraSensor>();
            // Use reflection to set serialized fields since they're private
            SetSensorFields(sensor, location, config);

            return sensor;
        }

        private static void SetSensorFields(JajuchaCameraSensor sensor, CameraLocation location, CameraConfig config)
        {
            // Use the SerializedObject approach for setting serialized fields
            // Since we can't use UnityEditor in runtime, we use reflection
            var locationField = typeof(JajuchaCameraSensor).GetField("_location",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (locationField != null)
                locationField.SetValue(sensor, location);

            var configField = typeof(JajuchaCameraSensor).GetField("_config",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (configField != null)
                configField.SetValue(sensor, config);
        }

        private void CaptureInitialFrames()
        {
            if (_context == null) return;
            long tick = _context.Clock.Tick;
            double time = _context.Clock.Time;

            _leftCamera?.CaptureInitialFrame(tick, time);
            _centerCamera?.CaptureInitialFrame(tick, time);
            _rightCamera?.CaptureInitialFrame(tick, time);

            SimLog.Info("[SENSOR] Initial camera frames captured");
        }
    }
}
