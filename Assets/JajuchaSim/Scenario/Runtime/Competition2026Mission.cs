using System;
using System.IO;
using JajuchaSim.Core;
using JajuchaSim.Course;
using UnityEngine;

namespace JajuchaSim.Scenario
{
    [Serializable]
    public sealed class CompetitionMissionSettings
    {
        public string lastStage = "preliminary";
        public AdditionalMissionMode mode = AdditionalMissionMode.Unconfigured;
        public AdditionalMissionType missionType = AdditionalMissionType.Unconfigured;
        public string candidateId = "";
        public ulong randomSeed = 2026UL;
        public float practiceSpeedLimitCmS = 20f;
        public float obstacleWaitSec = 3f;
        public float obstacleExitSec = 1f;

        public bool IsConfigured => mode == AdditionalMissionMode.Random ||
            (mode == AdditionalMissionMode.Fixed &&
             missionType != AdditionalMissionType.Unconfigured &&
             !string.IsNullOrEmpty(candidateId));
    }

    public readonly struct CompetitionMissionAssignment
    {
        public AdditionalMissionType MissionType { get; }
        public string CandidateId { get; }
        public ulong Seed { get; }

        public CompetitionMissionAssignment(AdditionalMissionType missionType, string candidateId, ulong seed)
        {
            MissionType = missionType;
            CandidateId = candidateId ?? "";
            Seed = seed;
        }
    }

    public static class CompetitionMissionPlanner
    {
        public const string YellowFlagId = "mission_yellow_flag";
        public const string ObstacleId = "mission_dynamic_obstacle";
        public const string SpeedAId = "mission_speed_a";
        public const string SpeedBId = "mission_speed_b";

        public static CompetitionMissionAssignment Resolve(
            CompetitionMissionSettings settings,
            Competition2026Data course)
        {
            if (settings == null || !settings.IsConfigured)
                throw new InvalidOperationException("2026 additional mission must be configured before starting.");
            if (course?.missionCandidates == null || course.missionCandidates.Length != 5)
                throw new InvalidOperationException("2026 course must define five mission candidates.");

            if (settings.mode == AdditionalMissionMode.Fixed)
            {
                if (course.FindCandidate(settings.candidateId) == null)
                    throw new InvalidOperationException("Selected mission candidate is not present in this course.");
                return new CompetitionMissionAssignment(settings.missionType, settings.candidateId, settings.randomSeed);
            }

            var random = new SimulationRandom(settings.randomSeed);
            var type = random.NextInt(0, 2) == 0
                ? AdditionalMissionType.YellowFlagSpeed
                : AdditionalMissionType.DynamicObstacle;
            int index = random.NextInt(0, course.missionCandidates.Length);
            return new CompetitionMissionAssignment(type, course.missionCandidates[index].id, settings.randomSeed);
        }

        public static void Apply(
            CourseDocument document,
            CompetitionMissionAssignment assignment,
            CompetitionMissionSettings settings = null)
        {
            if (document?.Competition2026 == null)
                throw new InvalidOperationException("Mission assignment requires a 2026 competition course.");

            document.RemoveObject(YellowFlagId);
            document.RemoveObject(ObstacleId);
            document.RemoveTrigger(SpeedAId);
            document.RemoveTrigger(SpeedBId);

            var c = document.Competition2026.FindCandidate(assignment.CandidateId);
            if (c == null) throw new InvalidOperationException("Mission candidate not found: " + assignment.CandidateId);

            if (assignment.MissionType == AdditionalMissionType.YellowFlagSpeed)
            {
                document.PlaceObject(ObjectType.YellowFlag,
                    new GridCoordinate(c.obstacleCellX, c.obstacleCellZ),
                    c.obstacleRotationDeg, id: YellowFlagId);
                var edge = GridOrientationUtil.ParseEdge(c.terminalEdge);
                document.PlaceSpeedTerminal(c.terminalACellX, c.terminalACellZ, edge,
                    "mission_speed_pair", SpeedTerminalRole.A, c.terminalWidthTiles, SpeedAId);
                document.PlaceSpeedTerminal(c.terminalBCellX, c.terminalBCellZ, edge,
                    "mission_speed_pair", SpeedTerminalRole.B, c.terminalWidthTiles, SpeedBId);
            }
            else if (assignment.MissionType == AdditionalMissionType.DynamicObstacle)
            {
                var obstacle = document.PlaceObject(ObjectType.DynamicObstacle,
                    new GridCoordinate(c.obstacleCellX, c.obstacleCellZ),
                    c.obstacleRotationDeg, ObstacleFootprint.Wide, ObstacleId);
                obstacle.ObstacleWaitSec = settings?.obstacleWaitSec > 0f ? settings.obstacleWaitSec : 3f;
                obstacle.ObstacleExitSec = settings?.obstacleExitSec > 0f ? settings.obstacleExitSec : 1f;
            }
        }
    }

    public static class CompetitionMissionPreferences
    {
        public static string PreferencesPath => Path.Combine(
            Application.persistentDataPath, "JajuchaSim", "UserConfig", "competition_2026.json");

        public static CompetitionMissionSettings Load()
        {
            try
            {
                if (!File.Exists(PreferencesPath)) return new CompetitionMissionSettings();
                return JsonUtility.FromJson<CompetitionMissionSettings>(File.ReadAllText(PreferencesPath))
                    ?? new CompetitionMissionSettings();
            }
            catch { return new CompetitionMissionSettings(); }
        }

        public static void Save(CompetitionMissionSettings settings)
        {
            if (settings == null) return;
            string dir = Path.GetDirectoryName(PreferencesPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(PreferencesPath, JsonUtility.ToJson(settings, true));
        }
    }
}
