# Jajucha Simulator v2

A clean, deterministic Unity simulator for the **Jajucha** autonomous-driving
vehicle. The official Jajucha manual (`docs/자주차 매뉴얼.pdf`) is the primary
source of truth for all real-vehicle behavior and APIs.

## Status

Steps **1–8** are implemented (core kernel, vehicle, bridge, sensors, shared
tile grid, structures/objects/triggers, runtime map editor, scenario/timing/
scoring). See `docs/IMPLEMENTATION_STATUS.md` for details. Next: Step 9 ANN
perception + FSM integration/debugging support.

## World scale

`1 Unity unit = 1 centimeter`. Default gravity `-981 cm/s²`. Vehicle prefab
root scale `(1,1,1)`.

## Requirements

- Unity 6 LTS (developed with 6000.3.20f1).

## Open the project

1. Open Unity Hub → Add project from disk → `C:\dev\jajucha-sim`.
2. Open the `Simulation` scene at `Assets/JajuchaSim/Scenes/Simulation.unity`.
3. Press Play. The runtime HUD (Start/Pause/Resume/Step/Stop/Reset) and a
   status readout appear; the kernel is in `Ready` state.

## Run automated tests

```pwsh
$u = "C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe"
& $u -batchmode -nographics -projectPath C:\dev\jajucha-sim -runTests `
    -testPlatform editmode -testResults test-results-editmode.xml -logFile unity-editmode.log
& $u -batchmode -nographics -projectPath C:\dev\jajucha-sim -runTests `
    -testPlatform playmode -testResults test-results-playmode.xml -logFile unity-playmode.log
```

Expected (Step 8): EditMode 381/381, PlayMode 37/37, 0 project-code warnings.

## Documentation

- `docs/ARCHITECTURE.md` — subsystem map and authority order.
- `docs/DESIGN_DECISIONS.md` — ADR log (DD-001..009).
- `docs/MANUAL_COMPATIBILITY.md` — `jchm` feature tracking + APPROXIMATE values.
- `docs/CONFIGURATION.md` — kernel config fields.
- `docs/architecture/01-core-simulation.md` — Step 1 kernel contract.
- `docs/TESTING.md`, `docs/SCORING.md`, `docs/COURSE_FORMAT.md`,
  `docs/IMPLEMENTATION_STATUS.md`, `docs/CHANGELOG.md`.

## Code conventions

- Everything under namespace `JajuchaSim.*` respecting subsystem boundaries.
- No `GameObject.Find` / `FindObjectOfType` / static mutable globals; use
  explicit references or `SimulationContext`.
- Core never imports `UnityEditor`; runtime debug works in standalone builds.
- Manual unknowns are marked APPROXIMATE/UNKNOWN in code comments and
  `docs/MANUAL_COMPATIBILITY.md`, never hidden as silent literals.