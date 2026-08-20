using System;
using System.Collections.Generic;
using UnityEngine;

namespace JajuchaSim.Core
{
    /// <summary>
    /// The single authoritative owner of the simulation lifecycle.
    ///
    /// Responsibilities:
    ///  - Initialize / Start / Pause / Resume / Step / Stop / Reset / Shutdown
    ///  - Drive simulation ticks from a fixed-timestep accumulator (separate from
    ///    rendering FPS) while <see cref="State"/> == Running.
    ///  - Register and tick <see cref="ISimulationSystem"/>s in deterministic order.
    ///
    /// The manager must NOT drive the car, capture cameras, detect lanes, talk to
    /// Python, score the course, render UI, or place objects. Those are separate
    /// systems registered through <see cref="simulationSystemBehaviours"/>.
    ///
    /// Physics is stepped in this one authoritative location (Step 2+).
    /// Unity's automatic FixedUpdate-physics is disabled (<see cref="Physics.simulationMode"/>
    /// set to Script) so that the fixed-timestep scheduler is the sole owner of
    /// physics progression.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimulationManager : MonoBehaviour
    {
        [SerializeField] private SimulationConfig config;
        [SerializeField] private SimulationSystemBehaviour[] simulationSystemBehaviours =
            Array.Empty<SimulationSystemBehaviour>();

        /// <summary>All registered systems (interfaces), in registration order.</summary>
        private readonly List<ISimulationSystem> _systems = new List<ISimulationSystem>();

        public SimulationState State { get; private set; } = SimulationState.Uninitialized;

        public SimulationClock Clock { get; private set; }
        public SimulationEventBus Events { get; private set; }
        public SimulationRandom Random { get; private set; }
        public SimulationContext Context { get; private set; }

        /// <summary>
        /// Raised after all simulation systems and the authoritative physics
        /// step have completed for a tick.  Diagnostics and deterministic
        /// tests use this event to observe the post-physics state rather than
        /// a render-frame approximation.
        /// </summary>
        public event Action<long, double> TickCompleted;

        // accumulator for fixed-timestep scheduling
        private double _accumulator;

        // ---- Lifecycle -------------------------------------------------

        private void Awake()
        {
            // Convenience: auto-initialize from serialized config so the scene
            // "just starts" Ready. Lifecycle methods remain the authority and
            // may be called explicitly by tests/headless runners.
            if (config != null && State == SimulationState.Uninitialized)
                Initialize();
        }

        /// <summary>
        /// Initializes the kernel from <see cref="config"/>. Idempotent if the
        /// config has not changed; re-initialization is treated as a reset.
        /// </summary>
        public void Initialize()
        {
            if (config == null)
                throw new InvalidOperationException(
                    "[JajuchaSim] SimulationManager cannot initialize: SimulationConfig is not assigned.");

            if (config.fixedDeltaTime <= 0f || !float.IsFinite(config.fixedDeltaTime))
                throw new InvalidOperationException(
                    "[JajuchaSim] SimulationManager: SimulationConfig.fixedDeltaTime must be positive and finite.");

            if (config.maxTicksPerFrame <= 0)
                throw new InvalidOperationException(
                    "[JajuchaSim] SimulationManager: SimulationConfig.maxTicksPerFrame must be > 0.");

            // 1. Create core services
            Clock = new SimulationClock(config.fixedDeltaTime);
            Events = new SimulationEventBus();
            Random = new SimulationRandom(unchecked((ulong)config.randomSeed));

            // Project world-scale convention (1 unit = 1 cm → gravity -981 cm/s²).
            // Anchored here so every simulation run uses centimeter gravity
            // regardless of when the vehicle/physics arrives (Step 2+).
            Physics.gravity = new Vector3(0f, -981f, 0f);

            // Take sole ownership of physics stepping. The fixed-timestep
            // scheduler drives Physics.Simulate in RunOneTick.
            Physics.simulationMode = SimulationMode.Script;
            Time.fixedDeltaTime = config.fixedDeltaTime;
            Physics.defaultSolverIterations = 20;
            Physics.defaultSolverVelocityIterations = 5;

            // 2. Build context
            Context = new SimulationContext(Clock, Events, Random);

            // 3. Register+initialize subsystems in inspector order
            _systems.Clear();
            if (simulationSystemBehaviours != null)
            {
                for (int i = 0; i < simulationSystemBehaviours.Length; i++)
                {
                    var b = simulationSystemBehaviours[i];
                    if (ReferenceEquals(b, null))
                        throw new InvalidOperationException(
                            $"[JajuchaSim] SimulationManager: system at index {i} is null/missing.");
                    if (b is not ISimulationSystem sys)
                        throw new InvalidOperationException(
                            $"[JajuchaSim] SimulationManager: '{b.GetType().Name}' does not implement ISimulationSystem.");
                    // Reset before handing context so prior state never leaks.
                    sys.ResetSimulation();
                    sys.Initialize(Context);
                    _systems.Add(sys);
                }
            }

            // 4. Apply default time scale
            Clock.SetTimeScale(config.defaultTimeScale);

            // 5. State
            State = SimulationState.Ready;
            _accumulator = 0.0;
            SimLog.Info($"Initialized seed={config.randomSeed} dt={config.fixedDeltaTime} systems={_systems.Count}");

            if (config.autoStart)
                StartSimulation();
        }

        public void StartSimulation()
        {
            EnsureInitialized();
            if (State == SimulationState.Stopped)
                throw new InvalidOperationException(
                    "[JajuchaSim] Cannot StartSimulation from Stopped; ResetSimulation() first.");

            if (State != SimulationState.Ready && State != SimulationState.Paused)
                return; // already running

            State = SimulationState.Running;
            Clock.SetPaused(false);
            Events.Publish(new SimulationStartedEvent(Clock.Time));
            SimLog.Info($"Started tick={Clock.Tick} time={Clock.Time}");
        }

        public void Pause()
        {
            if (State != SimulationState.Running)
                return;
            State = SimulationState.Paused;
            Clock.SetPaused(true);
            Events.Publish(new SimulationPausedEvent(Clock.Tick, Clock.Time));
            SimLog.Info($"Paused tick={Clock.Tick} time={Clock.Time:0.00}");
        }

        public void Resume()
        {
            if (State != SimulationState.Paused)
                return;
            State = SimulationState.Running;
            Clock.SetPaused(false);
            Events.Publish(new SimulationResumedEvent(Clock.Tick, Clock.Time));
            SimLog.Info($"Resumed tick={Clock.Tick} time={Clock.Time:0.00}");
        }

        public void Stop()
        {
            if (State != SimulationState.Running && State != SimulationState.Paused)
                return;

            // Shutdown systems but preserve their final observable state.
            for (int i = 0; i < _systems.Count; i++)
                _systems[i].Shutdown();

            State = SimulationState.Stopped;
            Clock.SetPaused(true);
            Events.Publish(new SimulationStoppedEvent(Clock.Tick, Clock.Time));
            SimLog.Info($"Stopped tick={Clock.Tick} time={Clock.Time:0.00}");
        }

        public void ResetSimulation()
        {
            EnsureInitialized();
            long prevTick = Clock.Tick;
            for (int i = 0; i < _systems.Count; i++)
                _systems[i].ResetSimulation();
            Clock.Reset();
            Random.Reset();
            Events.Clear();
            _accumulator = 0.0;
            State = SimulationState.Ready;
            SimLog.Info($"Reset (was tick={prevTick})");
            Events.Publish(new SimulationResetEvent(0));
        }

        /// <summary>
        /// Advances exactly one simulation tick. Intended for single-stepping
        /// while paused, but works regardless of pause state for testing.
        /// Is a no-op unless Running or Paused.
        /// </summary>
        public void Step()
        {
            EnsureInitialized();
            bool ok = State == SimulationState.Running || State == SimulationState.Paused;
            if (!ok) return;
            RunOneTick();
        }

        /// <summary>
        /// Runs <paramref name="count"/> simulation ticks synchronously, exactly
        /// <see cref="SimulationClock.FixedDeltaTime"/> each, independent of real
        /// time. Used by tests, headless/batch runners, and command replay. Is
        /// a no-op unless Running or Paused.
        /// </summary>
        public void Advance(int count)
        {
            EnsureInitialized();
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "count must be >= 0.");
            bool ok = State == SimulationState.Running || State == SimulationState.Paused;
            if (!ok) return;
            for (int i = 0; i < count; i++)
                RunOneTick();
        }

