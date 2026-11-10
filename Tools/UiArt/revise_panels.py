"""One-off art revision of three gameplay sprites written by build_ui.py.

- Adventure/AdventureFrame.png: the right panel is narrowed from 600 to 400 pixels. Each band of rows (hero, the former
  warhost box, the mission box, the buttons, the bottom edge) drops 200 columns of its own plain middle, the warhost's
  slots are painted over (the box and the mission box below it become the mission card) and the ride button is removed
  (the end day box is widened instead). The map viewport grows into the freed columns.
  Later steps erase the leftover lines of the mission card (it is one piece: text above, image below).
- Victory/VictoryFrame.png: rows are inserted below the losses line, so the casualties fit inside the frame.
- DialogueBox/DialogueBoxFrame.png: the portrait painted into the frame is covered, so only the speaker's portrait shows.

Each step checks whether it already ran, so running the script twice changes nothing. Run build_ui.py first if the
sprites are rebuilt from the mockups, then this script. The project works without it: delete it and its mention in
Tools/README.md.

    python revise_panels.py
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
SPRITES = os.path.join(ROOT, "Assets", "HeroesOfMetalAge", "Content", "UI", "Sprites", "Gameplay")

PANEL_LEFT = 1302      # the right panel's left border starts here, right after the map viewport
VIEWPORT_TOP = 82
VIEWPORT_BOTTOM = 996
CUT = 200              # columns removed from the panel

VICTORY_SCALE = 0.78   # layout units per source pixel of the victory frame (screens_victory.py)
VICTORY_INSERT_ROW = 790
VICTORY_INSERT_LAYOUT = 236
VICTORY_INNER = (66, 860)   # the frame's dark inside, in source columns


def path(*parts):
    return os.path.join(SPRITES, *parts)


def narrow_adventure():
    file = path("Adventure", "AdventureFrame.png")
    image = np.array(Image.open(file).convert("RGBA"))
    if image[500, PANEL_LEFT + 100, 3] == 0:
        print("AdventureFrame already narrowed")
        return

    source = image.copy()
    # (rows, kept column intervals from the panel's left border to the screen edge); a stretched interval is
    # (start, end, width)
    bands = [
        ((VIEWPORT_TOP, 345), [(1302, 1600), (1800, 1920)]),
        ((345, 604), [(1302, 1400), (1500, 1730), (1830, 1920)]),
        ((604, 848), [(1302, 1340), (1540, 1920)]),
        ((848, 955), [(1302, 1340), (1612, 1700), (1700, 1760, 132), (1760, 1920)]),
        ((955, VIEWPORT_BOTTOM), [(1302, 1400), (1600, 1920)]),
    ]
    for (top, bottom), intervals in bands:
        pieces = []
        for interval in intervals:
            start, end = interval[0], interval[1]
            piece = source[top:bottom, start:end]
            if len(interval) == 3:
                piece = np.array(Image.fromarray(piece).resize((interval[2], bottom - top), Image.LANCZOS))
            pieces.append(piece)

        row = np.concatenate(pieces, axis=1)
        if row.shape[1] != 1920 - PANEL_LEFT - CUT:
            raise ValueError("band %d-%d is %d wide" % (top, bottom, row.shape[1]))

        image[top:bottom, PANEL_LEFT + CUT:] = row

    image[VIEWPORT_TOP:VIEWPORT_BOTTOM, PANEL_LEFT:PANEL_LEFT + CUT] = 0
    # the warhost slots (now at 1540-1882) are covered by the mission box's plain dark interior
    patch = source[690:690 + 140, 1352:1352 + 342]
    image[418:558, 1540:1882] = patch
    Image.fromarray(image).save(file)
    print("AdventureFrame narrowed")


def extend_victory():
    file = path("Victory", "VictoryFrame.png")
    image = Image.open(file).convert("RGBA")
    if image.height != 947:
        print("VictoryFrame already extended")
        return

    rows = round(VICTORY_INSERT_LAYOUT / VICTORY_SCALE)
    source = np.array(image)
    # the side borders run straight down, so repeating one band of rows continues them
    band = source[VICTORY_INSERT_ROW:VICTORY_INSERT_ROW + 24]
    strips = []
    flip = False
    while sum(strip.shape[0] for strip in strips) < rows:
        strips.append(band[::-1] if flip else band)
        flip = not flip

    filler = np.concatenate(strips, axis=0)[:rows]
    # inside the borders a repeated band would stretch the texture and the erased text's faint traces into streaks: the
    # dark inside becomes the rows' colour, smoothed sideways (it keeps the shaded edges), with a fine grain
    left, right = VICTORY_INNER
    around = source[VICTORY_INSERT_ROW - 12:VICTORY_INSERT_ROW + 12, left:right, :3].astype(float).mean(axis=0)
    kernel = np.ones(31) / 31.0
    padded = np.pad(around, ((15, 15), (0, 0)), mode="edge")
    smooth = np.stack([np.convolve(padded[:, channel], kernel, mode="valid") for channel in range(3)], axis=1)
    grain = np.random.default_rng(7).normal(0.0, 2.2, (rows, right - left, 1))
    inside = np.clip(smooth[np.newaxis, :, :] + grain, 0, 255).astype(np.uint8)
    filler[:, left:right, :3] = inside
    filler[:, left:right, 3] = 255
    result = np.concatenate([source[:VICTORY_INSERT_ROW], filler, source[VICTORY_INSERT_ROW:]], axis=0)
    Image.fromarray(result).save(file)
    print("VictoryFrame extended by %d rows to %d" % (rows, result.shape[0]))


def cover_dialogue_portrait():
    file = path("DialogueBox", "DialogueBoxFrame.png")
    image = np.array(Image.open(file).convert("RGBA"))
    # the portrait slot (PortraitImage in DialogueBox.json, 1.6667 source pixels per layout unit)
    left, top, right, bottom = 86, 66, 442, 428
    slot = image[top:bottom, left:right]
    if np.abs(slot[:, :, :3].astype(int) - slot[:, :, :3].mean(axis=(0, 1)).astype(int)).max() < 2:
        print("DialogueBoxFrame portrait already covered")
        return

    image[top:bottom, left:right, :3] = (24, 22, 20)
    image[top:bottom, left:right, 3] = 255
    Image.fromarray(image).save(file)
    print("DialogueBoxFrame portrait covered")


def erase_mission_line():
    """The warhost box's top edge, left in the middle of the mission card: blended from the rows above and below."""
    file = path("Adventure", "AdventureFrame.png")
    image = np.array(Image.open(file).convert("RGBA"))
    top, bottom, left, right = 656, 662, 1536, 1662
    # the line's brightest row against the dark rows under it
    if image[659, 1545:1630, :3].mean() < image[663:668, 1545:1630, :3].mean() + 15:
        print("AdventureFrame mission line already erased")
        return

    above = image[top - 1, left:right, :3].astype(float)
    below = image[bottom, left:right, :3].astype(float)
    grain = np.random.default_rng(11).normal(0.0, 1.5, (bottom - top, right - left, 1))
    for row in range(top, bottom):
        t = (row - top + 1) / float(bottom - top + 1)
        line = above * (1.0 - t) + below * t
        image[row, left:right, :3] = np.clip(line + grain[row - top], 0, 255).astype(np.uint8)

    Image.fromarray(image).save(file)
    print("AdventureFrame mission line erased")


