using System;
using System.IO;
using System.Linq;
using JajuchaSim.Course;
using NUnit.Framework;

namespace JajuchaSim.App.Tests
{
    public class Competition2026CourseTests
    {
        private static CourseDocument Load(string stage)
        {
            string path = Path.Combine(RuntimeDataPaths.ProjectRoot(), "Courses", $"2026_{stage}.json");
            Assert.IsTrue(File.Exists(path), "Missing 2026 course: " + path);
            var document = CourseDocument.FromJson(File.ReadAllText(path));
            Assert.IsNotNull(document);
            return document;
        }

        [TestCase("preliminary")]
        [TestCase("final")]
        public void OfficialLayout_HasExactPanelInventoryAndDimensions(string stage)
        {
            var document = Load(stage);
            var metadata = document.Competition2026;

            Assert.AreEqual(5f, document.Grid.TileSizeCm);
            Assert.IsEmpty(Competition2026Specification.ValidateDocument(document));
            Assert.AreEqual(41, metadata.panels.Length);
            Assert.AreEqual(990f, metadata.physicalWidthCm);
            Assert.AreEqual(540f, metadata.physicalLengthCm);
            Assert.AreEqual(5, metadata.missionCandidates.Length);

            foreach (var expected in Competition2026Specification.PanelCounts)
                Assert.AreEqual(expected.Value, metadata.panels.Count(p => p.code == expected.Key), expected.Key);
        }

        [TestCase("preliminary")]
        [TestCase("final")]
        public void OfficialLayout_HasNoBlockingEditorValidationErrors(string stage)
        {
            var session = new MapEditorSession(Load(stage)) { IsReadOnly = true };
            var errors = session.Validate().Where(result => result.IsError).ToList();
            Assert.IsEmpty(errors, string.Join(" | ", errors));
        }

        [Test]
        public void Preliminary_CheckpointsFollowOfficialOrder()
        {
            var ids = Load("preliminary").Competition2026.checkpoints.OrderBy(c => c.order).Select(c => c.id);
            CollectionAssert.AreEqual(new[]
            {
                "start", "s_curve", "right_angle", "u_tunnel", "straight_hill",
                "hill_exit", "zigzag", "obstacle_section", "curve", "finish"
            }, ids);
        }

        [Test]
        public void Final_CheckpointsFollowOfficialOrder()
        {
            var ids = Load("final").Competition2026.checkpoints.OrderBy(c => c.order).Select(c => c.id);
            CollectionAssert.AreEqual(new[]
            {
                "start", "s_tunnel", "right_angle", "u_turn", "corner_hill",
                "zigzag", "obstacle_section", "curve", "finish"
            }, ids);
        }

        [TestCase("preliminary", "u_tunnel")]
        [TestCase("final", "s_tunnel")]
        public void Tunnel_UsesOfficialPathAndDimensions(string stage, string profile)
        {
            var tunnel = Load(stage).Structures.Single(s => s.Type == StructureType.Tunnel);
            Assert.AreEqual(profile, tunnel.Profile);
            Assert.AreEqual(22f, tunnel.HeightCm);
            Assert.AreEqual(39f, tunnel.OpeningWidthCm);
            Assert.AreEqual(26f, tunnel.RoofLongCm);
            Assert.AreEqual(9.8f, tunnel.RoofShortCm);
            Assert.GreaterOrEqual(tunnel.PathPoints.Length, 5);

            var mesh = CompetitionPathGeometry.BuildTunnel(tunnel);
            Assert.Greater(mesh.Vertices.Count, 0);
            Assert.Greater(mesh.Triangles.Count, 0);
            Assert.Greater(CompetitionPathGeometry.BuildTunnelInteriorMask(tunnel).Vertices.Count, 0);
            if (profile == "u_tunnel")
            {
                // The authored U profile is expanded into a 5 cm sampled
                // semicircle; a four-segment sharp V would be physically
                // un-drivable for the vehicle and must not regress.
                Assert.Greater(mesh.Vertices.Count, 600);
            }
        }

