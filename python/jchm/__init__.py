"""
JCHM - Jajucha Control and Handling Module

This is the compatibility layer that provides the same API as the real
Jajucha vehicle runtime. Import this module in your student code exactly
as you would on the real vehicle:

    import jchm
    jchm.control.set_motor(-5, -5, 3)

The backend (real vehicle vs. simulator) is selected automatically.
No code changes are needed between real and simulated runs.
"""

from . import camera
from . import lidar
from . import control
from ._backend import get_backend, set_backend, BackendType

__all__ = ["camera", "lidar", "control", "get_backend", "set_backend", "BackendType"]
