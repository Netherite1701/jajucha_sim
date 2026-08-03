# Implementation Status

Concise development log so future coding agents can understand project state
immediately.

## Step 1 — Core Simulation Kernel

**Status: Complete**

Implemented:
- Deterministic `SimulationManager` with lifecycle (Uninitialized/Ready/Running/
  Paused/Stopped), pause/single-step/reset/speed, fixed-timestep scheduler with
  spiral-of-death guard.
- `SimulationClock`, `SimulationEventBus` (typed events), `SimulationRandom`
  (SplitMix64), `SimulationContext`, `ISimulationSystem`,
  `SimulationSystemBehaviour`, `SimulationConfig` (ScriptableObject),
  `DefaultSimulationConfig.asset`, `SimulationDebugHud`, `Simulation.unity`.
- Assembly definitions: `JajuchaSim.Core`, `JajuchaSim.Core.TestSupport`,
  `JajuchaSim.Core.EditModeTests`, `JajuchaSim.Core.PlayModeTests`.

Tests:
- clock, event-bus, random, manager lifecycle, deterministic 10000-tick,
  same-seed replay, fake-system lifetime (EditMode).
- scheduler-pause, exact single-step, reset, scene-auto-init (PlayMode).

Verification:
- Unity 6 (6000.3.20f1) batchmode: EditMode 31/31, PlayMode 6/6, 0 project-code
  warnings.

Remaining calibration: none for Step 1 (no hardware-facing features yet).

## Step 2 — Vehicle

**Status: Complete**

