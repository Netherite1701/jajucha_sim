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
  Scenario/    Objectives + scenario (later)
  Scoring/     Penalty records + ScoreManager (later)
  Testing/     Controller-agnostic test/batch runner (later)
  Debug/       Runtime debug panels (later)
  UI/          Driving view + HUD (later)
```

Each subsystem owns its code and exposes explicit interfaces. No giant manager
classes, no `GameObject.Find`, no `FindObjectOfType`, no hidden globals, no
uncontrolled singletons.

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