        public void SetTimeScale(float scale)
        {
            EnsureInitialized();
            Clock.SetTimeScale(scale);
        }

        /// <summary>
        /// Registers an additional <see cref="ISimulationSystem"/> at runtime and
        /// initializes it with the current <see cref="Context"/>. Useful for
        /// headless runners and tests that do not use inspector-assigned
        /// <see cref="SimulationSystemBehaviour"/>s. Must be called after
        /// <see cref="Initialize"/>. No-op if the same instance is already
        /// registered.
        /// </summary>
        public void RegisterSystem(ISimulationSystem system)
        {
            EnsureInitialized();
            if (system == null) throw new ArgumentNullException(nameof(system));
            if (_systems.Contains(system)) return;
            // Note: we intentionally do NOT call ResetSimulation() here. A newly
            // registered system is expected to be in a fresh state; calling
            // ResetSimulation() would inflate reset counters and blur reset
            // semantics. Use ResetSimulation() explicitly to clear state.
            system.Initialize(Context);
            _systems.Add(system);
        }

        /// <summary>Number of registered systems (inspector + runtime-registered).</summary>
        public int SystemCount => _systems.Count;

        // ---- Per-frame scheduler --------------------------------------

        private void Update()
        {
            EnsureInitializedNoThrow();
            if (State != SimulationState.Running)
                return;

            float wallDelta = Time.unscaledDeltaTime;
            if (wallDelta < 0f || !float.IsFinite(wallDelta))
                wallDelta = 0f;

            _accumulator += wallDelta * Clock.TimeScale;

            // spiral-of-death guard
            int cap = config.maxTicksPerFrame;

            while (_accumulator >= Clock.FixedDeltaTime && cap-- > 0)
            {
                RunOneTick();
                _accumulator -= Clock.FixedDeltaTime;
            }

            // If still in debt after cap, drop residual accumulator to avoid
            // unbounded catch-up (e.g. after a render stall).
            if (_accumulator > Clock.FixedDeltaTime * config.maxTicksPerFrame)
                _accumulator = 0.0;
        }

