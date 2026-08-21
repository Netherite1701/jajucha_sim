"""08_camera_lane_drive.py - gridFront 기반 카메라 자율주행 예제.

실제 차량에서 사용하는 다음 호출 형식을 그대로 사용한다.

    image = jchm.camera.get_image('center')
    (V, L, R), grid = jchm.camera.gridFront(image)

Ctrl+C를 누르면 모터를 정지하고 종료한다.
"""

from __future__ import annotations

import argparse
from pathlib import Path
import sys
import time

import cv2

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import jchm
from jchm.errors import JchmConnectionError, JchmError


LOOP_DELAY_SECONDS = 0.03
SIDE_TOLERANCE = 3
CORNER_THRESHOLD = 150
CORNER_HOLD_FRAMES = 45
OPPOSITE_TURN_LOCK_FRAMES = 100


def side_direction(left: int, right: int) -> int:
    """Return -1 for left, +1 for right, or 0 inside the dead band."""

    if right + SIDE_TOLERANCE < left:
        return -1
    if left + SIDE_TOLERANCE < right:
        return 1
    return 0


def command_from_grid(V: list[int], L: list[int], R: list[int]) -> tuple[int, int, str]:
    """Convert the real JCHM gridFront distances into a motor command."""

    if len(V) < 5 or len(L) < 3 or len(R) < 3:
        raise ValueError("gridFront must return at least V[0:5], L[0:3], R[0:3]")

    front = V[3]
    left = L[2]
    right = R[2]
    direction = side_direction(left, right)
    corner_average = (V[2] + V[3] + V[4]) / 3.0

    # 충돌 직전: 제공된 코드와 같이 후진해 공간을 만든다.
    if front <= 5:
        if direction < 0:
            return -8, -3, "collision-reverse-left"
        if direction > 0:
            return 8, -3, "collision-reverse-right"
        return 0, -5, "collision-reverse"

    # 원본에서는 V[3] 범위 분기 뒤에 있어 실행될 수 없었던 조건이다.
    # 먼저 검사해 직각 회전 구간에서 실제로 동작하게 한다.
    if corner_average < CORNER_THRESHOLD:
        if direction < 0:
            return -10, 4, "corner-left"
        if direction > 0:
            return 10, 4, "corner-right"
        if front < 60:
            return 0, -3, "corner-reverse"
        return 0, 3, "corner-straight"

    # 약간 충돌 직전.
    if front < 60:
        if direction < 0:
            return -8, -3, "near-reverse-left"
        if direction > 0:
            return 8, -3, "near-reverse-right"
        return 0, -5, "near-reverse"

    # 단거리.
    if front < 100:
        if direction < 0:
            return -10, 4, "short-left"
        if direction > 0:
            return 10, 4, "short-right"
        return 0, 4, "short-straight"

    # 중거리.
    if front < 120:
        if direction < 0:
            return -10, 3, "middle-left"
        if direction > 0:
            return 10, 3, "middle-right"
        return 0, 5, "middle-straight"

    # 장거리. 실제 코드의 R[2] 임계값과 L/R 비교를 유지한다.
    if right < 120:
        return -10, 3, "long-left"
    if direction > 0:
        return 10, 3, "long-right"
    return 0, 7, "long-straight"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--speed",
        type=int,
        default=7,
        help="forward speed upper limit (default: 7)",
    )
    parser.add_argument(
        "--max-seconds",
        type=float,
        default=0.0,
        help="stop after this many seconds; 0 means run until interrupted",
    )
    parser.add_argument("--once", action="store_true", help="print one gridFront result without driving")
    parser.add_argument("--no-display", action="store_true", help="do not show the gridFront preview")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    speed_limit = max(0, min(args.speed, 30))
    started = time.monotonic()
    frame_count = 0
    corner_hold_remaining = 0
    corner_hold_steering = 0
    opposite_turn_lock_remaining = 0
    last_corner_steering = 0

    print(f"[camera-drive] gridFront driving start (speed limit={speed_limit})", flush=True)
    try:
        while True:
            image = jchm.camera.get_image('center')
            (V, L, R), grid = jchm.camera.gridFront(image)
            steering, speed, mode = command_from_grid(V, L, R)
            frame_count += 1

            # A sharp turn can disappear from the sampling grid as soon as
            # the nose starts rotating. Keep maximum steering briefly so the
            # vehicle commits to the corner instead of going straight again.
            if mode in ("corner-left", "corner-right"):
                is_early_opposite = (
                    opposite_turn_lock_remaining > 0
                    and last_corner_steering != 0
                    and steering * last_corner_steering < 0
                )
                if is_early_opposite:
                    steering = -4 if steering < 0 else 4
                    speed = 3
                    mode = "corner-correction-left" if steering < 0 else "corner-correction-right"
                else:
                    corner_hold_steering = steering
                    corner_hold_remaining = CORNER_HOLD_FRAMES
                    opposite_turn_lock_remaining = OPPOSITE_TURN_LOCK_FRAMES
                    last_corner_steering = steering
            elif corner_hold_remaining > 0 and speed > 0:
                steering = corner_hold_steering
                speed = 4
                mode = "corner-hold-left" if steering < 0 else "corner-hold-right"
                corner_hold_remaining -= 1
            elif (
                opposite_turn_lock_remaining > 0
                and last_corner_steering != 0
                and steering * last_corner_steering < 0
                and speed > 0
            ):
                steering = -4 if steering < 0 else 4
                speed = min(speed, 3)
                mode = "turn-correction-left" if steering < 0 else "turn-correction-right"

            if opposite_turn_lock_remaining > 0:
                opposite_turn_lock_remaining -= 1

            if args.once:
                print(f"[camera-drive] V={V}", flush=True)
                print(f"[camera-drive] L={L}", flush=True)
                print(f"[camera-drive] R={R}", flush=True)
                print(
                    f"[camera-drive] command steer={steering:+d} speed={speed:+d} mode={mode}",
                    flush=True,
                )
                return

            if speed > 0:
                speed = min(speed, speed_limit)
            jchm.control.set_motor(steering, steering, speed)

            if not args.no_display:
                cv2.putText(
                    grid,
                    f"V3={V[3]} L2={L[2]} R2={R[2]} {mode}",
                    (10, 30),
                    cv2.FONT_HERSHEY_SIMPLEX,
                    0.65,
                    (0, 255, 255),
                    2,
                )
                jchm.camera.show_image(grid, "camera-grid-drive")

            if frame_count == 1 or frame_count % 5 == 0:
                print(
                    f"[camera-drive] frame={frame_count} V3={V[3]:3d} "
                    f"L2={L[2]:3d} R2={R[2]:3d} mode={mode} "
                    f"steer={steering:+d} speed={speed:+d}",
                    flush=True,
                )

            if args.max_seconds > 0 and time.monotonic() - started >= args.max_seconds:
                print(f"[camera-drive] Test duration reached ({args.max_seconds:.1f}s)", flush=True)
                return

            time.sleep(LOOP_DELAY_SECONDS)

    except KeyboardInterrupt:
        print("[camera-drive] Interrupted; stopping vehicle.", flush=True)
    except JchmConnectionError as exc:
        print("[camera-drive] Simulator bridge is not reachable.", flush=True)
        print(f"[camera-drive] Details: {exc}", flush=True)
    except JchmError as exc:
        print(f"[camera-drive] JCHM error: {exc}", flush=True)
    finally:
        try:
            jchm.control.stop_motor()
        except Exception as exc:  # noqa: BLE001 - best-effort safety stop
            print(f"[camera-drive] Could not send stop command: {exc}", flush=True)
        if not args.no_display:
            cv2.destroyAllWindows()
        print("[camera-drive] Stopped.", flush=True)


if __name__ == "__main__":
    main()
