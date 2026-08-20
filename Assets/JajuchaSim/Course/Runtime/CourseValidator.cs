using System;
using System.Collections.Generic;
using System.Linq;

namespace JajuchaSim.Course
{
    /// <summary>
    /// Validation results for a single course feature or the whole course.
    /// </summary>
    public sealed class ValidationResult
    {
        /// <summary>Severity of the validation message.</summary>
        public enum Severity { Info, Warning, Error }

        public Severity Level { get; }
        public string Message { get; }
        public string FeatureId { get; }
        public string FeatureType { get; }

        public ValidationResult(Severity level, string message, string featureId = null, string featureType = null)
        {
            Level = level;
            Message = message;
            FeatureId = featureId;
            FeatureType = featureType;
        }

        public bool IsError => Level == Severity.Error;
        public bool IsWarning => Level == Severity.Warning;

        public override string ToString()
        {
            var prefix = Level == Severity.Error ? "ERROR" :
                         Level == Severity.Warning ? "WARN" : "INFO";
            var tag = FeatureId != null ? $"[{FeatureId}] " : "";
            return $"{prefix}: {tag}{Message}";
        }
    }

    /// <summary>
    /// Validates course data before saving and at load time.
    /// Checks structure/object/trigger placement rules.
    /// </summary>
    public static class CourseValidator
    {
        /// <summary>
        /// Validate the entire course grid.
        /// Returns a list of validation results (empty = no issues).
        /// </summary>
        public static List<ValidationResult> Validate(CourseGrid grid)
        {
            var results = new List<ValidationResult>();

            if (grid == null)
            {
                results.Add(new ValidationResult(ValidationResult.Severity.Error, "Course grid is null."));
                return results;
            }

            if (grid.RoadTileCount == 0)
            {
                results.Add(new ValidationResult(ValidationResult.Severity.Warning,
                    "Course has no road tiles. Add road tiles before placing structures or triggers."));
            }

            // Validate structures
            var structureTiles = new Dictionary<GridCoordinate, StructureType>();
            foreach (var kv in grid.AllStructures())
                structureTiles[kv.Key] = kv.Value;

            ValidateStructures(grid, structureTiles, results);

            // Validate objects
            var objectTiles = new Dictionary<GridCoordinate, ObjectType>();
            foreach (var kv in grid.AllObjects())
                objectTiles[kv.Key] = kv.Value;

            ValidateObjects(grid, objectTiles, structureTiles, results);

            // Validate triggers
            ValidateTriggers(grid, results);

            return results;
        }

