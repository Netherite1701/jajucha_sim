using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Course.Tests
{
    public class StructureGeometryTests
    {
        [Test]
        public void TunnelGeometry_GeneratesWallsAndRoof()
        {
            var tunnel = new StructureInstance("tunnel_01", StructureType.Tunnel, new GridRegion(0, 0, 4, 8))
            {
                HeightCm = 55f,
                WallThicknessCm = 2f
            };

            var mesh = TunnelGeometry.Build(tunnel, 20f);
            Assert.Greater(mesh.Vertices.Count, 8, "Tunnel should have multiple box faces");
            Assert.Greater(mesh.Triangles.Count, 12);
            Assert.AreEqual(mesh.Vertices.Count, mesh.Normals.Count);

            // All vertices within expected bounds
            float xMax = 4 * 20f;
            float zMax = 8 * 20f;
            foreach (var v in mesh.Vertices)
            {
                Assert.GreaterOrEqual(v.x, -0.01f);
                Assert.LessOrEqual(v.x, xMax + 0.01f);
                Assert.GreaterOrEqual(v.z, -0.01f);
                Assert.LessOrEqual(v.z, zMax + 0.01f);
                Assert.GreaterOrEqual(v.y, -0.01f);
                Assert.LessOrEqual(v.y, 55f + 0.01f);
            }
        }

        [Test]
        public void TunnelGeometry_RejectsNonTunnel()
        {
            var ramp = new StructureInstance("r", StructureType.Ramp, new GridRegion(0, 0, 2, 2));
            Assert.Throws<System.ArgumentException>(() => TunnelGeometry.Build(ramp, 20f));
        }

        [Test]
        public void RampElevation_MonotonicNorth()
        {
            var ramp = new StructureInstance("ramp", StructureType.Ramp, new GridRegion(0, 0, 3, 6))
            {
                Direction = GridDirection.North,
                RiseCm = 30f
            };

            Assert.IsTrue(RampGeometry.IsMonotonic(ramp));

            float prev = -1f;
            for (int z = 0; z < 6; z++)
            {
                float e = RampGeometry.ElevationAtTile(ramp, new GridCoordinate(1, z));
                Assert.GreaterOrEqual(e + 1e-4f, prev);
                prev = e;
            }
            Assert.AreEqual(0f, RampGeometry.ElevationAtTile(ramp, new GridCoordinate(0, 0)), 1e-4f);
            Assert.AreEqual(30f, RampGeometry.ElevationAtTile(ramp, new GridCoordinate(0, 5)), 1e-4f);
        }

        [Test]
        public void RampElevation_OutsideRegion_IsZero()
        {
            var ramp = new StructureInstance("ramp", StructureType.Ramp, new GridRegion(5, 5, 2, 2))
            {
                Direction = GridDirection.East,
                RiseCm = 20f
            };
            Assert.AreEqual(0f, RampGeometry.ElevationAtTile(ramp, new GridCoordinate(0, 0)));
        }

        [Test]
        public void RampSurface_HasFourVertices()
        {
            var ramp = new StructureInstance("ramp", StructureType.Ramp, new GridRegion(0, 0, 2, 4))
            {
                Direction = GridDirection.South,
                RiseCm = 10f
            };
            var mesh = RampGeometry.BuildSurface(ramp, 20f);
            Assert.AreEqual(4, mesh.Vertices.Count);
            Assert.AreEqual(6, mesh.Triangles.Count); // 2 tris
        }

        [Test]
        public void MeshData_ToUnityMesh_Succeeds()
        {
            var tunnel = new StructureInstance("t", StructureType.Tunnel, new GridRegion(0, 0, 2, 2))
            {
                HeightCm = 40f
            };
            var data = TunnelGeometry.Build(tunnel, 20f);
            var unityMesh = data.ToUnityMesh();
            Assert.IsNotNull(unityMesh);
            Assert.AreEqual(data.Vertices.Count, unityMesh.vertexCount);
        }
    }
}
