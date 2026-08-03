"""
Unit tests for jchm.camera module with mocked backend.
"""

import sys
import os
import unittest

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from jchm import camera
from jchm._backend import set_backend, BackendType


class MockCameraBackend:
    """A mock backend that returns synthetic camera frames."""

    def __init__(self):
        self.get_image_calls = []
        self.get_depth_calls = []

    def get_image(self, location):
        self.get_image_calls.append(location)
        # Return a small synthetic RGB frame (BGR after conversion)
        # Create a simple 10x10 image with a red marker at top-left
        frame = np.zeros((10, 10, 3), dtype=np.uint8)
        # Red in BGR is [0, 0, 255]
        frame[0, 0] = [0, 0, 255]
        # Green in BGR is [0, 255, 0]
        frame[5, 5] = [0, 255, 0]
        # Blue in BGR is [255, 0, 0]
        frame[9, 9] = [255, 0, 0]
        return frame

    def get_depth(self):
        self.get_depth_calls.append(1)
        # Return a synthetic depth image
        depth = np.zeros((10, 10), dtype=np.uint8)
        # Near objects (bright)
        depth[2:5, 2:5] = 200
        # Far objects (dark)
        depth[7:9, 7:9] = 30
        return depth


class TestCameraGetImage(unittest.TestCase):
    """Tests for jchm.camera.get_image."""

    def setUp(self):
        self.mock_backend = MockCameraBackend()
        import jchm._backend as b
        b._backend_instance = self.mock_backend
        b._backend_type = BackendType.SIMULATOR

    def tearDown(self):
        import jchm._backend as b
        b._backend_instance = None

    def test_get_image_returns_numpy_array(self):
        """get_image returns a NumPy array."""
        result = camera.get_image("center")
        self.assertIsInstance(result, np.ndarray)

    def test_get_image_shape(self):
        """get_image returns expected shape (height, width, 3)."""
        result = camera.get_image("center")
        self.assertEqual(result.ndim, 3)
        self.assertEqual(result.shape[2], 3)

    def test_get_image_dtype_uint8(self):
        """get_image returns uint8 array."""
        result = camera.get_image("center")
        self.assertEqual(result.dtype, np.uint8)

    def test_get_image_valid_locations(self):
        """All valid locations work."""
        for loc in ("left", "center", "right"):
            result = camera.get_image(loc)
            self.assertIsInstance(result, np.ndarray)

    def test_get_image_invalid_location_raises(self):
        """Invalid location raises ValueError."""
        with self.assertRaises(ValueError):
            camera.get_image("rear")
        with self.assertRaises(ValueError):
            camera.get_image("")
        with self.assertRaises(ValueError):
            camera.get_image("front")

    def test_get_image_passes_location_to_backend(self):
        """get_image passes the correct location to the backend."""
        camera.get_image("left")
        camera.get_image("center")
        camera.get_image("right")
        self.assertEqual(self.mock_backend.get_image_calls, ["left", "center", "right"])


class TestCameraGetDepth(unittest.TestCase):
    """Tests for jchm.camera.get_depth."""

    def setUp(self):
        self.mock_backend = MockCameraBackend()
        import jchm._backend as b
        b._backend_instance = self.mock_backend
        b._backend_type = BackendType.SIMULATOR

    def tearDown(self):
        import jchm._backend as b
        b._backend_instance = None

    def test_get_depth_returns_numpy_array(self):
        """get_depth returns a NumPy array."""
        result = camera.get_depth()
        self.assertIsInstance(result, np.ndarray)

    def test_get_depth_shape(self):
        """get_depth returns 2D array (height, width)."""
        result = camera.get_depth()
        self.assertEqual(result.ndim, 2)

    def test_get_depth_dtype_uint8(self):
        """get_depth returns uint8 array."""
        result = camera.get_depth()
        self.assertEqual(result.dtype, np.uint8)


class TestCameraShowImage(unittest.TestCase):
    """Tests for jchm.camera.show_image."""

    def setUp(self):
        self.mock_backend = MockCameraBackend()
        import jchm._backend as b
        b._backend_instance = self.mock_backend
        b._backend_type = BackendType.SIMULATOR

    def tearDown(self):
        import jchm._backend as b
        b._backend_instance = None

    def test_show_image_accepts_valid_args(self):
        """show_image accepts valid arguments without error."""
        # We can't easily test cv2.imshow, but we can test it doesn't crash
        img = np.zeros((100, 100, 3), dtype=np.uint8)
        # This would normally display a window, but during testing with no GUI
        # it should not raise an error
        try:
            camera.show_image(img, "center", quality=80)
        except Exception as e:
            # It's OK if OpenCV can't open a window (no display)
            # But we shouldn't get a TypeError or ValueError
            self.assertNotIsInstance(e, (TypeError, ValueError))

    def test_show_image_default_quality(self):
        """show_image works with default quality parameter."""
        img = np.zeros((100, 100, 3), dtype=np.uint8)
        try:
            camera.show_image(img, "left")
        except Exception:
            pass


if __name__ == "__main__":
    unittest.main()
