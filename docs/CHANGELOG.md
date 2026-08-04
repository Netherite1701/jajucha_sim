# Changelog

## [Unreleased]

### Step 8 — Scenario, Timing, and Scoring System

Turned the simulator into a complete competition/test environment. The
simulator knows ground truth for scoring; the student's Python only sees the
simulated cameras and jchm interface (never `STARTED` flags or scores):

- `ScenarioManager` state machine Idle → Ready → Countdown → Running →
  Finished/Aborted, start-signal sequence (RED → YELLOW → GREEN, configurable
  durations), immediate or normal start mode, abort with propulsion stop
- `ScenarioDefinition` JSON: start/finish triggers, max run time, start timing
  mode (signal-green vs start-gate-crossing), slow-zone rules with
  Fail/Penalty/Informational modes, false-start rule, scoring enable, result
  auto-save
- `RunTimer` derived from `SimulationClock` ticks — deterministic at any
  simulation speed; max-run-time → TIME_LIMIT
- Slow-zone speed measurement from Rigidbody-derived forward speed (never the
  jchm motor command); overlapping zones tracked independently by id
- Collision recording with per-object debounce (one incident per contact
  session, not one per physics callback)
- Speed-gate pair measurements from Step-7 `SpeedMeasuredEvent`
- `RunSession` per run: events with SimulationTick+SimulationTime, raw
  measurements, penalties, final result; JSON export + reload
- Runtime `ScenarioPanel` sidebar and `ResultsPanel` overlay (Run Again /
  Details / Export Result), driving view stays visible
- Map-editor SCENARIO section: start/finish trigger pickers from placed ids,
  max time, slow-zone speed, start mode, signal preview (RED/YELLOW/GREEN)
- jchm_sim automation API: `start_run`, `abort_run`, `get_run_status`,
  `get_result`, `wait_for_result(timeout)` (simulator-only test harness)

### Step 8 — Two-Terminal Competition Speed Measurement

Replaced the generic single speed-gate concept with paired terminals that match
competition hardware:

- Terminal A + Terminal B share a `pairId`; distance `d` is computed from world
  line midpoints (never manually entered)
- Official speed `v = d / (t2 - t1)` uses `SimulationClock` crossing times
- `SpeedTerminalPairRule` records A then B and publishes `SpeedMeasuredEvent`
  for scoring (Rigidbody velocity remains debug-only)
- Line-crossing detection on segment P0→P1 (not tiny colliders)
- Reverse order (B→A) ignored by default
- Map editor tools: Speed A / Speed B; JSON `speed_terminal` with legacy
  `speed_gate` load path retained
- Events/Scenario panel SPEED MEASUREMENT block + event-log CROSS/SPEED lines

### Step 7 — Structures, Objects, Triggers + Runtime Map Editor

Built the full tile-aligned feature stack on top of the Step-6 grid:

- `CourseDocument` instance model with unique IDs and compact grid sync
- Tunnel / ramp geometry + mesh builder; obstacle / slow-sign / start-signal
- Trigger layer: slow zone, start/finish, generic event, speed terminal
- `TriggerDetectionSystem` enter/exit + segment terminal crossing on EventBus
- `EventLogSystem` / `EventPanelUI` runtime events panel
- `MapEditorSession` + standalone `MapEditorHud` (palette, preview, inspector,
  layers, undo/redo, save/load, test-drive loop) — works without Unity Editor
- `SimLayers.SimulatorDebug` + `CameraLayerConfig` so sensor cameras never see
  debug overlays
- Extended JSON schema with regions/IDs; legacy Step-6 format still loads
- Validation, snapshot undo, and EditMode coverage for placement/triggers/save

### Step 6 — Shared Tile Grid (Course)

Added the unified tile grid system for all course features. Roads, structures,
objects, and triggers now share one fixed-size grid with configurable tile size.

**New subsystem:**
- `JajuchaSim.Course` assembly with:
  - `CourseGrid` — authoritative grid with four overlapping layers
    (road, structure, object, trigger).
  - `GridCoordinate(x, z)` — int-based 2D coordinate with neighbour helpers.
  - `TileInfo` — compact per-tile layer snapshot.
  - `CourseConfig` — ScriptableObject with `tileSizeCm` (default 20).
  - `CourseData` + `CourseSerializer` — JSON round-trip via `JsonUtility`.
  - `CourseSystem` — `ISimulationSystem` that owns the active grid.
  - Assembly definitions: `JajuchaSim.Course`,
    `JajuchaSim.Course.EditModeTests`, `JajuchaSim.Course.PlayModeTests`.

### Tests

- `CourseGridTests` (EditMode) — 60+ tests covering all layers, overlap,
  ClearAll, Rectangle, GridCoordinate equality/neighbours, TileInfo.
- `CourseDataTests` (EditMode) — round-trip preserves all layers, grouping.
- `CourseSerializerJsonTests` (EditMode) — JSON round-trip, error handling,
  schema, enum name parsing.
- `CourseSystemPlayModeTests` (PlayMode) — registration, reset, tick, shutdown
  integration with SimulationManager.

### Documentation