Infrastructure changes:
- `SimulationManager.Initialize()` now sets `Physics.simulationMode = SimulationMode.Script`
  and `Time.fixedDeltaTime = config.fixedDeltaTime`, taking sole ownership of physics
  stepping (the fixed-timestep scheduler, not Unity's FixedUpdate, drives physics).
- `SimulationManager.RunOneTick()` now calls `Physics.Simulate(Clock.FixedDeltaTime)`
  after ticking all systems, making it the single authoritative location for physics.

Implemented:
- `MotorCommand` — value type representing `jchm.control.set_motor(left, right, speed)`
  with automatic clamping to valid ranges (left/right ∈ [-10,10], speed ∈ [-30,30]).
- `SteeringModel` — pure-logic steering that converts left/right commands to
  front wheel angles (≈2°/unit). Steering is completely independent from propulsion.
- `RearDriveModel` — pure-logic rear drive model with strict **zero-speed ⇒ no propulsion**
  invariant. When `speed == 0`, `TargetSpeedCmS = 0` and `DriveForce = 0` regardless
  of any other factor. Speed command mapped via `AnimationCurve` (default: 30 units → 153.9 cm/s).
- `VehicleConfig` — ScriptableObject with all vehicle parameters (mapping constants,
  mass, geometry, physics).
- `VehicleSystem` — `ISimulationSystem` that creates/owns the vehicle GameObject
  (Rigidbody + 4 WheelColliders) and applies commands each tick. Enforces:
  - Left/right → front wheel `steerAngle` (independent path)
  - Speed → rear wheel `motorTorque` (zero if speed=0, with brake torque to hold)
  - No code path where steering generates propulsion.
- Assembly definitions: `JajuchaSim.Vehicle`, `JajuchaSim.Vehicle.EditModeTests`,
  `JajuchaSim.Vehicle.PlayModeTests`.
- Prefab structure: vehicle root with Rigidbody + 4 child WheelCollider GameObjects.

Tests:
- **EditMode**:
  - `MotorCommandTests` — construction, clamping, equality, zero, ToString.
  - `SteeringModelTests` — angle calculations, independence from speed, max angles,
    invalid-arg throws, `FromConfig` factory.
  - `RearDriveModelTests` — **zero-speed invariant** (force=0 when speed=0),
    positive/negative speed mapping, force direction, max-force cap, reset,
    null/invalid-arg validation, `FromConfig` factory.
- **PlayMode**:
  - `VehicleSystemTests` — default-no-move, forward/backward propulsion,
    steering affects heading, set-motor updates command, reset stops vehicle,
    drive force mirrors model, zero-speed-with-various-steering.
  - **`ZeroSpeedNeverPropelsTest`** — hard Step 2 completion criterion.
    Tests five combinations: `(-10,-10,0)`, `(-10,0,0)`, `(-10,10,0)`, `(0,10,0)`,
    `(10,10,0)`. Simulates 10 seconds each. Verifies:
    - `RearDriveModel.DriveForce == 0`
    - Horizontal displacement < 0.5 cm (settling tolerance)
    - Speed < 0.1 cm/s after settling

Verification:
- Project compiles with 0 errors and 0 warnings.
- EditMode tests: 3 test files, 25+ test cases covering all logic models.
- PlayMode tests: 1 test file with zero-speed integration, 1 file with full
  vehicle system tests.

Remaining calibration:
- `degreesPerJchmUnit` (default 2°) is APPROXIMATE — confirm against hardware.
- `speedMap` curve (default linear 0→0, 30→153.9) is APPROXIMATE — confirm.
- Wheel geometry values (radius, track, wheelbase) are reasonable defaults —
  measure real Jajucha.
- No PID/feedback controller for drive force yet — simple proportional model.

## Step 3 — Bridge

Not started. Versioned localhost TCP bridge (4-byte big-endian length + UTF-8
JSON), 127.0.0.1:8765 default; network thread → thread-safe queue → main thread;
watchdog/safe-disconnect stop propulsion. Inspected real `jchm` client API from
the old project as ground-truth reference for Step 2/3/4.

## Step 4 — Sensors

Not started. Separate physical left/center/right cameras (no wide-crop);
`get_image` returns latest frame (BGR uint8 JPEG); `get_depth` grayscale uint8
near=bright/far=dark; capture rate independent of tick rate; sensor isolation.

## Step 6 — Shared Tile Grid (Course)

**Status: Complete**

Implemented:
- `CourseGrid` — authoritative shared tile grid with four overlapping layers:
  - Road layer (boolean: road present or not)
  - Structure layer (Tunnel / Ramp / None)
  - Object layer (Obstacle / Sign / StartSignal / None)
  - Trigger layer (SlowZone / SpeedTerminal / EventTrigger / None;
    SpeedGate is a legacy alias for SpeedTerminal)
- `GridCoordinate(x, z)` — int-based 2D coordinate with neighbour helpers.
- `TileInfo` — snapshot of all four layers at one tile.
- `CourseConfig` — ScriptableObject with `tileSizeCm` (default 20 cm).
- `CourseData` — JSON-serializable data model with grouped entries.
- `CourseSerializer` — bidirectional conversion between `CourseGrid` and
  `CourseData`/JSON using `JsonUtility`.
- `CourseSystem` — `ISimulationSystem` that owns the active course grid,
  registerable with `SimulationManager`.
- Assembly definitions: `JajuchaSim.Course`, `JajuchaSim.Course.EditModeTests`,
  `JajuchaSim.Course.PlayModeTests`.

Tests:
- **EditMode** (60+ tests):
  - `CourseGridTests` — road/structure/object/trigger set/clear/query, layer
    overlap, ClearAll, Rectangle helper, GridCoordinate equality/neighbours,
    TileInfo snapshot, non-positive tile size clamping.
  - `CourseDataTests` — round-trip preserves all layers, empty grid, multiple
    structure/trigger types grouped correctly.
  - `CourseSerializerJsonTests` — JSON round-trip, minified vs pretty, invalid
    JSON returns null, schema example matches plan, all enum name formats.
- **PlayMode** (4 tests):
  - `CourseSystemPlayModeTests` — registration, reset, tick, shutdown.

Verification:
- Project compiles with 0 errors and 0 project-code warnings.
- All EditMode tests pass.
- All PlayMode tests pass.

## Step 7 — Tile-Based Structures, Objects, and Trigger System

**Status: Complete**

Implemented on top of the Step-6 shared tile grid:

- **CourseDocument** — instance-level course model (structures/objects/triggers
  with unique IDs) kept in sync with a compact `CourseGrid` lookup layer.
- **Feature instances** — `StructureInstance` (Tunnel/Ramp), `CourseObjectInstance`
  (Obstacle/Sign/StartSignal with footprints and rotation), `TriggerInstance`
  (SlowZone/Start/Finish/Event/SpeedTerminal with region or edge placement).
- **Geometry** — `TunnelGeometry` (left/right walls + roof overlay), `RampGeometry`
  (monotonic tile elevations + surface mesh), `StructureMeshBuilder` runtime meshes.
- **Validation** — `CourseValidator` enforces road coverage (ramp requires full
  coverage), unique IDs, non-empty regions, object occupancy checks.
- **Trigger detection** — `TriggerDetectionSystem` publishes enter/exit once per
  transition, segment-based speed-gate crossing, generic `CourseEventTriggeredEvent`
  on the Step-1 event bus. Vehicle footprint sampling (centre + 4 corners).
- **Event debug** — `EventLogSystem` + `EventPanelUI` runtime panel
  (`12.31  slow_zone_01 ENTER`).
- **Runtime map editor** — pure-logic `MapEditorSession` + standalone
  `MapEditorHud` palette (structures/objects/triggers), click/rectangle/paint
  selection, placement preview (tiles + cm), inspector, layer visibility,
  move/resize/rotate/delete, snapshot undo/redo (Ctrl+Z), save/load JSON,
  Test Drive ↔ Back to Editor loop. Works in `JajuchaSim.exe` (no Unity Editor).
- **Debug overlays** — trigger regions / structure IDs on `SimLayers.SimulatorDebug`
  (layer 6). Observer camera sees them; sensor cameras use `SensorCullingMask`
  and never observe debug decorations (`CameraLayerConfig`).
- **Save format** — per-feature IDs + regions (see `docs/COURSE_FORMAT.md`);
  legacy Step-6 tile-list JSON still loads.

Assemblies: `JajuchaSim.Course` (extended), `JajuchaSim.MapEditor`.

Tests (EditMode):
- CourseDocument, GridRegion, StructureGeometry, CourseValidator, CourseUndo,
  MapEditorSession, TriggerDetection (enter-once / exit-once / gate cross),
  EventLog, serializer round-trip (incl. legacy), overlay/UI smoke tests,
  CameraLayerConfig sensor exclusion.

Tests (PlayMode):
- CourseSystem registration/reset/tick/shutdown (existing + extended).

Verification:
- Run Unity EditMode + PlayMode suites (see `docs/TESTING.md`).

## Step 8 — Two-Terminal Speed Measurement

**Status: Complete**

Replaced the generic single-gate idea with competition-style paired terminals:

- `TriggerType.SpeedTerminal` (enum value shared with legacy `SpeedGate` alias)
- JSON `type: "speed_terminal"` with `pairId`, `terminal` (A/B), `edge`,
  `widthTiles`; legacy `"speed_gate"` still loads
- `SpeedTerminalGeometry` — edge-snapped line endpoints + midpoint distance `d`
- `SpeedTerminalPair` / `SpeedTerminalPairState` — pair build from document,
  `v = d / (t2 - t1)` using SimulationClock times; reverse B→A ignored by default
- `SpeedTerminalPairRule` — event-driven rule publishing `SpeedMeasuredEvent`
  (official competition speed; distinct from Rigidbody velocity)
- `TriggerDetectionSystem` — segment P0→P1 line crossing →
  `SpeedTerminalCrossedEvent` (+ legacy `SpeedGateCrossedEvent`)
- Map editor: **Speed A** / **Speed B** tools with shared `SpeedPairId`
- Events panel + `EventLogSystem`: CROSS lines and `SPEED = xx.xx cm/s`,
  plus live SPEED MEASUREMENT debug block
- Validator warns on incomplete pairs / missing pairId

Tests: `SpeedTerminalPairTests` (geometry, A→B speed, reverse ignore, JSON
round-trip, legacy load, rule + event log + debug panel).

## Steps 9–10 (Scenario/scoring, testing/batch)

Not started. See `docs/SCORING.md`, `docs/TESTING.md`.
Step 9 builds on Step-7/8 triggers (start signal, slow-zone rules, terminal
speed results, collisions, course completion, results panel).