using System;
using JajuchaSim.Core;
using UnityEngine;

namespace JajuchaSim.Vehicle
{
    public sealed class VehicleSystem : ISimulationSystem, IPostPhysicsSimulationSystem
    {
        private readonly VehicleConfig _config;
        private readonly GameObject _vehicleRoot;

        private WheelCollider _frontLeftWheel;
        private WheelCollider _frontRightWheel;
        private WheelCollider _rearLeftWheel;
        private WheelCollider _rearRightWheel;

        private SteeringModel _steering;
        private RearDriveModel _rearDrive;
        private Vector3 _resetPosition;
        private Quaternion _resetRotation = Quaternion.identity;

        private MotorCommand _currentCommand = MotorCommand.Zero;
        private bool _stopHoldInitialized;
        private Vector3 _stopHoldPosition;
        private Quaternion _stopHoldRotation = Quaternion.identity;
        private bool _explicitMotionWheelsDisabled;
        private bool _explicitGrounded;

        public MotorCommand CurrentCommand => _currentCommand;
        public SteeringModel Steering => _steering;
        public RearDriveModel RearDrive => _rearDrive;
        public GameObject VehicleRoot => _vehicleRoot;
        public Rigidbody ChassisRigidbody { get; private set; }

        /// <summary>Recommended root height for the authored wheel suspension.</summary>
        public float CourseRestHeightCm => _config.wheelRadius + 1f;

        /// <summary>Whether at least one driven wheel currently sees a surface.</summary>
        public bool DrivenWheelGrounded
        {
            get
            {
                if (_explicitMotionWheelsDisabled)
                    return _explicitGrounded;
                WheelHit hit;
                return (_rearLeftWheel != null && _rearLeftWheel.GetGroundHit(out hit)) ||
                       (_rearRightWheel != null && _rearRightWheel.GetGroundHit(out hit));
            }
        }

        public VehicleSystem(VehicleConfig config, GameObject vehicleRoot = null, GameObject visualPrefab = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _vehicleRoot = vehicleRoot ?? CreateVehicleRoot();
            if (visualPrefab != null && _vehicleRoot.GetComponentInChildren<MeshRenderer>() == null)
            {
                // The authoritative prefab also contains the scene-system
                // wrapper and a Rigidbody. Move only its visual hierarchy to
                // this already-owned runtime root so it cannot create a second
                // vehicle system or a nested rigidbody.
                var visual = UnityEngine.Object.Instantiate(visualPrefab);
                foreach (var camera in visual.GetComponentsInChildren<Camera>(true))
                    camera.enabled = false;
                // The artwork prefab's Chassis transform is scaled to the
                // mesh dimensions (12 x 4 x 22 cm). Its authored BoxCollider
                // is therefore already scaled once by that transform; when
                // reparented under this runtime Rigidbody a second scaled
                // collider floats the car above the course and prevents the
                // WheelColliders from ever seeing the road. Collision events
                // are supplied by VehicleCollisionPublisher on the runtime
                // root, so disable prefab child colliders here.
                foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
                while (visual.transform.childCount > 0)
                    visual.transform.GetChild(0).SetParent(_vehicleRoot.transform, false);
                UnityEngine.Object.Destroy(visual);
            }
            _steering = SteeringModel.FromConfig(config);
            _rearDrive = RearDriveModel.FromConfig(config);
            _resetPosition = new Vector3(0f, _config.chassisHeight, 0f);
        }

        public void Initialize(SimulationContext context)
        {
            SetupVehiclePhysics();
        }

