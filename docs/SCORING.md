# Scoring

> Scoring manager not yet implemented (Step 9). Two-terminal speed measurement
> (the official speed input) is implemented (Step 8). Design contract captured
> so later steps are not improvised.

Final Score = Base Score − Penalties. Point values are **not** official until
verified against real competition rules; all values stay configurable.

## Penalties (planned, debounced)

- Line contact — evaluate a vehicle footprint, not only the center. Edge
  transition (not→touching) = one violation; stay touching = same violation;
  leave = episode ends. Do not deduct every tick.
- Course departure, obstacle/structure collision, false start, objective
  failure, speed violation, timeout.

## Two-terminal speed (Step 8 — implemented)

Official measured speed `v = d / (t2 - t1)` using `SimulationClock` timestamps
between Terminal A and Terminal B (`SpeedTerminalPairRule` →
`SpeedMeasuredEvent`). Distance `d` is derived from terminal world positions.
Rigidbody / internal vehicle velocity is **not** the official result. A terminal
pair resets between runs; reverse order (B then A) is ignored unless configured.

Scoring must subscribe to `SpeedMeasuredEvent` (or read
`SpeedTerminalPairRule.LatestResult` / `Results`), never sample Rigidbody speed
for pass/fail.

## Structured records

Penalty flow: course event → scoring rule → `PenaltyRecord` {rule, event,
points, simulation time, target, description} → `ScoreManager`. Never scatter
`score -= 5` across components.

## Objectives

States: Pending, Active, Passed, Failed, Skipped. Failure may yield
configurable negative points. Scoring evaluates only simulator-observable
outcomes — it never depends on the user's internal controller (ANN/FSM/PID/…).

## RunResult

Raw measurements + score: status, elapsed time, base/final score, penalties,
line violations, collisions, objective results, two-terminal speed results,
timeout/disconnect reason. Not a single number.