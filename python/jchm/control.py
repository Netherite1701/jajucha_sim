"""
Public JCHM control API.

This is the module that student code imports directly:

    import jchm
    jchm.control.set_motor(-5, -5, 3)

All functions delegate to the selected backend (real vehicle or simulator).
"""

from ._backend import get_backend


def set_motor(left: int, right: int, speed: int):
    """
    Set the motor command for the Jajucha vehicle.

    Args:
        left: Front-left steering command in JCHM units [-10, 10].
              Negative = left turn, Positive = right turn.
        right: Front-right steering command in JCHM units [-10, 10].
        speed: Rear drive speed command [-30, 30].
               Negative = backward, 0 = stop, Positive = forward.

    The values are clamped to their valid ranges by both the Python client
    and the Unity simulator.
    """
    # Validate and clamp at the Python side too
    left = _clamp(left, -10, 10)
    right = _clamp(right, -10, 10)
    speed = _clamp(speed, -30, 30)

    backend = get_backend()
    backend.set_motor(left, right, speed)


def stop_motor():
    """Stop propulsion and centre both steering channels.

    This mirrors the physical JCHM convenience API and uses the same backend
    command path as :func:`set_motor`, keeping simulator state, watchdog, and
    trace output consistent.
    """
    backend = get_backend()
    backend.set_motor(0, 0, 0)


def _clamp(value: int, min_val: int, max_val: int) -> int:
    """Clamp an integer to the given range."""
    if value < min_val:
        return min_val
    if value > max_val:
        return max_val
    return value
