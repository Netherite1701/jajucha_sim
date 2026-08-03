using System;

namespace JajuchaSim.Vehicle
{
    /// <summary>
    /// Pure-logic steering model that computes front wheel steering angles from
    /// JCHM left/right commands.
    ///
    /// Steering is evaluated independently of propulsion — there is no code path
    /// where steering alone generates longitudinal force.
    ///
    /// <code>
    /// left/right  ->  steering servos  ->  wheel angle
    /// speed       ->  rear drive motor ->  propulsion
    /// </code>
    /// </summary>
    public sealed class SteeringModel
    {
        private readonly float _degreesPerJchmUnit;

        /// <param name="degreesPerJchmUnit">Steering degrees per JCHM unit (default 2°).</param>
        public SteeringModel(float degreesPerJchmUnit)
        {
            if (degreesPerJchmUnit <= 0f || !float.IsFinite(degreesPerJchmUnit))
                throw new ArgumentOutOfRangeException(
                    nameof(degreesPerJchmUnit),
                    "degreesPerJchmUnit must be positive and finite.");
            _degreesPerJchmUnit = degreesPerJchmUnit;
        }

        /// <summary>Factory using a <see cref="VehicleConfig"/>.</summary>
        public static SteeringModel FromConfig(VehicleConfig config) =>
            new SteeringModel(config.degreesPerJchmUnit);

        /// <summary>
        /// Computes the front-left steering angle in degrees for the given command.
        /// Positive = steer right (clockwise), negative = steer left (CCW).
        /// </summary>
        public float LeftAngleDegrees(MotorCommand command) =>
            command.Left * _degreesPerJchmUnit;

        /// <summary>
        /// Computes the front-right steering angle in degrees for the given command.
        /// Positive = steer right (clockwise), negative = steer left (CCW).
        /// </summary>
        public float RightAngleDegrees(MotorCommand command) =>
            command.Right * _degreesPerJchmUnit;
    }
}