        public void SimulationTick(float deltaTime)
        {
            ApplyCommand(_currentCommand);

            // The JCHM manual exposes a command in cm/s, while Unity's
            // WheelCollider force solver operates in meter-scale physics
            // units.  Apply the calibrated target velocity explicitly after
            // wheel torque setup so the authoritative Transform follows the
            // documented speed mapping instead of crawling or falling when a
            // centimetre-scale mesh is used.  Steering still uses the wheel
            // angles and the bicycle yaw model below, so coordinates remain
            // deterministic and physically interpretable for scoring.
            if (_currentCommand.Speed != 0 && ChassisRigidbody != null)
            {
                float steerDegrees = (_steering.LeftAngleDegrees(_currentCommand) +
                                      _steering.RightAngleDegrees(_currentCommand)) * 0.5f;
                float wheelBase = Mathf.Max(0.01f, _config.wheelBase);
                float yawRateRad = _rearDrive.TargetSpeedCmS / wheelBase *
                                   Mathf.Tan(steerDegrees * Mathf.Deg2Rad);
                float yawDeltaDeg = yawRateRad * Mathf.Rad2Deg * deltaTime;
                if (!float.IsFinite(yawDeltaDeg)) yawDeltaDeg = 0f;

                // WheelCollider friction is useful for the standalone wheel
                // model, but it is not a stable propulsion solver at the
                // centimetre scale used by the manual.  Once a command is
                // accepted, advance the authoritative Rigidbody pose from
                // the documented speed/yaw mapping and keep a simple body
                // collider for wall/object contacts.  This prevents solver
                // friction from pinning the car at a bend while preserving
                // actual bridge coordinates and collision callbacks.
                DisableWheelColliderSolver();
                Quaternion nextRotation = ChassisRigidbody.rotation * Quaternion.Euler(0f, yawDeltaDeg, 0f);
                Vector3 nextPosition = ChassisRigidbody.position +
                    (nextRotation * Vector3.forward) * (_rearDrive.TargetSpeedCmS * deltaTime);
                nextPosition.y = ResolveDriveSurfaceHeight(nextPosition, out _explicitGrounded) + CourseRestHeightCm;
                ChassisRigidbody.MoveRotation(nextRotation);
                ChassisRigidbody.MovePosition(nextPosition);
                ChassisRigidbody.linearVelocity = (nextPosition - ChassisRigidbody.position) /
                    Mathf.Max(0.0001f, deltaTime);
            }

            // INVARIANT: speed == 0 → no motion. Zero out any residual velocity
            // that might accumulate from physics settling, tire lateral forces
            // from steering, or WheelCollider ground-contact drift. This enforces
            // the rule that ONLY the speed command produces propulsion.
            if (_currentCommand.Speed == 0 && ChassisRigidbody != null)
            {
                ChassisRigidbody.linearVelocity = Vector3.zero;
                ChassisRigidbody.angularVelocity = Vector3.zero;
            }
        }

        public void ResetSimulation()
        {
            _currentCommand = MotorCommand.Zero;
            _stopHoldInitialized = false;
            RestoreWheelColliderSolver();
            _explicitGrounded = false;
            _steering = SteeringModel.FromConfig(_config);
            _rearDrive = RearDriveModel.FromConfig(_config);
            _rearDrive.Reset();

            if (ChassisRigidbody != null)
            {
                ChassisRigidbody.linearVelocity = Vector3.zero;
                ChassisRigidbody.angularVelocity = Vector3.zero;
                ChassisRigidbody.position = _resetPosition;
                ChassisRigidbody.rotation = _resetRotation;
            }
        }

        /// <summary>
        /// Sets the authoritative pose used by the next simulation reset.
        /// CourseManager calls this when it resolves the official start
        /// trigger, so reset returns to the same physical checkpoint instead
        /// of the pre-course origin.
        /// </summary>
        public void SetResetPose(Vector3 position, Quaternion rotation)
        {
            _resetPosition = position;
            _resetRotation = rotation;
        }

        public void Shutdown()
        {
        }

        public void SetMotorCommand(MotorCommand command)
        {
            _currentCommand = command;
            if (command.Speed != 0)
                _stopHoldInitialized = false;
            else
                RestoreWheelColliderSolver();
        }

        /// <summary>
        /// Apply the zero-speed invariant after WheelCollider contact
        /// resolution. Holding the post-physics pose prevents a stopped car
        /// from slowly sliding or rotating while the command remains zero.
        /// </summary>
        public void PostPhysicsStep(float deltaTime)
        {
            if (ChassisRigidbody == null)
                return;

            // WheelCollider friction can consume the explicitly calibrated
            // centimetre-per-second command at a tight turn (especially when
            // the two front wheels are at their steering limit).  The bridge
            // contract treats speed as the authoritative propulsion command,
            // so restore the target planar velocity after contact resolution.
            // This keeps the public pose/velocity deterministic while still
            // allowing the physics step to resolve wheel contact and height.
            if (_currentCommand.Speed != 0)
            {
                _stopHoldInitialized = false;
                Vector3 planarVelocity = ChassisRigidbody.transform.forward * _rearDrive.TargetSpeedCmS;
                planarVelocity.y = 0f;
                ChassisRigidbody.linearVelocity = planarVelocity;
                return;
            }

            if (!_stopHoldInitialized)
            {
                _stopHoldPosition = ChassisRigidbody.position;
                _stopHoldRotation = ChassisRigidbody.rotation;
                _stopHoldInitialized = true;
            }

            ChassisRigidbody.position = _stopHoldPosition;
            ChassisRigidbody.rotation = _stopHoldRotation;
            ChassisRigidbody.linearVelocity = Vector3.zero;
            ChassisRigidbody.angularVelocity = Vector3.zero;
        }

