"""01_motor_test.py - Motor command demonstration (Step 11.13).

Demonstrates the manual-defined ``jchm.control.set_motor(left, right, speed)``
semantics:

    left   : front-left steering command, [-10, 10] (negative = left turn)
    right  : front-right steering command, [-10, 10]
    speed  : rear drive speed, [-30, 30] (negative = reverse, 0 = stop)

Run with the simulator active in Drive mode:

    python python/examples/01_motor_test.py

Each step runs for a short time then returns to a full stop.
"""

import time
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import jchm
from jchm.errors import JchmConnectionError, JchmError

# Seconds to hold each command so the motion is observable.
STEP_SECONDS = 1.0
COMMAND_REFRESH_SECONDS = 0.1


def step(label: str, left: int, right: int, speed: int, duration: float = STEP_SECONDS) -> None:
    """Keep refreshing one command so the bridge watchdog cannot cancel it."""
    print(
        f"[motor] {label:28s} set_motor({left:>3}, {right:>3}, {speed:>3})",
        flush=True,
    )
    deadline = time.monotonic() + duration
    while True:
        jchm.control.set_motor(left, right, speed)
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            break
        time.sleep(min(COMMAND_REFRESH_SECONDS, remaining))


def main() -> None:
    print("[motor] Motor test starting - watch the vehicle in the viewport.", flush=True)
    try:
        # 1. Stop (speed zero: no propulsion, no steering).
        step("stop", 0, 0, 0)
        print("1")
        # 2. Forward.
        step("forward", 0, 0, 10)
        print("2")
        # 3. Reverse.
        step("reverse", 0, 0, -10)
        print("3")
        # 4. Left steering while moving forward.
        step("left steering", -10, -10, 8)
        print("4")
        # 5. Right steering while moving forward.
        step("right steering", 10, 10, 8)
        print("5")
        # 6. Independent wheel steering (front wheels oppose).
        step("independent steering", -10, 10, 6)
        print("6")
        # 7. Speed zero with steering (steering works, no propulsion).
        step("speed zero + steering", -10, 10, 0, duration=0.5)
        print("7")
        print("[motor] Motor test complete.")

    except KeyboardInterrupt:
        print("[motor] Interrupted.")
    except JchmConnectionError as exc:
        print("[motor] Simulator bridge is not reachable.")
        print("[motor] Start Jajucha Simulator first, then run this script again.")
        print(f"[motor] Details: {exc}")
    except JchmError as exc:
        print(f"[motor] JCHM error: {exc}")
    finally:
        # Always stop where practical (manual-compatible shutdown).
        try:
            jchm.control.set_motor(0, 0, 0)
        except Exception as exc:  # noqa: BLE001
            print(f"[motor] Could not send stop command: {exc}")
        print("[motor] Stopped.")


if __name__ == "__main__":
    main()
