"""
JCHM-specific exceptions for clear error messaging.
"""


class JchmError(Exception):
    """Base exception for all JCHM-related errors."""
    pass


class JchmConnectionError(JchmError):
    """Raised when the simulator backend cannot connect to the Unity bridge."""
    pass


class JchmSimulatorTimeout(JchmError):
    """Raised when the simulator backend does not receive a timely response."""
    pass


class JchmProtocolError(JchmError):
    """Raised on protocol version mismatch or invalid message format."""
    pass


class JchmBackendError(JchmError):
    """Raised when the selected backend encounters an internal error."""
    pass