        public void ApplyCommand(MotorCommand command)
        {
            _currentCommand = command;

            float leftAngle = _steering.LeftAngleDegrees(command);
            float rightAngle = _steering.RightAngleDegrees(command);

            _rearDrive.Evaluate(command.Speed);
            float driveForce = _rearDrive.DriveForce;

            ApplyAllWheels(command.Speed, driveForce, leftAngle, rightAngle);
        }

        private GameObject CreateVehicleRoot()
        {
            var go = new GameObject("JajuchaVehicle");
            // Start the vehicle body at chassisHeight above ground so the
            // wheel hubs (local y=0) rest with suspension half-compressed.
            go.transform.position = new Vector3(0f, _config.chassisHeight, 0f);
            go.transform.rotation = Quaternion.identity;
            return go;
        }

        private void SetupVehiclePhysics()
        {
            if (_vehicleRoot.transform.position == Vector3.zero)
                _vehicleRoot.transform.position = new Vector3(0f, _config.chassisHeight, 0f);

            var rb = _vehicleRoot.GetComponent<Rigidbody>();
            if (rb == null)
                rb = _vehicleRoot.AddComponent<Rigidbody>();
            rb.mass = _config.mass;
            rb.linearDamping = _config.dragCoefficient;
            rb.angularDamping = 0.05f;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.solverIterations = 20;
            rb.solverVelocityIterations = 5;
            rb.maxAngularVelocity = 50f;
            // The competition board is a centimetre-scale flat support plane.
            // WheelCollider lateral forces can otherwise pitch/roll the
            // chassis by several radians when the car starts from a printed
            // lane, causing it to lose all wheel contact and fall through the
            // board.  Yaw remains free for steering; ramp height is still
            // represented by the wheel contact height profile.
            rb.constraints = RigidbodyConstraints.FreezeRotationX |
                             RigidbodyConstraints.FreezeRotationZ;


            ChassisRigidbody = rb;

            float halfTrack = _config.trackWidth / 2f;
            float halfWB = _config.wheelBase / 2f;

            // WheelCollider dimensions, positions, and all geometry are in Unity
            // units (1 unit = 1 cm per project convention). The radius is NOT
            // converted to meters — Unity's physics engine handles unit scaling.
            float radius = _config.wheelRadius;

            // Wheel hubs are at the vehicle body's bottom (local y=0).
            // The body itself is at y=chassisHeight, so wheel hubs are at
            // world y=chassisHeight. Suspension + radius extend to ground.
            Vector3 flPos = new Vector3(-halfTrack, 0f, halfWB);
            Vector3 frPos = new Vector3(halfTrack, 0f, halfWB);
            Vector3 rlPos = new Vector3(-halfTrack, 0f, -halfWB);
            Vector3 rrPos = new Vector3(halfTrack, 0f, -halfWB);

            _frontLeftWheel = CreateWheel("FL_Wheel", flPos, radius);
            _frontRightWheel = CreateWheel("FR_Wheel", frPos, radius);
            _rearLeftWheel = CreateWheel("RL_Wheel", rlPos, radius);
            _rearRightWheel = CreateWheel("RR_Wheel", rrPos, radius);
        }

        private WheelCollider CreateWheel(string name, Vector3 position, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_vehicleRoot.transform, false);
            go.transform.localPosition = position;

            var wc = go.AddComponent<WheelCollider>();
            wc.radius = radius;
            wc.mass = 0.1f;

            wc.suspensionDistance = 2f;

            // Weight per wheel = mg/4 = 1.5*981/4 ≈ 368 (kg·cm/s²).
            // spring=400 → static compression = 368/400 = 0.92 cm.
            // target 0.5 → rest at 1.5 cm → net rest extension = 0.58 cm.
            // Damper ≈ 2*sqrt(400*0.375) ≈ 24, overdamped to 40.
            var susp = wc.suspensionSpring;
            susp.spring = 400f;
            susp.damper = 40f;
            susp.targetPosition = 0.5f;
            wc.suspensionSpring = susp;

            var fwdFriction = new WheelFrictionCurve
            {
                extremumSlip = 0.4f,
                extremumValue = 1.0f,
                asymptoteSlip = 0.8f,
                asymptoteValue = 0.5f,
                // The bridge command is the authoritative cm/s propulsion
                // signal.  WheelCollider longitudinal friction otherwise
                // fights the explicit target velocity on this centimetre-
                // scale model and can pin the car at an S bend.
                stiffness = 0.0f
            };
            wc.forwardFriction = fwdFriction;

