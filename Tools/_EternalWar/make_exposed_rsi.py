#!/usr/bin/env python3
"""
Eternal War: draws the sprites of a human body without skin, cut into the body zones.

The upstream sprite parts are bigger than the zones: the arm is the upper arm and the forearm, the leg is the thigh
and the shin, the torso is the chest and the abdomen. They are cut across at the elbow, the knee and the waist
(the same rows in every direction), so every zone has its own piece:

  skin_<zone>  - the piece of the greyscale human part; the client tints it with the skin color.
                 Only for the zones of a cut part: there the whole part is drawn from pieces.
  flesh_<zone> - the bare muscles, cut from the drawn sprite of the whole skinless body (FLESH) by the mask of the
                 human part. The drawing has the male build: the few pixels a female part has beyond the male one
                 take the color of the nearest drawn pixel.
  bone_<zone>  - the bones, from the skeleton species.
  flesh_open_<zone>, bone_open_<zone> - the same with an open fracture: a shard of bone out of the wound,
                 or a break with blood across the bare bone.

Run from the repository root:  python Tools/_EternalWar/make_exposed_rsi.py
"""
import json
import os
from PIL import Image

HUMAN = "Resources/Textures/Mobs/Species/Human/parts.rsi"
SKELETON = "Resources/Textures/Mobs/Species/Skeleton/parts.rsi"
OUT = "Resources/Textures/_EternalWar/Mobs/Species/Human/exposed.rsi"
# The whole human body without skin, 4 directions like a part state.
FLESH = "Спрайты для Клода/Без кожи.png"

# Rows (in every 32x32 frame) where the lower zone starts.
ELBOW = 15
KNEE = 26
WAIST = 18

# zone state -> (human part, skeleton part, first row, last row + 1, has a skin piece)
PIECES = {
    "head_m": ("head_m", "head", 0, 32, False),
    "head_f": ("head_f", "head", 0, 32, False),
    "chest_m": ("torso_m", "torso_m", 0, WAIST, True),
    "chest_f": ("torso_f", "torso_f", 0, WAIST, True),
    "abdomen_m": ("torso_m", "torso_m", WAIST, 32, True),
    "abdomen_f": ("torso_f", "torso_f", WAIST, 32, True),
    "l_upper_arm": ("l_arm", "l_arm", 0, ELBOW, True),
    "l_forearm": ("l_arm", "l_arm", ELBOW, 32, True),
    "r_upper_arm": ("r_arm", "r_arm", 0, ELBOW, True),
    "r_forearm": ("r_arm", "r_arm", ELBOW, 32, True),
    "l_hand": ("l_hand", "l_hand", 0, 32, False),
    "r_hand": ("r_hand", "r_hand", 0, 32, False),
    "l_thigh": ("l_leg", "l_leg", 0, KNEE, True),
    "l_shin": ("l_leg", "l_leg", KNEE, 32, True),
    "r_thigh": ("r_leg", "r_leg", 0, KNEE, True),
    "r_shin": ("r_leg", "r_leg", KNEE, 32, True),
    "l_foot": ("l_foot", "l_foot", 0, 32, False),
    "r_foot": ("r_foot", "r_foot", 0, 32, False),
}



def male(part: str) -> str:
    return part[:-2] + "_m" if part.endswith("_f") else part


def flesh(drawn: Image.Image, part: str) -> Image.Image:
    """The muscles of a whole part: the drawn body under the mask of the part."""
    mask = Image.open(f"{HUMAN}/{part}.png").convert("RGBA").load()
    male_mask = Image.open(f"{HUMAN}/{male(part)}.png").convert("RGBA").load()
    src = drawn.load()
    w, h = drawn.size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    po = out.load()
    for y in range(h):
        for x in range(w):
            if mask[x, y][3] == 0:
                continue
            if src[x, y][3] > 0:
                po[x, y] = src[x, y]
            elif male_mask[x, y][3] == 0:
                po[x, y] = nearest(src, x, y)
    return out


