from pathlib import Path
from shutil import copy2
import sys

from PIL import Image, ImageFilter


def remove_checkerboard(source: Path, destination: Path) -> None:
    image = Image.open(source).convert("RGBA")
    pixels = []
    for red, green, blue, _ in image.getdata():
        spread = max(red, green, blue) - min(red, green, blue)
        if min(red, green, blue) >= 190 and spread <= 24:
            pixels.append((red, green, blue, 0))
            continue
        alpha = min(255, round((255 - min(red, green, blue)) / 96 * 255))
        if alpha >= 250:
            pixels.append((red, green, blue, 255))
            continue
        ratio = max(1 / 255, alpha / 255)
        clean = tuple(
            max(0, min(255, round((channel - 255 * (1 - ratio)) / ratio)))
            for channel in (red, green, blue)
        )
        pixels.append((*clean, alpha))
    image.putdata(pixels)
    mask = image.getchannel("A").point(lambda value: 255 if value >= 40 else 0)
    image.putalpha(mask.filter(ImageFilter.MinFilter(5)))
    destination.parent.mkdir(parents=True, exist_ok=True)
    image.save(destination, optimize=True)


def main() -> None:
    if len(sys.argv) != 7:
        raise SystemExit(
            "usage: process_primordial_meadow_layers.py "
            "MASTER FAR MID ARENA FOREGROUND PROPS"
        )

    master, far, mid, arena, foreground, props = map(Path, sys.argv[1:])
    project = Path(__file__).resolve().parents[1]
    resource_dir = project / "Assets/Resources/Environment/PrimordialMeadowLayered"
    concept_dir = project / "Assets/Art/EnvironmentConcepts/PrimordialMeadowLayered"
    resource_dir.mkdir(parents=True, exist_ok=True)
    concept_dir.mkdir(parents=True, exist_ok=True)

    copy2(master, concept_dir / "MasterComposite_v1.png")
    copy2(master, resource_dir / "BattleBackground.png")
    copy2(far, resource_dir / "LayerFar.png")
    remove_checkerboard(mid, resource_dir / "LayerMid.png")
    remove_checkerboard(arena, resource_dir / "LayerArena.png")
    remove_checkerboard(foreground, resource_dir / "LayerForeground.png")
    remove_checkerboard(props, resource_dir / "ReactiveProps.png")

    preview = Image.open(resource_dir / "LayerFar.png").convert("RGBA")
    for name in ("LayerMid.png", "LayerArena.png", "LayerForeground.png"):
        preview.alpha_composite(Image.open(resource_dir / name).convert("RGBA"))
    capture_dir = project / "Captures"
    capture_dir.mkdir(parents=True, exist_ok=True)
    preview.save(capture_dir / "primordial_meadow_layered_composite_preview.png", optimize=True)

    for path in sorted(resource_dir.glob("*.png")):
        with Image.open(path) as image:
            extrema = image.getchannel("A").getextrema() if image.mode == "RGBA" else None
            print(f"{path.name}: {image.size[0]}x{image.size[1]} {image.mode} alpha={extrema}")


if __name__ == "__main__":
    main()
