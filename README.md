# Jajucha Simulator v2

A clean, deterministic Unity simulator for the **Jajucha** autonomous-driving
vehicle. The official Jajucha manual (`docs/자주차 매뉴얼.pdf`) is the primary
source of truth for all real-vehicle behavior and APIs.

## Status

Steps **1–11** are implemented: core kernel, vehicle, Python bridge, sensors,
shared 5 cm competition mask, structures/objects/triggers, runtime map editor,
scenario/timing/scoring, competition scoring + automated testing, and the
2026 preliminary/final workflow. See
`docs/IMPLEMENTATION_STATUS.md` for details.

## Normal workflow

```powershell
# 1. Set up the Python environment once (project-local .venv)
.\scripts\setup_python.ps1

# 2. Launch the simulator (standalone build or Unity Play Mode)
.\scripts\run_simulator.ps1

# 3. In another terminal, run the user program
.\.venv\Scripts\python.exe .\python\user\main.py
```

The authoritative scene is `Assets/JajuchaSim/Scenes/JajuchaSimulator.unity`;
it loads `Courses/2026_preliminary.json` on first startup, remembers the last
preliminary/final selection, and enters Drive mode
automatically. See `docs/USER_WORKFLOW.md` for the full workflow (drive, map
edit, test, batch).

## VS Code workflow

Open the project root (`C:\dev\jajucha-sim`) in VS Code. The included
`.vscode` workspace configuration provides tasks for Python setup, opening the
Unity simulator, checking the bridge, running `python/user/main.py`, and
running Python tests. It also provides Python debug configurations for the
user controller and pytest. See `docs/VSCODE_WORKFLOW.md`.

## World scale

`1 Unity unit = 1 centimeter`. Default gravity `-981 cm/s²`. Vehicle prefab
root scale `(1,1,1)`.

## Requirements

- Unity 6 LTS (developed with 6000.3.20f1).
- Python 3.9+ for the user workspace (installed into `.venv` by the setup
  script; no global install required).

## Open the project (development only)

1. Open Unity Hub → Add project from disk → this folder.
2. Open `Assets/JajuchaSim/Scenes/JajuchaSimulator.unity`.
3. Press Play. The bootstrap loads the selected 2026 course and enters Drive mode.

## Python workspace

```
python/
├─ jchm/          real-compatible vehicle API (also runs on the real car)
├─ jchm_sim/      simulator-only lifecycle/testing tools
├─ examples/      01_motor_test … 06_test_run
├─ user/          YOUR code (main.py + rules in README.md)
├─ tests/         pytest suite
└─ requirements.txt
```

## Scripts

| Script | Purpose |
|---|---|
| `scripts/setup_python.ps1` / `.sh` | Create `.venv`, install requirements, verify imports |
| `scripts/activate_python.ps1` | Activate `.venv` |
| `scripts/run_simulator.ps1` / `.bat` | Launch the standalone build |
| `scripts/run_development.ps1` | Simulator + bridge wait + user program |
| `scripts/check_bridge.py` | Bridge readiness check (exit code) |
| `scripts/validate_project.ps1` | Full project validation |
| `scripts/build_windows.ps1` | Build the Windows standalone distribution |

## Run automated tests

```pwsh
$u = "C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe"
& $u -batchmode -nographics -projectPath <this-folder> -runTests `
    -testPlatform editmode -testResults test-results-editmode.xml -logFile unity-editmode.log
& $u -batchmode -nographics -projectPath <this-folder> -runTests `
    -testPlatform playmode -testResults test-results-playmode.xml -logFile unity-playmode.log
```

Python:

```powershell
.\.venv\Scripts\python.exe -m pytest python\tests\ -q
```

Expected (Step 11): EditMode 473/473, PlayMode 48/48, Python 29/29,
0 project-code warnings.

## Documentation

- `docs/README.md` — documentation entry point.
- `docs/USER_WORKFLOW.md` — the normal user workflow.
- `docs/ARCHITECTURE.md` — subsystem map and authority order.
- `docs/DESIGN_DECISIONS.md` — ADR log (DD-001..025).
- `docs/MANUAL_COMPATIBILITY.md` — `jchm` feature tracking + APPROXIMATE values.
- `docs/CONFIGURATION.md` — kernel + application config fields.
- `docs/COMPETITION_2026.md` — 2026 course, mission, signal, and practice defaults.
- `docs/COURSE_FORMAT.md` — 2026 course JSON format.
- `docs/TESTING.md`, `docs/SCORING.md`, `docs/TROUBLESHOOTING.md`,
  `docs/IMPLEMENTATION_STATUS.md`, `docs/CHANGELOG.md`.

## Code conventions

- Everything under namespace `JajuchaSim.*` respecting subsystem boundaries.
- No `GameObject.Find` / `FindObjectOfType` / static mutable globals in
  simulation logic; the App-level bootstrap may resolve optional scene wiring
  with `FindFirstObjectByType` (the authoritative scene serializes references).
- Core never imports `UnityEditor`; runtime debug works in standalone builds.
- Manual unknowns are marked APPROXIMATE/UNKNOWN in code comments and
  `docs/MANUAL_COMPATIBILITY.md`, never hidden as silent literals.
