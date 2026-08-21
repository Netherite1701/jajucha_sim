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

import cv2
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
    cv2.imshow(location, img)
    cv2.waitKey(1)


def canny(img: np.ndarray, par1: int = 200, par2: int = 400) -> np.ndarray:
    """Return the edge image used by the real JCHM ``gridFront`` helper."""

    lightness = cv2.cvtColor(img, cv2.COLOR_BGR2HLS)[:, :, 1]
    blurred = cv2.bilateralFilter(lightness, 7, 10, 20)
    return cv2.Canny(blurred, par1, par2)


def drawGrid(
    img: np.ndarray,
    v_bounds: list[int],
    u_bounds: list[int],
    u_max: int,
    v_max: int,
    c_v: int,
    c_u: int,
    v_line_color: tuple[int, int, int],
    h_line_color: tuple[int, int, int],
) -> np.ndarray:
    """Draw the sampling grid, matching the public real-vehicle helper."""

    del c_u  # Kept in the signature for real JCHM API compatibility.
    for v_bound in v_bounds:
        cv2.line(img, (0, v_bound), (u_max, v_bound), h_line_color, 2)
    for u_bound in u_bounds:
        cv2.line(img, (u_bound, c_v), (u_bound, v_max), v_line_color, 2)
    return img


def findGrid(
    img: np.ndarray,
    img2: np.ndarray,
    cols: int,
    rows: int,
    v_max: int,
    u_max: int,
    ths1: int,
    ths2: int,
    v_line_color: tuple[int, int, int],
    h_line_color: tuple[int, int, int],
    v_point_color: tuple[int, int, int],
    u_point_color: tuple[int, int, int],
    y_max: int,
) -> tuple[tuple[list[int], list[int], list[int]], np.ndarray]:
    """Measure edge distances on the same grid as real JCHM 2.0.2."""

    vertical: list[int] = []
    left_distances: list[int] = []
    right_distances: list[int] = []
    edge = canny(img, ths1, ths2)
    c_v, c_u = v_max // 2, u_max // 2
    c_v = 400 - y_max
    v_bounds = [int(c_v + (v_max - c_v) * i / (rows + 1)) for i in range(1, rows + 1)]
    u_bounds = [int(u_max * i / (cols + 1)) for i in range(1, cols + 1)]

    img2 = drawGrid(
        img2,
        v_bounds,
        u_bounds,
        u_max,
        v_max,
        c_v,
        c_u,
        v_line_color,
        h_line_color,
    )

    for u_bound in u_bounds:
        y_values, = np.nonzero(edge[:, u_bound])
        y_values = y_values[y_values >= c_v]
        if len(y_values):
            edge_y = int(np.max(y_values))
            vertical.append(v_max - edge_y)
            cv2.circle(img2, (u_bound, edge_y), 5, v_point_color, -1)
        else:
            vertical.append(v_max - c_v + 1)

    for v_bound in v_bounds:
        x_values, = np.nonzero(edge[v_bound, :])
        left = x_values[x_values <= c_u]
        if len(left):
            edge_x = int(np.max(left))
            left_distances.append(c_u - edge_x)
            cv2.circle(img2, (edge_x, v_bound), 5, u_point_color, -1)
        else:
            left_distances.append(c_u + 1)

        right = x_values[x_values >= c_u]
        if len(right):
            edge_x = int(np.min(right))
            right_distances.append(edge_x - c_u)
            cv2.circle(img2, (edge_x, v_bound), 5, u_point_color, -1)
        else:
            right_distances.append(u_max - c_u + 1)

    return (vertical, left_distances, right_distances), img2


def gridFront(
    img: np.ndarray,
    cols: int = 7,
    rows: int = 3,
    y_max: int = 200,
    ths1: int = 100,
    ths2: int = 300,
    v_line_color: tuple[int, int, int] = (39, 200, 47),
    h_line_color: tuple[int, int, int] = (0, 0, 255),
    v_point_color: tuple[int, int, int] = (18, 246, 255),
    u_point_color: tuple[int, int, int] = (221, 0, 255),
) -> tuple[tuple[list[int], list[int], list[int]], np.ndarray]:
    """Return ``(V, L, R), grid`` in the real JCHM 2.0.2 format.

    ``V`` contains one vertical edge distance per column. ``L`` and ``R``
    contain horizontal distances from the image centre at each sampled row.
    The returned image is the resized camera frame with the sampling grid and
    detected points drawn on it.
    """

    if not isinstance(img, np.ndarray) or img.ndim != 3 or img.shape[2] != 3:
        raise ValueError("img must be a BGR image with shape (height, width, 3)")
    if cols < 1 or rows < 1:
        raise ValueError("cols and rows must both be positive")

    aspect_ratio = img.shape[1] / img.shape[0]
    new_width = 640
    new_height = int(new_width / aspect_ratio)
    resized = cv2.resize(img, (new_width, new_height))
    v_max, u_max = resized.shape[:2]
    y_max = max(1, min(int(y_max), 399))

    return findGrid(
        resized,
        resized.copy(),
        cols,
        rows,
        v_max,
        u_max,
        ths1,
        ths2,
        v_line_color,
        h_line_color,
        v_point_color,
        u_point_color,
        y_max,
    )
