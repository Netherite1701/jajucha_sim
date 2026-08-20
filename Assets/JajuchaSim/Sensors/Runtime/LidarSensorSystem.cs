using System;
using JajuchaSim.Core;
using JajuchaSim.Vehicle;
using UnityEngine;

namespace JajuchaSim.Sensors
{
    /// <summary>
    /// Physics-raycast lidar mounted on the vehicle.
    ///
    /// The scan is captured after the previous tick's pose has been applied,
    /// uses the same centimetre world as the vehicle/course, ignores the
    /// vehicle's own colliders, and publishes immutable metadata for bridge
    /// and trace comparison.
    /// </summary>
    public sealed class LidarSensorSystem : ISimulationSystem
    {
        private readonly VehicleSystem _vehicle;
        private readonly LidarConfig _config;
        private CameraCaptureScheduler _scheduler;
        private SimulationContext _context;
        private GameObject _mount;
        private bool _initialized;
        private bool _initialScanCaptured;
        private long _frameId;
        private LidarScan _latestScan;

        public LidarScan LatestScan => _latestScan;
        public LidarConfig Config => _config;
        public Transform Mount => _mount != null ? _mount.transform : null;

        public LidarSensorSystem(VehicleSystem vehicle, LidarConfig config)
        {
            _vehicle = vehicle ?? throw new ArgumentNullException(nameof(vehicle));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public void Initialize(SimulationContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (_vehicle.VehicleRoot == null)
                throw new InvalidOperationException("Lidar requires a vehicle root.");

            _mount = new GameObject("LidarMount");
            _mount.transform.SetParent(_vehicle.VehicleRoot.transform, false);
            _mount.transform.localPosition = _config.mountPosition;
            _mount.transform.localRotation = Quaternion.identity;
            _scheduler = new CameraCaptureScheduler(_config.FrameIntervalSec);
            _initialized = true;
        }

        public void SimulationTick(float deltaTime)
        {
            if (!_initialized || _context == null) return;

            if (!_initialScanCaptured)
            {
                Capture(_context.Clock.Tick, _context.Clock.Time);
                _initialScanCaptured = true;
                return;
            }

            if (_scheduler.Advance(deltaTime, out _))
                Capture(_context.Clock.Tick, _context.Clock.Time);
        }

        public void ResetSimulation()
        {
            _initialScanCaptured = false;
            _frameId = 0;
            _latestScan = null;
            _scheduler?.Reset();
        }

        public void Shutdown()
        {
            _initialized = false;
            _latestScan = null;
        }

        /// <summary>Capture immediately for deterministic tests and diagnostics.</summary>
        public LidarScan CaptureNow()
        {
            long tick = _context != null && _context.Clock != null ? _context.Clock.Tick : 0L;
            double time = _context != null && _context.Clock != null ? _context.Clock.Time : 0.0;
            return Capture(tick, time);
        }

        private LidarScan Capture(long tick, double time)
        {
            var vehicleTransform = _vehicle.VehicleRoot.transform;
            var origin = vehicleTransform.TransformPoint(_config.mountPosition);
            int count = _config.ClampedRayCount;
            float fov = _config.ClampedFovDeg;
            float minDistance = _config.ClampedMinDistanceCm;
            float maxDistance = _config.ClampedMaxDistanceCm;
            bool fullCircle = fov >= 359.999f;
            // Full scans start at the vehicle's forward axis (0 degrees) and
            // wrap before 360 degrees, matching jchm.lidar.get_lidar().
            float angleMin = fullCircle ? 0f : -fov * 0.5f;
            float increment = count > 1 ? (fullCircle ? fov / count : fov / (count - 1)) : 0f;
            var distances = new float[count];
            int layerMask = _config.EffectiveLayerMask;

            for (int i = 0; i < count; i++)
            {
                float angle = angleMin + increment * i;
                Vector3 localDirection = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
                Vector3 direction = vehicleTransform.TransformDirection(localDirection).normalized;
                distances[i] = Measure(origin, direction, minDistance, maxDistance, layerMask, vehicleTransform);
            }

            float angleMax = fullCircle ? 360f - increment : angleMin + increment * (count - 1);
            var scan = new LidarScan(++_frameId, tick, time, angleMin, angleMax, maxDistance, distances);
            _latestScan = scan;
            return scan;
        }

        private static float Measure(Vector3 origin, Vector3 direction, float minDistance,
            float maxDistance, int layerMask, Transform vehicleTransform)
        {
            var hits = Physics.RaycastAll(origin, direction, maxDistance, layerMask, QueryTriggerInteraction.Ignore);
            float nearest = maxDistance;
            for (int i = 0; i < hits.Length; i++)
            {
                var colliderTransform = hits[i].collider != null ? hits[i].collider.transform : null;
                if (colliderTransform == null || colliderTransform == vehicleTransform || colliderTransform.IsChildOf(vehicleTransform))
                    continue;
                float distance = hits[i].distance;
                if (distance >= minDistance && distance < nearest)
                    nearest = distance;
            }
            return nearest;
        }
    }
}
