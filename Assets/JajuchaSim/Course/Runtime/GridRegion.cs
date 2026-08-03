using System;

namespace JajuchaSim.Course
{
    /// <summary>
    /// Integer grid region defined by an origin (x, z), width, and height.
    /// All course features that span multiple tiles (tunnels, ramps,
    /// trigger zones) use this for their footprint.
    ///
    /// The physical extent in centimetres is computed as:
    ///   left   = originX * tileSizeCm
    ///   right  = (originX + width) * tileSizeCm
    ///   near   = originZ * tileSizeCm
    ///   far    = (originZ + height) * tileSizeCm
    /// </summary>
    [Serializable]
    public struct GridRegion : System.IEquatable<GridRegion>
    {
        public int x;
        public int z;
        public int width;
        public int height;

        public GridRegion(int x, int z, int width, int height)
        {
            if (width < 0) width = 0;
            if (height < 0) height = 0;
            this.x = x;
            this.z = z;
            this.width = width;
            this.height = height;
        }

        public readonly int Left => x;
        public readonly int Right => x + width - 1;
        public readonly int Near => z;
        public readonly int Far => z + height - 1;

        /// <summary>True when width &gt;= 1 and height &gt;= 1.</summary>
        public readonly bool IsValid => width >= 1 && height >= 1;

        /// <summary>Total number of tiles in this region.</summary>
        public readonly int TileCount => width * height;

        /// <summary>Enumerate all grid coordinates covered by this region.</summary>
        public readonly GridCoordinate[] ToCoordinates()
        {
            var result = new GridCoordinate[TileCount];
            int idx = 0;
            for (int zi = z; zi < z + height; zi++)
                for (int xi = x; xi < x + width; xi++)
                    result[idx++] = new GridCoordinate(xi, zi);
            return result;
        }

        /// <summary>Check if a coordinate is inside this region.</summary>
        public readonly bool Contains(GridCoordinate coord)
        {
            return coord.X >= x && coord.X < x + width &&
                   coord.Z >= z && coord.Z < z + height;
        }

        /// <summary>Check if this region overlaps another.</summary>
        public readonly bool Overlaps(GridRegion other)
        {
            return x < other.x + other.width &&
                   x + width > other.x &&
                   z < other.z + other.height &&
                   z + height > other.z;
        }

        public readonly int TileWidthCm(float tileSizeCm) => (int)(width * tileSizeCm);
        public readonly int TileHeightCm(float tileSizeCm) => (int)(height * tileSizeCm);

        public readonly bool Equals(GridRegion other)
            => x == other.x && z == other.z && width == other.width && height == other.height;

        public override readonly bool Equals(object obj)
            => obj is GridRegion other && Equals(other);

        public override readonly int GetHashCode()
            => System.HashCode.Combine(x, z, width, height);

        public static bool operator ==(GridRegion a, GridRegion b) => a.Equals(b);
        public static bool operator !=(GridRegion a, GridRegion b) => !a.Equals(b);

        public override readonly string ToString()
            => $"Region({x}, {z}) {width}x{height}";
    }
}
