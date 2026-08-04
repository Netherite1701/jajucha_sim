"""User entry point for the Jajucha simulator (Step 11.12).

This file is YOUR file. Put your own perception and driving algorithm here.

Rules (see python/user/README.md):
  * Real driving code uses ``jchm`` only (real-compatible vehicle API).
  * Simulator lifecycle/testing tools live in ``jchm_sim`` and are NOT used
    for normal autonomous driving.
  * Never modify simulator internals to implement autonomous logic.
  * Always send ``speed=0`` on shutdown where practical.

The default loop below reads the center camera and drives straight at a low
speed so you can verify the full simulator workflow in one run:
    simulator running  ->  ``python python/user/main.py``  ->  Ctrl+C to stop
"""

import time

import jchm
from jchm.errors import JchmConnectionError, JchmError


def main() -> None:
    print("[user] Jajucha user program starting (Ctrl+C to stop).")
    try:
        while True:
            image = jchm.camera.get_image("center")

            # ------------------------------------------------------------
            # Replace this section with the user's own perception and
            # driving algorithm. This template deliberately assumes neither
            # an ANN nor an FSM architecture.
            # ------------------------------------------------------------
            left = 0
            right = 0
            speed = 3  # low, safe speed (JCHM units, [-30, 30])

            jchm.control.set_motor(left, right, speed)
            # ------------------------------------------------------------

            # Give the simulator a moment between frames (matches ~30 Hz loop).
            time.sleep(0.03)

    except KeyboardInterrupt:
        print("[user] Interrupted, stopping vehicle.")
    except JchmConnectionError as exc:
        print("[user] Simulator bridge is not reachable.")
        print("[user] Start Jajucha Simulator first, then run this script again.")
        print(f"[user] Details: {exc}")
    except JchmError as exc:
        print(f"[user] JCHM error: {exc}")
    finally:
        # Always stop propulsion where practical (manual-compatible shutdown).
        try:
            jchm.control.set_motor(0, 0, 0)
        except Exception as exc:  # noqa: BLE001 - best-effort stop
            print(f"[user] Could not send stop command: {exc}")
        print("[user] Jajucha user program stopped.")


if __name__ == "__main__":
    main()