MISSION_DIVIDER_GEM = (1713, 585)   # the red gem in the middle of the divider between the mission text and its image


def erase_mission_divider():
    """
    The ornate divider (gold rod, red gem, side brackets) between the mission card's text box and its image: the card is
    one piece. The side brackets are covered by the plain frame border above them; between the borders the text box fades
    into the image, without the box's bottom edge.
    """
    file = path("Adventure", "AdventureFrame.png")
    image = np.array(Image.open(file).convert("RGBA"))
    x, y = MISSION_DIVIDER_GEM
    red, green = int(image[y, x, 0]), int(image[y, x, 1])
    if red < 90 or red < 2 * green:
        print("AdventureFrame mission divider already erased")
        return

    source = image.copy()
    for left, right in [(1500, 1541), (1884, 1920)]:
        image[545:640, left:right] = source[445:540, left:right]

    left, right, top, bottom = 1541, 1884, 552, 612
    kernel = np.ones(9) / 9.0

    def band(rows):
        mean = rows[:, :, :3].astype(float).mean(axis=0)
        padded = np.pad(mean, ((4, 4), (0, 0)), mode="edge")
        return np.stack([np.convolve(padded[:, channel], kernel, mode="valid") for channel in range(3)], axis=1)

    above = band(source[top - 7:top, left:right])
    below = band(source[bottom:bottom + 7, left:right])
    grain = np.random.default_rng(13).normal(0.0, 1.6, (bottom - top, right - left, 1))
    for index, row in enumerate(range(top, bottom)):
        t = (index + 1) / float(bottom - top + 1)
        t = t * t * (3.0 - 2.0 * t)
        image[row, left:right, :3] = np.clip(above * (1.0 - t) + below * t + grain[index], 0, 255).astype(np.uint8)

    image[top:bottom, left:right, 3] = 255
    Image.fromarray(image).save(file)
    print("AdventureFrame mission divider erased")


