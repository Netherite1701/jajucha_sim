# User Workflow

The normal, documented workflow for the Jajucha Simulator (Step 11.34).

## Core principle

```text
Open simulator
    ↓
Load or create course
    ↓
Start Python program
    ↓
Run simulation
    ↓
View scoring/debug information
    ↓
Reset or batch test
```

A new user does **not** need to:

* construct a scene hierarchy manually
* connect subsystem references manually
* create cameras manually
* configure the bridge manually
* copy scripts into arbitrary folders
* edit Unity prefabs to begin testing
* open the Unity Editor for normal simulator use

The standalone build provides the complete normal workflow.

학생 배포본에서 실제로 누르는 버튼과 카메라 조작은
`docs/STUDENT_QUICKSTART_KO.md`를 먼저 읽으세요.

---

## First setup

Run once (project-local `.venv`; no global Python install required):

```powershell
.\scripts\setup_python.ps1
```

This creates `.venv`, installs `python/requirements.txt`, verifies `jchm` /
`jchm_sim` imports, and prints the exact commands for running examples.

## Start the simulator

From a build (recommended for normal use):

```powershell
.\scripts\run_simulator.ps1
```

or with options:

```powershell
.\scripts\run_simulator.ps1 -Course "2026_preliminary" -Mode "Drive"
```

From the Unity Editor (development only):

1. Open `Assets/JajuchaSim/Scenes/JajuchaSimulator.unity`.
2. Press Play. The bootstrap loads the selected 2026 course and enters **Drive**
   mode automatically.

## Start the user program

In a **second terminal**:

```powershell
.\.venv\Scripts\python.exe .\python\user\main.py
```

The default `main.py` reads the center camera and drives straight at low speed.
Replace the loop body with your own algorithm (see `python/user/README.md`).

## View the simulation

* The main viewport shows the observer camera (chase mode by default; `F3`
  cycles chase / top-down / free-fly). In free-fly, WASD/arrow keys move,
  Q/E move vertically, Shift accelerates, and right-mouse drag looks around.
* The single dark `SimulatorDashboardUI` shows application mode, bridge state,
  stage, and run status. Its tabs are `주행 / 코스 편집 / 채점 / 센서 / 디버그`.
* The dashboard owns the controls; scenario, scoring, and debug components
  provide state only and do not create additional runtime canvases.
* In `디버그 → 사용자 스크립트`, enter a name and press `＋ 새 스크립트` to
  create a runnable Python controller template. New files are saved under the
  writable `JajuchaSim/Scripts` folder and can be opened with `편집`.

## Default key bindings

| Key | Action |
|---|---|
| `F1` | Toggle full debug UI |
| `F2` | Toggle map editor (Drive ↔ Edit Map) |
| `F3` | Switch observer camera mode (chase / top-down / free-fly) |
| `Space` | Pause / resume simulation (Drive mode) |
| `.` (period) | Single simulation step while paused (Drive mode) |
| `R` | Rotate selected map item (Map Editor) |
| `Delete` | Delete selected map item (Map Editor) |
| `Ctrl+Z` | Undo map edit |
| `Ctrl+Y` | Redo map edit |

All bindings are handled through the Unity Input System (no legacy Input
API) and are configurable in code: `ApplicationBootstrap` (F1/F2/Space/period),
`ObserverCameraController` (F3), and `MapEditorHud` (R/Delete/Ctrl+Z/Ctrl+Y).

## Stop safely

Press `Ctrl+C` in the Python terminal. The user script sends
`jchm.control.set_motor(0, 0, 0)` where practical. The simulator also has a
motor watchdog that stops propulsion when the bridge disconnects.

---

## Map-creation workflow (Step 11.35)

```text
Launch simulator
→ 코스 편집 탭
→ 공식 원본 확인 (읽기 전용)
→ 연습용 복사본 만들기
→ 도구 선택 후 도로/구조물/오브젝트/트리거 편집
→ 자동 검증
→ 시험 주행
→ 연습 코스 저장
```

Everything works in the standalone build. Courses are saved under the writable
data folder (`Courses/`), never into read-only build assets.

### Using the map editor

1. Open **코스 편집**. Official preliminary/final JSON is explicitly marked
   `공식 원본 · 읽기 전용`.
2. Press **연습용 복사본 만들기**; only then are tools, undo/redo, and save
   enabled. Road painting updates the visual and 5 cm judgement layers together.
3. Place tunnels/ramps by selecting a tool and clicking/dragging on the course;
   object and trigger tools use the same placement flow. **자동 검증** checks
   the 41-panel inventory, dimensions, structure rules, start/finish, sensors,
   and candidate IDs.
4. **시험 주행** snapshots the unsaved practice document and observer-camera
   state. Closing the result or pressing **시험 주행 종료** restores both; the
   test run is not saved.
5. **연습 코스 저장** writes `Courses/Practice/practice_2026_*.json`; an
   existing base name is numbered rather than overwritten. **최근 연습 코스
   불러오기** lists and loads those copies without changing the initial official
   course preference.

The runtime editor uses separate panels for tools, inspector, layers, actions,
events, and scenario settings. This keeps the controls readable at common
window sizes; resizing the simulator is supported.

## Competition-test workflow (Step 11.36)

```text
Load course
→ choose scenario (start/finish triggers, max time, slow zone)
→ Reset
→ start external controller (python/user/main.py)
→ Start Run
→ finish / timeout
→ inspect score
→ export result
```

The score is produced by the same `ScenarioManager → ScoreManager → RunResult`
path used by automated tests.

## Batch-test workflow (Step 11.37)

```text
Open Testing
→ select course/scenario
→ set run count
→ set seed
→ choose simulation speed
→ start batch
→ inspect summary (avg/best/worst, perfect runs, timeouts)
→ debug failed run (failure diagnostics export)
```

The batch system is generic and independent of ANN/FSM internals; it runs any
external controller through the same scenario/scoring path.

---

## Convenience launchers

* `.\scripts\run_development.ps1` — starts the simulator, waits for bridge
  readiness, then runs `python/user/main.py` (convenience wrapper only).
* `.\scripts\activate_python.ps1` — activates `.venv`.
* `.\scripts\check_bridge.py` — reports bridge readiness with a useful exit
  code.
* `.\scripts\validate_project.ps1` — validates the whole project.
* `.\scripts\build_windows.ps1` — builds the standalone distribution.

## Where files go

All user-created data is written under the **writable data folder**:

```text
{Application.persistentDataPath}/JajuchaSim/
├─ Courses/      (user courses, copies of edited maps)
├─ Runs/         (run results, diagnostics exports)
├─ Screenshots/
├─ Logs/         (simulator.log, bridge.log, scoring.log, testing.log)
└─ UserConfig/   (user-editable configuration overrides)
```

The resolved location is shown in the runtime status bar. Never write into
`JajuchaSimulator_Data/` or the `Assets/` folder of a build.
