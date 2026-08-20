using System;
using System.Collections.Generic;
using System.IO;
using JajuchaSim.Core;
using JajuchaSim.Course;
using JajuchaSim.Vehicle;
using UnityEngine;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Orchestrates one scenario run: state machine, start signal, simulation
    /// timer, course-event routing, rule evaluation, and result finalization
    /// (Step 8.4, 8.6–8.12, 8.23–8.28, 8.33–8.35, 8.48–8.51).
    ///
    /// Responsibilities (Step 8.4):
    ///   - prepare run / control scenario state
    ///   - start/stop the official run timer (SimulationClock driven)
    ///   - listen to Step-7 course events and forward them to scoring rules
    ///   - control the 2026 four-red-lamp StartSignal object
    ///   - finalize results / export JSON
    ///
    /// It does NOT drive the car, run the ANN, run an FSM, render cameras, or
    /// edit the map.
    ///
    /// The manager knows ground truth for scoring/timing; the student's Python
    /// only sees the simulated cameras + jchm interface (Step 8.7).
    /// </summary>
    public sealed class ScenarioManager : ISimulationSystem
    {
        private readonly SimulationClock _clock;
        private readonly SimulationEventBus _events;
        private readonly ScoreManager _score = new ScoreManager();
        private readonly RunTimer _timer;
        private readonly List<IRunRule> _rules = new List<IRunRule>();

        private CourseDocument _document;
        private ScenarioDefinition _definition;
        private ScenarioContext _context;
        private VehicleTelemetry _telemetry;
        private Vector3 _previousTelemetryPosition; // for finish-direction checks

        private ScenarioState _state = ScenarioState.Idle;
        private StartSignalState _signal = StartSignalState.Waiting;
        private SimulationRandom _random;
        private int _litLampCount;
        private bool _buzzerActive;
        private double _buzzerRemaining;
        private float _actualReleaseDelaySec;

        // ---- 2026 four-lamp countdown state ----
        private enum CountdownPhase { None, Lamps, ReleaseDelay }
        private CountdownPhase _phase = CountdownPhase.None;
        private double _countdownRemaining;

        // ---- run bookkeeping ----
        private int _runCounter;
        private bool _timerStarted;
        // The vehicle is intentionally spawned on the start checkpoint.  The
        // geometric trigger detector therefore reports an initial "enter"
        // while the countdown is being armed.  That overlap is not a crossed
        // line and must not become a false start; a later exit/re-entry (or a
        // non-zero motor command) is still handled normally.
        private bool _suppressInitialStartTriggerEnter;

        // ---- event-bus subscriptions ----
        private Action<TriggerEnteredEvent> _onTriggerEntered;
        private Action<TriggerExitedEvent> _onTriggerExited;
        private Action<SpeedTerminalCrossedEvent> _onTerminalCrossed;
        private Action<SpeedMeasuredEvent> _onSpeedMeasured;
        private Action<VehicleCollisionEvent> _onCollision;

        /// <summary>
        /// Ground-truth telemetry source (position + Rigidbody-derived forward
        /// speed in cm/s). Wired by the scene / test harness. Scoring never uses
        /// the jchm motor command (Step 8.14).
        /// </summary>
        public Func<VehicleTelemetry> GetTelemetry;

        /// <summary>Raised on every state transition (UI / bridge).</summary>
        public event Action<ScenarioState, StartSignalState> StateChanged;

        /// <summary>Raised once when a run finishes/aborts (UI / bridge).</summary>
        public event Action<RunSession> RunFinished;

        /// <summary>
        /// Optional main-thread preparation hook. The 2026 map editor uses it
        /// to resolve the fixed/random mission immediately before both UI and
        /// bridge starts, so no entry point can bypass mission selection.
        /// Return false to reject the start without changing the Ready state.
        /// </summary>
        public Func<bool> BeforeStart { get; set; }

        public ScenarioManager(SimulationClock clock, SimulationEventBus events)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _timer = new RunTimer(clock);
            BuildDefaultRules();
        }

        // ================================================================
        //  Public state
        // ================================================================

        public ScenarioState State => _state;
        public StartSignalState Signal => _signal;
        public StartLightSnapshot StartLight => new StartLightSnapshot(_signal, _litLampCount, _buzzerActive);
        public float ActualReleaseDelaySec => _actualReleaseDelaySec;
        public RunSession Session { get; private set; } = new RunSession();
        public ScoreManager Score => _score;
        public RunTimer Timer => _timer;
        public ScenarioDefinition Definition => _definition;
        public CourseDocument Document => _document;
        public IReadOnlyList<IRunRule> Rules => _rules;
        public bool IsRunActive => _state == ScenarioState.Countdown || _state == ScenarioState.Running;

        /// <summary>
        /// True only after the four-lamp countdown has released the vehicle.
        /// Bridge input is clamped to zero propulsion before this point.
        /// </summary>
        public bool IsMovementReleased => _state == ScenarioState.Running &&
            _signal == StartSignalState.Released;

        /// <summary>Total debounced collision incidents this run (Step 8.20).</summary>
        public int CollisionCount => Session != null ? Session.Collisions.Count : 0;

        // ================================================================
        //  ISimulationSystem
        // ================================================================

        public void Initialize(SimulationContext context)
        {
            _random = context?.Random ?? new SimulationRandom(2026UL);
            _onTriggerEntered = OnTriggerEntered;
            _onTriggerExited = OnTriggerExited;
            _onTerminalCrossed = OnSpeedTerminalCrossed;
            _onSpeedMeasured = OnSpeedMeasured;
            _onCollision = OnVehicleCollision;

            _events.Subscribe(_onTriggerEntered);
            _events.Subscribe(_onTriggerExited);
            _events.Subscribe(_onTerminalCrossed);
            _events.Subscribe(_onSpeedMeasured);
            _events.Subscribe(_onCollision);
        }

        public void SimulationTick(float deltaTime)
        {
            UpdateTelemetry();

            if (_buzzerActive)
            {
                _buzzerRemaining -= deltaTime;
                if (_buzzerRemaining <= 0.0)
                {
                    _buzzerActive = false;
                    _events.Publish(new ScenarioSignalChangedEvent(StartLight));
                }
            }

            if (_state == ScenarioState.Countdown)
            {
                TickCountdown(deltaTime);
            }
            else if (_state == ScenarioState.Running)
            {
                // Step 8.24 Option B: timer starts when the vehicle crosses the
                // start gate. Also handled on TriggerEntered; this covers the
                // case where the vehicle is already on the line at release.
                if (!_timerStarted && _definition != null &&
                    _definition.startTimingMode == StartTimingMode.StartGateCrossing)
                    TryStartTimerAtStartGate();

                for (int i = 0; i < _rules.Count; i++)
                    _rules[i].OnTick(deltaTime);

                // Step 8.12 max run time → TIME_LIMIT.
                if (_definition != null && _timerStarted && _timer.IsRunning &&
                    _timer.ElapsedSimulationTime >= _definition.maxRunTimeSec)
                {
                    FinalizeRun(RunResultStatus.TimedOut);
                }
            }
        }

        public void ResetSimulation()
        {
            // Step 8.48: Scenario → Ready (if a definition was prepared),
            // timer → 0, score → empty, signal → initial (RED), new run session.
            if (_definition != null && _document != null)
            {
                PrepareRunInternal(_definition, _document);
            }
            else
            {
                _state = ScenarioState.Idle;
                SetSignalInternal(StartSignalState.Waiting);
                _timer.Reset();
                _score.Reset();
                _phase = CountdownPhase.None;
                _timerStarted = false;
                Session.Clear();
            }
        }

        public void Shutdown()
        {
            if (_events == null) return;
            if (_onTriggerEntered != null) _events.Unsubscribe(_onTriggerEntered);
            if (_onTriggerExited != null) _events.Unsubscribe(_onTriggerExited);
            if (_onTerminalCrossed != null) _events.Unsubscribe(_onTerminalCrossed);
            if (_onSpeedMeasured != null) _events.Unsubscribe(_onSpeedMeasured);
            if (_onCollision != null) _events.Unsubscribe(_onCollision);
            _onTriggerEntered = null;
            _onTriggerExited = null;
            _onTerminalCrossed = null;
            _onSpeedMeasured = null;
            _onCollision = null;
        }

        // ================================================================
        //  Scenario control
        // ================================================================

        /// <summary>
        /// Load a scenario + course and enter Ready (Step 8.1/8.3). A fresh
        /// RunSession with a new run id is created (Step 8.27/8.28).
        /// </summary>
        public void PrepareRun(ScenarioDefinition definition, CourseDocument document)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (document == null) throw new ArgumentNullException(nameof(document));
            PrepareRunInternal(definition, document);
        }

        private void PrepareRunInternal(ScenarioDefinition definition, CourseDocument document)
        {
            _definition = definition;
            _document = document;

            _runCounter++;
            Session = new RunSession
            {
                RunId = $"run_{_runCounter:0000}",
                CourseId = definition.courseId,
                ScenarioId = definition.scenarioId,
                CompetitionStage = definition.competitionStage,
                AdditionalMission = definition.additionalMission,
                MissionCandidateId = definition.missionCandidateId,
                MissionRandomSeed = definition.missionRandomSeed
            };

            _score.Reset();
            _score.ScoringEnabled = definition.scoringEnabled;
            _score.Configure(definition.scoring);
            _score.BindSession(Session);
            _timer.Reset();
            _timerStarted = false;
            _phase = CountdownPhase.None;
            _countdownRemaining = 0.0;
            _litLampCount = 0;
            _buzzerActive = false;
            _buzzerRemaining = 0.0;
            _actualReleaseDelaySec = 0f;
            _suppressInitialStartTriggerEnter = false;

            _context = new ScenarioContext(_clock, _events, document, Session, _score, definition)
            {
                RequestAbort = status => FinalizeRun(status)
            };

            for (int i = 0; i < _rules.Count; i++)
                _rules[i].Initialize(_context);

            // Initial signal state for a waiting run (Step 8.6).
            SetSignalInternal(StartSignalState.Waiting);
            SetStateInternal(ScenarioState.Ready);
        }

        /// <summary>
        /// Begin the start sequence (Step 8.9). NormalSignal executes the
        /// Four lamps at 1.5 s intervals, seeded 3–6 s hold, then release.
        /// </summary>
        public bool RequestStart(StartMode mode = StartMode.NormalSignal)
        {
            if (_state == ScenarioState.Finished || _state == ScenarioState.Aborted)
                ResetSimulation();
            if (_state != ScenarioState.Ready) return false;
            if (_definition == null) return false;

            if (BeforeStart != null && !BeforeStart())
                return false;

            for (int i = 0; i < _rules.Count; i++)
                _rules[i].OnRunStart();

            // Prime the trigger detector's initial-overlap exception from the
            // same ground-truth telemetry used for start-gate timing.  If no
            // telemetry is wired (e.g. a unit test publishes an event
            // directly), the first event remains a real crossing and is not
            // suppressed.
            if (mode != StartMode.Immediate && GetTelemetry != null &&
                !string.IsNullOrEmpty(_definition.startTriggerId))
            {
                UpdateTelemetry();
                _suppressInitialStartTriggerEnter =
                    IsTelemetryInsideTrigger(_definition.startTriggerId);
            }

            if (mode == StartMode.Immediate || _definition.startMode == StartMode.Immediate)
            {
                ReleaseStart();
                EnterRunning();
                return true;
            }

            _litLampCount = 1;
            SetSignalInternal(StartSignalState.Lamp1);
            _phase = CountdownPhase.Lamps;
            _countdownRemaining = Math.Max(0.001f, _definition.lampIntervalSec);
            SetStateInternal(ScenarioState.Countdown);
            return true;
        }

        /// <summary>
        /// Abort the active run (Step 8.50): state → Aborted, timer stops,
        /// results collected so far are preserved. Vehicle propulsion is the
        /// caller's responsibility (speed = 0 enforced by the bridge/UI).
        /// </summary>
        public void AbortRun()
        {
            if (_state != ScenarioState.Countdown && _state != ScenarioState.Running) return;
            FinalizeRun(RunResultStatus.Aborted);
        }

        /// <summary>
        /// Debug override for the start signal (Step 8.46/8.55). Lets a human
        /// manually preview lamp counts and release so ANN detection can be tested.
        /// Does not alter the scenario state machine.
        /// </summary>
        public void SetSignalOverride(StartSignalState signal)
        {
            if (_state == ScenarioState.Idle) return;
            if (_state == ScenarioState.Running && signal != StartSignalState.Released) return;
            _litLampCount = (int)signal >= (int)StartSignalState.Lamp1 && (int)signal <= (int)StartSignalState.Lamp4
                ? (int)signal : 0;
            SetSignalInternal(signal);
        }

        /// <summary>
        /// Records a motor input received while the start signal is still red
        /// and lets the bridge enforce the no-motion invariant.  The input is
        /// a false-start attempt even though propulsion is blocked, matching
        /// the competition rule and keeping the result auditable.
        /// </summary>
        public void NotifyMotorCommandBeforeRelease(MotorCommand command)
        {
            if (_state != ScenarioState.Countdown || _signal == StartSignalState.Released)
                return;
            if (command == MotorCommand.Zero || _definition?.falseStart == null ||
                !_definition.falseStart.enabled || Session.FalseStart)
                return;

            Session.FalseStart = true;
            if (_score.Result != null)
                _score.Result.FalseStart = true;
            Session.Events.Add(new ScenarioEvent(_clock.Tick, _clock.Time,
                "FALSE START: motor input before release"));

            var cfg = _definition.falseStart;
            if (cfg.violationMode == ViolationMode.Penalty)
            {
                _score.AddPenalty(new PenaltyRecord(
                    "false_start",
                    "Motor input before 2026 light release/buzzer",
                    cfg.penalty,
                    _clock.Tick,
                    _clock.Time,
                    "false_start",
                    ""));
            }
            else if (cfg.violationMode == ViolationMode.Fail)
            {
                FinalizeRun(RunResultStatus.FalseStart);
            }
        }

        /// <summary>True when a result is available (Finished/Aborted).</summary>
        public bool HasResult =>
            _state == ScenarioState.Finished || _state == ScenarioState.Aborted;

        // ================================================================
        //  Course events (Step 8.4: listen and forward to rules)
        // ================================================================

        private void OnTriggerEntered(TriggerEnteredEvent e)
        {
            if (!IsRunActive) return; // Step 8.51: finished results are frozen

            if (e.Type == TriggerType.Start && _state == ScenarioState.Countdown &&
                _suppressInitialStartTriggerEnter &&
                MatchesConfiguredTrigger(_definition?.startTriggerId, e.TriggerId))
            {
                // Initial containment at spawn is not a line crossing.  Clear
                // the one-shot guard so an actual re-entry before release is
                // still recorded as a false start.
                _suppressInitialStartTriggerEnter = false;
                return;
            }

            // Step 8.23/8.62: finish detection.
            if (e.Type == TriggerType.Finish && _state == ScenarioState.Running)
            {
                if (MatchesConfiguredTrigger(_definition?.finishTriggerId, e.TriggerId))
                {
                    bool accept = !RequiresFinishDirection() || CrossingInCorrectDirection();
                    if (accept)
                    {
                        // Let rules observe the finish before finalizing so e.g.
                        // Finish/Trigger objectives can pass (Step 10).
                        for (int i = 0; i < _rules.Count; i++)
                            _rules[i].OnTriggerEntered(e);
                        FinalizeRun(RunResultStatus.Completed);
                    }
                }
                return;
            }

            // Step 8.24 Option B: crossing the start line starts the timer.
            if (e.Type == TriggerType.Start && _state == ScenarioState.Running &&
                !_timerStarted && _definition != null &&
                _definition.startTimingMode == StartTimingMode.StartGateCrossing &&
                MatchesConfiguredTrigger(_definition.startTriggerId, e.TriggerId))
            {
                StartTimerNow();
            }

            for (int i = 0; i < _rules.Count; i++)
                _rules[i].OnTriggerEntered(e);
        }

        private void OnTriggerExited(TriggerExitedEvent e)
        {
            if (!IsRunActive) return;
            for (int i = 0; i < _rules.Count; i++)
                _rules[i].OnTriggerExited(e);
        }

        private void OnSpeedTerminalCrossed(SpeedTerminalCrossedEvent e)
        {
            if (!IsRunActive) return;
            for (int i = 0; i < _rules.Count; i++)
                _rules[i].OnSpeedTerminalCrossed(e);
        }

        private void OnSpeedMeasured(SpeedMeasuredEvent e)
        {
            if (!IsRunActive) return;
            for (int i = 0; i < _rules.Count; i++)
                _rules[i].OnSpeedMeasured(e);
        }

        private void OnVehicleCollision(VehicleCollisionEvent e)
        {
            if (!IsRunActive) return;
            for (int i = 0; i < _rules.Count; i++)
                _rules[i].OnVehicleCollision(e);
        }

        // ================================================================
        //  Countdown / start sequence
        // ================================================================

        private void TickCountdown(float deltaTime)
        {
            if (_phase == CountdownPhase.None) return;

            _countdownRemaining -= deltaTime;

            if (_phase == CountdownPhase.Lamps && _countdownRemaining <= 0.0)
            {
                if (_litLampCount < 4)
                {
                    _litLampCount++;
                    SetSignalInternal((StartSignalState)_litLampCount);
                    if (_litLampCount == 4)
                        BeginReleaseDelay();
                    else
                        _countdownRemaining = Math.Max(0.001f, _definition != null ? _definition.lampIntervalSec : 1.5f);
                }
                else
                {
                    BeginReleaseDelay();
                }
            }
            else if (_phase == CountdownPhase.ReleaseDelay && _countdownRemaining <= 0.0)
            {
                CompleteCountdown();
            }
        }

        private void CompleteCountdown()
        {
            _phase = CountdownPhase.None;
            ReleaseStart();
            EnterRunning();
        }

        private void BeginReleaseDelay()
        {
            _phase = CountdownPhase.ReleaseDelay;
            float min = _definition != null ? _definition.releaseDelayMinSec : 3f;
            float max = _definition != null ? _definition.releaseDelayMaxSec : 6f;
            if (max < min) max = min;
            _actualReleaseDelaySec = min + (_random?.NextFloat() ?? 0f) * (max - min);
            // Publish the chosen delay immediately so the tick-level trace
            // and bridge-visible session state stay truthful during the
            // countdown, not only after finalization.
            if (Session != null)
                Session.StartReleaseDelaySec = _actualReleaseDelaySec;
            _countdownRemaining = _actualReleaseDelaySec;
        }

        private void ReleaseStart()
        {
            _litLampCount = 0;
            _buzzerActive = true;
            _buzzerRemaining = Math.Max(0f, _definition != null ? _definition.buzzerDurationSec : 1f);
            SetSignalInternal(StartSignalState.Released);
        }

        private void EnterRunning()
        {
            SetStateInternal(ScenarioState.Running);
            LogEvent($"START RELEASE ({_actualReleaseDelaySec:0.000}s)");

            // Step 8.24: timer start policy.
            if (_definition == null || _definition.startTimingMode == StartTimingMode.SignalRelease)
            {
                StartTimerNow();
            }
            else
            {
                TryStartTimerAtStartGate();
            }
        }

        // ================================================================
        //  Timer
        // ================================================================

        private void StartTimerNow()
        {
            if (_timerStarted) return;
            _timer.Start();
            _timerStarted = true;
            Session.StartTime = _timer.StartTime;
            LogEvent("RUN START");
        }

        private void TryStartTimerAtStartGate()
        {
            if (_state != ScenarioState.Running || _timerStarted) return;
            if (string.IsNullOrEmpty(_definition?.startTriggerId))
            {
                StartTimerNow();
                return;
            }
            // Without a wired ground-truth telemetry source we cannot know the
            // vehicle position; wait for the StartTriggerEntered event instead
            // of assuming the vehicle sits on the line (Step 8.24 Option B).
            if (GetTelemetry == null) return;
            UpdateTelemetry(); // refresh cached position before the containment check
            if (IsTelemetryInsideTrigger(_definition.startTriggerId))
                StartTimerNow();
        }

        private bool IsTelemetryInsideTrigger(string triggerId)
        {
            var trigger = _document?.FindTrigger(triggerId);
            if (trigger == null || trigger.IsSpeedTerminal || _document == null) return false;
            if (_telemetry.SamplePoints != null && _telemetry.SamplePoints.Length > 0)
            {
                foreach (var pt in _telemetry.SamplePoints)
                    if (trigger.Region.Contains(_document.Grid.WorldToGrid(pt)))
                        return true;
                return false;
            }
            return trigger.Region.Contains(_document.Grid.WorldToGrid(_telemetry.Position));
        }

        // ================================================================
        //  Finalize / results (Step 8.23, 8.26, 8.29)
        // ================================================================

        /// <summary>
        /// Stop the timer, finalize all rules, finalize the score, freeze the
        /// session, and publish the finished event. Safe to call multiple times
        /// (subsequent calls are no-ops once the run has ended).
        /// </summary>
        public void FinalizeRun(RunResultStatus status)
        {
            if (_state == ScenarioState.Finished || _state == ScenarioState.Aborted) return;
            if (_state != ScenarioState.Countdown && _state != ScenarioState.Running) return;

            if (_timerStarted)
                _timer.Stop();
            else
            {
                // A run that never started the timer (abort during countdown)
                // still records a zero-length interval at the current clock.
                _timer.Start();
                _timer.Stop();
            }

            Session.EndTime = _timer.EndTime;
            Session.Status = status;
            Session.StartReleaseDelaySec = _actualReleaseDelaySec;

            for (int i = 0; i < _rules.Count; i++)
                _rules[i].FinalizeRule();

            // Mirror raw measurements into the score result (raw data first,
            // points later — Step 8.42 / Step 10).
            var r = _score.Result;
            r.SlowZones.Clear();
            r.SlowZones.AddRange(Session.SlowZones);
            r.SpeedGates.Clear();
            r.SpeedGates.AddRange(Session.Measurements);
            r.Collisions.Clear();
            r.Collisions.AddRange(Session.Collisions);
            r.CollisionCount = Session.Collisions.Count;
            r.LineContactCount = Session.LineContactCount;
            r.CourseDepartureCount = Session.CourseDepartureCount;
            r.Objectives.Clear();
            r.Objectives.AddRange(Session.Objectives);
            r.SpeedMeasurements.Clear();
            r.SpeedMeasurements.AddRange(CollectSpeedMeasurements());

            _score.FinalizeScore();

            if (status == RunResultStatus.Completed)
                LogEvent("RUN FINISH");
            else if (status == RunResultStatus.TimedOut)
                LogEvent("RUN TIME LIMIT");
            else
                LogEvent("RUN ABORTED");

            SetStateInternal(
                status == RunResultStatus.Aborted || status == RunResultStatus.FalseStart
                    ? ScenarioState.Aborted
                    : ScenarioState.Finished);

            _events.Publish(new ScenarioRunFinishedEvent(Session));
            RunFinished?.Invoke(Session);

            // Step 8.35: automatic result file.
            if (_definition != null && _definition.autoSaveResults)
            {
                try
                {
                    string dir = Path.Combine(Application.persistentDataPath, _definition.runsDirectory);
                    Directory.CreateDirectory(dir);
                    ExportResult(Path.Combine(dir, Session.RunId + ".json"));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Scenario] Auto-save result failed: {ex.Message}");
                }
            }
        }

        /// <summary>Build the JSON export shape (Step 8.34).</summary>
        public RunResultJson BuildResultJson()
        {
            var r = _score.Result;
            bool dynamicObstacleCollision = Session.Collisions.Exists(c =>
                string.Equals(c.ObjectId, CompetitionMissionPlanner.ObstacleId, StringComparison.Ordinal));
            float measuredMissionSpeed = 0f;
            bool hasMissionSpeed = false;
            foreach (var speed in r.SpeedMeasurements)
            {
                if (!string.Equals(speed.PairId, "mission_speed_pair", StringComparison.Ordinal)) continue;
                measuredMissionSpeed = speed.SpeedCmS;
                hasMissionSpeed = true;
            }
            bool missionPassed = string.Equals(Session.AdditionalMission,
                    AdditionalMissionType.DynamicObstacle.ToString(), StringComparison.Ordinal)
                ? !dynamicObstacleCollision
                : hasMissionSpeed && SpeedWithinLimit("mission_speed_pair", measuredMissionSpeed);
            var json = new RunResultJson
            {
                runId = Session.RunId,
                course = Session.CourseId,
                scenario = Session.ScenarioId,
                status = Session.Status.ToString().ToLowerInvariant(),
                elapsedSec = Session.ElapsedSec,
                completed = Session.Status == RunResultStatus.Completed,
                timedOut = Session.Status == RunResultStatus.TimedOut,
                aborted = Session.Status == RunResultStatus.Aborted,
                falseStart = Session.FalseStart || Session.Status == RunResultStatus.FalseStart,
                collisions = Session.Collisions.Count,
                lineContacts = Session.LineContactCount,
                courseDepartures = Session.CourseDepartureCount,
                competitionStage = Session.CompetitionStage,
                additionalMission = Session.AdditionalMission,
                missionCandidateId = Session.MissionCandidateId,
                missionRandomSeed = Session.MissionRandomSeed,
                startReleaseDelaySec = Session.StartReleaseDelaySec,
                practiceValuesOfficial = false,
                practiceValueLabel = "비공식 연습값",
                measuredSpeedCmS = measuredMissionSpeed,
                dynamicObstacleCollision = dynamicObstacleCollision,
                additionalMissionPassed = missionPassed,
                violations = new ViolationsJson
                {
                    lineContacts = Session.LineContactCount,
                    collisions = Session.Collisions.Count
                },
                baseScore = r.BaseScore,
                totalPenalty = r.TotalPenalty,
                score = r.Score
            };

            var zones = new List<SlowZoneJson>();
            foreach (var z in Session.SlowZones)
                zones.Add(new SlowZoneJson
                {
                    triggerId = z.TriggerId,
                    allowedMaxCmS = z.AllowedMaxCmS,
                    maxSpeedCmS = z.MaxSpeedCmS,
                    averageSpeedCmS = z.AverageSpeedCmS,
                    timeAboveLimitSec = z.TimeAboveLimitSec,
                    passed = z.Passed
                });
            json.slowZones = zones.ToArray();

            var gates = new List<SpeedGateJson>();
            foreach (var g in Session.Measurements)
                gates.Add(new SpeedGateJson
                {
                    pairId = g.PairId,
                    firstGate = g.FirstGate,
                    secondGate = g.SecondGate,
                    distanceCm = g.DistanceCm,
                    startTime = g.StartTime,
                    endTime = g.EndTime,
                    averageSpeedCmS = g.AverageSpeedCmS
                });
            json.speedGates = gates.ToArray();

            var cols = new List<CollisionJson>();
            foreach (var c in Session.Collisions)
                cols.Add(new CollisionJson
                {
                    objectId = c.ObjectId,
                    relativeVelocityCmS = c.RelativeVelocityCmS,
                    simulationTime = c.SimulationTime
                });
            json.collisionList = cols.ToArray();

            var pens = new List<PenaltyJson>();
            foreach (var p in Session.Penalties)
                pens.Add(new PenaltyJson
                {
                    ruleId = p.RuleId,
                    reason = p.Reason,
                    value = p.Value,
                    simulationTime = p.SimulationTime,
                    eventType = p.EventType,
                    targetId = p.TargetId
                });
            json.penalties = pens.ToArray();

            var objs = new List<ObjectiveJson>();
            foreach (var o in Session.Objectives)
                objs.Add(new ObjectiveJson
                {
                    id = o.Id,
                    type = o.Type.ToString().ToLowerInvariant(),
                    targetId = o.TargetId,
                    status = o.State.ToString().ToLowerInvariant(),
                    passed = o.Passed,
                    penalty = o.Penalty
                });
            json.objectives = objs.ToArray();

            var speeds = new List<SpeedMeasurementJson>();
            foreach (var s in r.SpeedMeasurements)
                speeds.Add(new SpeedMeasurementJson
                {
                    pairId = s.PairId,
                    distanceCm = s.DistanceCm,
                    t1 = s.T1,
                    t2 = s.T2,
                    speedCmS = s.SpeedCmS,
                    result = SpeedWithinLimit(s.PairId, s.SpeedCmS) ? "pass" : "fail"
                });
            json.speedMeasurements = speeds.ToArray();

            var evts = new List<EventJson>();
            foreach (var ev in Session.Events)
                evts.Add(new EventJson
                {
                    time = ev.SimulationTime,
                    tick = ev.SimulationTick,
                    message = ev.Message
                });
            json.events = evts.ToArray();

            return json;
        }

        /// <summary>
        /// Collect official two-terminal measurements into a compact list.
        /// A GateMeasurement may lack the official SpeedCmS when only raw
        /// gates were crossed; the authoritative value comes from
        /// <see cref="SpeedMeasuredEvent"/> results kept in the session.
        /// </summary>
        private List<SpeedMeasurementResult> CollectSpeedMeasurements()
        {
            var list = new List<SpeedMeasurementResult>();
            if (Session == null) return list;
            foreach (var g in Session.Measurements)
            {
                list.Add(new SpeedMeasurementResult(
                    g.PairId,
                    g.FirstGate,
                    g.SecondGate,
                    g.StartTime,
                    g.EndTime,
                    g.DistanceCm,
                    g.AverageSpeedCmS));
            }
            return list;
        }

        private bool SpeedWithinLimit(string pairId, float speedCmS)
        {
            if (_definition == null) return true;
            foreach (var d in _definition.objectives)
            {
                if (d.type == ObjectiveType.SpeedPair &&
                    string.Equals(d.pairId, pairId, StringComparison.Ordinal))
                {
                    if (d.maxSpeedCmS <= 0f) return true;
                    return speedCmS <= d.maxSpeedCmS + 0.001f;
                }
            }
            // No configured limit → no violation.
            return true;
        }

        /// <summary>
        /// Write the run result JSON to <paramref name="path"/>. When path is
        /// null, writes to <c>persistentDataPath/Runs/run_XXXX.json</c>.
        /// Returns the written path, or null on failure.
        /// </summary>
        public string ExportResult(string path = null)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    string dir = Path.Combine(Application.persistentDataPath, "Runs");
                    Directory.CreateDirectory(dir);
                    path = Path.Combine(dir, Session.RunId + ".json");
                }
                File.WriteAllText(path, UnityEngine.JsonUtility.ToJson(BuildResultJson(), true));
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Scenario] Export result failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>Reload an exported result file (for verification/agents).</summary>
        public static RunResultJson LoadResultJson(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                return UnityEngine.JsonUtility.FromJson<RunResultJson>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Scenario] Load result failed: {ex.Message}");
                return null;
            }
        }

        // ================================================================
        //  Internals
        // ================================================================

        private void BuildDefaultRules()
        {
            _rules.Add(new SlowZoneRule());
            _rules.Add(new CollisionRule());
            _rules.Add(new FalseStartRule());
            _rules.Add(new SpeedGateRule());
            _rules.Add(new CompletionRule());
            // Step 10: competition scoring rules.
            _rules.Add(new LineContactRule());
            _rules.Add(new CourseDepartureRule());
            _rules.Add(new ObjectiveRule());
        }

        private void UpdateTelemetry()
        {
            if (GetTelemetry != null)
            {
                _previousTelemetryPosition = _telemetry.Position;
                _telemetry = GetTelemetry();
            }
            if (_context != null)
                _context.Telemetry = _telemetry;
        }

        private static bool MatchesConfiguredTrigger(string configuredId, string actualId)
        {
            return string.IsNullOrEmpty(configuredId) || string.Equals(configuredId, actualId, StringComparison.Ordinal);
        }

        /// <summary>
        /// Step 8.54: optional finish-direction check. When enabled, the finish
        /// only counts if the vehicle is moving from the start-trigger side
        /// toward the finish-trigger side. Falls back to accepting when the
        /// start trigger is unknown.
        /// </summary>
        private bool RequiresFinishDirection()
        {
            if (_definition == null || !_definition.requireFinishDirection) return false;
            return _document != null &&
                   !string.IsNullOrEmpty(_definition.startTriggerId) &&
                   _document.FindTrigger(_definition.startTriggerId) != null;
        }

        private bool CrossingInCorrectDirection()
        {
            var start = _document?.FindTrigger(_definition?.startTriggerId);
            var finish = _document?.FindTrigger(_definition?.finishTriggerId);
            if (start == null || finish == null || start.IsSpeedTerminal || finish.IsSpeedTerminal)
                return true; // cannot determine → accept

            var startCenter = RegionCenterWorld(start.Region, _document.Grid.TileSizeCm);
            var finishCenter = RegionCenterWorld(finish.Region, _document.Grid.TileSizeCm);
            var expected = finishCenter - startCenter;
            expected.y = 0f;

            var delta = _telemetry.Position - _previousTelemetryPosition;
            delta.y = 0f;

            if (expected.sqrMagnitude < 1e-6f || delta.sqrMagnitude < 1e-6f) return true;
            return Vector3.Dot(expected, delta) > 0f;
        }

        private void SetStateInternal(ScenarioState next)
        {
            if (_state == next) return;
            _state = next;
            if (_context != null) _context.State = _state;
            _events.Publish(new ScenarioStateChangedEvent(_state, _signal));
            StateChanged?.Invoke(_state, _signal);
        }

        private void SetSignalInternal(StartSignalState next)
        {
            if (_signal == next) return;
            _signal = next;
            if (_context != null) _context.Signal = _signal;
            _events.Publish(new ScenarioSignalChangedEvent(StartLight));
            // Reflect the signal onto the placed start-signal object (Step 8.6).
            if (_document != null && _definition != null &&
                !string.IsNullOrEmpty(_definition.startSignalObjectId))
            {
                var obj = _document.FindObject(_definition.startSignalObjectId);
                if (obj != null && obj.Type == ObjectType.StartSignal)
                    obj.SignalState = _signal;
            }
        }

        private void LogEvent(string message)
        {
            Session.Events.Add(new ScenarioEvent(_clock.Tick, _clock.Time, message));
        }

        private static Vector3 RegionCenterWorld(GridRegion region, float tileSizeCm)
        {
            float cx = (region.x + region.width * 0.5f) * tileSizeCm;
            float cz = (region.z + region.height * 0.5f) * tileSizeCm;
            return new Vector3(cx, 0f, cz);
        }
    }
}
