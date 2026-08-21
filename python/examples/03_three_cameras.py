"""03_three_cameras.py - Left / center / right cameras (Step 11.15).

Displays all three Jajucha cameras side by side:

    left    center    right

The cameras are independent physical sensors. Their confirmed hardware
calibration is NOT assumed to be identical (the simulator uses documented
APPROXIMATE defaults per camera - see docs/MANUAL_COMPATIBILITY.md). This
example simply shows each frame as delivered.
"""

import cv2
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import jchm
from jchm.errors import JchmConnectionError, JchmError

CAMERAS = ("left", "center", "right")


def main() -> None:
    print("[camera] Fetching frames from left / center / right...")
    try:
        frames = {}
        for location in CAMERAS:
            frames[location] = jchm.camera.get_image(location)
            print(f"[camera] {location}: shape={frames[location].shape}")

        # Stack them horizontally for one window, and show each individually.
        combined = cv2.hconcat([frames[loc] for loc in CAMERAS])
        jchm.camera.show_image(combined, "left | center | right")
        for location in CAMERAS:
            jchm.camera.show_image(frames[location], location)

        print("[camera] Press any key in the OpenCV window, or Ctrl+C to stop.")
        while True:
            if cv2.waitKey(30) >= 0:
                break

    except KeyboardInterrupt:
        print("[camera] Interrupted.")
    except JchmConnectionError as exc:
        print("[camera] Simulator bridge is not reachable.")
        print("[camera] Start Jajucha Simulator first, then run this script again.")
        print(f"[camera] Details: {exc}")
    except JchmError as exc:
        print(f"[camera] JCHM error: {exc}")
    finally:
        cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
