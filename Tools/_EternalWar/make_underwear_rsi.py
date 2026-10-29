#!/usr/bin/env python3
"""
Eternal War: sprites of the underwear items worn in their own slots (underwearTop and underwearBottom).

  _EternalWar/Clothing/Underwear/underwear.rsi - for each piece: "<name>" is the item lying on the floor (the front
      of the worn sprite, doubled and centered), "<name>-equipped" is what is drawn on the body, copied from the human underwear
      markings (Mobs/Customization/undergarments.rsi). Both are greyscale and tinted by the item's color.
  Interface/Default/Slots/underwear_top.png, underwear_bottom.png - the empty slot icons, drawn like the other
      slot icons: the frame and stripes of uniform.png with a bra or briefs silhouette. Other UI themes fall back
      to the default one.

Run from the repository root:  python Tools/_EternalWar/make_underwear_rsi.py
"""
import json
import os
from PIL import Image

MARKINGS = "Resources/Textures/Mobs/Customization/undergarments.rsi"
OUT = "Resources/Textures/_EternalWar/Clothing/Underwear/underwear.rsi"
SLOTS = "Resources/Textures/Interface/Default/Slots"

PIECES = ["boxers", "briefs", "satin", "classic", "sports", "binder", "tanktop"]

# Slot icon silhouettes: "#" fill, "+" shade. Centered in the slot.
BRA = [
    "..#..........#..",
    "..#..........#..",
    "..##........##..",
    ".####......####.",
    "######.##.######",
    "#######++#######",
    "+#####+..+#####+",
    ".+++++....+++++.",
]

BRIEFS = [
    "################",
    "################",
    "+##############+",
    ".+############+.",
    "..+##########+..",
    "...++######++...",
    ".....+####+.....",
    "......++++......",
]


def frames(image):
    """The four directions of a 2x2 RSI sheet: south, north, east, west."""
    return [image.crop(((i % 2) * 32, (i // 2) * 32, (i % 2) * 32 + 32, (i // 2) * 32 + 32)) for i in range(4)]


def icon(sheet):
    """The front of the worn sprite, twice as big (on the body it is only a few pixels), in the middle of the tile."""
    south = frames(sheet)[0]
    piece = south.crop(south.getbbox())
    piece = piece.resize((piece.width * 2, piece.height * 2), Image.NEAREST)
    out = Image.new("RGBA", (32, 32))
    out.paste(piece, ((32 - piece.width) // 2, (32 - piece.height) // 2))
    return out


def underwear():
    os.makedirs(OUT, exist_ok=True)
    states = []
    for name in PIECES:
        sheet = Image.open(f"{MARKINGS}/{name}.png").convert("RGBA")
        sheet.save(f"{OUT}/{name}-equipped.png")
        icon(sheet).save(f"{OUT}/{name}.png")
        states.append({"name": name})
        states.append({"name": f"{name}-equipped", "directions": 4})

    with open(f"{MARKINGS}/meta.json", encoding="utf-8") as f:
        source = json.load(f)

    meta = {
        "version": 1,
        "license": source["license"],
        "copyright": source["copyright"] + ". Worn sprites copied and item icons cut from them for Eternal War "
                                          "by Tools/_EternalWar/make_underwear_rsi.py",
        "size": {"x": 32, "y": 32},
        "states": states,
    }
    with open(f"{OUT}/meta.json", "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2)
        f.write("\n")


def slot_icon(shape, name):
    """The uniform slot with its silhouette painted over by the stripes, then the new silhouette on top."""
    base = Image.open(f"{SLOTS}/uniform.png").convert("RGBA")
    px = base.load()

    def bluish(c):
        return c[3] and c[2] > c[0] + 25

    fills = sorted({px[x, y] for x in range(32) for y in range(32) if bluish(px[x, y])}, key=sum)
    fill, shade = fills[len(fills) * 2 // 3], fills[0]

    # The stripes run across, so each row's background is the color at the left, inside the frame.
    for y in range(32):
        background = px[4, y]
        for x in range(32):
            if bluish(px[x, y]):
                px[x, y] = background

    top = (32 - len(shape)) // 2
    left = (32 - len(shape[0])) // 2
    for dy, row in enumerate(shape):
        for dx, ch in enumerate(row):
            if ch == "#":
                px[left + dx, top + dy] = fill
            elif ch == "+":
                px[left + dx, top + dy] = shade

    base.save(f"{SLOTS}/{name}.png")


def main():
    underwear()
    slot_icon(BRA, "underwear_top")
    slot_icon(BRIEFS, "underwear_bottom")


if __name__ == "__main__":
    main()
