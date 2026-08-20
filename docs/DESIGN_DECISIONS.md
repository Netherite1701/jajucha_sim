# Design Decisions

ADR-like log. Never silently make major architectural changes; if a decision
must change, document why, mark the old one superseded, and record the
replacement here and in affected architecture docs.

---

## DD-001 — Fresh implementation, not a repair of the old simulator

Status: Accepted

Context: The old `jajucha-sim` repository is a working but architecturally messy
Unity + Python project. The master plan mandates a fresh implementation.

Decision: Do not salvage or incrementally repair the old architecture. The old
project is reference only for manual-derived APIs (e.g. the `jchm` Python client
API and TCP protocol framing were inspected for ground truth).

Consequences: Clean assembly boundaries from day one; no inherited
technical debt.

---

## DD-002 — One simulation owner (SimulationManager)

Status: Accepted

Context: Avoid reproducing the old project's tangle of independent managers
(`VehicleManager`, `RuntimeManager`, `ScenarioManager`, …).

Decision: A single `SimulationManager` owns lifecycle states and ticks
registered `ISimulationSystem`s. It does **not** drive the car, capture
cameras, talk to Python, score, or render UI.

Consequences: New subsystems plug in via `ISimulationSystem` /
`SimulationSystemBehaviour`. The manager coordinates; subsystems implement.

---

## DD-003 — Fixed timestep 100 Hz, simulation time separate from render FPS

Status: Accepted

Context: Reproducibility, motor dynamics, sensor scheduling, command replay.

Decision: `FixedDeltaTime = 0.01` (100 Hz). A scheduler accumulates
`Time.unscaledDeltaTime * TimeScale` and ticks at the fixed rate, capped by
`maxTicksPerFrame` (spiral-of-death guard). `Step`/`Advance` run exact ticks
independent of real time.

Consequences: Determinism tests are exact on tick count; time uses a `1e-3`
tolerance because `FixedDeltaTime` is a float.

---

## DD-004 — Deterministic PRNG via SimulationRandom (SplitMix64)

Status: Accepted

Context: Identical seeds must reproduce identical outcomes across editor/player.

Decision: `SimulationRandom` implements SplitMix64. Core logic draws randomness
only from `SimulationContext.Random`. `UnityEngine.Random` is forbidden in core.

Consequences: No reliance on `System.Random`'s unstable cross-version algorithm.

---

## DD-005 — Typed event bus, not string messages

Status: Accepted

Context: Compiler-checked lifecycle signaling; avoid `Dictionary<string,object>`.

Decision: `SimulationEventBus` with `Subscribe<T>/Unsubscribe<T>/Publish<T>`
over `readonly struct` events. Publish snapshots subscribers. No priorities,
reflection, async, or network event replication.

---

## DD-006 — Project world scale: 1 Unity unit = 1 cm

Status: Accepted

Context: The master plan fixes the world scale permanently.

Decision: All dimensions, geometry, speeds, sensor positions use centimeters.
Gravity default `-981 cm/s²`. Vehicle prefab root scale `(1,1,1)`.

---

## DD-007 — Seed stored as `long` in SimulationConfig

Status: Accepted

Context: Unity's serializer does not serialize `ulong`.

Decision: `SimulationConfig.randomSeed` is `long` (default 12345) and cast to
`ulong` (unchecked) when constructing `SimulationRandom`.

---

## DD-008 — One tests asmdef split into Edit/Play + shared support

Status: Accepted (small deviation from plan 1.4)

Context: The plan recommends a single `JajuchaSim.Core.Tests.asmdef`. Unity's
EditMode test runner only collects test assemblies restricted to the Editor
platform (`includePlatforms: ["Editor"]`); a universal test asmdef is invisible
to the EditMode runner, and an Editor-only asmdef cannot ship PlayMode tests.

Decision: Three asmdefs under `Core/Tests/`:
- `JajuchaSim.Core.TestSupport` (all platforms, not a test assembly) — shared
  test doubles (`FakeSimulationSystem`, `CounterSimulationSystem`).
- `JajuchaSim.Core.EditModeTests` (Editor + TestAssemblies).
- `JajuchaSim.Core.PlayModeTests` (all platforms + TestAssemblies).