        /// <summary>
        /// Validate a full course document (instances + IDs + grid consistency).
        /// </summary>
        public static List<ValidationResult> ValidateDocument(CourseDocument doc)
        {
            var results = new List<ValidationResult>();
            if (doc == null)
            {
                results.Add(new ValidationResult(ValidationResult.Severity.Error, "Course document is null."));
                return results;
            }

            results.AddRange(Validate(doc.Grid));

            if (doc.Competition2026 != null)
            {
                foreach (string error in Competition2026Specification.ValidateDocument(doc))
                    results.Add(new ValidationResult(ValidationResult.Severity.Error,
                        error, doc.Competition2026.courseId, "competition_2026"));
            }

            // Unique IDs
            var seen = new HashSet<string>();
            void CheckId(string id, string kind)
            {
                if (string.IsNullOrEmpty(id))
                {
                    results.Add(new ValidationResult(ValidationResult.Severity.Error,
                        $"{kind} is missing an id.", null, kind));
                    return;
                }
                if (!seen.Add(id))
                {
                    results.Add(new ValidationResult(ValidationResult.Severity.Error,
                        $"Duplicate id '{id}'.", id, kind));
                }
            }

            foreach (var s in doc.Structures)
            {
                CheckId(s.Id, s.Type.ToString().ToLowerInvariant());
                if (!s.Region.IsValid)
                {
                    results.Add(new ValidationResult(ValidationResult.Severity.Error,
                        "Structure region is empty.", s.Id, s.Type.ToString().ToLowerInvariant()));
                }
                else
                {
                    var placementResults = ValidateStructurePlacement(doc.Grid, s.Region, s.Type);
                    // Official 2026 ramp regions describe the complete three-
                    // panel collision footprint, while the 5 cm road mask
                    // intentionally covers only the drivable lane through
                    // that footprint. Keep the generic editor warning, but
                    // do not reject an otherwise valid official competition
                    // ramp for the lane-vs-supporting-panel mismatch.
                    if (doc.Competition2026 != null && s.Type == StructureType.Ramp)
                    {
                        placementResults = placementResults
                            .Where(r => !(r.IsError && r.FeatureType == "ramp" &&
                                r.Message.StartsWith("Ramp requires road coverage", StringComparison.Ordinal)))
                            .ToList();
                    }
                    results.AddRange(placementResults
                        .Select(r => new ValidationResult(r.Level, r.Message, s.Id, r.FeatureType)));
                }
            }

            foreach (var o in doc.Objects)
            {
                CheckId(o.Id, o.Type.ToString().ToLowerInvariant());
                results.AddRange(ValidateObjectPlacement(doc.Grid, o.Tile, o.Type)
                    .Select(r => new ValidationResult(r.Level, r.Message, o.Id, r.FeatureType)));
            }

            foreach (var t in doc.Triggers)
            {
                CheckId(t.Id, t.Type.ToString().ToLowerInvariant());
                if (!t.IsSpeedTerminal && !t.Region.IsValid)
                {
                    results.Add(new ValidationResult(ValidationResult.Severity.Error,
                        "Trigger region cannot be empty.", t.Id, t.Type.ToString().ToLowerInvariant()));
                }

                if (t.IsSpeedTerminal)
                {
                    if (t.WidthTiles < 1)
                    {
                        results.Add(new ValidationResult(ValidationResult.Severity.Warning,
                            "Speed terminal widthTiles < 1; will clamp to 1.", t.Id, "speed_terminal"));
                    }
                    if (string.IsNullOrEmpty(t.PairId))
                    {
                        results.Add(new ValidationResult(ValidationResult.Severity.Warning,
                            "Speed terminal has no pairId; it cannot produce a competition speed measurement.",
                            t.Id, "speed_terminal"));
                    }
                }
            }

            // Speed terminal pair completeness: each pairId should have A and B.
            var pairMembers = new Dictionary<string, List<TriggerInstance>>(StringComparer.Ordinal);
            foreach (var t in doc.Triggers)
            {
                if (!t.IsSpeedTerminal || string.IsNullOrEmpty(t.PairId)) continue;
                if (!pairMembers.TryGetValue(t.PairId, out var list))
                {
                    list = new List<TriggerInstance>();
                    pairMembers[t.PairId] = list;
                }
                list.Add(t);
            }

            foreach (var kv in pairMembers)
            {
                bool hasA = kv.Value.Exists(t => t.TerminalRole == SpeedTerminalRole.A);
                bool hasB = kv.Value.Exists(t => t.TerminalRole == SpeedTerminalRole.B);
                if (!hasA || !hasB)
                {
                    results.Add(new ValidationResult(ValidationResult.Severity.Warning,
                        $"Speed pair '{kv.Key}' is incomplete (needs Terminal A and Terminal B).",
                        kv.Key, "speed_terminal"));
                }
                else if (kv.Value.Count > 2)
                {
                    results.Add(new ValidationResult(ValidationResult.Severity.Warning,
                        $"Speed pair '{kv.Key}' has {kv.Value.Count} terminals; expected exactly 2.",
                        kv.Key, "speed_terminal"));
                }
            }

            return results;
        }

        /// <summary>
        /// Validate a specific structure placement.
        /// </summary>
        public static List<ValidationResult> ValidateStructurePlacement(
            CourseGrid grid, GridRegion region, StructureType type)
        {
            var results = new List<ValidationResult>();

            if (!region.IsValid)
            {
                results.Add(new ValidationResult(ValidationResult.Severity.Error,
                    "Region is invalid (width and height must be >= 1)."));
                return results;
            }

            var tiles = region.ToCoordinates();

            // Check each tile has road underneath
            int roadCount = 0;
            int nonRoadCount = 0;
            foreach (var tile in tiles)
            {
                if (grid.HasRoad(tile))
                    roadCount++;
                else
                    nonRoadCount++;
            }

            if (nonRoadCount > 0)
            {
                results.Add(new ValidationResult(ValidationResult.Severity.Warning,
                    $"{nonRoadCount} of {tiles.Length} structure tiles do not contain road. " +
                    "Structure should be placed on road tiles.",
                    null, type.ToString().ToLowerInvariant()));
            }

            if (roadCount == 0)
            {
                results.Add(new ValidationResult(ValidationResult.Severity.Error,
                    "Structure has no road underneath. It must be placed on at least one road tile.",
                    null, type.ToString().ToLowerInvariant()));
            }

            // Check for overlapping structures
            foreach (var tile in tiles)
            {
                var existing = grid.GetStructure(tile);
                if (existing != StructureType.None && existing != type)
                {
                    results.Add(new ValidationResult(ValidationResult.Severity.Warning,
                        $"Structure overlaps with existing {existing} at {tile}.",
                        null, type.ToString().ToLowerInvariant()));
                }
            }

            // Ramp-specific: must be contiguous and rectangular (guaranteed by GridRegion)
            if (type == StructureType.Ramp)
            {
                // Ramps need full road coverage (all tiles must be road)
                if (nonRoadCount > 0)
                {
                    results.Add(new ValidationResult(ValidationResult.Severity.Error,
                        $"Ramp requires road coverage across the entire region. {nonRoadCount} tiles lack road.",
                        null, "ramp"));
                }
            }

            return results;
        }

