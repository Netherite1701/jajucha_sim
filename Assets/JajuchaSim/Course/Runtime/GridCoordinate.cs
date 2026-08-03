using System;

namespace JajuchaSim.Course
{
    /// <summary>
    /// Integer grid coordinate for the shared tile-based map.
    /// All course features (roads, structures, objects, triggers) snap to
    /// these coordinates. The physical position of a tile centre is computed
    /// as <c>(x * tileSizeCm, 0, z * tileSizeCm)</c> in Unity units (cm).
    /// </summary>
    [Serializable]
    public readonly struct GridCoordinate : IEquatable<GridCoordinate>
    {
        public readonly int X;
        public readonly int Z;

        public GridCoordinate(int x, int z)
        {
            X = x;
            Z = z;
        }

        // ---- Equality --------------------------------------------------

        public bool Equals(GridCoordinate other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is GridCoordinate other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Z);

        public static bool operator ==(GridCoordinate a, GridCoordinate b) => a.Equals(b);
        public static bool operator !=(GridCoordinate a, GridCoordinate b) => !a.Equals(b);

        public override string ToString() => $"({X}, {Z})";

        // ---- Neighbour helpers for renderer ----------------------------

        /// <summary>
        /// Returns the four orthogonal neighbours (up, down, left, right).
        /// Useful for road-mesh generation (checking connectivity).
        /// </summary>
        public GridCoordinate[] OrthogonalNeighbours()
        {
            return new[]
            {
                new GridCoordinate(X, Z - 1),
                new GridCoordinate(X, Z + 1),
                new GridCoordinate(X - 1, Z),
                new GridCoordinate(X + 1, Z)
            };
        }

        /// <summary>
        /// Returns all eight neighbours (orthogonal + diagonal).
        /// </summary>
        public GridCoordinate[] AllNeighbours()
        {
            return new[]
            {
                new GridCoordinate(X, Z - 1),
                new GridCoordinate(X, Z + 1),
                new GridCoordinate(X - 1, Z),
                new GridCoordinate(X + 1, Z),
                new GridCoordinate(X - 1, Z - 1),
                new GridCoordinate(X + 1, Z - 1),
                new GridCoordinate(X - 1, Z + 1),
                new GridCoordinate(X + 1, Z + 1)
            };
        }
    }
}
