"""Starter controller for the Jajucha simulator.

The controller loop is intentionally small and real-compatible::

    camera frame -> compute_command(frame) -> set_motor(...)

Replace :func:`compute_command` with your own perception and control logic.
Only the public ``jchm`` API should be used here; do not import ``jchm_sim``
for normal autonomous driving.

Run from the repository root while the simulator is in Drive mode::

    .\\.venv\\Scripts\\python.exe .\\python\\user\\main.py

Press Ctrl+C to stop safely.  A bounded smoke run is also available::

    python python/user/main.py --max-seconds 5 --speed 2
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
from pathlib import Path
import sys
import time
from typing import Any

# Make ``python python/user/main.py`` work without requiring PYTHONPATH.
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import jchm
from jchm.errors import JchmConnectionError, JchmError


LOOP_DELAY_SECONDS = 0.03
MAX_STEERING = 10
MAX_SPEED = 30


@dataclass(frozen=True)
class MotorCommand:
    """One safe-to-send vehicle command in JCHM units."""

    steering: int = 0
    speed: int = 3

    @property
    def left(self) -> int:
        return self.steering

    @property
    def right(self) -> int:
        return self.steering


def clamp(value: int, lower: int, upper: int) -> int:
    """Keep a command inside the public JCHM range."""

    return max(lower, min(upper, int(value)))


def compute_command(image: Any, *, cruise_speed: int = 3) -> MotorCommand:
    """Convert one camera frame into a motor command.

    This starter implementation drives straight at a low speed.  Replace the
    marked section with your algorithm, for example:

    1. crop a region of interest from ``image``;
    2. detect a lane or guide line;
    3. calculate a normalized left/right error;
    4. turn that error into a steering value in ``[-10, 10]``.

    ``image`` is a BGR NumPy array when returned by the simulator camera.
    """

    del image  # The starter controller does not use perception yet.

    # TODO: Replace this block with your perception and control algorithm.
    steering = 0
    speed = clamp(cruise_speed, 0, MAX_SPEED)
    return MotorCommand(
        steering=clamp(steering, -MAX_STEERING, MAX_STEERING),
        speed=speed,
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--speed",
        type=int,
        default=3,
        help="safe forward speed used by the starter controller (default: 3)",
    )
    parser.add_argument(
        "--max-seconds",
        type=float,
        default=0.0,
        help="stop automatically after this time; 0 means run until Ctrl+C",
    )
    return parser.parse_args()


def stop_vehicle() -> None:
    """Best-effort neutral command for every shutdown path."""

    try:
        jchm.control.set_motor(0, 0, 0)
    except Exception as exc:  # noqa: BLE001 - shutdown must not mask the cause
        print(f"[user] Could not send stop command: {exc}")


def main() -> None:
    args = parse_args()
    if args.max_seconds < 0:
        raise ValueError("--max-seconds must be zero or positive")

    speed = clamp(args.speed, 0, MAX_SPEED)
    deadline = time.monotonic() + args.max_seconds if args.max_seconds else None
    print("[user] Controller starting (Ctrl+C to stop).")
    print(f"[user] Starter mode: straight drive, speed={speed}")

    try:
        while deadline is None or time.monotonic() < deadline:
            image = jchm.camera.get_image("center")
            command = compute_command(image, cruise_speed=speed)
            jchm.control.set_motor(command.left, command.right, command.speed)
            time.sleep(LOOP_DELAY_SECONDS)

    except KeyboardInterrupt:
        print("[user] Interrupted.")
    except JchmConnectionError as exc:
        print("[user] Simulator bridge is not reachable.")
        print("[user] Start Jajucha Simulator first, then run this script again.")
        print(f"[user] Details: {exc}")
    except JchmError as exc:
        print(f"[user] JCHM error: {exc}")
    finally:
        stop_vehicle()
        print("[user] Controller stopped.")


if __name__ == "__main__":
    main()