Consequences: Both modes discover tests; no test-double duplication.

---

## DD-014 — Use one shared tile grid for course features

Status: Accepted

Context:
Roads, tunnels, ramps, signs, obstacles, and triggers need consistent placement
and runtime editing. The earlier plan considered module/socket/composite systems
or spline-based roads.

Decision:
All course features use the same fixed-size tile coordinate system
(`GridCoordinate`). Four layers (road, structure, object, trigger) may overlap
on the same tile. Roads are stored as boolean flags (no direction/spline data);
the renderer determines visual connectivity from neighbour tile states.

Consequences:
- Simple runtime snapping and editing.
- Consistent serialization (JSON via `CourseData`/`CourseSerializer`).
- Structures, objects, and triggers can share tiles with roads using layers.
- Course editing works identically in Editor and standalone build.
- Renderer must infer road shape from neighbour connectivity.
- No spline representation as source of truth.

Alternatives considered:
- Spline-only roads (rejected: complicates editing and snapping).
- Module/socket/composite system (rejected: over-engineered for tile-based map).
- Independent snapping systems per layer (rejected: inconsistent coordinates).
- Unity SceneView placement (rejected: not available in standalone build).

## DD-009 — Scene auto-initializes from assigned config in Awake

Status: Accepted

Context: Plan says "the manager should own initialization" and not depend on
arbitrary `Awake→Start` order. But loading `Simulation.unity` should yield
`State == Ready` without external code.

Decision: `Awake()` calls `Initialize()` once if a `SimulationConfig` is
assigned and the state is `Uninitialized`. Explicit lifecycle methods remain
the authority and may be called by tests/headless runners.

Consequences: Scene just works; tests that need controlled init call
`SetConfigForTesting` + `Initialize()` explicitly (config is null at
`AddComponent` time, so auto-init does not fire).

---

## DD-015 — Two-terminal speed measurement (not Rigidbody velocity)

Status: Accepted

Context: Competition hardware measures speed with two fixed terminals and
`v = d / (t2 - t1)`. An earlier single "speed gate" idea and Unity's internal
Rigidbody velocity do not reproduce that method.

Decision:
- Represent each gate as a `speed_terminal` feature (edge-snapped line with
  `pairId`, role A/B, width).
- Derive pair distance from world/grid geometry (`SpeedTerminalGeometry`).
- Detect crossings with segment-vs-line tests on P0→P1 each tick.
- Timestamp with `SimulationClock` only.
- `SpeedTerminalPairRule` produces the official `SpeedMeasuredEvent`; scoring
  must consume that value. Rigidbody speed stays debug-only.
- Default direction is A→B; reverse B→A is ignored unless explicitly allowed.
- Keep `TriggerType.SpeedGate` and JSON `"speed_gate"` as legacy aliases.

Consequences:
- Official results can differ slightly from instantaneous vehicle velocity.
- Incomplete pairs warn at validation and never emit a measurement.
- Map editor exposes separate Speed A / Speed B tools sharing a pair id.

Alternatives considered:
- Single gate + ground-truth velocity (rejected: does not match hardware).
- Physics trigger colliders (rejected: can miss fast crossings between ticks).
- Manually entered distance (rejected: configuration error risk).
---

## DD-016 — One authoritative simulator scene

Status: Accepted

Context: Earlier steps accumulated subsystem demos and a `Simulation.unity`
debug scene. Users should never guess which scene to open.

Decision: `Assets/JajuchaSim/Scenes/JajuchaSimulator.unity` is the one runtime
scene for Play Mode, standalone builds, runtime map editing, driving, scoring,
and automated testing. It contains a fixed, stable hierarchy (validated by
tests). Small isolated test scenes may exist under `Scenes/Tests/` but are not
user-facing. `Simulation.unity` remains only as a legacy kernel debug scene.

Consequences: One place to wire; the scene validation test keeps it honest.

## DD-017 — Runtime-generated course content (course-data authority)

Status: Accepted

Context: The runtime scene should not permanently contain the full road and
structures; the course file must stay authoritative.

