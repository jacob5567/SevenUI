#!/usr/bin/env python3
"""Renders the damage number sheet (512x512) in JetBrains Mono ExtraBold, in the game's layout and colors.

The game's damage number shader (HGDamageNumber) picks characters from a 4x4 grid of 128px cells: 0-9, two empty
cells, then K, M, B and !. Each glyph is white (tinted by the damage color) with a grey outline and a dark edge.

The shader lays the characters side by side on a particle four cells wide, so each character takes a square on
screen. The game's pixel digits are square; these are narrower, and would sit far apart. So each glyph is drawn in
a cell CELL_WIDTH wide, its natural shape, then stretched to fill the 128px cell; DamageNumberFont narrows the
particle by CELL_WIDTH / CELL to match, which squeezes the glyphs back and puts them CELL_WIDTH apart.

Usage: make_damage_numbers.py [out.png]
With no output path it overwrites textures/DamageNumbers.png.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

CELL = 128
# The natural width of a cell: the widest glyph with its outlines (M) is 97px, and this leaves its neighbors' edges
# a little apart. Must match DamageNumberFont.CellAspect (CELL_WIDTH / CELL).
CELL_WIDTH = 100
GRID = 4
SS = 2  # supersampling factor
CHARS = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", None, None, "K", "M", "B", "!"]
# The game's sheet puts "!" at the left of its cell, about 50px wide; the shader may give it a narrower advance, so
# it keeps that footprint on the sheet rather than being stretched.
NARROW = {"!"}
NARROW_WIDTH = 50

FILL = (255, 255, 255, 255)
OUTLINE = (90, 97, 107, 255)  # the game's grey outline
EDGE = (0, 0, 0, 201)  # the game's dark edge
OUTLINE_WIDTH = 8
EDGE_WIDTH = 6
# Room left around a glyph's outlines inside its cell.
MARGIN = 2

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_OUT = os.path.join(HERE, "..", "textures", "DamageNumbers.png")
FONT = os.path.join(HERE, "..", "fonts", "JetBrainsMono-ExtraBold.ttf")


def dilate(mask, radius):
    """Grows a mask by radius pixels in every direction (a disk), keeping its antialiased edge."""
    if radius <= 0:
        return mask
    src = np.asarray(mask)
    out = src.copy()
    r = int(round(radius))
    padded = np.pad(src, r)
    h, w = src.shape
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if dx * dx + dy * dy <= r * r:
                np.maximum(out, padded[r + dy:r + dy + h, r + dx:r + dx + w], out=out)
    return Image.fromarray(out)


def cell_width(char):
    """How wide the glyph's cell is drawn before it is fitted to the sheet's cell."""
    return CELL if char in NARROW else CELL_WIDTH


def glyph_mask(char, font):
    """The glyph's ink on a supersampled cell, centered (or at the left, if narrow) on the digits' baseline."""
    width, height = cell_width(char) * SS, CELL * SS
    # Drawn with room on both sides, then centered.
    mask = Image.new("L", (width * 2, height), 0)
    d = ImageDraw.Draw(mask)
    _, ref_top, _, ref_bottom = d.textbbox((0, 0), "0", font=font)
    y = (height - (ref_bottom - ref_top)) / 2 - ref_top
    d.text((width / 2, y), char, font=font, fill=255)
    # Centered on its ink, which the font's bounding box only approximates.
    columns = np.nonzero(np.asarray(mask).any(0))[0]
    span = NARROW_WIDTH * SS if char in NARROW else width
    shift = int(round((span - (columns[-1] + 1 - columns[0])) / 2 - columns[0]))
    centered = Image.new("L", (width, height), 0)
    centered.paste(mask, (shift, 0))
    return centered


def check_fits(char, layer):
    """Fails if the glyph's outlines reach the side of its cell, where they would be cut off."""
    columns = np.nonzero(np.asarray(layer).any(0))[0]
    if len(columns) and (columns[0] == 0 and char not in NARROW or columns[-1] >= layer.size[0] - 1):
        sys.exit(f"{char!r} doesn't fit in a {cell_width(char)}px cell; raise CELL_WIDTH")


def render_cell(char, font):
    mask = glyph_mask(char, font)
    outline = dilate(mask, OUTLINE_WIDTH * SS)
    edge = dilate(outline, EDGE_WIDTH * SS)
    check_fits(char, edge)
    cell = Image.new("RGBA", mask.size, (0, 0, 0, 0))
    for color, layer in ((EDGE, edge), (OUTLINE, outline), (FILL, mask)):
        solid = Image.new("RGBA", mask.size, color)
        alpha = np.asarray(layer, dtype=np.float32) / 255 * (color[3] / 255)
        solid.putalpha(Image.fromarray((alpha * 255).astype(np.uint8)))
        cell.alpha_composite(solid)
    return cell.resize((CELL, CELL), Image.LANCZOS)


def main(argv):
    out = argv[0] if argv else DEFAULT_OUT
    # A digit plus both outlines, top and bottom, fills the cell's height.
    cap_height = CELL - 2 * (OUTLINE_WIDTH + EDGE_WIDTH + MARGIN)
    probe = ImageFont.truetype(FONT, 100 * SS)
    _, top, _, bottom = ImageDraw.Draw(Image.new("L", (1, 1))).textbbox((0, 0), "0", font=probe)
    font = ImageFont.truetype(FONT, int(100 * SS * cap_height * SS / (bottom - top)))
    sheet = Image.new("RGBA", (CELL * GRID, CELL * GRID), (0, 0, 0, 0))
    for index, char in enumerate(CHARS):
        if char is None:
            continue
        sheet.alpha_composite(render_cell(char, font), ((index % GRID) * CELL, (index // GRID) * CELL))
    sheet.save(out)
    print(f"Wrote {out}")


if __name__ == "__main__":
    main(sys.argv[1:])
