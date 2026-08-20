using JajuchaSim.Core;
using JajuchaSim.Course;
using UnityEngine;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Ground-truth vehicle telemetry fed to scoring rules each tick (Step 8.14).
    /// Scoring must use this (Rigidbody-derived cm/s), never the jchm motor
    /// command. Set by the scene wiring / tests.
    /// </summary>
    public struct VehicleTelemetry
    {
        /// <summary>Vehicle centre world position (cm).</summary>
        public Vector3 Position;

        /// <summary>Optional footprint sample points (centre + corners).</summary>
        public Vector3[] SamplePoints;

        /// <summary>Forward speed in cm/s derived from the Rigidbody velocity.</summary>
        public float ForwardSpeedCmS;

        public static VehicleTelemetry At(Vector3 position, float forwardSpeedCmS)
            => new VehicleTelemetry
            {
                Position = position,
                SamplePoints = null,
                ForwardSpeedCmS = forwardSpeedCmS
            };
    }

    /// <summary>
    /// Context handed to every rule for one run (Step 8.40). References the
    /// shared session/score so rules stay modular instead of one giant
    /// ScoreManager.
    /// </summary>
    public sealed class ScenarioContext
    {
        public SimulationClock Clock { get; }
        public SimulationEventBus Events { get; }
        public CourseDocument Document { get; set; }
        public RunSession Session { get; set; }
        public ScoreManager Score { get; }
        public ScenarioDefinition Definition { get; set; }

        /// <summary>Updated every tick by the ScenarioManager.</summary>
        public VehicleTelemetry Telemetry { get; set; }

        /// <summary>Current scenario state (updated by the manager).</summary>
        public ScenarioState State { get; set; }

        /// <summary>Current start-signal state (updated by the manager).</summary>
        public StartSignalState Signal { get; set; }

        /// <summary>
        /// Rules may request an abort (e.g. a failing false-start rule).
        /// Wired by the ScenarioManager.
        /// </summary>
        public System.Action<RunResultStatus> RequestAbort;

        public long Tick => Clock?.Tick ?? 0;
        public double Time => Clock?.Time ?? 0.0;

        public ScenarioContext(
            SimulationClock clock,
            SimulationEventBus events,
            CourseDocument document,
            RunSession session,
            ScoreManager score,
            ScenarioDefinition definition)
        {
            Clock = clock;
            Events = events;
            Document = document;
            Session = session;
            Score = score;
            Definition = definition;
        }
    }

    /// <summary>
    /// Modular run-rule interface (Step 8.40):
    ///   OnRunStart / OnEvent (typed) / OnTick / FinalizeRule
    /// </summary>
    public interface IRunRule
    {
        string RuleId { get; }
        void Initialize(ScenarioContext context);
        void OnRunStart();
        void OnTick(float deltaTime);
        void OnTriggerEntered(TriggerEnteredEvent e);
        void OnTriggerExited(TriggerExitedEvent e);
        void OnSpeedTerminalCrossed(SpeedTerminalCrossedEvent e);
        void OnSpeedMeasured(SpeedMeasuredEvent e);
        void OnVehicleCollision(VehicleCollisionEvent e);
        void FinalizeRule();
    }

    /// <summary>
    /// Base class with no-op implementations so rules only override what they
    /// need.
    /// </summary>
    public abstract class RuleEvaluator : IRunRule
    {
        protected ScenarioContext Ctx { get; private set; }

        public virtual string RuleId => GetType().Name;

        public virtual void Initialize(ScenarioContext context) => Ctx = context;

        public virtual void OnRunStart() { }
        public virtual void OnTick(float deltaTime) { }
        public virtual void OnTriggerEntered(TriggerEnteredEvent e) { }
        public virtual void OnTriggerExited(TriggerExitedEvent e) { }
        public virtual void OnSpeedTerminalCrossed(SpeedTerminalCrossedEvent e) { }
        public virtual void OnSpeedMeasured(SpeedMeasuredEvent e) { }
        public virtual void OnVehicleCollision(VehicleCollisionEvent e) { }
        public virtual void FinalizeRule() { }
    }
}
