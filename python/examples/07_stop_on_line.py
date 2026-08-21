"""07_stop_on_line.py - Stop at a bright transverse line.

This example uses only the manual-compatible public APIs already present in
this project:

    jchm.camera.get_image("center")
    jchm.control.set_motor(left, right, speed)

It detects a bright, mostly horizontal line in the lower center of the
camera image.  The line's ground distance is estimated from a small camera
calibration block near the top of this file.  The simulator's camera height
and mount angle are approximate because those hardware values are not
specified in the manual compatibility notes; tune them for the real car.

Run with the simulator active in Drive mode:

    python python/examples/07_stop_on_line.py

For a one-frame camera/algorithm check without opening a window:

    python python/examples/07_stop_on_line.py --once --no-display
"""

from __future__ import annotations

import argparse
import math
from pathlib import Path
import sys
import time
from dataclasses import dataclass
from typing import Optional

import cv2
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import jchm
from jchm.errors import JchmConnectionError, JchmError


# Manual-compatible motor limits are [-10, 10] for steering and [-30, 30]
# for speed.  These defaults are deliberately conservative.
DRIVE_SPEED = 3
LEFT_STEERING = 0
RIGHT_STEERING = 0

# Camera geometry is configurable because the manual compatibility record
# marks camera mount height/angle as unknown and FOV as approximate.
CAMERA_HEIGHT_CM = 24.0
CAMERA_PITCH_DEG = 0.0
VERTICAL_FOV_DEG = 46.8  # derived from the documented approximate 60-degree HFOV

# A white stop line is expected in the lower-center image region.
MIN_BRIGHT_VALUE = 150
MAX_WHITE_SATURATION = 100
ROI_TOP_RATIO = 0.45
ROI_BOTTOM_RATIO = 0.92
ROI_LEFT_RATIO = 0.12
ROI_RIGHT_RATIO = 0.88
MIN_LINE_FILL_RATIO = 0.18
MIN_LINE_WIDTH_RATIO = 0.35


@dataclass(frozen=True)
class StopLine:
    """Detected line location and its estimated forward ground distance."""

    row: int
    distance_cm: float
    fill_ratio: float


def estimate_ground_distance_cm(
    row: float,
    image_height: int,
    *,
    camera_height_cm: float = CAMERA_HEIGHT_CM,
    camera_pitch_deg: float = CAMERA_PITCH_DEG,
    vertical_fov_deg: float = VERTICAL_FOV_DEG,
) -> float:
    """Estimate distance to a ground point at ``row`` in the image.

    ``camera_pitch_deg`` is positive when the camera points down.  Rows below
    the image centre therefore correspond to larger downward ray angles.
    """

    if image_height <= 0 or camera_height_cm <= 0:
        return math.inf

    ray_angle_deg = camera_pitch_deg + (row - image_height / 2.0) * (
        vertical_fov_deg / image_height
    )
    if ray_angle_deg <= 0.0:
        return math.inf
    return camera_height_cm / math.tan(math.radians(ray_angle_deg))


def detect_stop_line(image: np.ndarray) -> Optional[StopLine]:
    """Find a bright transverse line in the lower-center camera image."""

    if image.ndim != 3 or image.shape[2] != 3:
        raise ValueError("Expected a BGR image with shape (height, width, 3)")

    height, width = image.shape[:2]
    top = max(0, int(height * ROI_TOP_RATIO))
    bottom = min(height, int(height * ROI_BOTTOM_RATIO))
    left = max(0, int(width * ROI_LEFT_RATIO))
    right = min(width, int(width * ROI_RIGHT_RATIO))
    if top >= bottom or left >= right:
        return None

    roi = image[top:bottom, left:right]
    hsv = cv2.cvtColor(roi, cv2.COLOR_BGR2HSV)
    white = cv2.inRange(
        hsv,
        np.array([0, 0, MIN_BRIGHT_VALUE], dtype=np.uint8),
        np.array([180, MAX_WHITE_SATURATION, 255], dtype=np.uint8),
    )

    # Close small gaps so a painted line or a dashed line produces continuous
    # evidence across several neighbouring rows.
    kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (9, 3))
    white = cv2.morphologyEx(white, cv2.MORPH_CLOSE, kernel)

    row_counts = np.count_nonzero(white, axis=1)
    if row_counts.size == 0:
        return None

    # Score a five-pixel band instead of one row to tolerate anti-aliasing.
    band_height = min(5, row_counts.size)
    band_scores = np.convolve(row_counts.astype(np.float32), np.ones(band_height), mode="valid")
    best_start = int(np.argmax(band_scores))
    best_end = min(row_counts.size, best_start + band_height)
    best_count = int(np.max(row_counts[best_start:best_end]))
    fill_ratio = float(band_scores[best_start] / (band_height * roi.shape[1]))

    if fill_ratio < MIN_LINE_FILL_RATIO:
        return None
    if best_count < int(roi.shape[1] * MIN_LINE_WIDTH_RATIO):
        return None

    row = top + best_start + band_height // 2
    distance_cm = estimate_ground_distance_cm(row, height)
    return StopLine(row=row, distance_cm=distance_cm, fill_ratio=fill_ratio)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--stop-distance-cm",
        type=float,
        default=120.0,
        help="stop when the estimated line distance is at or below this value",
    )
    parser.add_argument(
        "--speed",
        type=int,
        default=DRIVE_SPEED,
        help="forward JCHM speed command while searching (default: 3)",
    )
    parser.add_argument("--once", action="store_true", help="read and classify one frame, then exit")
    parser.add_argument("--no-display", action="store_true", help="do not open the OpenCV preview window")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    if args.stop_distance_cm <= 0:
        raise ValueError("--stop-distance-cm must be positive")

    print(
        "[line-stop] Looking for a bright transverse line; "
        f"stop distance={args.stop_distance_cm:.1f} cm"
    )
    try:
        while True:
            image = jchm.camera.get_image("center")
            line = detect_stop_line(image)

            if not args.no_display:
                preview = image.copy()
                if line is not None:
                    cv2.line(preview, (0, line.row), (preview.shape[1] - 1, line.row), (0, 0, 255), 2)
                    label = f"line {line.distance_cm:.0f} cm"
                    cv2.putText(preview, label, (12, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 0, 255), 2)
                jchm.camera.show_image(preview, "line-stop")

            if line is not None:
                print(
                    f"[line-stop] line row={line.row}, "
                    f"estimated_distance={line.distance_cm:.1f} cm, "
                    f"fill={line.fill_ratio:.2f}"
                )

            if args.once:
                if line is None:
                    print("[line-stop] no line detected in this frame")
                else:
                    print("[line-stop] one-frame check complete")
                return

            if line is not None and line.distance_cm <= args.stop_distance_cm:
                jchm.control.stop_motor()
                print("[line-stop] STOP: line reached the configured distance")
                return

            jchm.control.set_motor(LEFT_STEERING, RIGHT_STEERING, args.speed)
            time.sleep(0.03)

    except KeyboardInterrupt:
        print("[line-stop] Interrupted; stopping vehicle.")
    except JchmConnectionError as exc:
        print("[line-stop] Simulator bridge is not reachable.")
        print(f"[line-stop] Details: {exc}")
    except JchmError as exc:
        print(f"[line-stop] JCHM error: {exc}")
    finally:
        try:
            jchm.control.stop_motor()
        except Exception as exc:  # noqa: BLE001 - best-effort safety stop
            print(f"[line-stop] Could not send stop command: {exc}")
        if not args.no_display:
            cv2.destroyAllWindows()
        print("[line-stop] Stopped.")


if __name__ == "__main__":
    main()
