"""Regression checks for the 2026 printed-artwork road masks.

The mask is sampled from the official artwork.  In particular, green infield
pixels must not be classified as asphalt: otherwise the inner openings of the
U/S-turns become legal driving tiles even though the camera shows a hole.
"""

from __future__ import annotations

import json
from collections import deque
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


def _components(cells: set[tuple[int, int]], width: int, height: int) -> list[int]:
    seen: set[tuple[int, int]] = set()
    sizes: list[int] = []
    for start in cells:
        if start in seen:
            continue
        queue = deque([start])
        seen.add(start)
        size = 0
        while queue:
            x, z = queue.popleft()
            size += 1
            for neighbor in ((x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1)):
                if neighbor in cells and neighbor not in seen:
                    seen.add(neighbor)
                    queue.append(neighbor)
        sizes.append(size)
    return sizes


def _load(stage: str) -> tuple[set[tuple[int, int]], int, int]:
    data = json.loads((ROOT / "Courses" / f"2026_{stage}.json").read_text(encoding="utf-8"))
    meta = data["competition2026"]
    width = meta["physicalWidthCm"] // data["tileSizeCm"]
    height = meta["physicalLengthCm"] // data["tileSizeCm"]
    road = {(item["x"], item["z"]) for item in data["road"]}
    return road, width, height


def test_2026_road_masks_are_single_connected_route_with_one_infield():
    for stage in ("preliminary", "final"):
        road, width, height = _load(stage)
        route_components = sorted(_components(road, width, height), reverse=True)
        assert len(route_components) == 1, (stage, route_components[:5])

        empty = {
            (x, z)
            for x in range(width)
            for z in range(height)
            if (x, z) not in road
        }
        enclosed: list[int] = []
        seen: set[tuple[int, int]] = set()
        for start in empty:
            if start in seen:
                continue
            queue = deque([start])
            seen.add(start)
            size = 0
            touches_edge = False
            while queue:
                x, z = queue.popleft()
                size += 1
                touches_edge |= x in (0, width - 1) or z in (0, height - 1)
                for neighbor in ((x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1)):
                    if (
                        0 <= neighbor[0] < width
                        and 0 <= neighbor[1] < height
                        and neighbor in empty
                        and neighbor not in seen
                    ):
                        seen.add(neighbor)
                        queue.append(neighbor)
            if not touches_edge:
                enclosed.append(size)

        # Both official layouts have one continuous infield. Small enclosed
        # pockets indicate the green artwork was accidentally marked as road.
        assert len(enclosed) == 1, (stage, sorted(enclosed, reverse=True)[:10])
