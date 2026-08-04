"""
Simulator-specific simulation control functions.

These functions are NOT available on the real Jajucha vehicle.
They provide direct control over the Unity simulation lifecycle.
"""

from typing import Any, Dict, Optional

from jchm._backend import get_backend as _get_backend
from jchm._sim_backend import SimulatorBackend


def _ensure_sim_backend():
    """Get the backend and verify it's a SimulatorBackend."""
    backend = _get_backend()
    if not isinstance(backend, SimulatorBackend):
        raise RuntimeError(
            "jchm_sim functions require the simulator backend. "
            "Current backend is not the simulator."
        )
    return backend


def connect():
    """
    Force a connection to the simulator.

    Normally the connection is established automatically on the first
    command. This function can be used to verify the connection early.
    """
    backend = _ensure_sim_backend()
    backend._ensure_connected()


def disconnect():
    """
    Disconnect from the simulator.

    The connection will be re-established automatically on the next command.
    """
    backend = _ensure_sim_backend()
    backend.disconnect()


def ping() -> Dict[str, Any]:
    """
    Send a ping command to check simulator connectivity.

    Returns:
        The response payload containing sim_time.
    """
    backend = _ensure_sim_backend()
    return backend.ping()


def status() -> Dict[str, Any]:
    """
    Get the current simulator and vehicle status.

    Returns:
        A dictionary with state, tick, sim_time, and vehicle info.
    """
    backend = _ensure_sim_backend()
    return backend.get_status()


def reset():
    """Reset the simulation to its initial state."""
    backend = _ensure_sim_backend()
    backend.sim_reset()


def start():
    """Start or resume the simulation."""
    backend = _ensure_sim_backend()
    backend.sim_start()


def pause():
    """Pause the simulation."""
    backend = _ensure_sim_backend()
    backend.sim_pause()


def step():
    """
    Advance exactly one simulation tick.

    Use this when the simulation is paused to step through
    frame by frame for debugging.
    """
    backend = _ensure_sim_backend()
    backend.sim_step()


def start_run():
    """
    Start the scenario run (begin the start-signal sequence).

    This is simulator-only: the student's autonomous code should never call
    this during normal driving (Step 8.37).
    """
    backend = _ensure_sim_backend()
    backend.sim_start_run()


def abort_run():
    """
    Abort the active scenario run (state → Aborted, propulsion stopped).

    Results collected so far are preserved.
    """
    backend = _ensure_sim_backend()
    backend.sim_abort_run()


def get_run_status() -> Dict[str, Any]:
    """
    Get the current scenario run status.

    Returns:
        A dict with state, signal, run_id, elapsed_sec, has_result,
        collisions, sim_time, tick.
    """
    backend = _ensure_sim_backend()
    response = backend.sim_get_run_status()
    return response.get("payload", response)


def get_result() -> Dict[str, Any]:
    """
    Get the final run result.

    Returns:
        A parsed dict with runId, course, status, elapsedSec, collisions,
        slowZones, speedGates, etc.

    Raises:
        RuntimeError if the run has not finished yet.
    """
    backend = _ensure_sim_backend()
    return backend.sim_get_result()


def wait_for_result(timeout: float = 180.0, poll_interval: float = 0.5) -> Dict[str, Any]:
    """
    Poll until the run finishes (or the timeout expires).

    Args:
        timeout: Maximum wall-clock seconds to wait (Step 8.39).
        poll_interval: Seconds between status polls.

    Returns:
        The parsed final result, or None if the timeout expired before the
        run finished.
    """
    import time as _time

    backend = _ensure_sim_backend()
    deadline = _time.monotonic() + timeout
    while True:
        status = backend.sim_get_run_status()
        payload = status.get("payload", status)
        if payload.get("has_result", False):
            return backend.sim_get_result()
        if _time.monotonic() >= deadline:
            return None
        _time.sleep(poll_interval)
