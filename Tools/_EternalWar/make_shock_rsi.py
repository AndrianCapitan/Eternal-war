#!/usr/bin/env python3
"""
Eternal War: draws the alert icon of traumatic shock, a pale human shaking from side to side.

  Interface/Alerts/shock.rsi - the whole human (Mobs/Species/Human/parts.rsi, "full") in a pale, bluish skin tone,
                               shifted a pixel left and right between frames, with white shake marks at its sides.

Run from the repository root:  python Tools/_EternalWar/make_shock_rsi.py
"""
import json
import os
from PIL import Image

HUMAN = "Resources/Textures/Mobs/Species/Human/parts.rsi"
OUT = "Resources/Textures/_EternalWar/Interface/Alerts/shock.rsi"

PALE = (205, 212, 220)
OUTLINE = (40, 32, 36, 255)
MARK = (255, 255, 255, 230)


def body():
    """The human, recoloured pale, cropped to its silhouette."""
    part = Image.open(f"{HUMAN}/full.png").convert("RGBA").crop((0, 0, 32, 32))
    part = part.crop(part.getbbox())

    pale = Image.new("RGBA", part.size)
    src, dst = part.load(), pale.load()
    for y in range(part.height):
        for x in range(part.width):
            r, g, b, a = src[x, y]
            if a:
                light = (r + g + b) / 3 / 255
                dst[x, y] = tuple(int(c * (0.55 + 0.45 * light)) for c in PALE) + (255,)
    return pale


def outline(icon):
    """A one pixel dark outline around the silhouette, as on the other alert icons."""
    out = Image.new("RGBA", icon.size)
    src, dst = icon.load(), out.load()
    for y in range(32):
        for x in range(32):
            if src[x, y][3]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < 32 and 0 <= ny < 32 and src[nx, ny][3]:
                    dst[x, y] = OUTLINE
                    break
    out.alpha_composite(icon)
    return out


def frame(pale, shift, marks):
    """The body moved by shift pixels, with short shake marks beside its head and arms when marks is set."""
    icon = Image.new("RGBA", (32, 32))
    left = (32 - pale.width) // 2 + shift
    top = 32 - pale.height
    icon.paste(pale, (left, top), pale)
    icon = outline(icon)

    if marks:
        px = icon.load()
        for side_x, step in ((left - 3, -1), (left + pale.width + 2, 1)):
            for y0 in (top + 2, top + 10):
                # A little zigzag: two pixels out, one back.
                for i, dx in enumerate((0, step, 0)):
                    x, y = side_x + dx, y0 + i
                    if 0 <= x < 32 and 0 <= y < 32 and not px[x, y][3]:
                        px[x, y] = MARK
    return icon


def main():
    pale = body()
    frames = [frame(pale, s, m) for s, m in ((-1, True), (1, False), (-1, False), (1, True))]

    os.makedirs(OUT, exist_ok=True)
    sheet = Image.new("RGBA", (32 * len(frames), 32))
    for i, f in enumerate(frames):
        sheet.paste(f, (32 * i, 0))
    sheet.save(os.path.join(OUT, "shock.png"))

    meta = {
        "version": 1,
        "license": "CC-BY-SA-3.0",
        "copyright": "Drawn by Tools/_EternalWar/make_shock_rsi.py from the human of "
                     "Mobs/Species/Human/parts.rsi (tgstation, modified by DrSmugleaf)",
        "size": {"x": 32, "y": 32},
        "states": [{"name": "shock", "delays": [[0.15, 0.15, 0.15, 0.15]]}],
    }
    with open(os.path.join(OUT, "meta.json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=4)


if __name__ == "__main__":
    main()
