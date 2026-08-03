using JajuchaSim.Core;

namespace JajuchaSim.Course
{
    /// <summary>
    /// Simulation system that provides runtime access to the active course
    /// (<see cref="CourseDocument"/> / <see cref="CourseGrid"/>) during a run.
    /// Owns the course and exposes it to other systems (scoring, triggers, vehicle).
    ///
    /// This system does NOT drive tick behaviour itself; it is a data provider.
    /// Detection of trigger entry/exit is handled by <see cref="TriggerDetectionSystem"/>.
    /// </summary>
    public sealed class CourseSystem : ISimulationSystem
    {
        /// <summary>The active course document (instances + grid). May be null.</summary>
        public CourseDocument Document { get; private set; }

        /// <summary>The active course grid. Null until initialized.</summary>
        public CourseGrid Grid => Document != null ? Document.Grid : _gridOnly;

        private CourseGrid _gridOnly;

        /// <summary>Creates a CourseSystem with a pre-built document.</summary>
        public CourseSystem(CourseDocument document)
        {
            Document = document;
        }

        /// <summary>Creates a CourseSystem with a pre-built grid (no instance layer).</summary>
        public CourseSystem(CourseGrid grid)
        {
            _gridOnly = grid;
        }

        /// <summary>
        /// Creates a CourseSystem with no grid. Call <see cref="SetGrid"/> or
        /// <see cref="SetDocument"/> before the simulation runs.
        /// </summary>
        public CourseSystem()
        {
        }

        /// <summary>Set or replace the active course document at runtime.</summary>
        public void SetDocument(CourseDocument document)
        {
            Document = document;
            _gridOnly = null;
        }

        /// <summary>Set or replace the active course grid at runtime.</summary>
        public void SetGrid(CourseGrid grid)
        {
            _gridOnly = grid;
            Document = null;
        }

        public void Initialize(SimulationContext context)
        {
            // Nothing to initialize — grid/document is set externally.
        }

        public void SimulationTick(float deltaTime)
        {
            // Course data is static during a simulation run.
            // Trigger evaluation is handled by TriggerDetectionSystem.
        }

        public void ResetSimulation()
        {
            // Grid data is reset externally via SetGrid/SetDocument or by loading.
        }

        public void Shutdown()
        {
            Document = null;
            _gridOnly = null;
        }
    }
}
