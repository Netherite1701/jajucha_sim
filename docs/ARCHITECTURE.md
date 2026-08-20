# Jajucha Simulator v2 — Architecture

A clean, deterministic Unity simulator for the Jajucha autonomous-driving
vehicle, faithful to the official Jajucha manual (`docs/자주차 매뉴얼.pdf`) above
all else.

## World scale

**1 Unity unit = 1 centimeter.**
1 m = 100 Unity units. Default gravity = `-981 cm/s²`
(`Physics.gravity = new Vector3(0f, -981f, 0f)` — applied in Step 2 when physics
arrives). Vehicle prefab root scale is `(1,1,1)`; never compensate geometry with
arbitrary Transform scaling.

## Subsystem map

```
Assets/JajuchaSim/
  Core/        Deterministic simulation kernel (clock, lifecycle, events, random)
  Vehicle/     Jajucha vehicle model (Step 2+)
  Sensors/     left/center/right cameras + depth (later)
  Bridge/      Versioned localhost TCP bridge to Python `jchm` (later)
  Course/      Shared tile grid + structures/objects/triggers (Steps 6–7)
  MapEditor/   Runtime map editor HUD (Step 7 — implemented)
  Structures/  (geometry lives under Course; folder reserved)
  Scenario/    Objectives + scenario (Steps 8–10)
  Scoring/     Penalty records + ScoreManager (Steps 8–10)
  Testing/     Controller-agnostic test/batch runner (Steps 10)
  App/         Authoritative scene bootstrap, modes, data paths, logs (Step 11)
  Debug/       Runtime debug panels
  UI/          Single legacy-style SimulatorDashboardUI canvas
```

Each subsystem owns its code and exposes explicit interfaces. No giant manager
classes, no `GameObject.Find`, no `FindObjectOfType`, no hidden globals, no
uncontrolled singletons. (The single exception: `ApplicationBootstrap` and a
few App-level components use `FindFirstObjectByType` to *resolve optional
scene wiring*, never to drive simulation behavior; the authoritative scene
serializes the references so resolution is deterministic.)

## Authoritative scene and startup

`Assets/JajuchaSim/Scenes/JajuchaSimulator.unity` is the **one** runtime scene
(Step 11). It contains configuration (roots, managers, vehicle behaviour,
observer camera, runtime UI, services) — the course itself is loaded from
`Courses/2026_preliminary.json` or `Courses/2026_final.json` and generated at runtime.

Fixed hierarchy (stable, validated by `SceneHierarchyValidationTests`):

```text
JajuchaSimulator
├─ _Core      SimulationManager, SimulationClock, SimulationRunner,
│             SimulationEventBus, ApplicationBootstrap
├─ _Course    CourseManager, CourseRuntimeRoot, RoadLayerRoot,
│             StructureLayerRoot, ObjectLayerRoot, TriggerLayerRoot,
│             RuntimeOverlayRoot
├─ _Vehicle   JajuchaVehicle (VehicleSystemBehaviour)
├─ _Sensors   SensorRuntimeRoot (CameraSensorSystemBehaviour)
├─ _Bridge    JajuchaBridgeServer
├─ _Scenario  ScenarioManager, ScoreManager, TestRunner
├─ _Observer  ObserverCamera, ObserverCameraController
├─ _RuntimeUI MainViewport and controller-only legacy panels
└─ _Services  SaveLoadService, RuntimeFileDialogService, ScreenshotService,
              ApplicationShutdownService
```

`ApplicationBootstrap` (in `JajuchaSim.App`) runs the ordered startup:
config → kernel → data paths → course → runtime course → vehicle → sensors →
bridge → scenario → UI → READY. Every step returns an explicit
`BootstrapResult` (Success / FailedSystem / ErrorCode / Message); failures are
shown on screen, never left as a NullReferenceException (Step 11.4/11.5).
Random `Awake`/`Start` ordering never defines system initialization.

`ApplicationBootstrap.StepInitUi` creates one `SimulatorDashboardCanvas` and
binds `Assets/JajuchaSim/App/Runtime/SimulatorDashboardUI.cs`. Its tabs are
`주행`, `코스 편집`, `채점`, `센서`, and `디버그`; ScenarioPanel, ResultsPanel,
ScoringPanel, SimulationDebugHud, and RuntimeStatusBar remain data/control
facades with standalone rendering disabled in the authoritative scene. The
official 2026 JSON is loaded as `OfficialReadOnly`; editing starts only after
`연습용 복사본 만들기`, and practice saves are written under the user's
`Courses/Practice` data directory.

## Dependency direction (one way, upward)

```
Core  ←  Vehicle  ←  Sensors  ←  Bridge
       (Course, Scoring, Testing depend on Core and each other via interfaces)
```

Assembly definitions enforce boundaries. `JajuchaSim.Core` references nothing of
ours; later subsystems reference downward only.

## Implementation state

See `docs/IMPLEMENTATION_STATUS.md` for the current step-by-step status and
`docs/architecture/01-core-simulation.md` for the Step 1 kernel contract.

## Authority order for decisions

1. Official Jajucha manual.
2. Explicit requirements in this project specification.
3. Measured/calibrated real hardware behavior.
4. Existing documented design decisions.
5. Reasonable engineering assumptions (marked APPROXIMATE / UNKNOWN).

When the manual is silent on a value, it is exposed as **configuration** and
marked `APPROXIMATE` / `UNKNOWN` in `docs/MANUAL_COMPATIBILITY.md`.
