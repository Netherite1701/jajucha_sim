using System;
using System.Collections.Generic;

namespace JajuchaSim.Course
{
    public enum CourseStage
    {
        Preliminary = 0,
        Final = 1
    }

    public enum AdditionalMissionMode
    {
        Unconfigured = 0,
        Fixed = 1,
        Random = 2
    }

    public enum AdditionalMissionType
    {
        Unconfigured = 0,
        YellowFlagSpeed = 1,
        DynamicObstacle = 2
    }

    [Serializable]
    public sealed class StructurePathPointData
    {
        public float xCm;
        public float zCm;
        public float heightCm;

        public StructurePathPointData() { }
        public StructurePathPointData(float xCm, float zCm, float heightCm = 0f)
        {
            this.xCm = xCm;
            this.zCm = zCm;
            this.heightCm = heightCm;
        }
    }

    [Serializable]
    public sealed class CompetitionPanelData
    {
        public string code;
        public int column;
        public int row;
        public int rotationDeg;
    }

    [Serializable]
    public sealed class CourseCheckpointData
    {
        public int order;
        public string id;
        public string label;
        public GridRegion region;
    }

    [Serializable]
    public sealed class MissionCandidateData
    {
        public string id;
        public GridRegion region;
        public string direction;
        public int terminalACellX;
        public int terminalACellZ;
        public int terminalBCellX;
        public int terminalBCellZ;
        public string terminalEdge;
        public int terminalWidthTiles = 8;
        public int obstacleCellX;
        public int obstacleCellZ;
        public int obstacleRotationDeg;

        public bool IsValid => !string.IsNullOrEmpty(id) && region.IsValid;
    }

    /// <summary>
    /// Official 2026 competition metadata.  Distances are centimetres and the
    /// logical road mask uses the parent course's tile size (5 cm in shipped
    /// courses).  Values marked as practice defaults are intentionally kept in
    /// configuration instead of being presented as official rules.
    /// </summary>
    [Serializable]
    public sealed class Competition2026Data
    {
        public int edition = 2026;
        public string stage = "preliminary";
        public string courseId = "2026_preliminary";
        public string visualProfile = "competition_2026";
        public float panelSizeCm = 90f;
        public float physicalWidthCm = 990f;
        public float physicalLengthCm = 540f;
        public CompetitionPanelData[] panels = Array.Empty<CompetitionPanelData>();
        public CourseCheckpointData[] checkpoints = Array.Empty<CourseCheckpointData>();
        public MissionCandidateData[] missionCandidates = Array.Empty<MissionCandidateData>();

        public CourseStage Stage => string.Equals(stage, "final", StringComparison.OrdinalIgnoreCase)
            ? CourseStage.Final
            : CourseStage.Preliminary;

        public MissionCandidateData FindCandidate(string id)
        {
            if (missionCandidates == null) return null;
            foreach (var candidate in missionCandidates)
                if (candidate != null && string.Equals(candidate.id, id, StringComparison.Ordinal))
                    return candidate;
            return null;
        }
    }

    public static class Competition2026Specification
    {
        public const int Edition = 2026;
        public const int PanelCount = 41;
        public const float PanelSizeCm = 90f;
        public const float WidthCm = 990f;
        public const float LengthCm = 540f;
        public const float LogicalTileSizeCm = 5f;
        public const float HillRiseCm = 10f;
        public const float TunnelOpeningWidthCm = 39f;
        public const float TunnelHeightCm = 22f;
        public const float TunnelRoofLongCm = 26f;
        public const float TunnelRoofShortCm = 9.8f;
        public const float SpeedTerminalDistanceCm = 30f;
        public const int CandidateCount = 5;

