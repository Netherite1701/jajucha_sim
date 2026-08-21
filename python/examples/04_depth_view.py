"""04_depth_view.py - Depth image example (Step 11.16).

Reads the manual-compatible depth image from the center camera:

    depth = jchm.camera.get_depth()

The result is a grayscale uint8 array: brighter pixels = nearer objects,
darker pixels = farther objects. This is a normalized grayscale view, NOT
metric depth unless an explicitly simulator-only metric API is used.
"""

import cv2
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import jchm
from jchm.errors import JchmConnectionError, JchmError


def main() -> None:
    print("[depth] Fetching depth image from the center camera...")
    try:
        depth = jchm.camera.get_depth()
        print(f"[depth] Shape: {depth.shape}, dtype: {depth.dtype} "
              f"(near=bright, far=dark)")

        # Already grayscale uint8; display as-is.
        jchm.camera.show_image(depth, "depth")

        print("[depth] Press any key in the OpenCV window, or Ctrl+C to stop.")
        while True:
            if cv2.waitKey(30) >= 0:
                break

    except KeyboardInterrupt:
        print("[depth] Interrupted.")
    except JchmConnectionError as exc:
        print("[depth] Simulator bridge is not reachable.")
        print("[depth] Start Jajucha Simulator first, then run this script again.")
        print(f"[depth] Details: {exc}")
    except JchmError as exc:
        print(f"[depth] JCHM error: {exc}")
    finally:
        cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
