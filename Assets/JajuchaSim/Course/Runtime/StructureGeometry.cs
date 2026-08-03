using System;
using System.Collections.Generic;
using UnityEngine;

namespace JajuchaSim.Course
{
    /// <summary>
    /// Pure geometry description for a generated structure mesh.
    /// Vertices are in world centimetres (1 unit = 1 cm).
    /// </summary>
    public sealed class StructureMeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<int> Triangles = new List<int>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Vector2> UVs = new List<Vector2>();
        public string Name;

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            int i = Vertices.Count;
            Vertices.Add(a); Vertices.Add(b); Vertices.Add(c); Vertices.Add(d);
            Normals.Add(normal); Normals.Add(normal); Normals.Add(normal); Normals.Add(normal);
            UVs.Add(new Vector2(0, 0)); UVs.Add(new Vector2(1, 0));
            UVs.Add(new Vector2(1, 1)); UVs.Add(new Vector2(0, 1));
            // Two triangles (a-b-c, a-c-d) — winding for outward normal
            Triangles.Add(i); Triangles.Add(i + 1); Triangles.Add(i + 2);
            Triangles.Add(i); Triangles.Add(i + 2); Triangles.Add(i + 3);
        }

        public Mesh ToUnityMesh()
        {
            var mesh = new Mesh { name = Name ?? "StructureMesh" };
            mesh.SetVertices(Vertices);
            mesh.SetTriangles(Triangles, 0);
            mesh.SetNormals(Normals);
            mesh.SetUVs(0, UVs);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>
    /// Generates tunnel wall/roof geometry over a rectangular road region.
    /// The road underneath is unchanged — tunnel is an overlay structure.
    ///
    /// Layout (looking along tunnel long axis):
    /// <code>
    ///          roof
    ///      ┌──────────┐
    /// wall │   road   │ wall
    ///      └──────────┘
    /// </code>
    /// </summary>
    public static class TunnelGeometry
    {
        public const float DefaultHeightCm = 55f;
        public const float DefaultWallThicknessCm = 2f;

        /// <summary>
        /// Build mesh data for a tunnel instance.
        /// Orientation: long axis = tunnel direction (derived from region).
        /// width &lt; height (in tiles) ⇒ long axis is Z; else long axis is X.
        /// Optional explicit rotation 0/90 overrides.
        /// </summary>
        public static StructureMeshData Build(StructureInstance tunnel, float tileSizeCm, int rotationDeg = 0)
        {
            if (tunnel == null) throw new ArgumentNullException(nameof(tunnel));
            if (tunnel.Type != StructureType.Tunnel)
                throw new ArgumentException("Instance is not a tunnel.", nameof(tunnel));

            float h = tunnel.HeightCm > 0 ? tunnel.HeightCm : DefaultHeightCm;
            float t = tunnel.WallThicknessCm > 0 ? tunnel.WallThicknessCm : DefaultWallThicknessCm;
            var region = tunnel.Region;

            float x0 = region.x * tileSizeCm;
            float z0 = region.z * tileSizeCm;
            float x1 = (region.x + region.width) * tileSizeCm;
            float z1 = (region.z + region.height) * tileSizeCm;

            // Long axis: default from region aspect; rotation 90 swaps.
            bool longAxisZ = region.width <= region.height;
            if (rotationDeg == 90 || rotationDeg == 270)
                longAxisZ = !longAxisZ;

            var mesh = new StructureMeshData { Name = tunnel.Id ?? "tunnel" };

            if (longAxisZ)
            {
                // Walls along Z (left = -X side, right = +X side) + roof
                AddBox(mesh, x0, 0, z0, t, h, z1 - z0);           // left wall
                AddBox(mesh, x1 - t, 0, z0, t, h, z1 - z0);       // right wall
                AddBox(mesh, x0, h - t, z0, x1 - x0, t, z1 - z0); // roof
            }
            else
            {
                // Walls along X (near = -Z, far = +Z)
                AddBox(mesh, x0, 0, z0, x1 - x0, h, t);
                AddBox(mesh, x0, 0, z1 - t, x1 - x0, h, t);
                AddBox(mesh, x0, h - t, z0, x1 - x0, t, z1 - z0);
            }

            return mesh;
        }

        private static void AddBox(StructureMeshData mesh, float x, float y, float z, float sx, float sy, float sz)
        {
            // 6 faces of axis-aligned box from (x,y,z) with size (sx,sy,sz)
            Vector3 p000 = new Vector3(x, y, z);
            Vector3 p001 = new Vector3(x, y, z + sz);
            Vector3 p010 = new Vector3(x, y + sy, z);
            Vector3 p011 = new Vector3(x, y + sy, z + sz);
            Vector3 p100 = new Vector3(x + sx, y, z);
            Vector3 p101 = new Vector3(x + sx, y, z + sz);
            Vector3 p110 = new Vector3(x + sx, y + sy, z);
            Vector3 p111 = new Vector3(x + sx, y + sy, z + sz);

            mesh.AddQuad(p000, p100, p110, p010, Vector3.back);   // -Z
            mesh.AddQuad(p001, p011, p111, p101, Vector3.forward); // +Z
            mesh.AddQuad(p000, p010, p011, p001, Vector3.left);    // -X
            mesh.AddQuad(p100, p101, p111, p110, Vector3.right);   // +X
            mesh.AddQuad(p000, p001, p101, p100, Vector3.down);    // -Y
            mesh.AddQuad(p010, p110, p111, p011, Vector3.up);      // +Y
        }
    }

    /// <summary>
    /// Generates ramp surface elevation across a rectangular road region.
    /// Elevation rises monotonically from 0 to riseCm along the ramp direction.
    /// </summary>
    public static class RampGeometry
    {
        /// <summary>
        /// Compute the elevation (cm) at a given grid tile within the ramp.
        /// Returns 0 if the tile is outside the ramp region.
        /// Elevation is measured at the tile centre along the ramp direction.
        /// </summary>
        public static float ElevationAtTile(StructureInstance ramp, GridCoordinate tile)
        {
            if (ramp == null || ramp.Type != StructureType.Ramp) return 0f;
            if (!ramp.Region.Contains(tile)) return 0f;

            float rise = ramp.RiseCm > 0 ? ramp.RiseCm : ramp.HeightCm;
            if (rise <= 0f) return 0f;

            var r = ramp.Region;
            float t; // 0 at bottom, 1 at top
            switch (ramp.Direction)
            {
                case GridDirection.North:
                    // Rise along +Z: first row (Near) = 0, last row (Far) = rise
                    t = r.height <= 1 ? 1f : (tile.Z - r.z) / (float)(r.height - 1);
                    break;
                case GridDirection.South:
                    t = r.height <= 1 ? 1f : (r.Far - tile.Z) / (float)(r.height - 1);
                    break;
                case GridDirection.East:
                    t = r.width <= 1 ? 1f : (tile.X - r.x) / (float)(r.width - 1);
                    break;
                case GridDirection.West:
                    t = r.width <= 1 ? 1f : (r.Right - tile.X) / (float)(r.width - 1);
                    break;
                default:
                    t = 0f;
                    break;
            }
            t = Mathf.Clamp01(t);
            return t * rise;
        }

        /// <summary>
        /// Elevation samples for every tile in the ramp, ordered row-major.
        /// Guarantees monotonic non-decreasing sequence along the ramp direction.
        /// </summary>
        public static float[] Elevations(StructureInstance ramp)
        {
            if (ramp == null) throw new ArgumentNullException(nameof(ramp));
            var coords = ramp.Region.ToCoordinates();
            var result = new float[coords.Length];
            for (int i = 0; i < coords.Length; i++)
                result[i] = ElevationAtTile(ramp, coords[i]);
            return result;
        }

        /// <summary>
        /// Build a simple sloped quad mesh for the ramp surface.
        /// </summary>
        public static StructureMeshData BuildSurface(StructureInstance ramp, float tileSizeCm)
        {
            if (ramp == null) throw new ArgumentNullException(nameof(ramp));
            if (ramp.Type != StructureType.Ramp)
                throw new ArgumentException("Instance is not a ramp.", nameof(ramp));

            float rise = ramp.RiseCm > 0 ? ramp.RiseCm : ramp.HeightCm;
            var r = ramp.Region;
            float x0 = r.x * tileSizeCm;
            float z0 = r.z * tileSizeCm;
            float x1 = (r.x + r.width) * tileSizeCm;
            float z1 = (r.z + r.height) * tileSizeCm;

            // Four corners with elevations based on direction
            float e00, e10, e01, e11; // (x0,z0), (x1,z0), (x0,z1), (x1,z1)
            switch (ramp.Direction)
            {
                case GridDirection.North:
                    e00 = 0; e10 = 0; e01 = rise; e11 = rise;
                    break;
                case GridDirection.South:
                    e00 = rise; e10 = rise; e01 = 0; e11 = 0;
                    break;
                case GridDirection.East:
                    e00 = 0; e01 = 0; e10 = rise; e11 = rise;
                    break;
                case GridDirection.West:
                    e00 = rise; e01 = rise; e10 = 0; e11 = 0;
                    break;
                default:
                    e00 = e10 = e01 = e11 = 0;
                    break;
            }

            var mesh = new StructureMeshData { Name = ramp.Id ?? "ramp" };
            Vector3 a = new Vector3(x0, e00, z0);
            Vector3 b = new Vector3(x1, e10, z0);
            Vector3 c = new Vector3(x1, e11, z1);
            Vector3 d = new Vector3(x0, e01, z1);
            Vector3 n = Vector3.Cross(b - a, d - a).normalized;
            if (n.y < 0) n = -n;
            mesh.AddQuad(a, b, c, d, n);
            return mesh;
        }

        /// <summary>
        /// True if elevations rise monotonically along the ramp direction.
        /// </summary>
        public static bool IsMonotonic(StructureInstance ramp)
        {
            var r = ramp.Region;
            switch (ramp.Direction)
            {
                case GridDirection.North:
                    for (int x = r.x; x < r.x + r.width; x++)
                    {
                        float prev = float.NegativeInfinity;
                        for (int z = r.z; z < r.z + r.height; z++)
                        {
                            float e = ElevationAtTile(ramp, new GridCoordinate(x, z));
                            if (e + 1e-4f < prev) return false;
                            prev = e;
                        }
                    }
                    break;
                case GridDirection.South:
                    for (int x = r.x; x < r.x + r.width; x++)
                    {
                        float prev = float.NegativeInfinity;
                        for (int z = r.Far; z >= r.z; z--)
                        {
                            float e = ElevationAtTile(ramp, new GridCoordinate(x, z));
                            if (e + 1e-4f < prev) return false;
                            prev = e;
                        }
                    }
                    break;
                case GridDirection.East:
                    for (int z = r.z; z < r.z + r.height; z++)
                    {
                        float prev = float.NegativeInfinity;
                        for (int x = r.x; x < r.x + r.width; x++)
                        {
                            float e = ElevationAtTile(ramp, new GridCoordinate(x, z));
                            if (e + 1e-4f < prev) return false;
                            prev = e;
                        }
                    }
                    break;
                case GridDirection.West:
                    for (int z = r.z; z < r.z + r.height; z++)
                    {
                        float prev = float.NegativeInfinity;
                        for (int x = r.Right; x >= r.x; x--)
                        {
                            float e = ElevationAtTile(ramp, new GridCoordinate(x, z));
                            if (e + 1e-4f < prev) return false;
                            prev = e;
                        }
                    }
                    break;
            }
            return true;
        }
    }
}
