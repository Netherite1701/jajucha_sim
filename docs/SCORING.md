# Scoring

Final Score = Base Score − Penalties. Point values are **not** official until
verified against real competition rules; all values stay configurable through
`ScenarioDefinition.scoring` (`ScoringConfig`).

## Scoring flow

```
Scenario events
      ↓
ScoreManager
      ↓
Rules (LineContact / CourseDeparture / Collision / FalseStart / SlowZone /
       SpeedPair objective / Completion)
      ↓
PenaltyRecords
      ↓
FinalScore = BaseScore − Σ Penalties
```

No component ever mutates a global score integer directly; every deduction
creates a structured `PenaltyRecord` (RuleId, EventType, Points,
SimulationTime, TargetId, Description).

## Configurable scoring rules (Step 10.18)

```json
{
  "scoring": {
    "baseScore": 100,
    "lineContactPenalty": 5,
    "courseDeparturePenalty": 5,
    "collisionPenalty": 5,
    "falseStartPenalty": 10,
    "objectiveFailurePenalty": 10,
    "timeoutPenalty": 10,
    "finalizeUnfinishedObjectives": true
  }
}
```

Per-objective overrides (Step 10.19):

```json
{
  "objectives": [
    { "id": "tunnel_01", "type": "pass_structure", "targetId": "tunnel_01", "failurePenalty": 15 },
    { "id": "slow_zone_01", "type": "slow_zone", "targetId": "slow_zone_01", "failurePenalty": 5 }
  ]
}
```

## Penalty categories (Step 10.36)

| Rule | Event | Penalty source |
|------|-------|----------------|
| Line violation | vehicle footprint touches forbidden line | `scoring.lineContactPenalty` |
| Collision | vehicle hits obstacle/structure | `collisions.penalty` (per-rule config) |
| Course departure | vehicle leaves valid road (majority of footprint) | `scoring.courseDeparturePenalty` |
| False start | moves/crosses before allowed | `falseStart.penalty` (per-rule config) |
| Objective failure | fails required feature | objective `failurePenalty` or `scoring.objectiveFailurePenalty` |
| Speed violation | two-terminal measurement exceeds target | objective `failurePenalty` (SpeedPair objective) |
| Timeout | course not finished in time | `scoring.timeoutPenalty` |

## Line contact (Step 10.2/10.3)

The road tile grid distinguishes **road surface** and **boundary line**
(`CourseGrid.SetLine/HasLine`, serialized as `lines`). Line tiles are still
road (drivable); scoring decides what contact costs. Detection samples the
whole vehicle footprint (centre + four corners), never just the centre.

Violations are debounced into episodes:

```
not touching → touching  = one LineViolation
remain touching          = same violation
leave line               = episode ends
later touch again        = new violation
```

The same debouncing principle applies to course departures and collisions.

## Objectives (Step 10.4–10.7, 10.37)

Each important course feature can define an objective. The scorer only cares
about simulator-observable outcomes — it never inspects the user's controller
internals.

States: `Pending → Active → Passed | Failed | Skipped`.

Types:
- `trigger` — pass when the target trigger is entered (start/event).
- `finish` — pass when the finish is reached.
- `pass_structure` — enter and correctly exit a structure (e.g. tunnel);
  a collision with it fails the objective.
- `avoid_object` — navigate an object's region without colliding.
- `slow_zone` — pass/fail from the slow-zone measurement.
- `speed_pair` — two-terminal official speed within `maxSpeedCmS`.

A missing terminal measurement (A crossed, B never) fails the objective at
finish/timeout instead of silently disappearing (Step 10.13). Objective
successes are recorded too, so batch statistics can show e.g. "tunnel success
98%" without inspecting controller internals.

## Two-terminal speed (Step 8 — implemented)

Official measured speed `v = d / (t2 - t1)` using `SimulationClock` timestamps
between Terminal A and Terminal B (`SpeedTerminalPairRule` →
`SpeedMeasuredEvent`). Distance `d` is derived from terminal world positions.
Rigidbody / internal vehicle velocity is **not** the official result. Scoring
subscribes to `SpeedMeasuredEvent` (or reads the pair results), never samples
Rigidbody speed for pass/fail.

## Finish / timeout (Step 10.14/10.15)

On finish: the official timer stops, unfinished required objectives are
finalized (failed), penalties are finalized, and the score is calculated into
a `RunResult`. On timeout the same happens plus `scoring.timeoutPenalty`.

## RunResult

Raw measurements + score: status, elapsed time, base/final score, penalties,
line violations, course departures, collisions, objective results, two-terminal
speed results with pass/fail, timeout/disconnect reason. Manual runs and
automated tests (TestRunner/BatchRunner) use exactly the same
Scenario → ScoreManager → RunResult path.

## Runtime UI

- `ScoringPanel` — live scoring HUD (current score, penalties, objective
  states, "-5 LINE CONTACT" toast). Observer/debug only; never sensor cameras.
- `ResultsPanel` — final results (base score, penalty breakdown, final score,
  speed terminal, objectives) with Run Again / Details / Export.
- Map editor SCORING section — runtime-editable base score and penalty values.