def nearest(src, x: int, y: int):
    """The nearest drawn pixel in the same 32x32 frame."""
    fx, fy = x - x % 32, y - y % 32
    best, color = None, (0, 0, 0, 0)
    for yy in range(fy, fy + 32):
        for xx in range(fx, fx + 32):
            if src[xx, yy][3] == 0:
                continue
            d = (xx - x) ** 2 + (yy - y) ** 2
            if best is None or d < best:
                best, color = d, src[xx, yy]
    return color


def cut(image: Image.Image, top: int, bottom: int) -> Image.Image:
    """Keeps rows top..bottom-1 of every 32x32 frame of a 4-direction state."""
    image = image.convert("RGBA").copy()
    px = image.load()
    w, h = image.size
    for y in range(h):
        if top <= y % 32 < bottom:
            continue
        for x in range(w):
            px[x, y] = (0, 0, 0, 0)
    return image


def open_fracture(piece: Image.Image, on_bone: bool) -> Image.Image:
    """An open fracture on a piece: a shard of bone sticking out of the wound (on the muscles), or a break with blood
    across the bare bone. Drawn around the middle of the piece in every frame."""
    out = piece.copy()
    px = out.load()
    for fx, fy in ((0, 0), (32, 0), (0, 32), (32, 32)):
        points = [(x, y) for y in range(fy, fy + 32) for x in range(fx, fx + 32) if px[x, y][3] > 0]
        if not points:
            continue
        cx = round(sum(p[0] for p in points) / len(points))
        cy = round(sum(p[1] for p in points) / len(points))
        if on_bone:
            # A dark red break across the bone, a drop of blood under it.
            for dx in (-1, 0, 1):
                if px[cx + dx, cy][3] > 0:
                    px[cx + dx, cy] = (110, 10, 14, 255)
            if px[cx, cy + 1][3] > 0:
                px[cx, cy + 1] = (150, 20, 24, 255)
        else:
            # A dark wound with a white shard of bone sticking out of it.
            for dx, dy in ((-1, 0), (1, 0), (0, 1), (-1, 1), (1, 1)):
                x, y = cx + dx, cy + dy
                if px[x, y][3] > 0:
                    px[x, y] = (60, 6, 10, 255)
            for x, y, shade in ((cx, cy, 235), (cx, cy - 1, 250), (cx + 1, cy - 2, 210)):
                if fx <= x < fx + 32 and fy <= y < fy + 32:
                    px[x, y] = (shade, shade, shade - 15, 255)
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    for old in os.listdir(OUT):
        os.remove(os.path.join(OUT, old))

    drawn = Image.open(FLESH).convert("RGBA")
    states = []
    for zone, (human, skeleton, top, bottom, has_skin) in PIECES.items():
        part = Image.open(f"{HUMAN}/{human}.png")
        flesh_piece = cut(flesh(drawn, human), top, bottom)
        bone_piece = cut(Image.open(f"{SKELETON}/{skeleton}.png"), top, bottom)
        flesh_piece.save(f"{OUT}/flesh_{zone}.png")
        bone_piece.save(f"{OUT}/bone_{zone}.png")
        open_fracture(flesh_piece, on_bone=False).save(f"{OUT}/flesh_open_{zone}.png")
        open_fracture(bone_piece, on_bone=True).save(f"{OUT}/bone_open_{zone}.png")
        states += [{"name": f"{kind}_{zone}", "directions": 4} for kind in ("flesh", "bone", "flesh_open", "bone_open")]
        if has_skin:
            cut(part, top, bottom).save(f"{OUT}/skin_{zone}.png")
            states.append({"name": f"skin_{zone}", "directions": 4})

    meta = {
        "version": 1,
        "license": "CC-BY-SA-3.0",
        "copyright": "skin_* cut for Eternal War from the human parts "
                     "(https://github.com/tgstation/tgstation/blob/8024397cc81c5f47f74cf4279e35728487d0a1a7/icons/mob/human_parts_greyscale.dmi, modified by DrSmugleaf); "
                     "flesh_* cut from the skinless human drawn for Eternal War; "
                     "bone_* are cut from the skeleton parts created by EmoGarbage",
        "size": {"x": 32, "y": 32},
        "states": states,
    }
    with open(f"{OUT}/meta.json", "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=4)


if __name__ == "__main__":
    main()
