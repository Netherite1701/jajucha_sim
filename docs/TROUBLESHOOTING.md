# Troubleshooting

Diagnostic steps for common problems (Step 11.48). Each item gives concrete
checks, not vague advice.

---

## Simulator does not start

1. Check the writable log file: open the data folder (shown in the status bar)
   → `Logs/simulator.log`. Read the last lines.
2. If the bootstrap failed, an on-screen panel shows the failing system, the
   reason, and the error code. Check that `Courses/template_course.json` exists
   next to the executable (or in the writable `Courses/` folder).
3. In the Unity Editor: confirm you opened
   `Assets/JajuchaSim/Scenes/JajuchaSimulator.unity` (the authoritative scene),
   not a test scene.
4. Confirm `Config/default_simulator.json` is valid JSON. A malformed config
   falls back to built-in defaults (check the log for `Command-line overrides`
   / startup messages).

## Bridge connection refused

1. Is the simulator running? `jchm` connects to `127.0.0.1:8765` by default.
2. Run the readiness check:
   ```powershell
   .\.venv\Scripts\python.exe .\scripts\check_bridge.py
   ```
   Exit `0` = ready, `1` = reachable but not ready/protocol issue, `2` = not
   reachable.
3. Confirm the bridge port matches `Config/default_simulator.json` →
   `bridgePort` (default 8765). If you changed the port, pass `--port` to
   `check_bridge.py` or set `JCHM` backend port accordingly.
4. Check `Logs/bridge.log` for `Listening on 127.0.0.1:8765` or
   `Failed to start listener` (port already in use by another process).

## Protocol mismatch

1. The bridge protocol version is `1` (see `python/jchm/_protocol.py` and
   `Assets/JajuchaSim/Bridge/Runtime/BridgeProtocol.cs`).
2. If Python and Unity disagree, both sides must be from the same repository
   checkout. Re-run `.\scripts\setup_python.ps1` after updating the repo.
3. `check_bridge.py` reports `Protocol v1` after a successful handshake; a
   mismatch fails the handshake with a readable error.

## Python package not found

1. Use the project-local environment:
   ```powershell
   .\.venv\Scripts\python.exe .\python\user\main.py
   ```
2. If you installed globally by accident, re-run `.\scripts\setup_python.ps1`
   and use `.venv`.
3. Verify imports:
   ```powershell
   .\.venv\Scripts\python.exe -c "import jchm, jchm_sim; print('ok')"
   ```

## Camera returns no frame

1. Confirm the vehicle has been spawned (status bar shows the app in Drive
   mode; the viewport shows the car).
2. Confirm the sensor cameras exist: `Logs/simulator.log` should contain
   `[SENSOR] CameraSensorSystemBehaviour initialized`.
3. Run `python/examples/02_center_camera.py`. If it reports a frame shape, the
   pipeline works; if it raises, follow the bridge troubleshooting above.
4. Camera capture happens on the first simulation tick after initialization.
   If the simulation is paused, frames are still captured at the configured
   rate while ticking; an idle (unstarted) simulation produces no frames.

## Course fails validation

1. Run the course through the validator in the map editor (Save validates) or
   check the log for `[CourseSerializer] Failed to parse course JSON`.
2. Confirm `tileSizeCm` is positive, structures sit on road tiles, and speed
   terminal pairs have both A and B with a shared `pairId`.
3. Re-save from the map editor; the editor normalizes the JSON.

## Vehicle does not move

1. Confirm the bridge is connected (`bridge:CONNECTED` in the status bar) and
   the user program is running.
2. Confirm the user script sends `speed != 0`. `jchm.control.set_motor(l, r, 0)`
   never propels (manual zero-speed invariant).
3. Check `Logs/simulator.log` for watchdog messages (`speed` forced to 0 after
   a command timeout / disconnect). Reconnect the Python client.
4. In the Unity Editor, confirm the simulation is running (not paused).

## Vehicle moves after reset

1. `ResetSimulation` returns the kernel to `Ready` and stops vehicle motion
   (velocity zeroed). If the vehicle still drifts, the physics needs a moment
   to settle; wait a few ticks before asserting position.
2. Check that no stray Python client is sending motor commands after reset.
   The bridge watchdog stops propulsion ~1s after the last command.

## PowerShell execution policy issue

Use a one-session bypass (does NOT change machine-wide policy):

```powershell
Set-ExecutionPolicy -Scope Process Bypass
```

Then run the script. See `scripts/activate_python.ps1`.

## Standalone build cannot save files

1. The build writes to the **writable data folder** shown in the status bar
   (`{persistentDataPath}/JajuchaSim/`). Never write into
   `JajuchaSimulator_Data/`.
2. Confirm the OS user has write permission to that folder.
3. If the folder is on a read-only volume, set a different `persistentDataPath`
   (not normally needed).

## Debug UI missing

1. Press `F1` to toggle the debug UI (status bar + runtime panels).
2. Confirm `debugUiEnabled` is `true` in `Config/default_simulator.json`
   (or omit `--no-debug-ui` when launching).
3. The map editor palette is part of the runtime UI; if it is missing, check
   `Logs/simulator.log` for UI build errors.

## Sensor image contains an overlay

1. Sensor cameras exclude the `SimulatorDebug` layer (layer 6); debug overlays
   (trigger colors, grid, structure IDs) are visible only to the observer
   camera. If a sensor image shows an overlay, the sensor camera's culling mask
   was changed — reset it to `SimLayers.SensorCullingMask`.
2. This is verified by `CameraLayerConfig` tests; run the EditMode suite.

## Batch run never finishes

1. Check `Logs/testing.log` and `Logs/simulator.log` for the batch progress.
2. Confirm each run has a `maxRunTimeSec` timeout so runs cannot hang
   forever (the scenario forces `TIME_LIMIT`).
3. Confirm the external controller either sends motor commands or the batch is
   configured with a controller; an idle vehicle will eventually time out.
4. Check the batch summary CSV/JSON under the writable `Runs/` folder.

---

## Diagnostics export

Use **Export Diagnostics** (or the API) to gather:
simulator version, configuration, course/scenario ids, bridge status, recent
events/errors, and log tails. The export never includes user source code.