        private static readonly Dictionary<string, int> RequiredPanelCounts =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "A", 9 }, { "B", 2 }, { "C", 1 }, { "D", 5 },
                { "F", 2 }, { "G", 1 }, { "H", 1 }, { "I", 1 },
                { "J", 5 }, { "K", 3 }, { "L", 1 }, { "M", 2 },
                { "N", 1 }, { "O", 1 }, { "P", 4 }, { "Q", 2 }
            };

        public static IReadOnlyDictionary<string, int> PanelCounts => RequiredPanelCounts;

        public static List<string> Validate(Competition2026Data data)
        {
            var errors = new List<string>();
            if (data == null)
            {
                errors.Add("Missing 2026 competition metadata.");
                return errors;
            }
            if (data.edition != Edition) errors.Add("edition must be 2026");
            if (Math.Abs(data.panelSizeCm - PanelSizeCm) > 0.001f) errors.Add("panelSizeCm must be 90");
            if (Math.Abs(data.physicalWidthCm - WidthCm) > 0.001f) errors.Add("physicalWidthCm must be 990");
            if (Math.Abs(data.physicalLengthCm - LengthCm) > 0.001f) errors.Add("physicalLengthCm must be 540");

            var actual = new Dictionary<string, int>(StringComparer.Ordinal);
            if (data.panels != null)
            {
                foreach (var panel in data.panels)
                {
                    if (panel == null || string.IsNullOrEmpty(panel.code)) continue;
                    actual.TryGetValue(panel.code, out int count);
                    actual[panel.code] = count + 1;
                }
            }
            foreach (var pair in RequiredPanelCounts)
            {
                actual.TryGetValue(pair.Key, out int count);
                if (count != pair.Value)
                    errors.Add($"panel {pair.Key}: expected {pair.Value}, got {count}");
            }
            if (data.panels == null || data.panels.Length != PanelCount)
                errors.Add($"panel count must be {PanelCount}");
            if (data.missionCandidates == null || data.missionCandidates.Length != CandidateCount)
                errors.Add($"mission candidate count must be {CandidateCount}");
            return errors;
        }

        public static List<string> ValidateDocument(CourseDocument document)
        {
            var errors = Validate(document?.Competition2026);
            if (document == null) return errors;
            var data = document.Competition2026;
            if (data == null) return errors;

            if (Math.Abs(document.Grid.TileSizeCm - LogicalTileSizeCm) > 0.001f)
                errors.Add("logical tile size must be 5 cm");

            var occupiedPanels = new HashSet<string>(StringComparer.Ordinal);
            foreach (var panel in data.panels ?? Array.Empty<CompetitionPanelData>())
            {
                if (panel == null) continue;
                if (panel.column < 0 || panel.column >= 11 || panel.row < 0 || panel.row >= 6)
                    errors.Add($"panel {panel.code} is outside the 11 x 6 physical layout");
                if (!occupiedPanels.Add($"{panel.column}:{panel.row}"))
                    errors.Add($"duplicate panel position {panel.column}:{panel.row}");
            }

            string[] expected = data.Stage == CourseStage.Final
                ? new[] { "start", "s_tunnel", "right_angle", "u_turn", "corner_hill", "zigzag", "obstacle_section", "curve", "finish" }
                : new[] { "start", "s_curve", "right_angle", "u_tunnel", "straight_hill", "hill_exit", "zigzag", "obstacle_section", "curve", "finish" };
            var sourceCheckpoints = data.checkpoints ?? Array.Empty<CourseCheckpointData>();
            var checkpoints = new CourseCheckpointData[sourceCheckpoints.Length];
            Array.Copy(sourceCheckpoints, checkpoints, sourceCheckpoints.Length);
            Array.Sort(checkpoints, (a, b) => (a?.order ?? 0).CompareTo(b?.order ?? 0));
            if (checkpoints.Length != expected.Length)
                errors.Add($"checkpoint count must be {expected.Length} for {data.stage}");
            for (int i = 0; i < Math.Min(checkpoints.Length, expected.Length); i++)
                if (checkpoints[i] == null || !string.Equals(checkpoints[i].id, expected[i], StringComparison.Ordinal))
                    errors.Add($"checkpoint {i + 1} must be {expected[i]}");

            // Runtime start/finish triggers must be the same 5 cm-grid regions
            // as the authoritative checkpoints.  Keeping these as independent
            // or overlapping hand-authored rectangles lets the vehicle spawn
            // in one place while the scenario starts/finishes in another.
            var startCheckpoint = checkpoints.Length > 0 ? checkpoints[0] : null;
            var finishCheckpoint = checkpoints.Length > 0 ? checkpoints[checkpoints.Length - 1] : null;
            TriggerInstance startTrigger = null;
            TriggerInstance finishTrigger = null;
            foreach (var trigger in document.Triggers)
            {
                if (trigger.Type == TriggerType.Start && startTrigger == null) startTrigger = trigger;
                if (trigger.Type == TriggerType.Finish && finishTrigger == null) finishTrigger = trigger;
            }
            if (startTrigger == null || finishTrigger == null)
            {
                errors.Add("2026 course must define distinct start and finish triggers");
            }
            else
            {
                if (startCheckpoint == null || !RegionsEqual(startTrigger.Region, startCheckpoint.region))
                    errors.Add("start trigger region must match the start checkpoint");
                if (finishCheckpoint == null || !RegionsEqual(finishTrigger.Region, finishCheckpoint.region))
                    errors.Add("finish trigger region must match the finish checkpoint");
                if (RegionsEqual(startTrigger.Region, finishTrigger.Region))
                    errors.Add("start and finish trigger regions must be distinct");
            }

            StructureInstance tunnel = null;
            StructureInstance hill = null;
            foreach (var structure in document.Structures)
            {
                if (structure.Type == StructureType.Tunnel) tunnel = structure;
                if (structure.Type == StructureType.Ramp) hill = structure;
            }
            if (tunnel == null) errors.Add("missing 2026 tunnel");
            else
            {
                if (Math.Abs(tunnel.HeightCm - TunnelHeightCm) > 0.001f) errors.Add("tunnel height must be 22 cm");
                if (Math.Abs(tunnel.OpeningWidthCm - TunnelOpeningWidthCm) > 0.001f) errors.Add("tunnel opening must be 39 cm");
                if (Math.Abs(tunnel.RoofLongCm - TunnelRoofLongCm) > 0.001f) errors.Add("tunnel roof long side must be 26 cm");
                if (Math.Abs(tunnel.RoofShortCm - TunnelRoofShortCm) > 0.001f) errors.Add("tunnel roof short side must be 9.8 cm");
                if (tunnel.PathPoints == null || tunnel.PathPoints.Length < 5) errors.Add("tunnel needs a path profile");
            }
            if (hill == null) errors.Add("missing 2026 hill");
            else
            {
                if (Math.Abs(hill.RiseCm - HillRiseCm) > 0.001f) errors.Add("hill rise must be 10 cm");
                if (hill.PathPoints == null || hill.PathPoints.Length != 4) errors.Add("hill must contain slope, flat block, and descent");
                else if (Math.Abs(hill.PathPoints[0].heightCm) > 0.001f ||
                         Math.Abs(hill.PathPoints[1].heightCm - HillRiseCm) > 0.001f ||
                         Math.Abs(hill.PathPoints[2].heightCm - HillRiseCm) > 0.001f ||
                         Math.Abs(hill.PathPoints[3].heightCm) > 0.001f)
                    errors.Add("hill height profile must be 0, 10, 10, 0 cm");
            }

            for (int i = 0; i < (data.missionCandidates?.Length ?? 0); i++)
            {
                var candidate = data.missionCandidates[i];
                string expectedId = $"candidate_{i + 1}";
                if (candidate == null || !string.Equals(candidate.id, expectedId, StringComparison.Ordinal))
                {
                    errors.Add($"mission candidate {i + 1} must be {expectedId}");
                    continue;
                }
                float dx = candidate.terminalBCellX - candidate.terminalACellX;
                float dz = candidate.terminalBCellZ - candidate.terminalACellZ;
                float distance = (float)Math.Sqrt(dx * dx + dz * dz) * document.Grid.TileSizeCm;
                if (Math.Abs(distance - SpeedTerminalDistanceCm) > 0.001f)
                    errors.Add($"{candidate.id} sensor distance must be 30 cm");
            }
            return errors;
        }

        private static bool RegionsEqual(GridRegion a, GridRegion b)
        {
            return a.x == b.x && a.z == b.z &&
                   a.width == b.width && a.height == b.height;
        }
    }
}
