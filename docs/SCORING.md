# Scoring

> Not yet implemented (Steps 8–9). Design contract captured so later steps are
> not improvised.

Final Score = Base Score − Penalties. Point values are **not** official until
verified against real competition rules; all values stay configurable.

## Penalties (planned, debounced)

- Line contact — evaluate a vehicle footprint, not only the center. Edge
  transition (not→touching) = one violation; stay touching = same violation;
  leave = episode ends. Do not deduct every tick.
- Course departure, obstacle/structure collision, false start, objective
  failure, speed violation, timeout.

## Two-terminal speed (Step 7)

Official measured speed `v = d / (t2 − t1)` using simulation timestamps
between Terminal A and Terminal B. Rigidbody velocity is **not** the official
result. A terminal pair resets between runs; reverse order is ignored unless
configured.

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