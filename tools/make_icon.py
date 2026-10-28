#!/usr/bin/env python3
"""Renders the SevenUI icon (256x256): pieces of the HUD in the mod's own style, on a navy background.

Usage: make_icon.py [--option 1..8] [out.png]
The option defaults to 7, the one Jacob chose. With no output path it overwrites thunderstore/icon.png.
"""
import os
import sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

SIZE = 256
SS = 4  # supersampling factor
W = SIZE * SS

BACKGROUND = (8, 10, 22, 255)
PANEL = (0, 0, 0, 115)  # the HUD's 45% black panels
HEADER = (255, 255, 255, 16)
ACCENT = (184, 196, 255)  # #b8c4ff
TEXT = (231, 233, 240)  # #e7e9f0
SECONDARY = (169, 174, 187)  # #a9aebb
TROUGH = (58, 63, 76)  # #3a3f4c
HEALTH = (111, 191, 115)  # #6fbf73
SHIELD = (95, 163, 224)  # #5fa3e0
ICON_FILL = (90, 96, 112)  # #5a6070

DEFAULT_OPTION = 7
HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_OUT = os.path.join(HERE, "..", "thunderstore", "icon.png")
CONDENSED = os.path.join(HERE, "..", "fonts", "BarlowCondensed-SemiBold.ttf")
SEMIBOLD = os.path.join(HERE, "..", "fonts", "Barlow-SemiBold.ttf")
NAME = "SEVENUI"


def s(v):
    return int(round(v * SS))


def box(d, x0, y0, x1, y1, fill):
    d.rectangle([s(x0), s(y0), s(x1) - 1, s(y1) - 1], fill=fill)


def layer():
    return Image.new("RGBA", (W, W), (0, 0, 0, 0))


def glow(art, shape, color, blur, strength):
    """A soft halo behind a shape, from the shape's own alpha."""
    alpha = shape.getchannel("A").filter(ImageFilter.GaussianBlur(s(blur))).point(lambda v: int(v * strength))
    halo = Image.new("RGBA", (W, W), color + (0,))
    halo.putalpha(alpha)
    art.alpha_composite(halo)


def text_center(d, cx, cy, string, font, fill):
    x0, y0, x1, y1 = d.textbbox((0, 0), string, font=font)
    d.text((s(cx) - (x0 + x1) / 2, s(cy) - (y0 + y1) / 2), string, font=font, fill=fill)


def tracked_center(d, cx, cy, string, font, fill, tracking):
    """Spaced capitals like the HUD's header strips: each letter drawn on its own, tracking in icon pixels."""
    advances = [font.getlength(c) for c in string]
    width = sum(advances) + s(tracking) * (len(string) - 1)
    _, y0, _, y1 = d.textbbox((0, 0), string, font=font)
    x = s(cx) - width / 2
    for c, advance in zip(string, advances):
        d.text((x, s(cy) - (y0 + y1) / 2), c, font=font, fill=fill)
        x += advance + s(tracking)


def health_bar(d, x0, y, x1, h, health, shield):
    box(d, x0, y, x1, y + h, TROUGH)
    hx = x0 + (x1 - x0) * health
    box(d, x0, y, hx, y + h, HEALTH)
    box(d, hx, y, hx + (x1 - x0) * shield, y + h, SHIELD)


def option_skills(art):
    """Four skill squares, one on cooldown, over a health bar: the bottom corners of the HUD."""
    shapes = layer()
    d = ImageDraw.Draw(shapes)
    size, gap = 46, 10
    left = (SIZE - (4 * size + 3 * gap)) / 2
    top = 74
    for i in range(4):
        x = left + i * (size + gap)
        box(d, x, top, x + size, top + size, TROUGH)
        box(d, x + 9, top + 9, x + size - 9, top + size - 9, ICON_FILL)
    # The second skill is cooling down: a dark cover over its top part, with the seconds left.
    x = left + size + gap
    box(d, x, top, x + size, top + 30, (10, 12, 16, 200))
    text_center(d, x + size / 2, top + 21, "3", ImageFont.truetype(CONDENSED, s(30)), TEXT)
    health_bar(d, left, 150, SIZE - left, 18, 0.62, 0.18)
    box(d, left, 174, left + (SIZE - 2 * left) * 0.7, 178, ACCENT)
    glow(art, shapes, ACCENT, 10, 0.35)
    art.alpha_composite(shapes)


def option_seven(art):
    """A big Barlow Condensed 7 with a health bar under it."""
    shapes = layer()
    d = ImageDraw.Draw(shapes)
    text_center(d, SIZE / 2, 112, "7", ImageFont.truetype(CONDENSED, s(190)), ACCENT)
    health_bar(d, 60, 192, SIZE - 60, 16, 0.66, 0.16)
    glow(art, shapes, ACCENT, 12, 0.4)
    art.alpha_composite(shapes)


def option_panel(art):
    """An objective panel: header strip, an objective with its charge bar, a checkbox line."""
    shapes = layer()
    d = ImageDraw.Draw(shapes)
    x0, x1 = 40, SIZE - 40
    box(d, x0, 56, x1, 200, PANEL)
    box(d, x0, 56, x1, 84, HEADER)
    box(d, x0 + 14, 66, x0 + 92, 74, SECONDARY)
    box(d, x0 + 14, 100, x0 + 120, 112, TEXT)
    box(d, x1 - 46, 100, x1 - 14, 112, ACCENT)
    box(d, x0 + 14, 124, x1 - 14, 130, TROUGH)
    box(d, x0 + 14, 124, x0 + 14 + (x1 - x0 - 28) * 0.64, 130, ACCENT)
    box(d, x0 + 14, 152, x0 + 30, 168, SECONDARY)
    box(d, x0 + 18, 156, x0 + 26, 164, BACKGROUND)
    box(d, x0 + 40, 154, x0 + 134, 166, TEXT)
    glow(art, shapes, ACCENT, 10, 0.25)
    art.alpha_composite(shapes)


