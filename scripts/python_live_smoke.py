"""Exercise the public Python JCHM API against a live Windows build.

This intentionally uses the same calls shown in the supplied manual:
three camera frames, center depth, ``jchm.lidar.get_lidar()``, and motor/
simulation controls.  The JSON result is an auditable bridge/runtime record.
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import time
from pathlib import Path

import cv2
import numpy as np

# Allow the checked-in client to run directly from a source checkout.
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "python"))
import jchm
from jchm._sim_backend import SimulatorBackend
import jchm._backend as backend_module
from jchm._backend import BackendType


def _payload(response: dict) -> dict:
    if not response.get("ok", False):
        raise RuntimeError(response)
    return response.get("payload", {})


def _status_pose(response: dict) -> dict:
    payload = _payload(response)
    return payload.get("vehicle", {}).get("position_cm", {})


def _save_lidar_plot(theta: np.ndarray, distance_mm: np.ndarray, path: Path) -> None:
    size = 600
    canvas = np.zeros((size, size, 3), dtype=np.uint8)
    center = np.array([size // 2, size // 2], dtype=np.int32)
    radius = size // 2 - 20
    cv2.circle(canvas, tuple(center), radius, (80, 80, 80), 1)
    cv2.line(canvas, (center[0], center[1] - radius), (center[0], center[1] + radius), (40, 60, 70), 1)
    cv2.line(canvas, (center[0] - radius, center[1]), (center[0] + radius, center[1]), (40, 60, 70), 1)
    # The public API returns millimetres.  Use the observed scan range for
    # this evidence plot; otherwise the manual helper's 1 m default clips a
    # 2–10 m simulator scan into an uninformative ring at the border.
    finite = distance_mm[np.isfinite(distance_mm)]
    max_range_mm = max(float(np.max(finite)) if finite.size else 1000.0, 1000.0)
    scale = radius / max_range_mm
    for angle, distance in zip(theta, distance_mm):
        if not np.isfinite(distance):
            continue
        r = min(float(distance), max_range_mm) * scale
        rad = np.deg2rad(float(angle))
        x = int(center[0] + r * np.sin(rad))
        y = int(center[1] - r * np.cos(rad))
        if 0 <= x < size and 0 <= y < size:
            canvas[y, x] = (60, 240, 120)
    cv2.imwrite(str(path), canvas)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", default="test-artifacts/python")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--settle", type=float, default=0.45)
    args = parser.parse_args()

    out = Path(args.output_dir)
    out.mkdir(parents=True, exist_ok=True)
    backend = SimulatorBackend(args.host, args.port)
    # Route the public jchm.camera/control/lidar modules through this exact
    # connection so the smoke test does not accidentally create a second
    # single-client bridge connection.
    backend_module._backend_instance = backend
    backend_module._backend_type = BackendType.SIMULATOR
    checks: dict[str, object] = {}
    result: dict[str, object] = {"passed": False, "timestamp": time.time(), "checks": checks}
    try:
        status0 = backend.get_status()
        checks["handshake_and_status"] = bool(status0.get("ok"))
        # The standalone is auto-started by the authoritative scene.  Keep the
        # connection on the same session while fetching sensor frames; the
        # explicit pause/step/reset commands below exercise lifecycle control.

        frames: dict[str, dict] = {}
        for location in ("left", "center", "right"):
            image = jchm.camera.get_image(location)
            if image.ndim != 3 or image.shape[2] != 3 or image.dtype != np.uint8:
                raise AssertionError(f"invalid {location} frame: {image.shape} {image.dtype}")
            image_path = out / f"{location}_camera_python.png"
            cv2.imwrite(str(image_path), image)
            frames[location] = {"shape": list(image.shape), "dtype": str(image.dtype), "path": str(image_path)}
        depth = jchm.camera.get_depth()
        if depth.ndim != 2 or depth.dtype != np.uint8:
            raise AssertionError(f"invalid depth frame: {depth.shape} {depth.dtype}")
        depth_path = out / "center_depth_python.png"
        cv2.imwrite(str(depth_path), depth)
        result["cameras"] = frames
        result["depth"] = {"shape": list(depth.shape), "dtype": str(depth.dtype), "path": str(depth_path)}
        checks["three_cameras"] = len(frames) == 3 and all(np.prod(v["shape"]) > 0 for v in frames.values())
        checks["depth"] = int(depth.size) > 0 and bool(np.any(depth))

        theta, distance_mm = jchm.lidar.get_lidar()
        if theta.ndim != 1 or distance_mm.ndim != 1 or theta.shape != distance_mm.shape:
            raise AssertionError(f"invalid lidar arrays: {theta.shape} {distance_mm.shape}")
        if theta.size < 300 or not np.isfinite(theta).all() or not np.isfinite(distance_mm).all():
            raise AssertionError("invalid lidar data")
        lidar_path = out / "lidar_python.png"
        _save_lidar_plot(theta, distance_mm, lidar_path)
        result["lidar"] = {
            "ray_count": int(theta.size),
            "angle_min_deg": float(theta.min()),
            "angle_max_deg": float(theta.max()),
            "distance_min_mm": float(distance_mm.min()),
            "distance_max_mm": float(distance_mm.max()),
            "path": str(lidar_path),
        }
        checks["lidar_manual_contract"] = bool(theta.min() >= 0 and theta.max() < 360 and distance_mm.min() > 0)

        pose0 = _status_pose(status0)
        jchm.control.set_motor(8, 8, 8)
        time.sleep(args.settle)
        status1 = backend.get_status()
        pose1 = _status_pose(status1)
        distance = float(np.hypot(float(pose1.get("x", 0)) - float(pose0.get("x", 0)), float(pose1.get("z", 0)) - float(pose0.get("z", 0))))
        checks["motor_changes_pose"] = distance > 0.01
        checks["motor_displacement_cm"] = distance

        jchm.control.stop_motor()
        stopped = backend.get_status()
        stopped_command = _payload(stopped).get("vehicle", {}).get("command", {})
        checks["stop_motor_zeroes_command"] = (
            stopped_command.get("left") == 0
            and stopped_command.get("right") == 0
            and stopped_command.get("speed") == 0
        )

        backend.sim_pause()
        paused0 = backend.get_status()
        time.sleep(0.25)
        paused1 = backend.get_status()
        p0 = _payload(paused0)
        p1 = _payload(paused1)
        checks["pause_freezes_tick"] = p0.get("tick") == p1.get("tick")
        backend.sim_step()
        stepped = backend.get_status()
        checks["step_exactly_one_tick"] = _payload(stepped).get("tick") == p0.get("tick", 0) + 1
        backend.sim_reset()
        reset = backend.get_status()
        reset_payload = _payload(reset)
        checks["reset_zeroes_tick_and_speed"] = reset_payload.get("tick") == 0 and reset_payload.get("vehicle", {}).get("command", {}).get("speed") == 0
        reset_pose = reset_payload.get("vehicle", {}).get("position_cm", {})
        checks["reset_returns_course_start"] = (
            abs(float(reset_pose.get("x", 0)) - float(pose0.get("x", 0))) <= 5.0
            and abs(float(reset_pose.get("z", 0)) - float(pose0.get("z", 0))) <= 5.0
        )
        result["status"] = {"initial": _payload(status0), "after_motor": _payload(status1), "after_reset": reset_payload}
        result["passed"] = all(bool(value) for key, value in checks.items() if key not in ("motor_displacement_cm",))
        return 0 if result["passed"] else 2
    finally:
        try:
            jchm.control.stop_motor()
            backend.disconnect()
        except Exception:
            pass
        path = out / f"python_live_smoke_{time.strftime('%Y%m%d_%H%M%S')}.json"
        path.write_text(json.dumps(result, ensure_ascii=False, indent=2, default=str), encoding="utf-8")
        print(path)


if __name__ == "__main__":
    raise SystemExit(main())