        /// <summary>
        /// Validate a specific object placement.
        /// </summary>
        public static List<ValidationResult> ValidateObjectPlacement(
            CourseGrid grid, GridCoordinate tile, ObjectType type)
        {
            var results = new List<ValidationResult>();

            // Must be on a road tile
            if (!grid.HasRoad(tile))
            {
                results.Add(new ValidationResult(ValidationResult.Severity.Warning,
                    $"Object placed at {tile} which is not a road tile.",
                    null, type.ToString().ToLowerInvariant()));
            }

            // Check no conflicting object
            var existing = grid.GetObject(tile);
            if (existing != ObjectType.None && existing != type)
            {
                results.Add(new ValidationResult(ValidationResult.Severity.Warning,
                    $"Tile {tile} already has an object ({existing}). Overwriting.",
                    null, type.ToString().ToLowerInvariant()));
            }

            // Check if inside a tunnel structure (could be problematic)
            var structure = grid.GetStructure(tile);
            if (structure == StructureType.Tunnel)
            {
                results.Add(new ValidationResult(ValidationResult.Severity.Info,
                    $"Object is inside a tunnel at {tile}.",
                    null, type.ToString().ToLowerInvariant()));
            }

            return results;
        }

        /// <summary>
        /// Validate a specific trigger region.
        /// </summary>
        public static List<ValidationResult> ValidateTriggerPlacement(
            CourseGrid grid, GridRegion region, TriggerType type)
        {
            var results = new List<ValidationResult>();

            if (!region.IsValid)
            {
                results.Add(new ValidationResult(ValidationResult.Severity.Error,
                    "Trigger region is invalid (width and height must be >= 1)."));
                return results;
            }

            return results;
        }

        // ---- Internal helpers -----------------------------------------

        private static void ValidateStructures(
            CourseGrid grid,
            Dictionary<GridCoordinate, StructureType> structureTiles,
            List<ValidationResult> results)
        {
            if (structureTiles.Count == 0) return;

            // Check road coverage for each structure tile
            foreach (var kv in structureTiles)
            {
                if (!grid.HasRoad(kv.Key))
                {
                    results.Add(new ValidationResult(ValidationResult.Severity.Warning,
                        $"{kv.Value} at {kv.Key} has no road underneath.",
                        null, kv.Value.ToString().ToLowerInvariant()));
                }
            }
        }

        private static void ValidateObjects(
            CourseGrid grid,
            Dictionary<GridCoordinate, ObjectType> objectTiles,
            Dictionary<GridCoordinate, StructureType> structureTiles,
            List<ValidationResult> results)
        {
            foreach (var kv in objectTiles)
            {
                if (!grid.HasRoad(kv.Key))
                {
                    results.Add(new ValidationResult(ValidationResult.Severity.Warning,
                        $"{kv.Value} at {kv.Key} is not on a road tile.",
                        null, kv.Value.ToString().ToLowerInvariant()));
                }
            }
        }

        private static void ValidateTriggers(CourseGrid grid, List<ValidationResult> results)
        {
            // Triggers are mostly free-form; just check they exist
            if (grid.TriggerTileCount == 0)
            {
                results.Add(new ValidationResult(ValidationResult.Severity.Info,
                    "No triggers defined. Add start/finish triggers for scoring."));
            }
        }
    }
}
