# VS Code Workflow

The repository includes a Windows-oriented VS Code workspace configuration for
editing the Python controller, running tests, checking the bridge, and opening
the Unity simulator.

## First-time setup

Open the project root in VS Code:

```text
C:\dev\jajucha-sim
```

Install the recommended extensions when VS Code offers them, then run the
task **Jajucha: Setup Python** from `Terminal > Run Task`. This creates the
project-local `.venv` and installs the Python dependencies.

For C# editing, use the Unity Visual Studio Tools extension. Do not convert
the generated Unity `.csproj` files to SDK-style projects. Unity regenerates
those files, and C# Dev Kit does not support their traditional Unity format;
if C# Dev Kit is installed, disable it for this workspace to avoid repeated
unsupported-project warnings.

The default Unity path is configured for Unity `6000.3.20f1`. If Unity is
installed elsewhere, edit `jajuchaSim.unityPath` in `.vscode/settings.json`.

## Normal editor workflow

Use this sequence:

1. Run **Jajucha: Run simulator (build if missing)** from `Terminal > Run Task`.
   This launches the standalone simulator. The first run builds it if the
   executable is not already present.
2. Run **Jajucha: Check bridge** and confirm that it reports protocol v1 and
   simulation readiness.
3. Run **Jajucha: Run user controller**, or select **Jajucha: Debug user
   controller** under `Run and Debug` when you want breakpoints.
4. Edit `python/user/main.py`. Stop and rerun the controller after changes.
5. Stop the controller with Ctrl+C, or stop its VS Code task/debug session.

The simulator and controller use separate terminals. The controller reads only
the real-compatible `jchm` API:

```python
import jchm

image = jchm.camera.get_image("center")
jchm.control.set_motor(0, 0, 3)
```

Always send `jchm.control.set_motor(0, 0, 0)` when shutting down.

## Useful tasks

| Task | Purpose |
|---|---|
| `Jajucha: Setup Python` | Create/update `.venv` and install dependencies |
| `Jajucha: Open Unity Editor` | Open the authoritative Unity scene for editing or Play Mode |
| `Jajucha: Run simulator (build if missing)` | Build when necessary and launch the standalone simulator |
| `Jajucha: Check bridge` | Verify TCP bridge readiness |
| `Jajucha: Run user controller` | Run `python/user/main.py` |
| `Jajucha: Run Python tests` | Run the Python pytest suite |
| `Jajucha: Validate project` | Run repository validation without Unity tests |
| `Jajucha: Build Windows standalone` | Build `dist/JajuchaSimulator` |
| `Jajucha: Run standalone simulator` | Launch an existing standalone build |

## Debugging and tests

Use **Jajucha: Debug user controller** for Python breakpoints. Use
**Jajucha: Debug Python tests** to debug a failing Python test.

Unity EditMode and PlayMode tests remain Unity tests. Run them from the Unity
Test Runner or with the commands documented in `docs/TESTING.md`.

## Standalone workflow

After building a standalone executable, **Jajucha: Run standalone simulator**
can be used instead of opening Unity. The standalone build is optional for
development; the Unity Editor workflow is the easiest path while developing.
