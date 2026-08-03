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