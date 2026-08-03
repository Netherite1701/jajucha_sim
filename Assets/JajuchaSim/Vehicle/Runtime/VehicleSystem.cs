using System;
using JajuchaSim.Core;
using UnityEngine;

namespace JajuchaSim.Vehicle
{
    public sealed class VehicleSystem : ISimulationSystem
    {
        private readonly VehicleConfig _config;
        private readonly GameObject _vehicleRoot;

        private WheelCollider _frontLeftWheel;
        private WheelCollider _frontRightWheel;
        private WheelCollider _rearLeftWheel;
        private WheelCollider _rearRightWheel;

        private SteeringModel _steering;
        private RearDriveModel _rearDrive;

        private MotorCommand _currentCommand = MotorCommand.Zero;

        public MotorCommand CurrentCommand => _currentCommand;
        public SteeringModel Steering => _steering;
        public RearDriveModel RearDrive => _rearDrive;
        public GameObject VehicleRoot => _vehicleRoot;
        public Rigidbody ChassisRigidbody { get; private set; }

        public VehicleSystem(VehicleConfig config, GameObject vehicleRoot = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _vehicleRoot = vehicleRoot ?? CreateVehicleRoot();
            _steering = SteeringModel.FromConfig(config);
            _rearDrive = RearDriveModel.FromConfig(config);
        }

        public void Initialize(SimulationContext context)
        {
            SetupVehiclePhysics();
        }

        public void SimulationTick(float deltaTime)
        {
            ApplyCommand(_currentCommand);

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
            _steering = SteeringModel.FromConfig(_config);
            _rearDrive = RearDriveModel.FromConfig(_config);
            _rearDrive.Reset();

            if (ChassisRigidbody != null)
            {
                ChassisRigidbody.linearVelocity = Vector3.zero;
                ChassisRigidbody.angularVelocity = Vector3.zero;
                ChassisRigidbody.position = new Vector3(0f, _config.chassisHeight, 0f);
                ChassisRigidbody.rotation = Quaternion.identity;
            }
        }

        public void Shutdown()
        {
        }

        public void SetMotorCommand(MotorCommand command)
        {
            _currentCommand = command;
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
                stiffness = 1.0f
            };
            wc.forwardFriction = fwdFriction;

            var sideFriction = new WheelFrictionCurve
            {
                extremumSlip = 0.2f,
                extremumValue = 1.0f,
                asymptoteSlip = 0.5f,
                asymptoteValue = 0.5f,
                stiffness = 1.0f
            };
            wc.sidewaysFriction = sideFriction;

            return wc;
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
