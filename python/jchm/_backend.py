"""
Backend abstraction for JCHM operations.

Selects between the real vehicle backend and the simulator backend.
Default is simulator when running outside the real vehicle environment.
"""

import os
from enum import Enum
from typing import Optional


class BackendType(Enum):
    REAL = "real"
    SIMULATOR = "sim"


_backend_instance = None  # type: Optional[object]
_backend_type = BackendType.SIMULATOR  # default to simulator


def get_backend():
    """Get the current backend instance, creating it if necessary."""
    global _backend_instance, _backend_type

    if _backend_instance is not None:
        return _backend_instance

    if _backend_type == BackendType.SIMULATOR:
        from ._sim_backend import SimulatorBackend
        _backend_instance = SimulatorBackend()
    else:
        # Real vehicle backend - would import from real jchm runtime
        # Not implemented in this simulator package
        raise NotImplementedError(
            "Real vehicle backend is not available in the simulator package. "
            "Set JCHM_BACKEND=sim or use the default."
        )

    return _backend_instance


def set_backend(backend_type: BackendType):
    """Force a specific backend type for testing/configuration."""
    global _backend_type, _backend_instance
    _backend_type = backend_type
    _backend_instance = None  # force re-creation


# Check environment variable for backend selection
_env_backend = os.environ.get("JCHM_BACKEND", "").lower().strip()
if _env_backend == "real":
    _backend_type = BackendType.REAL
elif _env_backend == "sim":
    _backend_type = BackendType.SIMULATOR