        [TestCase("preliminary")]
        [TestCase("final")]
        public void Hill_IsThreeBlocksWithTenCentimetrePlateau(string stage)
        {
            var hill = Load(stage).Structures.Single(s => s.Type == StructureType.Ramp);
            Assert.AreEqual("three_panel_hill", hill.Profile);
            Assert.AreEqual(10f, hill.RiseCm);
            Assert.AreEqual(4, hill.PathPoints.Length);
            CollectionAssert.AreEqual(new[] { 0f, 10f, 10f, 0f }, hill.PathPoints.Select(p => p.heightCm).ToArray());

            var mesh = CompetitionPathGeometry.BuildHill(hill);
            Assert.Greater(mesh.Vertices.Count, 0);
            Assert.IsTrue(mesh.Vertices.Any(v => Math.Abs(v.y - 10f) < 0.001f));
        }

        [TestCase("preliminary")]
        [TestCase("final")]
        public void MissionCandidates_AreFiveOuterStraightsWithThirtyCentimetreSensors(string stage)
        {
            var document = Load(stage);
            var candidates = document.Competition2026.missionCandidates;
            CollectionAssert.AreEqual(new[]
            {
                "candidate_1", "candidate_2", "candidate_3", "candidate_4", "candidate_5"
            }, candidates.Select(c => c.id).ToArray());

            foreach (var candidate in candidates)
            {
                float dx = candidate.terminalBCellX - candidate.terminalACellX;
                float dz = candidate.terminalBCellZ - candidate.terminalACellZ;
                float distance = (float)Math.Sqrt(dx * dx + dz * dz) * document.Grid.TileSizeCm;
                Assert.AreEqual(Competition2026Specification.SpeedTerminalDistanceCm, distance, 0.001f, candidate.id);
            }
        }

        [TestCase("preliminary")]
        [TestCase("final")]
        public void OfficialPrintedObjectsAndStartFinishArePresent(string stage)
        {
            var document = Load(stage);
            Assert.IsTrue(document.Objects.Any(o => o.Type == ObjectType.StartSignal));
            Assert.IsTrue(document.Objects.Any(o => o.Type == ObjectType.PitBarrier));
            Assert.IsTrue(document.Triggers.Any(t => t.Type == TriggerType.Start));
            Assert.IsTrue(document.Triggers.Any(t => t.Type == TriggerType.Finish));
            Assert.Greater(document.Grid.RoadTileCount, 0);
            Assert.Greater(document.Grid.LineTileCount, 0);
        }

        [TestCase("preliminary", 350f, 45f, 630f, 45f)]
        [TestCase("final", 355f, 45f, 630f, 45f)]
        public void StartAndFinishTriggersMatchOfficialCheckpointCoordinates(
            string stage, float startX, float startZ, float finishX, float finishZ)
        {
            var document = Load(stage);
            var start = document.Triggers.Single(t => t.Type == TriggerType.Start);
            var finish = document.Triggers.Single(t => t.Type == TriggerType.Finish);
            Assert.AreNotEqual(start.Region, finish.Region,
                "Start and finish must not share a trigger region.");

            float tile = document.Grid.TileSizeCm;
            float actualStartX = (start.Region.x + start.Region.width * 0.5f) * tile;
            float actualStartZ = (start.Region.z + start.Region.height * 0.5f) * tile;
            float actualFinishX = (finish.Region.x + finish.Region.width * 0.5f) * tile;
            float actualFinishZ = (finish.Region.z + finish.Region.height * 0.5f) * tile;
            Assert.AreEqual(startX, actualStartX, 0.001f);
            Assert.AreEqual(startZ, actualStartZ, 0.001f);
            Assert.AreEqual(finishX, actualFinishX, 0.001f);
            Assert.AreEqual(finishZ, actualFinishZ, 0.001f);
        }
    }
}
