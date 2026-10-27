#!/usr/bin/env python3
"""
Eternal War: draws the sprites of blood on clothes, footprints and the shower.

  blood_stains.rsi - stains over worn clothing, cut to the human body (Mobs/Species/Human/parts.rsi):
                     body (jumpsuit, coat), hands (gloves), feet (shoes), head (hat), face (mask);
                     level 1 - a few spots, level 2 - soaked. Greyscale, the client tints them with the blood color.
  footprints.rsi   - a shoe print and a bare foot print, left and right, pointing north. White, tinted by the decal.
  shower.rsi       - the shower head, off and running.

Run from the repository root:  python Tools/_EternalWar/make_hygiene_rsi.py
"""
import json
import os
import random
from PIL import Image, ImageDraw

HUMAN = "Resources/Textures/Mobs/Species/Human/parts.rsi"
OUT = "Resources/Textures/_EternalWar"

FRAMES = [(0, 0), (32, 0), (0, 32), (32, 32)]  # south, north, east, west


def write_meta(path, states, copyright_text):
    meta = {
        "version": 1,
        "license": "CC-BY-SA-3.0",
        "copyright": copyright_text,
        "size": {"x": 32, "y": 32},
        "states": states,
    }
    with open(os.path.join(path, "meta.json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=4)


def reset(path):
    os.makedirs(path, exist_ok=True)
    for old in os.listdir(path):
        os.remove(os.path.join(path, old))


# region Stains

def part_mask(names, rows=None):
    """Alpha mask (64x64, 4 frames) of the union of human parts, optionally only rows top..bottom of each part."""
    mask = Image.new("L", (64, 64), 0)
    mp = mask.load()
    for name in names:
        im = Image.open(f"{HUMAN}/{name}.png").convert("RGBA")
        px = im.load()
        for fx, fy in FRAMES:
            frame = im.crop((fx, fy, fx + 32, fy + 32))
            box = frame.getbbox()
            if box is None:
                continue
            top, bottom = box[1], box[3]
            if rows is not None:
                height = bottom - top
                top, bottom = top + int(height * rows[0]), top + int(round(height * rows[1]))
            for y in range(top, bottom):
                for x in range(32):
                    if px[fx + x, fy + y][3] > 0:
                        mp[fx + x, fy + y] = 255
    return mask


def splatter(mask, level, seed):
    """Spots of blood inside the mask: a few at level 1, most of it soaked at level 2, with drips."""
    rng = random.Random(seed)
    out = Image.new("RGBA", mask.size, (0, 0, 0, 0))
    po, pm = out.load(), mask.load()
    for fx, fy in FRAMES:
        pixels = [(x, y) for y in range(fy, fy + 32) for x in range(fx, fx + 32) if pm[x, y]]
        if not pixels:
            continue
        spots = max(2, len(pixels) // (16 if level == 1 else 8))
        for _ in range(spots):
            cx, cy = rng.choice(pixels)
            radius = rng.choice([0, 1, 1]) if level == 1 else rng.choice([0, 1, 1, 2])
            shade = rng.randint(185, 255)
            for y in range(cy - radius, cy + radius + 1):
                for x in range(cx - radius, cx + radius + 1):
                    if (x - cx) ** 2 + (y - cy) ** 2 > radius * radius + 1:
                        continue
                    if fx <= x < fx + 32 and fy <= y < fy + 32 and pm[x, y]:
                        po[x, y] = (shade, shade, shade, rng.randint(170, 235))
            # A drip running down.
            if rng.random() < (0.25 if level == 1 else 0.5):
                for y in range(cy + 1, cy + rng.randint(2, 4)):
                    if fy <= y < fy + 32 and pm[cx, y]:
                        po[cx, y] = (shade, shade, shade, 210)
    return out


def stains():
    path = f"{OUT}/Objects/Misc/blood_stains.rsi"
    reset(path)
    parts = {
        "body": part_mask(["torso_m", "torso_f", "l_arm", "r_arm", "l_leg", "r_leg"]),
        "hands": part_mask(["l_hand", "r_hand"]),
        "feet": part_mask(["l_foot", "r_foot"]),
        "head": part_mask(["head_m", "head_f"], rows=(0.0, 0.55)),
        "face": part_mask(["head_m", "head_f"], rows=(0.45, 1.0)),
    }
    states = []
    for i, (name, mask) in enumerate(parts.items()):
        for level in (1, 2):
            splatter(mask, level, seed=100 * i + level).save(f"{path}/{name}_{level}.png")
            states.append({"name": f"{name}_{level}", "directions": 4})
    write_meta(path, states, "Made for Eternal War, cut to the human parts "
                             "(https://github.com/tgstation/tgstation/blob/8024397cc81c5f47f74cf4279e35728487d0a1a7/icons/mob/human_parts_greyscale.dmi, modified by DrSmugleaf)")

# endregion

# region Footprints

def footprint(bare, left):
    im = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    white = (255, 255, 255, 255)
    # Pointing north: toes at the top. The left foot's arch is on its right side.
    if bare:
        d.ellipse([14, 13, 17, 19], fill=white)
        d.ellipse([14, 19, 17, 22], fill=white)
        for i, x in enumerate([13, 14, 15, 16, 17]):
            d.point((x, 11 if i in (1, 2) else 12), fill=white)
    else:
        d.rectangle([14, 11, 17, 17], fill=white)
        d.rectangle([15, 10, 16, 10], fill=white)
        d.rectangle([14, 19, 17, 21], fill=white)
    if left:
        im = im.transpose(Image.FLIP_LEFT_RIGHT)
    return im


def footprints():
    path = f"{OUT}/Decals/footprints.rsi"
    reset(path)
    states = []
    for bare in (False, True):
        for left in (True, False):
            name = f"{'bare' if bare else 'shoe'}_{'left' if left else 'right'}"
            footprint(bare, left).save(f"{path}/{name}.png")
            states.append({"name": name})
    write_meta(path, states, "Made for Eternal War")

# endregion

# region Shower

def shower_base():
    im = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    metal, dark, light = (150, 156, 166, 255), (78, 82, 92, 255), (205, 210, 218, 255)
    # The pipe comes out of the wall above.
    d.rectangle([15, 0, 16, 4], fill=metal)
    d.line([(17, 0), (17, 4)], fill=dark)
    d.rectangle([14, 2, 17, 3], fill=dark)
    # The head.
    d.ellipse([11, 4, 20, 9], fill=metal, outline=dark)
    d.line([(13, 5), (17, 5)], fill=light)
    for x in (13, 15, 17):
        d.point((x, 8), fill=dark)
    # The valve on the wall.
    d.rectangle([22, 1, 25, 3], fill=dark)
    d.point((23, 2), fill=(190, 40, 40, 255))
    return im


def shower_frame(frame):
    im = shower_base()
    d = ImageDraw.Draw(im)
    water = (150, 200, 255, 150)
    bright = (210, 235, 255, 200)
    for i, x in enumerate(range(11, 21, 2)):
        for y in range(10, 30):
            if (y + frame * 2 + i) % 5 < 3:
                d.point((x + (1 if y > 20 and x > 15 else 0) - (1 if y > 20 and x < 15 else 0), y), fill=water)
        d.point((x, 10 + (frame * 5 + i * 3) % 18), fill=bright)
    # Splashes on the floor.
    for i in range(4):
        x = 10 + (frame * 3 + i * 4) % 12
        d.point((x, 29 - (i + frame) % 2), fill=bright)
    return im


def shower():
    path = f"{OUT}/Structures/Furniture/shower.rsi"
    reset(path)
    shower_base().save(f"{path}/shower.png")
    frames = [shower_frame(f) for f in range(4)]
    sheet = Image.new("RGBA", (32 * 4, 32), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        sheet.paste(f, (32 * i, 0))
    sheet.save(f"{path}/shower_on.png")
    write_meta(path, [{"name": "shower"}, {"name": "shower_on", "delays": [[0.12, 0.12, 0.12, 0.12]]}], "Made for Eternal War")

# endregion


if __name__ == "__main__":
    stains()
    footprints()
    shower()