Decision: The scene contains system roots, managers, the vehicle behaviour,
observer camera, runtime UI, and services only. The course loads from
the selected `Courses/2026_preliminary.json` or `Courses/2026_final.json` and
is generated at runtime under the course roots.

Consequences: Course data is version-controlled and testable in isolation;
scene files stay small.

## DD-018 — External user Python code (never inside Unity)

Status: Accepted

Context: Student ANN/FSM implementations must run unchanged on the real
Jajucha; they cannot live inside Unity.

Decision: User code lives in `python/user/` (owned entirely by the user).
`jchm` is the real-compatible vehicle API; `jchm_sim` is simulator/testing
tooling only. The simulator never writes into `python/user/` and never
requires that it launched the Python process (11.24).

Consequences: Users run `python python/user/main.py` from a separate terminal;
`run_development.ps1` is a convenience wrapper only.

## DD-019 — One shared tile grid

Status: Accepted

Context: Roads, structures, objects, and triggers must share coordinates so
runtime generation, scoring, and the map editor agree.

Decision: A single fixed-size tile grid (`CourseGrid`) is the authoritative
course representation; all features snap to it. No splines become source of
truth.

Consequences: Consistent world coordinates (`GridToWorld`), simple validation,
and a clean JSON format.

## DD-020 — Standalone-first runtime tools

Status: Accepted

Context: Normal users must not open the Unity Editor.

Decision: All runtime tools (bootstrap, map editor, scenario/scoring panels,
status bar, error display, file logging) work in the standalone build with
zero Editor dependencies. The Unity Editor is only needed to build the
executable.

Consequences: `ApplicationBootstrap`, `CourseManager`, `RuntimeFileLogger`,
and friends live in `JajuchaSim.App` and never import `UnityEditor`.

## DD-021 — 2026 competition courses are authoritative

Status: Accepted

Context: Training geometry must match the supplied 2026 documents, while the
day-of additional mission and location remain unknown.

Decision: ship preliminary and final 2026 courses as the only defaults. The
official print artwork drives both the visible surface and the 5 cm logical
mask. Mission type/location are selected before every first run or drawn from
a recorded seed in Random mode. Unspecified scoring/timing values are labelled
`비공식 연습값`.

Consequences: the simulator does not present an example layout as an official
course, and every random training run is reproducible.

## DD-022 — One authoritative vehicle prefab

Status: Accepted

Context: Duplicated slightly-different vehicle prefabs caused drift.

Decision: `_Vehicle/JajuchaVehicle` in the authoritative scene is the single
vehicle node; `VehicleSystemBehaviour` builds the chassis + 4 wheel colliders +
sensor mounts on it at runtime from one `VehicleConfig`. All root transforms
are identity. The same hierarchy ships as the authoritative template asset
`Assets/JajuchaSim/Prefabs/Vehicle/JajuchaVehicle.prefab` (generated by
`tools/generate_prefabs.py`, Step 11.30/11.31) — one source, no duplicates.

Consequences: Vehicle structure is generated deterministically and validated by
`SceneHierarchyValidationTests` and workflow tests.

## DD-023 — Generic controller-independent testing

Status: Accepted

Context: Batch/testing must not depend on ANN/FSM internals.

Decision: `TestRunner`/`BatchRunner` drive any external controller through the
same `Scenario → ScoreManager → RunResult` path; pass criteria are separate
from the competition score.

Consequences: The same controller can be tested manually or in batch; results
are comparable.

## DD-024 — Scoring shared by manual and automated runs

Status: Accepted

Context: Manual runs and batch tests must produce the same scores.

Decision: One `ScoreManager` + `ScoringConfig` path is used by both the runtime
scenario and the automated runners.

Consequences: A score from the UI and a score from a batch run are directly
comparable; `docs/SCORING.md` documents the shared path.

## DD-025 — Explicit application modes

Status: Accepted

Context: Mode must never be inferred from which UI panel is visible.

Decision: `ApplicationMode` (Drive / MapEditor / SingleTest / BatchTest) is an
explicit state on `ApplicationBootstrap`; `SetMode` applies the behavior and
switching is logged. Default key bindings (F1/F2/F3) are configurable in code.

Consequences: Tests and UI can rely on a single source of truth for mode.
