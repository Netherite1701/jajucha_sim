"""05_drive_and_view.py - Combined driving + viewing example (Step 11.17).

Demonstrates the normal user workflow:

    * read the center camera
    * display the camera image
    * send a safe low-speed motor command
    * stop when interrupted (Ctrl+C)

This is a usage example only - it is not an ANN or FSM implementation.
"""

import time

import cv2

import jchm
from jchm.errors import JchmConnectionError, JchmError


def main() -> None:
    print("[drive] Driving at low speed while showing the center camera.")
    print("[drive] Press Ctrl+C to stop.")
    try:
        while True:
            image = jchm.camera.get_image("center")
            jchm.camera.show_image(image, "center")

            # Safe low-speed forward command (steering straight).
            jchm.control.set_motor(0, 0, 5)

            if cv2.waitKey(30) >= 0:
                break
            time.sleep(0.03)

    except KeyboardInterrupt:
        print("[drive] Interrupted.")
    except JchmConnectionError as exc:
        print("[drive] Simulator bridge is not reachable.")
        print("[drive] Start Jajucha Simulator first, then run this script again.")
        print(f"[drive] Details: {exc}")
    except JchmError as exc:
        print(f"[drive] JCHM error: {exc}")
    finally:
        try:
            jchm.control.set_motor(0, 0, 0)
        except Exception as exc:  # noqa: BLE001
            print(f"[drive] Could not send stop command: {exc}")
        cv2.destroyAllWindows()
        print("[drive] Stopped.")


if __name__ == "__main__":
    main()