VICTORY_ICONS = (290, 645, 356, 770)   # the experience and losses icons, in source pixels


def erase_victory_icons():
    """The victory screen's first step shows only the casualties: the icons are covered by the frame's mirrored right side."""
    file = path("Victory", "VictoryFrame.png")
    image = np.array(Image.open(file).convert("RGBA"))
    left, top, right, bottom = VICTORY_ICONS
    if image[top:bottom, left:right, :3].max() < 110:
        print("VictoryFrame icons already erased")
        return

    width = image.shape[1]
    mirrored = image[top:bottom, width - right:width - left][:, ::-1]
    image[top:bottom, left:right] = mirrored
    Image.fromarray(image).save(file)
    print("VictoryFrame icons erased")


DEFEAT_TOP = 418           # the skull medallion's top; above it only the victorious hero stands
BORDER_TOP = 455           # the frame's top border, under the hero
MEDALLION = (463, 481, 57) # centre and radius of the skull medallion
BUTTON_SLOT = (280, 1108, 648, 1177)   # the painted slot of the victory's single button


def build_defeat_frame():
    """The defeat screen uses the victory frame without the hero and without the single button's slot (it has three)."""
    source = np.array(Image.open(path("Victory", "VictoryFrame.png")).convert("RGBA"))
    image = source.copy()
    ys, xs = np.mgrid[0:image.shape[0], 0:image.shape[1]]
    cx, cy, radius = MEDALLION
    hero = (ys < BORDER_TOP) & ((xs - cx) ** 2 + (ys - cy) ** 2 > radius ** 2)
    image[hero] = 0

    # the slot is covered by the rows above it, mirrored, so the texture runs on without a seam
    left, top, right, bottom = BUTTON_SLOT
    rows = bottom - top
    image[top:bottom, left:right] = source[top - rows:top, left:right][::-1]
    Image.fromarray(image[DEFEAT_TOP:]).save(path("Dialog", "DefeatFrame.png"))
    print("DefeatFrame built from the victory frame")


def cover_combat_portrait():
    """The war council's portrait slot held the mockup's figure; it becomes a plain red backdrop for any unit's figure."""
    file = path("Combat", "CombatFrame.png")
    image = np.array(Image.open(file).convert("RGBA"))
    left, top, right, bottom = 1367, 219, 1520, 381
    slot = image[top:bottom, left:right, :3].astype(int)
    height, width = bottom - top, right - left
    ys, xs = np.mgrid[0:height, 0:width]
    distance = np.sqrt(((xs - width / 2.0) / (width / 2.0)) ** 2 + ((ys - height * 0.45) / (height / 2.0)) ** 2)
    t = np.clip(distance / 1.25, 0.0, 1.0)[:, :, np.newaxis] ** 1.4
    centre = np.array([104.0, 20.0, 12.0])
    edge = np.array([38.0, 6.0, 3.0])
    grain = np.random.default_rng(5).normal(0.0, 2.0, (height, width, 1))
    backdrop = np.clip(centre * (1.0 - t) + edge * t + grain, 0, 255)
    if np.abs(slot[40:120, 40:110] - backdrop[40:120, 40:110].astype(int)).mean() < 2:
        print("CombatFrame portrait already covered")
        return

    # the frame's gold corner pieces reach into the slot: they are kept
    corner = 15
    gold = (slot[:, :, 0] > 100) & (slot[:, :, 1] > 60) & (slot[:, :, 2] < slot[:, :, 0] * 0.7)
    near_corner = ((xs < corner) | (xs >= width - corner)) & ((ys < corner) | (ys >= height - corner))
    keep = gold & near_corner
    region = image[top:bottom, left:right]
    region[~keep, :3] = backdrop[~keep].astype(np.uint8)
    region[:, :, 3] = 255
    Image.fromarray(image).save(file)
    print("CombatFrame portrait covered")


if __name__ == "__main__":
    narrow_adventure()
    extend_victory()
    cover_dialogue_portrait()
    erase_mission_line()
    erase_mission_divider()
    erase_victory_icons()
    build_defeat_frame()
    cover_combat_portrait()
