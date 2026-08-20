# Configuration

The simulator is configuration-driven. No hard-coded constants in core logic.

## Simulation kernel

`Assets/JajuchaSim/Settings/DefaultSimulationConfig.asset`
(`SimulationConfig` ScriptableObject):

| Field | Type | Default | Meaning |
|---|---|---|---|
| `fixedDeltaTime` | float | 0.01 | Fixed simulation timestep (100 Hz). |
| `defaultTimeScale` | float | 1.0 | Multiplier applied to wall-clock time by the scheduler. |
| `randomSeed` | long | 12345 | Seed for `SimulationRandom` (cast to `ulong`). |
| `maxTicksPerFrame` | int | 100 | Cap on ticks per frame to prevent spiral-of-death. |
| `autoStart` | bool | false | If true, start running immediately after initialize. |

Create new configs via `Create > JajuchaSim > Simulation Config` (Editor) or by
duplicating the default asset. The active config is referenced by the
`SimulationManager` in `Simulation.unity`.

## World scale (project convention, not an asset)

`1 Unity unit = 1 centimeter`. Default gravity `-981 cm/s²`
(applied when physics arrives in Step 2). Vehicle prefab root scale `(1,1,1)`.

## Configuration assets

### CourseConfig

`Assets/JajuchaSim/Settings/DefaultCourseConfig.asset` *(to be created when editor UI lands)*:

| Field | Type | Default | Meaning |
|---|---|---|---|
| `tileSizeCm` | float | 20 | Tile size in centimetres. 1 Unity unit = 1 cm. |

## Configuration surfaces by subsystem

| Area | Config surface | Step |
|---|---|---|
| Simulation kernel | `SimulationConfig` | 1 |
| Vehicle geometry / wheelbase / tire radius | `VehicleConfig` | 2 |
| Steering (°/unit) / speed (cm/s per unit) | `VehicleConfig` | 2 |
| Cameras (FOV / resolution / FPS / mount) | `CameraConfig` | 4 |
| Depth mapping (near/far cm → gray8) | `CameraConfig` | 4 |
| Course tile size (cm) | `CourseConfig` | 6 |
| Bridge port / watchdog / protocol version | `BridgeConfig` | 3 |
| Scoring penalties / base score | `ScoringConfig` | 8 |
| Application (course, mode, speed, paths) | `ApplicationConfig` | 11 |

All unspecified-by-manual values are marked APPROXIMATE/UNKNOWN in
`docs/MANUAL_COMPATIBILITY.md`.

## Application configuration (Step 11.41/11.42)

`Config/default_simulator.json` (copied next to standalone builds) drives the
application-level workflow:

```json
{
  "defaultCourse": "2026_preliminary",
  "bridgePort": 8765,
  "debugUiEnabled": true,
  "observerMode": "chase",
  "simulationSpeed": 1.0,
  "mode": "drive",
  "coursesDirectory": "Courses",
  "runsDirectory": "Runs",
  "screenshotsDirectory": "Screenshots",
  "logsDirectory": "Logs",
  "userConfigDirectory": "UserConfig",
  "batchConfig": ""
}
```

Configuration hierarchy (highest wins):

```text
built-in defaults
    ↓
project/default config (Config/default_simulator.json)
    ↓
user config (writable UserConfig/default_simulator.json)
    ↓
command-line overrides (--course, --mode, --simulation-speed,
                         --no-debug-ui, --batch-config)
```

### Fields

| Field | Type | Default | Meaning |
|---|---|---|---|
| `defaultCourse` | string | `2026_preliminary` | 2026 course loaded at first startup; the saved stage wins thereafter. |
| `bridgePort` | int | 8765 | Python bridge TCP port. |
| `debugUiEnabled` | bool | true | Whether the full debug UI is shown. |
| `observerMode` | string | `chase` | Observer camera mode (chase/top/free). |
| `simulationSpeed` | float | 1.0 | Simulation speed multiplier. |
| `mode` | string | `drive` | Initial application mode (drive/edit/test/batch). |
| `coursesDirectory` | string | `Courses` | Writable user-course folder name. |
| `runsDirectory` | string | `Runs` | Run results / diagnostics folder name. |
| `screenshotsDirectory` | string | `Screenshots` | Screenshot folder name. |
| `logsDirectory` | string | `Logs` | Log folder name. |
| `userConfigDirectory` | string | `UserConfig` | User-editable config folder name. |
| `batchConfig` | string | `` | Optional batch configuration file for BatchTest. |

Hardware-related approximations (camera FOV/resolution, speed mapping, steering
mapping) are configuration-backed and documented in
`docs/MANUAL_COMPATIBILITY.md`; they are NOT hidden Inspector-only settings.
