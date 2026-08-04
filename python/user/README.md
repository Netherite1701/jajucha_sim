# User Python Workspace

This folder belongs entirely to **you** (the user/student). The simulator
never writes here and never runs your code for you.

## Rules

1. **Real driving code should use `jchm`.** The `jchm` package is the
   real-compatible vehicle API (`jchm.control.set_motor`, `jchm.camera.*`).
   Code written against it runs unchanged on the real Jajucha vehicle.

2. **Simulator lifecycle/testing tools use `jchm_sim`.** Reset, start/pause,
   scenario runs, batch results — these exist only in the simulator and are
   **not** available on the real vehicle.

3. **Do not modify simulator internals to implement autonomous logic.** Your
   algorithm lives here in `python/user/` (or in `python/examples/` as a copy
   to experiment with). The Unity project is a tool, not your code.

4. **Always send `speed=0` on shutdown where practical.**
   ```python
   jchm.control.set_motor(0, 0, 0)
   ```
   This is the manual-compatible safe stop.

5. **Handle bridge disconnects safely.** If the simulator is not running, calls
   raise `jchm.errors.JchmConnectionError`. Catch it and print a readable
   message instead of crashing:
   ```python
   from jchm.errors import JchmConnectionError
   try:
       jchm.control.set_motor(0, 0, 3)
   except JchmConnectionError as exc:
       print("Simulator not reachable:", exc)
   ```

6. **Do not depend on simulator ground truth for normal autonomous driving.**
   Your driving algorithm should use only what `jchm` exposes (cameras, depth,
   motor control) — the same information the real vehicle has. Ground truth
   (positions, scores, speed measurements) is for testing/scoring only.

7. **Keep your user code real-compatible.** The whole point of `jchm` is that
   the same code drives the simulator and the real car. Avoid simulator-only
   imports (`jchm_sim`) inside your driving logic.

## Files

- `main.py` — your entry point. The default template reads the center camera
  and drives straight at a low speed. Replace the loop body with your own
  algorithm.

## Run

From a second terminal (with the simulator running in Drive mode):

```powershell
.\.venv\Scripts\python.exe .\python\user\main.py
```

or (POSIX):

```bash
./.venv/bin/python python/user/main.py
```

Press `Ctrl+C` to stop safely.
