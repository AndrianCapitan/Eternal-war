#!/usr/bin/env python3
"""
Eternal War: draws the sprites of a human body without skin, cut into the body zones.

The upstream sprite parts are bigger than the zones: the arm is the upper arm and the forearm, the leg is the thigh
and the shin, the torso is the chest and the abdomen. They are cut across at the elbow, the knee and the waist
(the same rows in every direction), so every zone has its own piece:

  skin_<zone>  - the piece of the greyscale human part; the client tints it with the skin color.
                 Only for the zones of a cut part: there the whole part is drawn from pieces.
  flesh_<zone> - the bare muscles: red, shaded like the skin, with fibres and a dark edge.
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

DARK = (92, 14, 20)
BRIGHT = (196, 52, 52)
EDGE = (64, 8, 12)
FIBRE = 0.82


def flesh(src: Image.Image) -> Image.Image:
    """The muscles of a whole part. Edges are found on the whole part, so a cut doesn't get an outline."""
    src = src.convert("RGBA")
    w, h = src.size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px, po = src.load(), out.load()
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            lum = (r + g + b) / (3 * 255)
            col = [DARK[i] + (BRIGHT[i] - DARK[i]) * lum for i in range(3)]
            # Muscle fibres: every third column of each 32x32 frame a bit darker.
            if (x % 32) % 3 == 0:
                col = [c * FIBRE for c in col]
            edge = any(
                not (0 <= x + dx < w and 0 <= y + dy < h) or px[x + dx, y + dy][3] == 0
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            if edge:
                col = list(EDGE)
            po[x, y] = (int(col[0]), int(col[1]), int(col[2]), a)
    return out


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

    states = []
    for zone, (human, skeleton, top, bottom, has_skin) in PIECES.items():
        part = Image.open(f"{HUMAN}/{human}.png")
        flesh_piece = cut(flesh(part), top, bottom)
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
        "copyright": "skin_* and flesh_* made for Eternal War from the human parts "
                     "(https://github.com/tgstation/tgstation/blob/8024397cc81c5f47f74cf4279e35728487d0a1a7/icons/mob/human_parts_greyscale.dmi, modified by DrSmugleaf); "
                     "bone_* are cut from the skeleton parts created by EmoGarbage",
        "size": {"x": 32, "y": 32},
        "states": states,
    }
    with open(f"{OUT}/meta.json", "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=4)


if __name__ == "__main__":
    main()
