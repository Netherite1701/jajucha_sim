# Testing

"Testing is integrated with scoring" and "controller-agnostic" are master-plan
rules. The full automated test/batch system arrives in Steps 9–10; this document
tracks what exists now and what is planned.

## Step 1 (implemented)

**Assemblies**
- `JajuchaSim.Core.TestSupport` — shared test doubles (`FakeSimulationSystem`,
  `CounterSimulationSystem`).
- `JajuchaSim.Core.EditModeTests` — pure-C#/EditMode kernel tests.
- `JajuchaSim.Core.PlayModeTests` — Unity PlayMode tests incl. scene load.

**EditMode coverage**
- `SimulationClockTests` — initial zero, 100-tick time, reset, time-scale,
  invalid-arg throws, negative-advance throws.
- `SimulationEventBusTests` — subscribe/publish once, unsubscribe, payload,
  multiple subscribers, clear, unsubscribe-during-publish safety, null-arg throw.
- `SimulationRandomTests` — same-seed reproducibility, float/int range, distinct
  seeds diverge, reset restores sequence.
- `SimulationManagerTests` — initialize→Ready, null-config throws, start, pause/
  resume, stop, stop→start blocked, reset from stopped/running, single-step,
  step-before-start no-op, N-tick advance, **deterministic 10000-tick test**
  (clock + counter), **same-seed replay equivalence**, fake-system lifetime
  through the manager (initialize→ticks→shutdown).

**PlayMode coverage**
- Initialize→Ready under real Unity.
- Start→Pause stops auto-advancing the clock.
- Single `Step()` advances exactly one tick (paused).
- Reset returns to Ready/tick 0.
- Registered system receives scheduler ticks under real time.
- `Simulation.unity` loads and the manager auto-initializes to Ready from the
  assigned config asset.

## Running the tests

From the command line (Unity 6000.3.20f1):

```pwsh
$u = "C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe"
& $u -batchmode -nographics -projectPath C:\dev\jajucha-sim `
    -runTests -testPlatform editmode -testResults test-results-editmode.xml `
    -logFile unity-editmode.log
& $u -batchmode -nographics -projectPath C:\dev\jajucha-sim `
    -runTests -testPlatform playmode -testResults test-results-playmode.xml `
    -logFile unity-playmode.log
```

Current recorded result (Step 8):
- EditMode: **331/331 passed** (includes `SpeedTerminalPairTests`).
- Project-code compiler warnings: **0**.

Step 7–8 EditMode coverage (Course/MapEditor) includes tunnel/ramp geometry,
placement validation, map-editor session tools, trigger enter/exit once,
speed-terminal segment crossing, two-terminal `v = d/(t2-t1)` measurement
(`SpeedTerminalPairTests`), document save/load (incl. legacy `speed_gate` JSON),
snapshot undo/redo, event log panel, and sensor-camera debug-layer exclusion.

## Determinism rules for tests

- Tick count is exact; time uses `1e-3` tolerance (float `FixedDeltaTime`).
- Identical course + seed + initial state + command sequence ⇒ repeatable.
- `ResetSimulation()` clears clock, random, events, accumulator, and systems.

## Planned (Steps 9–10)

**Step 10 implemented** — the combined Competition Evaluation System:

- Controller-agnostic `RunResult` (status, elapsed time, base/final score,
  penalties, line violations, collisions, objective results, two-terminal
  speed with pass/fail, timeout reason).
- `TestRunner.RunSingle` — Reset → Start → external controller drives →
  Scenario ends → ScoreManager finalizes → RunResult → TEST PASS/FAIL from
  pass criteria. Uses the same scoring path as manual runs.
- `BatchRunner` — deterministic seed per run (baseSeed+i), `BatchSummary`
  (runs, average/best/worst score, perfect runs, completed, timeouts, line
  violations, collisions, objective failures, pass rate), batch CSV export.
- `PassCriteria` — `mustComplete`, `minimumScore`, `maximumCollisions`,
  `maximumLineContacts`, `requiredObjectives`; SEPARATE from the competition
  score (a run can score 85 and still FAIL a test with minimumScore=90).
- `RegressionReport` — baseline vs current batch comparison from official
  RunResults only (no controller internals).
- `CommandRecorder`/`CommandReplay` — motor-command trace (`set_motor(left,
  right, speed)`) + replay.
- `FailureDiagnostics` — event log, penalty log, motor trace, final pose,
  objective states, camera/depth frame slots; JSON save/load.
- `ScenarioRunSnapshot`/`DebugReRun` — same course + scenario + seed re-run
  at 1× speed with full runtime UI.
- Per-run JSON (`RunResultJson`) with objectives/speedMeasurements/base score,
  and batch CSV (Step 10.31).

Manual runs and batch runs produce identical official results (same
Scenario → ScoreManager → RunResult path).
## Step 11 — Workflow / scene / bootstrap tests

New assembly `JajuchaSim.App` (runtime) + `JajuchaSim.App.EditModeTests` +
`JajuchaSim.App.PlayModeTests`:

**EditMode**
- `BootstrapResultTests` — Success/FailedSystem/ErrorCode/Message + readable
  display.
- `ApplicationConfigTests` — defaults, JSON load, normalization, command-line
  overrides (`--course/--mode/--simulation-speed/--no-debug-ui/--batch-config`),
  mode parsing.
- `RuntimeDataPathsTests` — writable data root, sub-directories, and 2026 course resolution.
- `Competition2026CourseTests` — both stages validate 41 panels, dimensions,
  official checkpoint order, path structures, print objects, and five 30 cm candidates.
- `SceneHierarchyValidationTests` — loads `JajuchaSimulator.unity` and verifies
  the fixed hierarchy, exactly-one SimulationManager / observer camera / bridge
  / vehicle / sensors / map editor, wired references, and required layers
  (Step 11.28).

**PlayMode**
- `WorkflowIntegrationTests` — programmatic bootstrap: success + course load,
  missing-course readable failure, vehicle spawn, bridge readiness, first
  camera frame, reset lifecycle, explicit mode transitions, scene validation
  (Step 11.51).
- `SceneBootstrapPlayModeTests` — loads the authoritative scene and verifies
  bootstrap reaches READY, Drive mode starts the simulation, observer + bridge
  exist, and MapEditor mode pauses (Step 11.51).

**Python**
- `python/tests/test_examples.py` — `jchm`/`jchm_sim` imports, all examples and
  `python/user/main.py` compile (`py_compile`), and a readable connection-error
  behavior check (Step 11.52).

Verification (Step 11): Unity EditMode **473/473**, PlayMode **48/48**, Python
**29/29**, 0 project-code warnings.
