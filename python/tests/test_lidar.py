"""Manual-compatible lidar API tests."""

import os
import sys
import unittest

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from jchm import lidar
from jchm._backend import BackendType


class MockLidarBackend:
    def __init__(self):
        self.calls = 0

    def get_lidar(self):
        self.calls += 1
        return {
            "distances_cm": np.linspace(10.0, 100.0, 4, dtype=np.float32),
            "frame_id": self.calls,
            "simulation_tick": self.calls * 2,
            "simulation_time": self.calls * 0.02,
            "ray_count": 4,
            "angle_min_deg": 0.0,
            "angle_max_deg": 270.0,
            "angle_increment_deg": 90.0,
            "max_distance_cm": 1000.0,
        }


class TestLidar(unittest.TestCase):
    def setUp(self):
        self.backend = MockLidarBackend()
        import jchm._backend as backend
        self.backend_module = backend
        backend._backend_instance = self.backend
        backend._backend_type = BackendType.SIMULATOR

    def tearDown(self):
        self.backend_module._backend_instance = None

    def test_manual_tuple_api_uses_degrees_and_millimetres(self):
        theta, distance = lidar.get_lidar()
        np.testing.assert_allclose(theta, [0.0, 90.0, 180.0, 270.0])
        np.testing.assert_allclose(distance, [100.0, 400.0, 700.0, 1000.0])
        self.assertEqual(theta.dtype, np.float32)
        self.assertEqual(distance.dtype, np.float32)

    def test_scan_metadata_preserves_centimetres(self):
        scan = lidar.get_scan_with_metadata()
        self.assertEqual(scan.ray_count, 4)
        self.assertAlmostEqual(float(scan.distances_cm[0]), 10.0)
        np.testing.assert_allclose(scan.angles_deg, [0.0, 90.0, 180.0, 270.0])

    def test_manual_show_lidar_rejects_mismatched_arrays(self):
        with self.assertRaises(ValueError):
            lidar.show_lidar(np.zeros(2), np.zeros(3))


if __name__ == "__main__":
    unittest.main()
