using System.IO;
using System.Linq;
using JajuchaSim.Course;
using NUnit.Framework;

namespace JajuchaSim.Scenario.Tests
{
    public class Competition2026MissionTests
    {
        private static CourseDocument LoadCourse()
        {
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            return CourseDocument.FromJson(File.ReadAllText(Path.Combine(root, "Courses", "2026_preliminary.json")));
        }

        [Test]
        public void InitialState_RequiresMissionSelection()
        {
            var settings = new CompetitionMissionSettings();
            Assert.IsFalse(settings.IsConfigured);

            settings.mode = AdditionalMissionMode.Fixed;
            Assert.IsFalse(settings.IsConfigured);
            settings.missionType = AdditionalMissionType.YellowFlagSpeed;
            settings.candidateId = "candidate_1";
            Assert.IsTrue(settings.IsConfigured);
        }

        [Test]
        public void FixedMission_UsesSelectedTypeAndCandidate()
        {
            var course = LoadCourse();
            var settings = new CompetitionMissionSettings
            {
                mode = AdditionalMissionMode.Fixed,
                missionType = AdditionalMissionType.DynamicObstacle,
                candidateId = "candidate_4",
                randomSeed = 77UL
            };

            var assignment = CompetitionMissionPlanner.Resolve(settings, course.Competition2026);
            Assert.AreEqual(AdditionalMissionType.DynamicObstacle, assignment.MissionType);
            Assert.AreEqual("candidate_4", assignment.CandidateId);
            Assert.AreEqual(77UL, assignment.Seed);

            CompetitionMissionPlanner.Apply(course, assignment);
            Assert.IsTrue(course.Objects.Any(o => o.Id == CompetitionMissionPlanner.ObstacleId));
        }

        [Test]
        public void RandomMission_IsReproducibleAndSelectsKnownCandidate()
        {
            var metadata = LoadCourse().Competition2026;
            var settings = new CompetitionMissionSettings
            {
                mode = AdditionalMissionMode.Random,
                randomSeed = 20260815UL
            };

            var first = CompetitionMissionPlanner.Resolve(settings, metadata);
            var second = CompetitionMissionPlanner.Resolve(settings, metadata);
            Assert.AreEqual(first.MissionType, second.MissionType);
            Assert.AreEqual(first.CandidateId, second.CandidateId);
            Assert.IsNotNull(metadata.FindCandidate(first.CandidateId));
            Assert.IsTrue(first.MissionType == AdditionalMissionType.YellowFlagSpeed ||
                          first.MissionType == AdditionalMissionType.DynamicObstacle);
        }

        [Test]
        public void YellowFlagMission_CreatesThirtyCentimetreSpeedPairAndComputesDistanceOverTime()
        {
            var course = LoadCourse();
            var assignment = new CompetitionMissionAssignment(
                AdditionalMissionType.YellowFlagSpeed, "candidate_2", 1UL);
            CompetitionMissionPlanner.Apply(course, assignment);

            var pair = SpeedTerminalPair.BuildFromDocument(course)
                .Single(p => p.PairId == "mission_speed_pair");
            Assert.AreEqual(30f, pair.DistanceCm, 0.001f);

            var state = new SpeedTerminalPairState(pair);
            Assert.IsFalse(state.TryRecordCrossing(SpeedTerminalRole.A, 10.0, out _));
            Assert.IsTrue(state.TryRecordCrossing(SpeedTerminalRole.B, 11.5, out float speed));
            Assert.AreEqual(20f, speed, 0.001f);
            Assert.LessOrEqual(speed, new CompetitionMissionSettings().practiceSpeedLimitCmS);
        }
    }
}
