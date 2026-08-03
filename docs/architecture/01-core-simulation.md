# 01 — Core Simulation Kernel

> Step 1 of the Jajucha Simulator v2 implementation plan. This document is the
> architectural contract that later subsystems (vehicle, sensors, bridge,
> course, scoring, testing) must satisfy.

## Purpose

A single deterministic owner of simulation time and lifecycle, with **no
knowledge of "cars", cameras, Python, courses, or scoring**. Step 1 must know
nothing about the vehicle — that is the sign the separation is correct.

## Subsystem layout

```
Assets/JajuchaSim/
  Core/
    Runtime/   JajuchaSim.Core.asmdef        (JajuchaSim.Core)
    Tests/
      Shared/   JajuchaSim.Core.TestSupport.asmdef
      EditMode/ JajuchaSim.Core.EditModeTests.asmdef  (Editor + TestAssemblies)
      PlayMode/ JajuchaSim.Core.PlayModeTests.asmdef  (all platforms + TestAssemblies)
  Vehicle/  Sensors/  Bridge/  Course/  MapEditor/  Structures/
  Scenario/ Scoring/  Testing/  Debug/  UI/          (empty reserved folders)
```

## Lifecycle

`SimulationState`: `Uninitialized → Ready → Running ⇄ Paused → Stopped → (Reset → Ready)`.

`SimulationManager` is the **only** object that owns these transitions:

| Method | Transition | Notes |
|---|---|---|
| `Initialize()` | Uninitialized → Ready | Validates config; creates Clock/Events/Random/Context; registers+initializes inspector systems. Called automatically from `Awake()` if a `SimulationConfig` is assigned, so the scene "just starts" in Ready. |
| `StartSimulation()` | Ready/Paused → Running | Resets the scheduler accumulator implicitly via clock state. |
| `Pause()` | Running → Paused | Scheduler stops ticking. |
| `Resume()` | Paused → Running | |
| `Step()` | (any active) | Advances **exactly one** tick (`FixedDeltaTime`). Intended for single-stepping while paused. |
| `Advance(int n)` | (active) | Runs `n` ticks synchronously, `FixedDeltaTime` each, independent of real time. For tests/headless/command replay. |
| `Stop()` | Running/Paused → Stopped | Shuts down all systems; preserves final state for inspection. |
| `ResetSimulation()` | any → Ready | Resets Clock/Random/Events/accumulator and every system. Re-running with the same seed reproduces the same result. |
| `RegisterSystem(ISimulationSystem)` | (Ready+) | Plugs an arbitrary system into the tick loop and initializes it with the current context. For headless/test usage without inspector refs. |

## Clock

`SimulationClock` is the only authority on **simulation time**.

- `Tick` (`long`), `Time` (`double`), `FixedDeltaTime` (`float`, default `0.01` → 100 Hz).
- `TimeScale` multiplies wall-clock delta in the scheduler; `Step`/`Advance` always use real `FixedDeltaTime`.
- Rendering/Unity time is never used as simulation time. The scheduler feeds `Time.unscaledDeltaTime` into a fixed-step accumulator and ticks at `FixedDeltaTime` resolution.

`Time` is a `double` accumulation of the **float** `FixedDeltaTime`. Over long
runs this is not bit-exact (e.g. 100 × `0.01f` ≈ 0.99999997). Tests use a
tolerance of `1e-3` on time; `Tick` is exact.

## Scheduler (spiral-of-death guard)

`Update()` accumulates `Time.unscaledDeltaTime * TimeScale` and ticks while
`accumulator >= FixedDeltaTime`, capped by `SimulationConfig.maxTicksPerFrame`
(default 100). Residual accumulator debt beyond the cap is discarded to avoid
unbounded catch-up after a render stall. The scheduler only ticks when
`State == Running`, so paused/step semantics are exact.

## ISimulationSystem

```
void Initialize(SimulationContext);
void SimulationTick(float deltaTime);
void ResetSimulation();
void Shutdown();
```

Systems are ticked in registration order (inspector order first, then
runtime-registered). Implementations must be **deterministic** given the same
context and inputs. Subsystems receive the `SimulationContext` (Clock, Events,
Random) up front — they must **not** discover dependencies via
`GameObject.Find`, `FindObjectOfType`, `Camera.main`, or globals.

## Event bus

`SimulationEventBus`: tiny typed pub/sub. `Subscribe<T>/Unsubscribe<T>/Publish<T>/Clear`.
Publish snapshots subscribers so handlers may unsubscribe during a publish
without invalidating iteration. It is a **state/lifecycle channel**, not a
high-frequency sensor channel.

Types: `SimulationStartedEvent`, `SimulationPausedEvent`,
`SimulationResumedEvent`, `SimulationStoppedEvent`, `SimulationResetEvent`.

## Determinism

- `SimulationRandom` (SplitMix64) seeded from `SimulationConfig.randomSeed`.
  Core simulation code must draw randomness only from
  `SimulationContext.Random`. `UnityEngine.Random` is forbidden in core logic.
- Same course + seed + initial state + command sequence ⇒ equivalent outcomes
  within float physics tolerances.
- Reset fully clears clock, random, events, accumulator, and every system.

## Configuration

`SimulationConfig` (ScriptableObject) at `Assets/JajuchaSim/Settings/`. Default
instance: `DefaultSimulationConfig.asset`.

| Field | Default | Notes |
|---|---|---|
| `fixedDeltaTime` | 0.01 | 100 Hz. |
| `defaultTimeScale` | 1.0 | |
| `randomSeed` | 12345 | Stored as `long` because Unity's serializer does not serialize `ulong`; cast to `ulong` internally. |
| `maxTicksPerFrame` | 100 | |
| `autoStart` | false | |

## Scene

`Assets/JajuchaSim/Scenes/Simulation.unity` contains a `SimulationRoot`
GameObject with `SimulationManager` (referencing `DefaultSimulationConfig`) and
`SimulationDebugHud`. The HUD is the temporary Step 1 proof-of-life UI and will
be replaced by the full debug sidebar/panel system in later steps while the main
driving view remains permanent.

## Runtime HUD

`SimulationDebugHud` builds a minimal Canvas (status text + Start/Pause/Resume/
Step/Stop/Reset buttons) programmatically, so the scene needs no authored UI.
Works in standalone builds and Play Mode (no Editor-only APIs).

## Rules for new systems (architectural contract)

1. One class, one responsibility. No class should casually exceed ~300 lines.
2. No static mutable global state. No `GameObject.Find` / `FindObjectOfType`.
3. Dependencies via constructor/context/serialized reference only.
4. Physics is stepped in **one** authoritative location: `SimulationManager.RunOneTick()`
   calls `Physics.Simulate(Clock.FixedDeltaTime)` after ticking all systems.
   `Physics.simulationMode` is set to `SimulationMode.Script` on initialization
   so Unity's automatic FixedUpdate-physics does not interfere.
5. New subsystems implement `ISimulationSystem` (or `SimulationSystemBehaviour`)
   and are registered with the kernel.
6. Never weaken, delete, or bypass tests to obtain a green result.
7. The kernel never imports `UnityEditor`; runtime debug must work in standalone builds.