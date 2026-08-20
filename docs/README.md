# Documentation

Documentation entry point for the Jajucha Simulator (Step 11.45).

## Quick start

```powershell
# 1. Set up the Python environment once
.\scripts\setup_python.ps1

# 2. Launch the simulator (standalone build or Unity Play Mode)
.\scripts\run_simulator.ps1

# 3. In another terminal, run the user program
.\.venv\Scripts\python.exe .\python\user\main.py
```

See [USER_WORKFLOW.md](USER_WORKFLOW.md) for the full normal workflow.

## Guides

| Document | Purpose |
|---|---|
| [USER_WORKFLOW.md](USER_WORKFLOW.md) | The normal user workflow: setup, drive, map, test, batch. |
| [VSCODE_WORKFLOW.md](VSCODE_WORKFLOW.md) | VS Code tasks and Python debugging workflow. |
| [CALIBRATION.md](CALIBRATION.md) | Confirmed vehicle dimensions and provisional camera-array values. |
| [COMPETITION_2026.md](COMPETITION_2026.md) | 2026 preliminary/final course, mission, signal, and practice defaults. |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Subsystem map, authority order, scene hierarchy. |
| [COURSE_FORMAT.md](COURSE_FORMAT.md) | The 5 cm-mask course JSON format. |
| [CONFIGURATION.md](CONFIGURATION.md) | Every configurable field and the configuration hierarchy. |
| [SCORING.md](SCORING.md) | Competition scoring and how it is shared by manual + automated runs. |
| [TESTING.md](TESTING.md) | Running the automated test suites. |
| [MANUAL_COMPATIBILITY.md](MANUAL_COMPATIBILITY.md) | `jchm`/`jchm_sim` API compatibility against the Jajucha manual. |
| [DESIGN_DECISIONS.md](DESIGN_DECISIONS.md) | ADR log of accepted design choices. |
| [TROUBLESHOOTING.md](TROUBLESHOOTING.md) | Diagnostic steps for common problems. |
| [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md) | Development log of all steps. |
| [CHANGELOG.md](CHANGELOG.md) | Change history. |
| [protocol-v1.md](protocol-v1.md) | The Python↔Unity bridge protocol. |
| [architecture/01-core-simulation.md](architecture/01-core-simulation.md) | Step 1 kernel contract. |
| [architecture/03-python-bridge.md](architecture/03-python-bridge.md) | Step 3 Python bridge design. |

## Python workspace

- `python/user/README.md` — user script rules (the `python/user/` folder belongs to the user).
- `python/examples/01_motor_test.py` … `06_test_run.py` — runnable usage examples.
- `python/jchm/` — real-compatible vehicle API.
- `python/jchm_sim/` — simulator-only lifecycle/testing tools.

## Manual reference

> When the simulator behavior and the Jajucha manual disagree, inspect the
> manual first.

The four supplied 2026 track/sign PDFs are the primary source for competition
course behavior. Values absent from them are labelled `비공식 연습값`.
