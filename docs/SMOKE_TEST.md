# Standalone Distribution Smoke Test (Step 11.50)

This procedure must pass using **only the distribution folder** — no Unity
Editor may be used.

Prerequisites: a built distribution at `dist/JajuchaSimulator/` (from
`.\scripts\build_windows.ps1`), Python 3.9+ with `.venv` set up (or a system
Python with `python/requirements.txt` installed).

## Steps

1. **Launch the simulator**
   ```powershell
   .\dist\JajuchaSimulator\JajuchaSimulator.exe
   ```
   Expect: window opens, status bar shows `READY`, mode `Drive`, and the
   template course is loaded (vehicle visible in the viewport).

2. **Load template course**
   The bootstrap loads `Courses/template_course.json` automatically. Verify the
   status bar shows `READY` and the vehicle sits at the start.

3. **Open Map Editor** — press `F2` (or use the runtime UI). Expect: simulation
   pauses, top-down camera, tile grid/editor palette visible.

4. **Edit and save a copy** — add/remove a road tile or object, then use Save.
   Expect: a course JSON file appears under the writable data folder
   (`Logs`/status bar shows the data folder path).

5. **Return to Drive mode** — press `F2` again. Expect: simulation resumes.

6. **Connect Python example** — in a second terminal:
   ```powershell
   .\.venv\Scripts\python.exe .\scripts\check_bridge.py
   ```
   Expect exit code 0 (`[OK] Simulator bridge reachable`, `Protocol v1`,
   `Simulation READY`).

7. **Run scenario** — run `python/examples/06_test_run.py` (or start a run from
   the scenario panel), then `python/examples/01_motor_test.py` to drive.
   Expect: the vehicle responds and the run finishes.

8. **Receive score** — when the run finishes, the results panel shows the final
   score (base score − penalties).

9. **Run a short batch** — use the Testing UI (or `jchm_sim`) with a small run
   count. Expect: a batch summary appears and `Runs/` gets results.

10. **Export results** — use Export Diagnostics. Expect: a JSON diagnostics file
    under the writable `Runs/` folder.

11. **Close and relaunch** — quit the simulator and start it again.

12. **Verify saved course remains available** — the course saved in step 4 is in
    the writable `Courses/` folder and can be loaded via the map editor Load
    button (or `--course`).

## Pass criteria

- No Unity Editor used.
- Steps 1–12 all succeed.
- `check_bridge.py` exits 0 while the simulator runs.
- Logs exist under the writable data folder (`Logs/simulator.log` at minimum).
