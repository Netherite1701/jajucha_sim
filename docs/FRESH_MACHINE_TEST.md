# Fresh-Machine Test (Step 11.49)

Verifies the complete workflow from a clean copy. Run this on a machine with no
Jajucha Simulator state.

## Prerequisites

- Git
- Python 3.9+ (for the user workspace; the simulator itself does not need it)
- Unity 6 LTS (6000.3.20f1) — only needed to build the standalone; not needed
  to run a pre-built distribution
- Windows 10/11 (for the standalone build; `setup_python.sh` covers POSIX)

## Process

```text
clone/copy repository
→ run Python setup
→ open/build simulator
→ launch simulator
→ run motor example
→ run camera example
→ run template course
→ export result
```

### 1. Clone/copy

```powershell
git clone <repo-url> jajucha-sim
cd jajucha-sim
```

### 2. Python setup

```powershell
.\scripts\setup_python.ps1
```

Expect: `.venv` created, `jchm`/`jchm_sim` imports verified, example commands
printed.

### 3. Build (or use a pre-built distribution)

```powershell
.\scripts\build_windows.ps1
```

Expect: `dist/JajuchaSimulator/JajuchaSimulator.exe` produced with
`Courses/`, `Config/`, `Python/`, `Docs/`, `Scripts/`.

(If you already have a distribution, copy it and skip this step.)

### 4. Launch simulator

```powershell
.\scripts\run_simulator.ps1
```

Expect: READY, Drive mode, template course loaded.

### 5. Run motor example

Second terminal:

```powershell
.\.venv\Scripts\python.exe .\python\examples\01_motor_test.py
```

Expect: stop / forward / reverse / left / right / independent steering / speed
zero with steering, then stop.

### 6. Run camera example

```powershell
.\.venv\Scripts\python.exe .\python\examples\02_center_camera.py
```

Expect: a frame shape printed and an OpenCV window with the center camera.

### 7. Run template course

Run a scenario (`06_test_run.py` or the scenario panel) and drive the template
course end to end; expect the finish trigger to produce a result with a score.

### 8. Export result

Use Export Diagnostics (or the results panel). Expect a JSON file under the
writable `Runs/` folder.

## Notes

- The simulator and Python are independent; `run_development.ps1` is an
  optional convenience.
- All user data goes to the writable data folder
  (`{persistentDataPath}/JajuchaSim/`), never into the build assets.
