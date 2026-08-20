"""Generate the shipped 2026 preliminary/final course data and track artwork.

The input is page 2 rendered from the official track-production PDF.  The crop
coordinates are normalized so another DPI can be used without changing output.
The checked-in JSON remains ordinary CourseDocument data and is readable by the
Unity runtime without Python.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from PIL import Image

TILE_CM = 5
GRID_W, GRID_H = 198, 108  # 990 cm x 540 cm

PANEL_ROWS = [
    [(0,"G"),(1,"A"),(2,"A"),(3,"A"),(4,"F"),(5,"F"),(6,"N"),(7,"P"),(8,"P"),(9,"Q")],
    [(0,"A"),(6,"Q"),(7,"P"),(8,"P"),(9,"O")],
    [(0,"A"),(1,"L"),(2,"B"),(3,"K"),(9,"J"),(10,"I")],
    [(0,"C"),(1,"D"),(2,"K"),(3,"J"),(10,"B")],
    [(0,"D"),(1,"D"),(2,"J"),(3,"K"),(10,"A")],
    [(0,"J"),(1,"J"),(3,"H"),(4,"D"),(5,"D"),(6,"A"),(7,"M"),(8,"A"),(9,"A"),(10,"M")],
]

CANDIDATES = [
    ("candidate_1", (72,0,36,18), "east", (76,8), (82,8), "north", (90,8,0)),
    ("candidate_2", (0,18,18,54), "north", (8,24), (8,30), "east", (8,45,90)),
    ("candidate_3", (36,90,36,18), "east", (42,99), (48,99), "north", (57,99,0)),
    ("candidate_4", (180,18,18,36), "south", (189,46), (189,40), "east", (189,31,90)),
    ("candidate_5", (144,0,36,18), "west", (172,8), (166,8), "north", (157,8,0)),
]


def region(x: int, z: int, w: int, h: int) -> dict:
    return {"x": x, "z": z, "width": w, "height": h}


def crop_track(page: Image.Image) -> Image.Image:
    # Measured against the complete visual review render of official manual p2.
    x0, y0, x1, y1 = 102/1285, 137/934, 1182/1285, 726/934
    return page.crop((round(page.width*x0), round(page.height*y0),
                      round(page.width*x1), round(page.height*y1)))


def build_masks(track: Image.Image) -> tuple[list[dict], list[dict]]:
    im = track.resize((GRID_W*6, GRID_H*6), Image.Resampling.LANCZOS).convert("RGB")
    road, lines = [], []
    for z in range(GRID_H):
        # Unity z=0 is the bottom of the printed plan.
        source_row = GRID_H - 1 - z
        for x in range(GRID_W):
            cell = im.crop((x*6, source_row*6, (x+1)*6, (source_row+1)*6))
            pixels = list(cell.getdata())
            dark = sum(1 for r,g,b in pixels if max(r,g,b) < 115 and abs(r-g) < 55)
            red = sum(1 for r,g,b in pixels if r > 130 and r > g*1.35 and r > b*1.25)
            if dark >= 7:
                road.append({"x": x, "z": z})
            if red >= 4:
                lines.append({"x": x, "z": z})
    return road, lines


def competition(stage: str) -> dict:
    panels = []
    for row, entries in enumerate(PANEL_ROWS):
        for col, code in entries:
            panels.append({"code": code, "column": col, "row": row, "rotationDeg": 0})

    candidates = []
    for cid, r, direction, a, b, edge, obstacle in CANDIDATES:
        candidates.append({
            "id": cid, "region": region(*r), "direction": direction,
            "terminalACellX": a[0], "terminalACellZ": a[1],
            "terminalBCellX": b[0], "terminalBCellZ": b[1],
            "terminalEdge": edge, "terminalWidthTiles": 8,
            "obstacleCellX": obstacle[0], "obstacleCellZ": obstacle[1],
            "obstacleRotationDeg": obstacle[2],
        })

    # End points of the numbered yellow route segments in the official
    # summary drawing, expressed in physical centimetres from bottom-left.
    prelim = [
        ("start", 350, 45), ("s_curve", 220, 175),
        ("right_angle", 265, 315), ("u_tunnel", 130, 175),
        ("straight_hill", 45, 450), ("hill_exit", 365, 495),
        ("zigzag", 545, 495), ("obstacle_section", 860, 355),
        ("curve", 945, 85), ("finish", 630, 45),
    ]
    final = [
        ("start", 355, 45), ("s_tunnel", 265, 315),
        ("right_angle", 135, 315), ("u_turn", 45, 365),
        ("corner_hill", 365, 495), ("zigzag", 545, 495),
        ("obstacle_section", 860, 365), ("curve", 945, 85),
        ("finish", 630, 45),
    ]
    route = prelim if stage == "preliminary" else final
    checkpoints = [
        {"order": i+1, "id": name, "label": name.replace("_", " "),
         "region": region(round(x_cm/TILE_CM)-2, round(z_cm/TILE_CM)-2, 4, 4)}
        for i, (name, x_cm, z_cm) in enumerate(route)
    ]
    return {
        "edition": 2026, "stage": stage, "courseId": f"2026_{stage}",
        "visualProfile": "competition_2026", "panelSizeCm": 90,
        "physicalWidthCm": 990, "physicalLengthCm": 540,
        "panels": panels, "checkpoints": checkpoints,
        "missionCandidates": candidates,
    }


def structures(stage: str) -> list[dict]:
    if stage == "preliminary":
        tunnel_path = [(135,180,0),(135,45,0),(90,15,0),(45,45,0),(45,180,0)]
        hill_path = [(45,180,0),(45,270,10),(45,360,10),(45,450,0)]
        tunnel_region = region(0,0,36,36)
        hill_region = region(0,36,18,54)
        tunnel_profile = "u_tunnel"
    else:
        tunnel_path = [(180,270,0),(270,270,0),(315,225,0),(225,180,0),(315,135,0),(360,90,0)]
        hill_path = [(45,360,0),(45,450,10),(90,495,10),(180,495,0)]
        tunnel_region = region(35,17,40,40)
        hill_region = region(0,70,40,38)
        tunnel_profile = "s_tunnel"

    def pts(values):
        return [{"xCm":x,"zCm":z,"heightCm":h} for x,z,h in values]

    return [
        {"id": f"{stage}_tunnel", "type":"tunnel", "region":tunnel_region,
         "heightCm":22, "wallThicknessCm":0.5, "profile":tunnel_profile,
         "openingWidthCm":39, "roofLongCm":26, "roofShortCm":9.8,
         "pathPoints":pts(tunnel_path)},
        {"id": f"{stage}_hill", "type":"ramp", "region":hill_region,
         "heightCm":10, "riseCm":10, "direction":"north", "profile":"three_panel_hill",
         "openingWidthCm":55, "pathPoints":pts(hill_path)},
    ]


def course(stage: str, road: list[dict], lines: list[dict]) -> dict:
    metadata = competition(stage)
    checkpoint_by_id = {item["id"]: item for item in metadata["checkpoints"]}
    # Start and finish are distinct official checkpoints.  Derive trigger
    # regions from that single source so vehicle spawn, trigger detection and
    # coordinate-based validation cannot drift apart.
    start_region = checkpoint_by_id["start"]["region"]
    finish_region = checkpoint_by_id["finish"]["region"]
    return {
        "tileSizeCm": TILE_CM,
        "competition2026": metadata,
        "road": road,
        "lines": lines,
        "structures": structures(stage),
        "objects": [
            {"id":"start_signal_2026", "type":"start_signal", "tile":{"x":123,"z":5}, "rotationDeg":0, "footprint":"1x1"},
            {"id":"pit_barrier_2026", "type":"pit_barrier", "tile":{"x":154,"z":2}, "rotationDeg":0, "footprint":"3x1"},
        ],
        "triggers": [
            {"id":"start_2026", "type":"start", "region":start_region},
            {"id":"finish_2026", "type":"finish", "region":finish_region},
        ],
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("rendered_manual_page_2", type=Path)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()

    track = crop_track(Image.open(args.rendered_manual_page_2).convert("RGB"))
    road, lines = build_masks(track)

    texture = track.resize((1980,1080), Image.Resampling.LANCZOS)
    pixels = texture.load()
    for y in range(texture.height):
        for x in range(texture.width):
            r,g,b = pixels[x,y]
            if r > 238 and g > 238 and b > 238:
                pixels[x,y] = (62,105,48)

    asset_dir = args.root / "Assets/JajuchaSim/Resources/Competition2026"
    course_dir = args.root / "Courses"
    asset_dir.mkdir(parents=True, exist_ok=True)
    course_dir.mkdir(parents=True, exist_ok=True)
    texture.save(asset_dir / "track_surface.png", optimize=True)

    for stage in ("preliminary", "final"):
        path = course_dir / f"2026_{stage}.json"
        path.write_text(json.dumps(course(stage, road, lines), ensure_ascii=False, indent=2), encoding="utf-8")

    print(f"road={len(road)} line={len(lines)} panels=41 candidates=5")


if __name__ == "__main__":
    main()
