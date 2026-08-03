"""
JCHM Simulator Extension - simulator-only tooling.

This package provides operations that exist only in the simulator
environment, NOT on the real Jajucha vehicle. Students can use these
for debugging, testing, and automation.

Example:
    import jchm
    import jchm_sim

    jchm.control.set_motor(0, 0, 3)
    status = jchm_sim.status()
    jchm_sim.reset()
"""

from .simulation import (
    connect,
    disconnect,
    ping,
    status,
    reset,
    start,
    pause,
    step,
)

__all__ = [
    "connect",
    "disconnect",
    "ping",
    "status",
    "reset",
    "start",
    "pause",
    "step",
]
