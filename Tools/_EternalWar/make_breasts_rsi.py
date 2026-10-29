#!/usr/bin/env python3
"""
Eternal War: draws the breast size markings of female humans, drawn over the female torso
(Mobs/Species/Human/parts.rsi, "torso_f") in its greyscale skin palette and tinted with the skin colour.

  Mobs/Customization/breasts.rsi - small, large, very_large, huge. The torso itself is the medium size, so there is
                                   no state for it. Seen from the front the breasts get taller and the shading under
                                   them deeper, from the side they stand out further. The back is unchanged.

The overlay can only paint over the torso, not cut into it, so the small size is flatter only from the front.

Run from the repository root:  python Tools/_EternalWar/make_breasts_rsi.py
"""
import json
import os
from PIL import Image

OUT = "Resources/Textures/_EternalWar/Mobs/Customization/breasts.rsi"

# The greyscale skin palette of parts.rsi: lit, light shade, shade, outline.
PALETTE = {
    "W": (255, 255, 255, 255),
    "L": (249, 238, 242, 255),
    "M": (234, 217, 220, 255),
    "D": (218, 180, 178, 255),
}

# Front (south) view: rows from FRONT_TOP, columns from FRONT_LEFT. "." leaves the torso as it is.
FRONT_TOP = 13
FRONT_LEFT = 9

# Side view facing east: rows from SIDE_TOP, columns from SIDE_LEFT. West is its mirror.
SIDE_TOP = 12
SIDE_LEFT = 16

SIZES = {
    "small": {
        "front": [
            ".............",
            "...WWWWWWW...",
            "...WLMWMLW...",
        ],
        "side": [],
    },
    "large": {
        "front": [
            ".............",
            ".DMWWLMLWWMD.",
            ".DMWWLDLWWMD.",
            "..DMLDMDLMD..",
            "...MMLWLMM...",
        ],
        "side": [
            "...M...",
            ".WWWM..",
            ".WWWM..",
            ".LWLD..",
            ".LMD...",
        ],
    },
    "very_large": {
        "front": [
            "......L......",
            ".DWWWWMWWWWD.",
            ".DLWWWDWWWLD.",
            ".DMLWLDLWLMD.",
            "..DMMDMDMMD..",
            "...MMLWLMM...",
        ],
        "side": [
            "..WWM..",
            ".WWWWM.",
            ".WWWWM.",
            ".WWWLD.",
            ".LLMD..",
            "LMD....",
        ],
    },
    "huge": {
        "front": [
            ".DWWWWLWWWWD.",
            ".DWWWWMWWWWD.",
            ".DWWWWDWWWWD.",
            ".DLWWWDWWWLD.",
            ".DMLWLDLWLMD.",
            "..DMMDMDMMD..",
            "...MMLWLMM...",
        ],
        "side": [
            "..WWM..",
            ".WWWWM.",
            ".WWWWWM",
            ".WWWWLD",
            ".WWLLD.",
            "LLMMD..",
            ".MMD...",
        ],
    },
}


def paint(frame, rows, top, left):
    px = frame.load()
    for dy, row in enumerate(rows):
        for dx, ch in enumerate(row):
            if ch != ".":
                px[left + dx, top + dy] = PALETTE[ch]


def state(size):
    # RSI directions: south, north, east, west, laid out two by two.
    south, north, east = (Image.new("RGBA", (32, 32)) for _ in range(3))
    paint(south, size["front"], FRONT_TOP, FRONT_LEFT)
    paint(east, size["side"], SIDE_TOP, SIDE_LEFT)
    west = east.transpose(Image.FLIP_LEFT_RIGHT)

    sheet = Image.new("RGBA", (64, 64))
    for i, frame in enumerate((south, north, east, west)):
        sheet.paste(frame, ((i % 2) * 32, (i // 2) * 32))
    return sheet


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, size in SIZES.items():
        state(size).save(f"{OUT}/{name}.png")

    meta = {
        "version": 1,
        "license": "CC-BY-SA-3.0",
        "copyright": "Drawn for Eternal War over the human torso of tgstation "
                     "(https://github.com/tgstation/tgstation/blob/8024397cc81c5f47f74cf4279e35728487d0a1a7/icons/mob/human_parts_greyscale.dmi), "
                     "by Tools/_EternalWar/make_breasts_rsi.py",
        "size": {"x": 32, "y": 32},
        "states": [{"name": name, "directions": 4} for name in SIZES],
    }
    with open(f"{OUT}/meta.json", "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2)
        f.write("\n")


if __name__ == "__main__":
    main()
