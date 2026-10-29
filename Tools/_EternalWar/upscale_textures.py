#!/usr/bin/env python3
"""
Eternal War: doubles world textures for EyeManager.PixelsPerMeter = 64.

Every PNG of a world RSI is scaled x2 with nearest-neighbour (the picture stays the same,
it just has 4 texels per old pixel, ready to be redrawn in real detail), and the "size"
in its meta.json is doubled. UI textures keep their size: the UI still works in 32 px per meter.

Usage: python Tools/_EternalWar/upscale_textures.py [--dry-run] [--only PATH ...]
Run it once on textures that are still 32 px per meter; running it twice doubles them again.
"""

import argparse
import json
import os
import re
import sys
from concurrent.futures import ProcessPoolExecutor

from PIL import Image

SCALE = 2
TEXTURES = os.path.join(os.path.dirname(__file__), "..", "..", "Resources", "Textures")

# Folders (relative to Resources/Textures) that are not drawn in the world at all.
SKIP_PREFIXES = (
    "Interface/",
    "_EternalWar/Interface/Targeting/",
    "Parallaxes/",  # Drawn by ParallaxOverlay with its own scale, see ParallaxSystem.
    "Logo/",
    "LobbyScreens/",
    "Tips/",
    "Shaders/",
    "Template/",  # Templates for artists, the game does not load them.
    "Effects/HeatBlur/",  # Shader noise, sampled in screen space.
    "Effects/LightMasks/",  # Light masks are stretched to the light radius.
    "Clothing/Head/Helmets/scrapHelmetOverlayTexture.png",  # Screen-space overlays.
    "Clothing/Head/Welding/weldingTexture.png",
)

# UI folders whose textures are still entity sprites (action and alert icons are drawn by SpriteView,
# airlock AI hud and the pointing arrow are layers of world entities).
FORCE_PREFIXES = (
    "Interface/Actions/",
    "Interface/Alerts/",
    "Interface/Misc/ai_hud.rsi/",
    "Interface/Misc/pointing.rsi/",
    "Interface/Misc/progress_bar.rsi/",  # Do-after bar, drawn in the world by DoAfterOverlay.
    "Interface/Default/blocked.png",  # Toggled layer of mask action icons.
)

# Files inside forced folders that are only used as admin verb icons in the UI.
FORCE_EXCEPTIONS = (
    "Interface/Alerts/Fire/fire.png",
    "Interface/Actions/disarm.png",
)


def should_scale(rel: str) -> bool:
    if rel in FORCE_EXCEPTIONS:
        return False
    if rel.startswith(FORCE_PREFIXES):
        return True
    return not rel.startswith(SKIP_PREFIXES)


def scale_png(path: str) -> str | None:
    try:
        with Image.open(path) as img:
            img.load()
            info = {}
            if "transparency" in img.info:
                info["transparency"] = img.info["transparency"]
            scaled = img.resize((img.width * SCALE, img.height * SCALE), Image.Resampling.NEAREST)
            scaled.save(path, format="PNG", **info)
        return None
    except Exception as e:  # noqa: BLE001 - report and continue with other files
        return f"{path}: {e}"


SIZE_RE = re.compile(r'("size"\s*:\s*\{)([^}]*)(\})')
AXIS_RE = re.compile(r'("[xy]"\s*:\s*)(\d+)')


def scale_meta(path: str) -> str | None:
    try:
        with open(path, encoding="utf-8-sig") as f:
            text = f.read()
        json.loads(text)  # Validate before touching.

        def fix_size(m: re.Match) -> str:
            return m.group(1) + AXIS_RE.sub(lambda a: a.group(1) + str(int(a.group(2)) * SCALE), m.group(2)) + m.group(3)

        new_text, count = SIZE_RE.subn(fix_size, text, count=1)
        if count != 1:
            return f"{path}: no size field"
        with open(path, "w", encoding="utf-8", newline="") as f:
            f.write(new_text)
        return None
    except Exception as e:  # noqa: BLE001
        return f"{path}: {e}"


def collect(roots: list[str]) -> tuple[list[str], list[str]]:
    pngs, metas = [], []
    base = os.path.abspath(TEXTURES)
    for root in roots:
        for dirpath, _, files in os.walk(root):
            for name in files:
                full = os.path.join(dirpath, name)
                rel = os.path.relpath(full, base).replace(os.sep, "/")
                if not should_scale(rel):
                    continue
                if name.endswith(".png"):
                    pngs.append(full)
                elif name == "meta.json" and dirpath.endswith(".rsi"):
                    metas.append(full)
    return pngs, metas


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--dry-run", action="store_true", help="only print what would be scaled")
    parser.add_argument("--only", nargs="*", help="limit to these folders (relative to Resources/Textures)")
    args = parser.parse_args()

    base = os.path.abspath(TEXTURES)
    roots = [os.path.join(base, p) for p in args.only] if args.only else [base]
    pngs, metas = collect(roots)
    print(f"PNG: {len(pngs)}, meta.json: {len(metas)}")
    if args.dry_run:
        return 0

    errors = []
    with ProcessPoolExecutor() as pool:
        errors += [e for e in pool.map(scale_png, pngs, chunksize=64) if e]
        errors += [e for e in pool.map(scale_meta, metas, chunksize=64) if e]

    for e in errors:
        print("ERROR", e, file=sys.stderr)
    print(f"Done, errors: {len(errors)}")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
