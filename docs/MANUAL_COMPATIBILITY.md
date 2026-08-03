# Manual Compatibility

Tracks every implemented `jchm`/`jchm_sim` feature and hardware value against the
official manual (`docs/자주차 매뉴얼.pdf`).

Classification:
- **CONFIRMED_MANUAL** — manual explicitly specifies this.
- **MEASURED** — calibrated/verified against real hardware.
- **APPROXIMATE** — reasonable default; manual silent. Keep configurable.
- **SIMULATOR_ONLY** — not a real-vehicle feature (sim convenience).
- **UNKNOWN** — undecided; needs measurement/input.

## Implemented features

| Feature | Manual behavior | Simulator | Status |
|---|---|---|---|
| Simulation kernel lifecycle | n/a (sim-only) | `SimulationManager` states Uninitialized/Ready/Running/Paused/Stopped; pause/single-step/reset/speed | SIMULATOR_ONLY |
| Fixed timestep | n/a | 100 Hz (Δt 0.01 s) | SIMULATOR_ONLY |
| Deterministic seed | n/a | `SimulationConfig.randomSeed` (12345) | SIMULATOR_ONLY |
| `jchm.control.set_motor(left, right, speed)` | left/right ∈ [-10,10] (~2°/unit, −=CCW left), speed ∈ [-30,30] (~5.13 cm/s/unit, +=forward) | `MotorCommand` clamps to valid ranges; `SteeringModel` computes front wheel angles; `RearDriveModel` maps speed to drive force | IMPLEMENTED (Step 2) |
| Zero-speed invariant | `speed==0` ⇒ no propulsion force, steering never propels | `RearDriveModel.Evaluate(0)` ⇒ force=0; `VehicleSystem` sets motorTorque=0 + brakeTorque when speed=0; `ZeroSpeedNeverPropelsTest` validates 5 combinations over 10s each | IMPLEMENTED (Step 2, hard test) |

## Planned features (not yet implemented — Steps 3–10)

| Feature | Manual behavior (target) | Status |
|---|---|---|
| `jchm.control.stop_motor()` | stop + center steering | PLANNED (Step 2) |
| `jchm.camera.get_image("left"\|"center"\|"right")` | BGR uint8 (h,w,3), JPEG | PLANNED (Step 4) |
| `jchm.camera.get_depth()` | grayscale uint8 (h,w), near=bright(255)/far=dark(0), gray8 | PLANNED (Step 4) |
| `jchm.camera.show_image(...)` | local display; not a vehicle API | PLANNED (client-side, Python) |
| Bridge protocol | 4-byte big-endian length + UTF-8 JSON, localhost (default 127.0.0.1:8765) | PLANNED (Step 3) |
| `jchm.camera.get_image("left"\|"center"\|"right")` | BGR uint8 (h,w,3), JPEG | PLANNED (Step 4) |
| `jchm.camera.get_depth()` | grayscale uint8 (h,w), near=bright(255)/far=dark(0), gray8 | PLANNED (Step 4) |
| `jchm.camera.show_image(...)` | local display; not a vehicle API | PLANNED (client-side, Python) |
| Bridge protocol | 4-byte big-endian length + UTF-8 JSON, localhost (default 127.0.0.1:8765) | PLANNED (Step 3) |

## Hardware values awaiting measurement

| Value | Default | Status | Reason / Action |
|---|---|---|---|
| Center camera FOV | 60° | APPROXIMATE | Manual does not specify FOV. Measure real Jajucha camera. |
| Camera resolution | 640×480 | APPROXIMATE | Manual does not specify. Confirm. |
| Camera FPS | 30 | APPROXIMATE | Manual does not specify. Confirm. |
| Camera mount position/angle | TBD | UNKNOWN | Manual does not specify. Measure. |
| Vehicle dimensions / wheelbase | TBD | UNKNOWN | Manual does not specify. Measure. |
| Tire radius | TBD | UNKNOWN | Manual does not specify. Measure. |
| Steering response speed | instant (configurable) | APPROXIMATE | Manual does not specify servo dynamics. |
| speed→cm/s mapping | 1 unit ≈ 5.13 cm/s | MEASURED (from old `jchm` client docstring) | Confirm against hardware. |
| left/right→° mapping | 1 unit ≈ 2° | MEASURED (from old `jchm` client docstring) | Confirm against hardware. |
| Exact depth curve | linear configurable | APPROXIMATE | Manual only specifies near=bright/far=dark, not the curve. |

> Rule: temporary values are clearly marked. Code never hides an unknown as a
> silent literal — config values are used and documented here.