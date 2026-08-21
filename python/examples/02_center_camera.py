"""02_center_camera.py - Center camera example (Step 11.14).

Reads one frame from the center camera and demonstrates OpenCV-compatible
image processing:

    image = jchm.camera.get_image("center")
    jchm.camera.show_image(image, "center")

The returned frame is a NumPy array of shape (height, width, 3), dtype=uint8,
in BGR order - ready for OpenCV. Close the OpenCV window or press Ctrl+C to
stop.
"""

import cv2
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import jchm
from jchm.errors import JchmConnectionError, JchmError


def main() -> None:
    print("[camera] Fetching a center-camera frame...")
    try:
        image = jchm.camera.get_image("center")
        print(f"[camera] Frame shape: {image.shape}, dtype: {image.dtype}")

        # OpenCV-compatible processing: convert to grayscale and threshold.
        gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
        _, binary = cv2.threshold(gray, 127, 255, cv2.THRESH_BINARY)

        jchm.camera.show_image(image, "center")
        jchm.camera.show_image(binary, "center_binary")

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
