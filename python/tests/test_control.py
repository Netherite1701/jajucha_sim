"""Unit tests for the jchm.control module with mocked backend."""

import unittest
from unittest.mock import MagicMock, patch

import sys
import os

# Add the python directory to the path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from jchm import control
from jchm._backend import set_backend, BackendType


class MockBackend:
    """A mock backend that records calls."""

    def __init__(self):
        self.calls = []
        self.set_motor_calls = []

    def set_motor(self, left, right, speed):
        self.set_motor_calls.append((left, right, speed))
        self.calls.append(("set_motor", left, right, speed))


class TestControlSetMotor(unittest.TestCase):
    """Tests for jchm.control.set_motor."""

    def setUp(self):
        self.mock_backend = MockBackend()
        # We need to inject the mock backend
        import jchm._backend as b
        b._backend_instance = self.mock_backend
        b._backend_type = BackendType.SIMULATOR

    def tearDown(self):
        import jchm._backend as b
        b._backend_instance = None

    def test_set_motor_preserves_arguments(self):
        """set_motor passes arguments correctly to backend."""
        control.set_motor(-5, -5, 3)
        self.assertEqual(len(self.mock_backend.set_motor_calls), 1)
        self.assertEqual(self.mock_backend.set_motor_calls[0], (-5, -5, 3))

    def test_set_motor_zero(self):
        """set_motor(0,0,0) passes correctly."""
        control.set_motor(0, 0, 0)
        self.assertEqual(self.mock_backend.set_motor_calls[0], (0, 0, 0))

    def test_set_motor_clamps_left(self):
        """set_motor clamps left value to [-10, 10]."""
        control.set_motor(-50, 0, 0)
        self.assertEqual(self.mock_backend.set_motor_calls[0][0], -10)

        control.set_motor(50, 0, 0)
        self.assertEqual(self.mock_backend.set_motor_calls[1][0], 10)

    def test_set_motor_clamps_right(self):
        """set_motor clamps right value to [-10, 10]."""
        control.set_motor(0, -50, 0)
        self.assertEqual(self.mock_backend.set_motor_calls[0][1], -10)

        control.set_motor(0, 50, 0)
        self.assertEqual(self.mock_backend.set_motor_calls[1][1], 10)

    def test_set_motor_clamps_speed(self):
        """set_motor clamps speed value to [-30, 30]."""
        control.set_motor(0, 0, -100)
        self.assertEqual(self.mock_backend.set_motor_calls[0][2], -30)

        control.set_motor(0, 0, 100)
        self.assertEqual(self.mock_backend.set_motor_calls[1][2], 30)

    def test_set_motor_boundary_values(self):
        """set_motor works with edge values."""
        control.set_motor(-10, -10, -30)
        self.assertEqual(self.mock_backend.set_motor_calls[0], (-10, -10, -30))

        control.set_motor(10, 10, 30)
        self.assertEqual(self.mock_backend.set_motor_calls[1], (10, 10, 30))


class TestControlErrors(unittest.TestCase):
    """Tests for error handling in control module."""

    def setUp(self):
        import jchm._backend as b
        b._backend_instance = None
        b._backend_type = BackendType.SIMULATOR

    def tearDown(self):
        import jchm._backend as b
        b._backend_instance = None

    @patch("jchm._sim_backend.SimulatorBackend.set_motor")
    def test_backend_error_propagates(self, mock_set_motor):
        """Errors from the backend propagate to the caller."""
        from jchm.errors import JchmConnectionError

        mock_set_motor.side_effect = JchmConnectionError("Test error")

        with self.assertRaises(JchmConnectionError):
            control.set_motor(0, 0, 3)


class TestConnectionErrors(unittest.TestCase):
    """Tests for connection error handling (3.57)."""

    def setUp(self):
        import jchm._backend as b
        b._backend_instance = None
        b._backend_type = BackendType.SIMULATOR

    def tearDown(self):
        import jchm._backend as b
        b._backend_instance = None

    def test_connection_refused_clear_error(self):
        """When simulator is not running, get clear error message (not raw socket error)."""
        from jchm.errors import JchmConnectionError
        from jchm._sim_backend import SimulatorBackend

        # Use a port that's definitely not listening
        backend = SimulatorBackend(host="127.0.0.1", port=59999)

        with self.assertRaises(JchmConnectionError) as ctx:
            backend.set_motor(0, 0, 3)

        error_msg = str(ctx.exception)
        # Should contain helpful information
        self.assertIn("JCHM Simulator connection failed", error_msg)
        self.assertIn("127.0.0.1", error_msg)
        self.assertIn("59999", error_msg)
        # Should NOT contain raw socket internals
        self.assertNotIn("WinError", error_msg)
        self.assertNotIn("WSAECONNREFUSED", error_msg)


if __name__ == "__main__":
    unittest.main()