- `docs/IMPLEMENTATION_STATUS.md` — Step 6 marked complete.
- `docs/COURSE_FORMAT.md` — filled in with actual implementation schema.
- `docs/ARCHITECTURE.md` — Course subsystem marked implemented.
- `docs/DESIGN_DECISIONS.md` — DD-014 (shared tile grid).
- `docs/CONFIGURATION.md` — CourseConfig added.

### Step 2 — Vehicle Model

Added the Jajucha vehicle model with strict separation of steering and
propulsion, and the zero-speed invariant.

**Infrastructure:**
- `SimulationManager` takes sole ownership of physics stepping:
  `Physics.simulationMode = SimulationMode.Script` in `Initialize()`, and
  `Physics.Simulate(Clock.FixedDeltaTime)` called in `RunOneTick()` after
  system ticks. This is the single authoritative physics advance location.
- `Time.fixedDeltaTime` is synced to `config.fixedDeltaTime` on init.
- Physics solver iterations increased (defaultSolverIterations=20,
  defaultSolverVelocityIterations=5) for stable WheelCollider contact.

- `MotorCommand` — value type for `jchm.control.set_motor(left, right, speed)`
  with automatic clamping and equality.
- `VehicleConfig` — ScriptableObject with all vehicle parameters (mapping
  constants, mass, geometry, suspension defaults).
- `SteeringModel` — pure-logic steering: left/right → front wheel angles.
  Completely independent from speed/propulsion.
- `RearDriveModel` — pure-logic rear drive with **zero-speed invariant**:
  when `speed == 0`, `TargetSpeedCmS = 0` and `DriveForce = 0`.
  No code path where steering generates propulsion.
- `VehicleSystem` — `ISimulationSystem` that creates a vehicle GameObject
  (Rigidbody + 4 WheelColliders) and applies: left/right → front `steerAngle`,
  speed → rear `motorTorque` (zero when speed=0, brake torque to hold).
- Assembly definitions: `JajuchaSim.Vehicle`,
  `JajuchaSim.Vehicle.EditModeTests`, `JajuchaSim.Vehicle.PlayModeTests`.

### Tests

- `MotorCommandTests`, `SteeringModelTests`, `RearDriveModelTests` (EditMode).
- `VehicleSystemTests` (PlayMode) — forward/backward, steering, reset,
  zero-speed-with-various-steering.
- **`ZeroSpeedNeverPropelsTest`** (PlayMode) — hard Step 2 completion criterion.
  Tests 5 combinations with speed=0 over 10s each: verifies drive force = 0,
  displacement < 0.5 cm, speed < 0.1 cm/s.

### Documentation

- `docs/IMPLEMENTATION_STATUS.md` — Step 2 marked complete.
- `docs/MANUAL_COMPATIBILITY.md` — set_motor and zero-speed invariant
  marked IMPLEMENTED.

### Step 1 — Core Simulation Kernel

Added the deterministic simulation foundation. No vehicle, camera, Python
bridge, editor, ANN, or FSM code yet (by design).

- Fresh Unity 6 (6000.3.20f1) project with `Assets/JajuchaSim/` subsystem
  folders and assembly definitions (`JajuchaSim.Core`).
- `SimulationManager` — single owner of lifecycle states
  (Uninitialized/Ready/Running/Paused/Stopped) with Initialize/Start/Pause/
  Resume/Step/Stop/Reset/Advance and a fixed-timestep scheduler with
  spiral-of-death guard (`maxTicksPerFrame`). Render FPS kept separate from
  simulation frequency.
- `SimulationClock` (Tick/Time/FixedDeltaTime/TimeScale/IsPaused).
- `SimulationEventBus` — typed pub/sub over `readonly struct` events with
  safe unsubscribe-during-publish.
- `SimulationRandom` (SplitMix64) seeded from config; core never uses
  `UnityEngine.Random`.
- `ISimulationSystem` + `SimulationSystemBehaviour` base; runtime
  `RegisterSystem` for headless/test use.
- `SimulationConfig` ScriptableObject + `DefaultSimulationConfig.asset`.
- `SimulationDebugHud` — runtime HUD (Canvas + Start/Pause/Resume/Step/Stop/
  Reset buttons) built programmatically; works in standalone builds.
- `Simulation.unity` scene with `SimulationRoot` referencing the default config.
- World scale convention set: 1 Unity unit = 1 cm (gravity `-981 cm/s²`
  applied when physics arrives in Step 2).

### Tests

- EditMode tests: clock, event bus, random, manager lifecycle, deterministic
  10000-tick test, same-seed replay equivalence, fake-system lifetime.
- PlayMode tests: real-time scheduler not double-ticking while paused, exact
  single-step, full reset, scene auto-init to Ready.

### Verification

- Unity 6 batchmode: EditMode **31/31 passed**, PlayMode **6/6 passed**.
- Project-code compiler warnings: 0.

### Documentation

- `docs/ARCHITECTURE.md`, `docs/DESIGN_DECISIONS.md` (DD-001..009),
  `docs/MANUAL_COMPATIBILITY.md`, `docs/CONFIGURATION.md`,
  `docs/COURSE_FORMAT.md`, `docs/TESTING.md`, `docs/SCORING.md`,
  `docs/IMPLEMENTATION_STATUS.md`,
  `docs/architecture/01-core-simulation.md`.