def option_type(art):
    """A type specimen, Aa in Barlow SemiBold, on a panel with a header strip."""
    shapes = layer()
    d = ImageDraw.Draw(shapes)
    box(d, 36, 52, SIZE - 36, SIZE - 52, PANEL)
    box(d, 36, 52, SIZE - 36, 78, HEADER)
    box(d, 50, 61, 120, 69, SECONDARY)
    text_center(d, SIZE / 2, 138, "Aa", ImageFont.truetype(SEMIBOLD, s(104)), TEXT)
    box(d, 50, 182, SIZE - 50, 188, TROUGH)
    box(d, 50, 182, 50 + (SIZE - 100) * 0.55, 188, ACCENT)
    glow(art, shapes, ACCENT, 10, 0.25)
    art.alpha_composite(shapes)


def option_header_seven(art):
    """A HUD panel whose header strip carries the name, with a big 7 and a health bar in its body."""
    shapes = layer()
    d = ImageDraw.Draw(shapes)
    x0, x1, y0, y1 = 36, SIZE - 36, 36, SIZE - 36
    box(d, x0, y0, x1, y1, PANEL)
    box(d, x0, y0, x1, y0 + 36, HEADER)
    tracked_center(d, SIZE / 2, y0 + 18, NAME, ImageFont.truetype(SEMIBOLD, s(19)), SECONDARY, 5)
    text_center(d, SIZE / 2, 126, "7", ImageFont.truetype(CONDENSED, s(118)), ACCENT)
    health_bar(d, x0 + 20, 182, x1 - 20, 14, 0.64, 0.16)
    glow(art, shapes, ACCENT, 10, 0.25)
    art.alpha_composite(shapes)


def option_stacked(art):
    """SEVEN over a UI tag, in Barlow Condensed, with the experience bar under it."""
    shapes = layer()
    d = ImageDraw.Draw(shapes)
    text_center(d, SIZE / 2, 98, "SEVEN", ImageFont.truetype(CONDENSED, s(92)), TEXT)
    box(d, 86, 146, SIZE - 86, 198, ACCENT)
    text_center(d, SIZE / 2, 172, "UI", ImageFont.truetype(CONDENSED, s(50)), BACKGROUND[:3])
    glow(art, shapes, ACCENT, 10, 0.3)
    art.alpha_composite(shapes)


def option_wordmark(art):
    """SevenUI on one line, the UI in the accent, between a header strip and a health bar, with room either side."""
    shapes = layer()
    d = ImageDraw.Draw(shapes)
    font = ImageFont.truetype(SEMIBOLD, s(48))
    x0, x1 = 22, SIZE - 22
    box(d, x0, 70, x1, SIZE - 70, PANEL)
    box(d, x0, 70, x1, 84, HEADER)
    seven = font.getlength("Seven")
    ix0, iy0, ix1, iy1 = d.textbbox((0, 0), "SevenUI", font=font)
    # Centered by the letters' ink, not their advance, so the padding is even on both sides.
    x = s(SIZE / 2) - (ix0 + ix1) / 2
    y = s(126) - (iy0 + iy1) / 2
    d.text((x, y), "Seven", font=font, fill=TEXT)
    d.text((x + seven, y), "UI", font=font, fill=ACCENT)
    # The health bar spans the same width as the name.
    health_bar(d, (x + ix0) / SS, 160, (x + ix1) / SS, 12, 0.6, 0.2)
    glow(art, shapes, ACCENT, 10, 0.25)
    art.alpha_composite(shapes)


def option_monogram(art):
    """A 7UI monogram: the 7 in the accent, UI in white, with the name spelled out in spaced caps below."""
    shapes = layer()
    d = ImageDraw.Draw(shapes)
    font = ImageFont.truetype(CONDENSED, s(140))
    seven = font.getlength("7")
    total = seven + font.getlength("UI")
    _, ty0, _, ty1 = d.textbbox((0, 0), "7UI", font=font)
    x = s(SIZE / 2) - total / 2
    y = s(100) - (ty0 + ty1) / 2
    d.text((x, y), "7", font=font, fill=ACCENT)
    d.text((x + seven, y), "UI", font=font, fill=TEXT)
    box(d, 48, 168, SIZE - 48, 172, ACCENT)
    tracked_center(d, SIZE / 2, 194, NAME, ImageFont.truetype(SEMIBOLD, s(17)), SECONDARY, 6)
    glow(art, shapes, ACCENT, 10, 0.3)
    art.alpha_composite(shapes)


OPTIONS = {1: option_skills, 2: option_seven, 3: option_panel, 4: option_type,
           5: option_header_seven, 6: option_stacked, 7: option_wordmark, 8: option_monogram}


def render(option):
    art = Image.new("RGBA", (W, W), BACKGROUND)
    OPTIONS[option](art)
    return art.resize((SIZE, SIZE), Image.LANCZOS)


def main(argv):
    option = DEFAULT_OPTION
    out = DEFAULT_OUT
    args = list(argv)
    if args[:1] == ["--option"]:
        option = int(args[1])
        args = args[2:]
    if args:
        out = args[0]
    render(option).save(out)
    print(f"Wrote option {option} to {out}")


if __name__ == "__main__":
    main(sys.argv[1:])