        // ---- Tick core -------------------------------------------------

        private void RunOneTick()
        {
            Clock.AdvanceOneTick();

            // 1. Tick all simulation systems (ISimulationSystem.SimulationTick).
            //    Systems apply commands, update state, etc.
            for (int i = 0; i < _systems.Count; i++)
                _systems[i].SimulationTick(Clock.FixedDeltaTime);

            // 2. Advance Unity physics by exactly one fixed step. This is the
            //    sole authoritative location for physics progression.
            Physics.Simulate(Clock.FixedDeltaTime);

            // 3. Let physics-owning systems restore post-simulation
            // invariants before diagnostics and bridge snapshots observe the
            // tick. This keeps a zero-speed vehicle fully stationary even
            // when WheelCollider contact resolution produces residual drift.
            for (int i = 0; i < _systems.Count; i++)
                if (_systems[i] is IPostPhysicsSimulationSystem postPhysics)
                    postPhysics.PostPhysicsStep(Clock.FixedDeltaTime);

            TickCompleted?.Invoke(Clock.Tick, Clock.Time);
        }

        private void EnsureInitialized()
        {
            if (State == SimulationState.Uninitialized)
                throw new InvalidOperationException(
                    "[JajuchaSim] SimulationManager is not initialized. Call Initialize() first.");
        }

        private void EnsureInitializedNoThrow()
        {
            if (State == SimulationState.Uninitialized && config != null)
                Initialize();
        }

        private void OnDestroy()
        {
            if (State == SimulationState.Running || State == SimulationState.Paused)
                Stop();
        }

        // ---- Test-side registration -----------------------------------

        public void SetConfigForTesting(SimulationConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            config = cfg;
        }
    }
}
