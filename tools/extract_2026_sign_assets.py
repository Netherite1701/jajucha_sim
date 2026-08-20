"""Extract print-scale 2026 sign artwork from reviewed PDF page renders."""

from __future__ import annotations

import argparse
from pathlib import Path
from PIL import Image


def transparent_white(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    data = []
    for r,g,b,a in rgba.getdata():
        if r > 247 and g > 247 and b > 247:
            data.append((255,255,255,0))
        else:
            data.append((r,g,b,a))
    rgba.putdata(data)
    return rgba


def crop_norm(path: Path, box: tuple[float,float,float,float], alpha: bool = False) -> Image.Image:
    im = Image.open(path).convert("RGB")
    x0,y0,x1,y1 = box
    out = im.crop((round(x0*im.width), round(y0*im.height), round(x1*im.width), round(y1*im.height)))
    return transparent_white(out) if alpha else out


def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument("a4_dir", type=Path)
    p.add_argument("b4_dir", type=Path)
    p.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    args = p.parse_args()
    out = args.root / "Assets/JajuchaSim/Resources/Competition2026"
    out.mkdir(parents=True, exist_ok=True)

    assets = {
        "starting_light_sign.png": crop_norm(args.a4_dir/"page-1.png", (.425,.155,.575,.61), True),
        "yellow_flag.png": crop_norm(args.a4_dir/"page-2.png", (.14,.16,.86,.78), False),
        "pit_barrier.png": crop_norm(args.b4_dir/"page-1.png", (.06,.15,.94,.66), False),
        "dynamic_obstacle.png": crop_norm(args.b4_dir/"page-2.png", (.13,.0,.79,.98), True),
    }
    for name, image in assets.items():
        image.save(out/name, optimize=True)
        print(name, image.size)


if __name__ == "__main__":
    main()
