using System;
using JajuchaSim.Core;
using JajuchaSim.Course;
using JajuchaSim.Scenario;
using JajuchaSim.Vehicle;

namespace JajuchaSim.Testing
{
    /// <summary>
    /// Deterministic scenario-run driver (Step 10.25). Creates the official
    /// Scenario → ScoreManager → RunResult path and drives simulation ticks
    /// until the run finishes. An external controller (or a recorded command
    /// trace) supplies the motor commands; the driver only orchestrates.
    ///
    /// Both single tests (<see cref="TestRunner"/>) and batch runs
    /// (<see cref="BatchRunner"/>) use exactly this path, so manual runs and
    /// automated tests score identically.
    /// </summary>
    public sealed class ScenarioRunDriver
    {
        /// <summary>Fixed timestep in simulated seconds (default 0.01 s).</summary>
        public float DeltaTime { get; }

        public SimulationClock Clock { get; }
        public SimulationEventBus Events { get; }
        public ScenarioManager Manager { get; }

        /// <summary>Records every motor command produced by the controller.</summary>
        public CommandRecorder Recorder { get; } = new CommandRecorder();

        /// <summary>
        /// External controller: called once per tick with (tick, simTime);
        /// may return a motor command (null = no command this tick).
        /// </summary>
        public Func<long, double, MotorCommand?> Controller;

        /// <summary>
        /// Optional per-tick hook (telemetry setup / event publishing) invoked
        /// after the clock advances but before the manager ticks.
        /// </summary>
        public Action<long, double> OnBeforeTick;

        public ScenarioRunDriver(
            ScenarioDefinition definition,
            CourseDocument document,
            ulong seed = 1UL,
            float dt = 0.01f)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (document == null) throw new ArgumentNullException(nameof(document));

            DeltaTime = dt > 0f ? dt : 0.01f;
            Clock = new SimulationClock(DeltaTime);
            Events = new SimulationEventBus();
            Manager = new ScenarioManager(Clock, Events);
            Manager.Initialize(new SimulationContext(Clock, Events, new SimulationRandom(seed)));
            Manager.PrepareRun(definition, document);
        }

        /// <summary>Begin the start sequence (defaults to the scenario's start mode).</summary>
        public void RequestStart(StartMode? mode = null)
            => Manager.RequestStart(mode ?? (Manager.Definition != null ? Manager.Definition.startMode : StartMode.NormalSignal));

        /// <summary>
        /// Advance exactly one simulation tick. Returns true when the run has a
        /// result (finished/aborted) after this tick.
        /// </summary>
        public bool Step()
        {
            Clock.AdvanceOneTick();
            OnBeforeTick?.Invoke(Clock.Tick, Clock.Time);
            var cmd = Controller?.Invoke(Clock.Tick, Clock.Time);
            if (cmd.HasValue)
                Recorder.Record(cmd.Value, Clock.Tick, Clock.Time);
            Manager.SimulationTick(DeltaTime);
            return Manager.HasResult;
        }

        /// <summary>
        /// Drive ticks until the run finishes or the tick budget is exhausted.
        /// Returns the official <see cref="RunResultJson"/> or null when the
        /// budget ran out first.
        /// </summary>
        public RunResultJson RunToCompletion(long maxTicks = 1_000_000)
        {
            for (long i = 0; i < maxTicks && !Manager.HasResult; i++)
            {
                if (Step()) break;
            }
            return Manager.HasResult ? Manager.BuildResultJson() : null;
        }
    }
}
