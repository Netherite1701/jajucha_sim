"""Example-script tests (Step 11.52).

Verifies that every example and the user entry point compile, and that the
public packages import cleanly. Examples fail with readable messages when the
simulator is unavailable (handled inside each example's own error handling).
"""

import os
import py_compile
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
PYTHON_DIR = os.path.dirname(HERE)
EXAMPLES_DIR = os.path.join(PYTHON_DIR, "examples")
USER_DIR = os.path.join(PYTHON_DIR, "user")

sys.path.insert(0, PYTHON_DIR)

EXAMPLE_FILES = [
    "01_motor_test.py",
    "02_center_camera.py",
    "03_three_cameras.py",
    "04_depth_view.py",
    "05_drive_and_view.py",
    "06_test_run.py",
    "07_stop_on_line.py",
    "08_camera_lane_drive.py",
]


class TestPackagesImport(unittest.TestCase):
    """Verify the public packages import (Step 11.52)."""

    def test_import_jchm(self):
        import jchm  # noqa: F401
        import jchm.camera  # noqa: F401
        import jchm.control  # noqa: F401

    def test_import_jchm_sim(self):
        import jchm_sim  # noqa: F401

    def test_import_errors(self):
        import jchm.errors  # noqa: F401


class TestExamplesCompile(unittest.TestCase):
    """Every example and the user entry point must compile."""

    def test_examples_compile(self):
        for name in EXAMPLE_FILES:
            path = os.path.join(EXAMPLES_DIR, name)
            self.assertTrue(os.path.isfile(path), f"missing example: {name}")
            py_compile.compile(path, doraise=True)

    def test_user_main_compiles(self):
        path = os.path.join(USER_DIR, "main.py")
        self.assertTrue(os.path.isfile(path), "missing python/user/main.py")
        py_compile.compile(path, doraise=True)


class TestExampleBehavior(unittest.TestCase):
    """Behavioral checks that do not need a running simulator."""

    def test_examples_fail_readably_without_simulator(self):
        """set_motor without a simulator raises JchmConnectionError with a
        readable message (not a raw socket traceback)."""
        from jchm.errors import JchmConnectionError
        from jchm._sim_backend import SimulatorBackend

        backend = SimulatorBackend(host="127.0.0.1", port=59998)
        with self.assertRaises(JchmConnectionError) as ctx:
            backend.set_motor(0, 0, 3)
        message = str(ctx.exception)
        self.assertIn("JCHM Simulator connection failed", message)
        self.assertIn("127.0.0.1", message)


if __name__ == "__main__":
    unittest.main()