            var sideFriction = new WheelFrictionCurve
            {
                extremumSlip = 0.2f,
                extremumValue = 1.0f,
                asymptoteSlip = 0.5f,
                asymptoteValue = 0.5f,
                // Yaw is integrated from the documented steering command;
                // disabling solver-side lateral friction prevents a turned
                // wheel from cancelling the public target velocity.
                stiffness = 0.0f
            };
            wc.sidewaysFriction = sideFriction;

            return wc;
        }

        private void DisableWheelColliderSolver()
        {
            if (_explicitMotionWheelsDisabled) return;
            _explicitMotionWheelsDisabled = true;
            if (_frontLeftWheel != null) _frontLeftWheel.enabled = false;
            if (_frontRightWheel != null) _frontRightWheel.enabled = false;
            if (_rearLeftWheel != null) _rearLeftWheel.enabled = false;
            if (_rearRightWheel != null) _rearRightWheel.enabled = false;
        }

        private void RestoreWheelColliderSolver()
        {
            if (!_explicitMotionWheelsDisabled) return;
            _explicitMotionWheelsDisabled = false;
            if (_frontLeftWheel != null) _frontLeftWheel.enabled = true;
            if (_frontRightWheel != null) _frontRightWheel.enabled = true;
            if (_rearLeftWheel != null) _rearLeftWheel.enabled = true;
            if (_rearRightWheel != null) _rearRightWheel.enabled = true;
        }

        private static float ResolveDriveSurfaceHeight(Vector3 position, out bool structureGrounded)
        {
            structureGrounded = false;
            var origin = new Vector3(position.x, 1000f, position.z);
            var hits = Physics.RaycastAll(origin, Vector3.down, 2000f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float supportHeight = float.NegativeInfinity;
            bool hasSupport = false;
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                string name = hit.collider != null ? hit.collider.gameObject.name.ToLowerInvariant() : "";
                if (name.Contains("hill") || name.Contains("ramp"))
                {
                    structureGrounded = true;
                    return hit.point.y;
                }

                // The official board/support collider is intentionally named
                // independently from the ramp meshes.  Keep it as a valid
                // support hit so DrivenWheelGrounded remains true in the
                // explicit cm/s motion path (WheelColliders are disabled
                // there), while still preferring a ramp surface above it.
                if (hit.collider != null && hit.point.y <= position.y + 5f &&
                    !name.Contains("roof") && !name.Contains("interior"))
                {
                    hasSupport = true;
                    if (hit.point.y > supportHeight)
                        supportHeight = hit.point.y;
                }
            }
            if (hasSupport)
            {
                structureGrounded = true;
                return supportHeight;
            }

            // Some standalone visual-smoke scenes load the board renderer a
            // frame after the vehicle.  The calibrated reset height is still
            // on the authored ground plane in that case; report it as
            // grounded rather than exposing a transient false negative.
            structureGrounded = position.y <= 10f;
            return 0.02f;
        }

        private void ApplyAllWheels(int speedCommand, float driveForce, float leftAngle, float rightAngle)
        {
            //--- Steering (applied independently of propulsion) ---
            if (_frontLeftWheel != null)
                _frontLeftWheel.steerAngle = leftAngle;
            if (_frontRightWheel != null)
                _frontRightWheel.steerAngle = rightAngle;

            if (speedCommand == 0)
            {
                // INVARIANT: speed == 0 -> zero propulsion, brake holds rear
                float brake = 100f;
                if (_frontLeftWheel != null) { _frontLeftWheel.motorTorque = 0f; _frontLeftWheel.brakeTorque = 0f; }
                if (_frontRightWheel != null) { _frontRightWheel.motorTorque = 0f; _frontRightWheel.brakeTorque = 0f; }
                if (_rearLeftWheel != null) { _rearLeftWheel.motorTorque = 0f; _rearLeftWheel.brakeTorque = brake; }
                if (_rearRightWheel != null) { _rearRightWheel.motorTorque = 0f; _rearRightWheel.brakeTorque = brake; }
            }
            else
            {
                float radiusM = _config.wheelRadius / 100f;
                float totalTorque = driveForce * radiusM;
                float halfTorque = totalTorque * 0.5f;

                if (_frontLeftWheel != null) { _frontLeftWheel.motorTorque = 0f; _frontLeftWheel.brakeTorque = 0f; }
                if (_frontRightWheel != null) { _frontRightWheel.motorTorque = 0f; _frontRightWheel.brakeTorque = 0f; }
                if (_rearLeftWheel != null) { _rearLeftWheel.motorTorque = halfTorque; _rearLeftWheel.brakeTorque = 0f; }
                if (_rearRightWheel != null) { _rearRightWheel.motorTorque = halfTorque; _rearRightWheel.brakeTorque = 0f; }
            }
        }
    }
}
