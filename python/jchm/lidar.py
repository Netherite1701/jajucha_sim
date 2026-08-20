"""Public JCHM-compatible lidar API for the simulator."""

from dataclasses import dataclass

import numpy as np

from ._backend import get_backend


@dataclass(frozen=True)
class LidarScan:
    """A single horizontal scan with its sensor geometry metadata."""

    distances_cm: np.ndarray
    frame_id: int
    simulation_tick: int
    simulation_time: float
    angle_min_deg: float
    angle_max_deg: float
    angle_increment_deg: float
    max_distance_cm: float

    @property
    def ray_count(self) -> int:
        return int(self.distances_cm.shape[0])

    @property
    def angles_deg(self) -> np.ndarray:
        return self.angle_min_deg + np.arange(self.ray_count, dtype=np.float32) * self.angle_increment_deg


def get_scan() -> np.ndarray:
    """Return one horizontal scan as float32 distances in centimetres."""
    return get_scan_with_metadata().distances_cm


def get_scan_with_metadata() -> LidarScan:
    """Return one scan together with frame, tick, angle and range metadata."""
    payload = get_backend().get_lidar()
    return LidarScan(
        distances_cm=payload["distances_cm"],
        frame_id=payload["frame_id"],
        simulation_tick=payload["simulation_tick"],
        simulation_time=payload["simulation_time"],
        angle_min_deg=payload["angle_min_deg"],
        angle_max_deg=payload["angle_max_deg"],
        angle_increment_deg=payload["angle_increment_deg"],
        max_distance_cm=payload["max_distance_cm"],
    )


def get_lidar():
    """Return ``(theta_array, dist_array)`` like the physical JCHM API.

    The manual uses degrees in the range ``[0, 360)`` and distances in
    millimetres (for example, ``500`` means 50 cm).  The simulator bridge
    keeps centimetres internally, so this compatibility function converts
    the distances at the public boundary.  ``get_scan_with_metadata`` remains
    available for simulator diagnostics that need centimetre units.
    """
    scan = get_scan_with_metadata()
    theta = scan.angles_deg.astype(np.float32, copy=False)
    if scan.angle_min_deg < 0:
        theta = np.mod(theta, 360.0).astype(np.float32)
    distances_mm = (scan.distances_cm * np.float32(10.0)).astype(np.float32, copy=False)
    return theta, distances_mm


def show_lidar(theta_array, dist_array, max_distance=1000):
    """Display a lightweight polar lidar plot for manual-compatible examples."""
    import cv2

    theta = np.asarray(theta_array, dtype=np.float32).reshape(-1)
    dist = np.asarray(dist_array, dtype=np.float32).reshape(-1)
    if theta.shape != dist.shape:
        raise ValueError("theta_array and dist_array must have the same shape")
    size = 500
    canvas = np.zeros((size, size, 3), dtype=np.uint8)
    center = (size // 2, size // 2)
    radius = size // 2 - 12
    scale = radius / max(float(max_distance), 1.0)
    cv2.circle(canvas, center, radius, (70, 70, 70), 1)
    for angle, distance in zip(theta, dist):
        if not np.isfinite(distance):
            continue
        r = min(float(distance), float(max_distance)) * scale
        rad = np.deg2rad(float(angle))
        x = int(center[0] + r * np.sin(rad))
        y = int(center[1] - r * np.cos(rad))
        cv2.circle(canvas, (x, y), 2, (0, 220, 80), -1)
    cv2.imshow("lidar", canvas)
    cv2.waitKey(1)
