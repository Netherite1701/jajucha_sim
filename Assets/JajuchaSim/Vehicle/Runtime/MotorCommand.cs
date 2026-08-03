using System;

namespace JajuchaSim.Vehicle
{
    /// <summary>
    /// Represents a JCHM motor command: <c>set_motor(left, right, speed)</c>.
    ///
    /// <list type="bullet">
    ///   <item><c>left</c>, <c>right</c>: front steering in JCHM units [-10, 10].
    ///       Each unit corresponds to approximately <see cref="VehicleConfig.degreesPerJchmUnit"/> degrees.
    ///       Negative = counter-clockwise (left turn) for the left wheel.</item>
    ///   <item><c>speed</c>: rear drive in JCHM units [-30, 30].
    ///       Positive = forward. Zero produces zero propulsion force (invariant).</item>
    /// </list>
    /// </summary>
    public readonly struct MotorCommand : IEquatable<MotorCommand>
    {
        /// <summary>Front-left steering command in JCHM units [-10, 10].</summary>
        public int Left { get; }

        /// <summary>Front-right steering command in JCHM units [-10, 10].</summary>
        public int Right { get; }

        /// <summary>Rear drive speed command in JCHM units [-30, 30].</summary>
        public int Speed { get; }

        public MotorCommand(int left, int right, int speed)
        {
            Left = Math.Clamp(left, -10, 10);
            Right = Math.Clamp(right, -10, 10);
            Speed = Math.Clamp(speed, -30, 30);
        }

        /// <summary>Zero command (all channels 0).</summary>
        public static MotorCommand Zero => new MotorCommand(0, 0, 0);

        public bool Equals(MotorCommand other) =>
            Left == other.Left && Right == other.Right && Speed == other.Speed;

        public override bool Equals(object obj) =>
            obj is MotorCommand other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Left, Right, Speed);

        public static bool operator ==(MotorCommand a, MotorCommand b) => a.Equals(b);
        public static bool operator !=(MotorCommand a, MotorCommand b) => !a.Equals(b);

        public override string ToString() =>
            $"MotorCommand(left={Left}, right={Right}, speed={Speed})";
    }
}
