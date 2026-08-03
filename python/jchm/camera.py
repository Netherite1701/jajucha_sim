"""
Public JCHM camera API.

This is the module that student code imports directly:

    import jchm
    image = jchm.camera.get_image("center")
    depth = jchm.camera.get_depth()
    jchm.camera.show_image(image, "center")

All functions delegate to the selected backend (real vehicle or simulator).
"""

from typing import Optional

import numpy as np

from ._backend import get_backend


def get_image(location: str) -> np.ndarray:
    """
    Get the latest camera frame from the specified camera.

    Args:
        location: Camera name, one of 'left', 'center', 'right'.

    Returns:
        A NumPy array of shape (height, width, 3) with dtype=uint8 in BGR order,
        ready for use with OpenCV.

    Raises:
        ValueError: If location is not one of 'left', 'center', 'right'.
        ConnectionError: If the simulator is not connected.
    """
    location = location.strip().lower()
    if location not in ("left", "center", "right"):
        raise ValueError(
            f"Camera location must be one of: 'left', 'center', 'right'. Got '{location}'."
        )

    backend = get_backend()
    return backend.get_image(location)


def get_depth() -> np.ndarray:
    """
    Get the latest depth image from the center camera.

    Returns:
        A NumPy array of shape (height, width) with dtype=uint8.
        Brighter pixels = nearer objects, darker pixels = farther objects.

    Raises:
        ConnectionError: If the simulator is not connected.
    """
    backend = get_backend()
    return backend.get_depth()


def show_image(img: np.ndarray, location: str = "center", quality: int = 80):
    """
    Display an image in a window.

    This is a local display function that uses OpenCV's imshow internally.
    The 'quality' parameter is accepted for API compatibility with the real
    Jajucha runtime, but has no effect in the simulator (OpenCV display
    does not use JPEG compression).

    Args:
        img: NumPy array image to display.
        location: Window name/location identifier (e.g. 'center', 'left', 'right', 'depth', 'lidar').
        quality: JPEG quality (ignored in local display mode).

    Note:
        On the real Jajucha vehicle, this sends the image to the vehicle's
        display system. In the simulator, it creates a local OpenCV window.
    """
    import cv2

    cv2.imshow(location, img)
    cv2.waitKey(1)
