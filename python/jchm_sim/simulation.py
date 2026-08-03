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
