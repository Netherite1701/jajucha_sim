using System;
using UnityEngine;

namespace JajuchaSim.Vehicle
{
    /// <summary>
    /// Pure-logic model for the rear drive motor.
    ///
    /// Key invariant: when <c>command.Speed == 0</c>, propulsion force is
    /// <b>exactly zero</b> regardless of any other factor. Steering never
    /// contributes to longitudinal propulsion — this model only handles the
    /// speed -> rear drive path.
    ///
    /// <code>
    /// speed  ->  RearDriveModel  ->  drive force (Newtons)
    /// </code>
    /// </summary>
    public sealed class RearDriveModel
    {
        private readonly AnimationCurve _speedMap;
        private readonly float _maxForce;
        private readonly float _mass;
        private readonly float _dragCoefficient;

        /// <summary>
        /// The most recently computed target speed (cm/s) from the speed map.
        /// 0 when speed command is 0.
        /// </summary>
        public float TargetSpeedCmS { get; private set; }

        /// <summary>
        /// The most recently computed drive force (Newtons).
        /// Exactly 0 when speed command is 0.
        /// </summary>
        public float DriveForce { get; private set; }

        /// <param name="speedMap">
        /// AnimationCurve mapping JCHM speed unit [-30..30] to target speed in cm/s.
        /// The curve should pass through (0, 0). Only the absolute value drives
        /// magnitude; sign is taken from the command.
        /// </param>
        /// <param name="maxForce">Maximum drive force in Newtons.</param>
        /// <param name="mass">Vehicle mass in kg (used for acceleration calculation).</param>
        /// <param name="dragCoefficient">Linear drag coefficient.</param>
        public RearDriveModel(
            AnimationCurve speedMap,
            float maxForce,
            float mass,
            float dragCoefficient)
        {
            _speedMap = speedMap ?? throw new ArgumentNullException(nameof(speedMap));
            if (maxForce <= 0f || !float.IsFinite(maxForce))
                throw new ArgumentOutOfRangeException(nameof(maxForce), "maxForce must be positive and finite.");
            if (mass <= 0f || !float.IsFinite(mass))
                throw new ArgumentOutOfRangeException(nameof(mass), "mass must be positive and finite.");
            _maxForce = maxForce;
            _mass = mass;
            _dragCoefficient = dragCoefficient;
        }

        /// <summary>Factory using a <see cref="VehicleConfig"/>.</summary>
        public static RearDriveModel FromConfig(VehicleConfig config) =>
            new RearDriveModel(config.speedMap, config.maxDriveForce, config.mass, config.dragCoefficient);

        /// <summary>
        /// Evaluates the drive model for the given speed command.
        /// Call once per simulation tick before accessing <see cref="TargetSpeedCmS"/>
        /// and <see cref="DriveForce"/>.
        /// </summary>
        public void Evaluate(int speedCommand)
        {
            if (speedCommand == 0)
            {
                // INVARIANT: speed == 0 -> zero propulsion force
                TargetSpeedCmS = 0f;
                DriveForce = 0f;
                return;
            }

            // Use absolute value for map lookup; sign from command determines direction.
            float absSpeed = Mathf.Abs((float)speedCommand);
            float absTargetSpeed = _speedMap.Evaluate(absSpeed);
            float sign = Mathf.Sign(speedCommand);

            TargetSpeedCmS = absTargetSpeed * sign;
            DriveForce = CalculateDriveForce(TargetSpeedCmS);
        }

        /// <summary>
        /// Calculates the drive force (Newtons) needed to accelerate toward the
        /// target speed, considering drag resistance.
        /// </summary>
        private float CalculateDriveForce(float targetSpeedCmS)
        {
            // Simple force model: proportional to target speed, capped at maxForce.
            // Scaled so that max speed (153.9 cm/s) maps to maxForce.
            float targetSpeedMagnitude = Mathf.Abs(targetSpeedCmS);
            float sign = Mathf.Sign(targetSpeedCmS);

            float desiredForce = sign * Mathf.Min(
                targetSpeedMagnitude * (_maxForce / 153.9f),
                _maxForce);

            return desiredForce;
        }

        /// <summary>
        /// Resets the model state to idle.
        /// </summary>
        public void Reset()
        {
            TargetSpeedCmS = 0f;
            DriveForce = 0f;
        }
    }
}
