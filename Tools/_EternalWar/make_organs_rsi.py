#!/usr/bin/env python3
"""
Eternal War: draws the alert icon of appendicitis, a torso with a sore spot low on its right side.

  Interface/Alerts/appendicitis.rsi - the human torso (Mobs/Species/Human/parts.rsi, facing south) scaled up,
                                      with a pulsing red spot where the appendix is (the viewer's left).

Run from the repository root:  python Tools/_EternalWar/make_organs_rsi.py
"""
import json
import os
from PIL import Image, ImageDraw, ImageFilter

HUMAN = "Resources/Textures/Mobs/Species/Human/parts.rsi"
OUT = "Resources/Textures/_EternalWar/Interface/Alerts/appendicitis.rsi"

SKIN = (222, 186, 160)
OUTLINE = (40, 28, 24, 255)


def torso():
    """The south frame of the human torso, scaled up twice and centered on 32x32, in a skin tone."""
    part = Image.open(f"{HUMAN}/torso_m.png").convert("RGBA").crop((0, 0, 32, 32))
    box = part.getbbox()
    part = part.crop(box)
    part = part.resize((part.width * 2, part.height * 2), Image.NEAREST)

    shaded = Image.new("RGBA", part.size)
    src, dst = part.load(), shaded.load()
    for y in range(part.height):
        for x in range(part.width):
            r, g, b, a = src[x, y]
            if a:
                light = (r + g + b) / 3 / 255
                dst[x, y] = tuple(int(c * (0.55 + 0.45 * light)) for c in SKIN) + (255,)

    icon = Image.new("RGBA", (32, 32))
    left = (32 - shaded.width) // 2
    top = (32 - shaded.height) // 2
    icon.paste(shaded, (left, top), shaded)
    return icon, left, top, box


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


def frame(base, spot_center, radius, strength):
    """The torso with a red glow of this radius and strength over the appendix, kept inside the silhouette."""
    glow = Image.new("L", (32, 32), 0)
    ImageDraw.Draw(glow).ellipse(
        (spot_center[0] - radius, spot_center[1] - radius, spot_center[0] + radius, spot_center[1] + radius),
        fill=int(255 * strength))
    glow = glow.filter(ImageFilter.GaussianBlur(1.2))

    result = base.copy()
    px, gp = result.load(), glow.load()
    for y in range(32):
        for x in range(32):
            r, g, b, a = px[x, y]
            if a and (r, g, b, a) != OUTLINE:
                t = gp[x, y] / 255
                px[x, y] = (int(r + (230 - r) * t), int(g + (20 - g) * t), int(b + (20 - b) * t), a)
    return result


def main():
    icon, left, top, box = torso()
    base = outline(icon)

    # Low on the right side of the belly: the viewer's left, a little above the bottom of the torso.
    width, height = (box[2] - box[0]) * 2, (box[3] - box[1]) * 2
    spot = (left + width * 3 // 10, top + height * 8 // 10)

    frames = [frame(base, spot, r, s) for r, s in ((3, 0.75), (4, 0.95), (3, 0.75), (2, 0.55))]

    os.makedirs(OUT, exist_ok=True)
    sheet = Image.new("RGBA", (32 * len(frames), 32))
    for i, f in enumerate(frames):
        sheet.paste(f, (32 * i, 0))
    sheet.save(os.path.join(OUT, "appendicitis.png"))

    meta = {
        "version": 1,
        "license": "CC-BY-SA-3.0",
        "copyright": "Drawn by Tools/_EternalWar/make_organs_rsi.py from the human torso of "
                     "Mobs/Species/Human/parts.rsi (tgstation, modified by DrSmugleaf)",
        "size": {"x": 32, "y": 32},
        "states": [{"name": "appendicitis", "delays": [[0.4, 0.4, 0.4, 0.4]]}],
    }
    with open(os.path.join(OUT, "meta.json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=4)


if __name__ == "__main__":
    main()
