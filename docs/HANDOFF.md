# Handoff Note

> Read this FIRST when you pick up this project as a coding agent.

## What this project is

JajuchaSim -- a Unity 6 simulator for the Jajucha ("jajucha") vehicle with a
Python `jchm` compatibility layer so student code that targets the real car
runs unchanged against the simulator.

The autonomous implementation runner is `run-implementation-plan.ps1`. It reads
task specs from `implementation_plan/NN.txt` and runs each through a fresh Pi
implement session followed by a fresh verify/fix loop. State lives in
`.pi-plan-state/` and logs in `.pi-plan-logs/`.

## Current progress (verified)

| Task file  | Meaning                          | Status                       |
|------------|----------------------------------|------------------------------|
| 00_MASTER_PLAN.txt | Master engineering rules | ALREADY COMPLETE (verified)  |
| 01.txt     | Step 1 spec (Core subsystem)     | ALREADY COMPLETE (verified)  |
| 02.txt     | Step 2 spec (Core impl)         | ALREADY COMPLETE (verified)  |
| 03.txt     | Step 3 spec (Vehicle / Step 2 impl) | ALREADY COMPLETE (verified) |
| **04.txt** | **Step 3 -- Python Bridge**     | **NOT STARTED -- BEGIN HERE** |
| 05-10.txt  | Steps 4-10                       | NOT STARTED                   |

Steps 1 and 2 (Core simulation kernel + Vehicle model with the zero-speed
invariant) are fully implemented, tested, and verified:
- EditMode: 31/31 (Core) + 62/62 (Vehicle) pass.
- PlayMode: 6/6 (Core) + 19/19 (Vehicle, incl. ZeroSpeedNeverPropelsTest) pass.
- 0 project-code compile warnings.

`Assets/JajuchaSim/Bridge/` exists but is EMPTY. There is no `python/` dir yet.

## NEXT TASK: implement 04.txt (Step 3 -- Python Bridge)

Run the runner normally; it will resume on the first NOT-complete task, which
is `04.txt`. The full spec is in `implementation_plan/04.txt` (70 numbered
sections + Definition of Done). Key points:
- Create `Assets/JajuchaSim/Bridge/` runtime + tests with `JajuchaSim.Bridge`
  asmdef depending on `JajuchaSim.Core` and `JajuchaSim.Vehicle`.
- TCP server on 127.0.0.1:8765 (configurable), persistent connection, newline-
  delimited JSON, protocol v1 handshake, request IDs, background network I/O
  with a thread-safe `ConcurrentQueue` consumed at the simulation tick boundary.
- Implement: `set_motor` (latest-wins, zero-speed invariant preserved from
  Step 2), watchdog (wall-time, stops propulsion on timeout / disconnect while
  keeping steering), single control client.
- Simulator-only ops under a separate `jchm_sim` package: connect/disconnect/
  ping/reset/start/pause/step/status. Keep `jchm.control` clean for real-car
  compatibility (`import jchm` must still work).
- Python package layout under `python/jchm/` and `python/jchm_sim/`.
- Tests: Unity EditMode (JSON parse, invalid JSON, unknown command, set_motor
  dispatch, zero-speed bridge, latest-wins, watchdog, disconnect, reconnect) +
  Python pytest (mocked backend, range, connection error) + a full Python->Unity
  integration test. Also `tools/bridge_smoke_test.py`.
- Docs: `docs/architecture/03-python-bridge.md` and `docs/protocol-v1.md`.
- DoD: see `04.txt` section 3.70 -- all boxes must be ticked, including 0
  compile errors and 0 project-code warnings.

## Verification record schema (IMPORTANT -- do not get confused)

The runner writes verification records. The full schema is defined at
`run-implementation-plan.ps1` around line 670 (search for
"Verification record JSON schema"). It is:

```json
{
  "status": "pass",
  "task": "<file name>",
  "attempt": 0,
  "summary": "Assessed already-complete during pre-flight.",
  "tests_run": [
    "exact command or check and result"
  ],
  "requirements_verified": [
    "requirement/result"
  ],
  "remaining_issues": []
}
```

Rules:
- The ONLY field the runner checks is `status == "pass"` (plus the `.done`
  marker existing). Extra fields are harmless but unnecessary -- keep records
  schema-clean.
- `tests_run` entries describe an exact command or check and its result
  (e.g. `"exact command: Unity batchmode editmode -> 31/31 passed"`).
- `requirements_verified` entries are short "requirement -- verified" strings.
- **Keep verification JSON files ASCII-only.** The runner's `Read-Verification`
  now reads with `-Encoding UTF8`, but ASCII-only is the safest choice and
  avoids any locale-dependent decode bug. Do NOT paste em-dashes, units like
  `cm/s^2` are fine; use `--` instead of em-dash.

## Do NOT

- Do NOT re-edit the `00-03` verification records; they are correct and
  schema-compliant. Re-editing them is what derailed the previous session.
- Do NOT treat single-word user messages as a schema to reconstruct; the full
  schema is in the `.ps1` file. If something seems fragmentary, READ the runner
  script and the master plan instead of guessing.
- Do NOT touch Steps 1-2 source/tests; they pass and are verified.
- Do NOT mix transport, JSON parsing, and car commanding into one big class
  (the Step 3 spec explicitly forbids this).

## Quick directories

- Specs: `implementation_plan/`
- Docs: `docs/`
- State: `.pi-plan-state/`  (`*.done`, `*.verification.json`)
- Logs: `.pi-plan-logs/`
- Code: `Assets/JajuchaSim/`