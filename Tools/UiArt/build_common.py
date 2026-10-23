"""Shared UI pieces (button glow, check mark) and the adventure map's placeholder object tokens and figures."""
import os
import re

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

from build_ui import segment
from uiart import load

PROJECT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "HeroesOfMetalAge")).replace("\\", "/")
COMMON = PROJECT + "/Content/UI/Sprites/Gameplay/Common"
OBJECTS = PROJECT + "/Content/World/MapObjects"
SCRATCH = os.path.join(os.environ.get("TEMP", "/tmp"), "homa-ui-debug")
os.makedirs(SCRATCH, exist_ok=True)
GOLD = (236, 196, 112)
GOLD_DARK = (150, 98, 36)


def glow():
    """A soft golden glow over a button while hovered or pressed (Button colour transition on its alpha)."""
    w, h = 256, 96
    image = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((10, 10, w - 10, h - 10), radius=14, fill=(255, 205, 120, 120), outline=(255, 228, 160, 230), width=3)
    image = image.filter(ImageFilter.GaussianBlur(4))
    image.save(COMMON + "/ButtonGlow.png")


def check():
    """A gold check mark with a dark outline, for completed goals and the mute toggle."""
    s = 96
    image = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    points = [(14, 52), (38, 76), (84, 18)]
    draw.line(points, fill=(20, 10, 4, 255), width=20, joint="curve")
    draw.line(points, fill=GOLD + (255,), width=12, joint="curve")
    image.save(COMMON + "/CheckMark.png")


def glyph_font():
    css = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "icons", "rpg-awesome.css"), encoding="utf-8").read()
    glyphs = dict(re.findall(r'\.ra-([a-z0-9-]+):before\s*\{\s*content:\s*"(.)"', css))
    return glyphs, ImageFont.truetype(os.path.join(os.path.dirname(os.path.abspath(__file__)), "icons", "rpgawesome.ttf"), 92)


def medallion(size, rim):
    """A bronze medallion with a dark enamel middle and four rivets; rim is the ring colour."""
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    c = size / 2
    draw.ellipse((4, 8, size - 4, size), fill=(0, 0, 0, 150))
    for r, colour in ((c - 6, (60, 36, 14)), (c - 9, rim), (c - 15, tuple(int(v * 0.55) for v in rim)), (c - 19, (28, 22, 18)), (c - 24, (38, 30, 24))):
        draw.ellipse((c - r, c - r - 2, c + r, c + r - 2), fill=colour + (255,))
    for angle in (45, 135, 225, 315):
        x = c + (c - 12) * np.cos(np.radians(angle))
        y = c - 2 + (c - 12) * np.sin(np.radians(angle))
        draw.ellipse((x - 4, y - 4, x + 4, y + 4), fill=(240, 210, 140, 255), outline=(70, 40, 14, 255))
    return image


def token(name, glyph, rim=(184, 128, 58), glyph_colour=GOLD, size=192):
    glyphs, font = glyph_font()
    image = medallion(size, rim)
    layer = Image.new("RGBA", image.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    text = glyphs[glyph] if glyph in glyphs else glyph
    box = draw.textbbox((0, 0), text, font=font)
    x = (size - (box[2] - box[0])) / 2 - box[0]
    y = (size - (box[3] - box[1])) / 2 - box[1] - 2
    for dx in (-3, 0, 3):
        for dy in (-3, 0, 3):
            draw.text((x + dx, y + dy), text, font=font, fill=(16, 8, 2, 255))
    draw.text((x, y), text, font=font, fill=glyph_colour + (255,))
    image.alpha_composite(layer)
    image.save(os.path.join(OBJECTS, name + ".png"))


def chest_token(size=192):
    """The chest is drawn: the icon font has none."""
    image = medallion(size, (184, 128, 58))
    draw = ImageDraw.Draw(image)
    c = size / 2
    body = (c - 46, c - 6, c + 46, c + 40)
    lid = (c - 46, c - 40, c + 46, c + 4)
    for outline, fill in ((8, (16, 8, 2)), (0, None)):
        o = outline
        draw.rounded_rectangle((body[0] - o, body[1] - o, body[2] + o, body[3] + o), radius=6, fill=(16, 8, 2) if fill else GOLD_DARK)
        draw.pieslice((lid[0] - o, lid[1] - o, lid[2] + o, lid[3] + 40 + o), 180, 360, fill=(16, 8, 2) if fill else GOLD)
    draw.rectangle((c - 46, c - 6, c + 46, c + 2), fill=(90, 54, 20))
    draw.rectangle((c - 7, c - 14, c + 7, c + 12), fill=(240, 220, 160), outline=(30, 16, 4), width=2)
    for x in (c - 30, c + 30):
        draw.rectangle((x - 4, c - 34, x + 4, c + 40), fill=(110, 70, 30))
    image.save(os.path.join(OBJECTS, "Chest.png"))


def figures():
    """Figures cut from the world map mockup: the horned demon guards the maiden as the minotaur, the warrior woman is the
    chained maiden."""
    image = load("01-world-map-main-ui.png")
    demon = segment(image, (8, 668, 175, 868), [(30, 720, 110, 860)], [(165, 660, 175, 860), (140, 660, 175, 700), (0, 668, 8, 868)])
    save_figure(image, demon, (8, 668, 175, 868), "Minotaur")
    maiden = segment(image, (1470, 705, 1672, 936), [(1580, 760, 1640, 900)],
                     [(1440, 700, 1500, 860), (1440, 700, 1672, 712), (1650, 700, 1672, 760), (1440, 700, 1560, 760)])
    save_figure(image, maiden, (1470, 705, 1672, 936), "ChainedMaiden")


def save_figure(image, mask, box, name):
    x0, y0, x1, y1 = box
    figure = image[y0:y1, x0:x1].copy()
    figure[:, :, 3] = np.minimum(figure[:, :, 3], mask[y0:y1, x0:x1])
    ys, xs = np.where(figure[:, :, 3] > 0)
    figure = figure[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    Image.fromarray(figure).save(os.path.join(OBJECTS, name + ".png"))


def main():
    os.makedirs(COMMON, exist_ok=True)
    os.makedirs(OBJECTS, exist_ok=True)
    glow()
    check()
    token("ArtefactCache", "relic-blade")
    token("Colosseum", "arena")
    token("IronBastion", "heavy-shield")
    token("ShrineOfHorns", "horns")
    token("Mill", "cog-wheel")
    token("RoadieCamp", "campfire")
    token("QuestBearer", "hood")
    token("RedTent", "metal-gate", rim=(170, 30, 24), glyph_colour=(250, 120, 100))
    token("Oracle", "dragon")
    token("ExitPortal", "fire-ring", rim=(200, 80, 20), glyph_colour=(255, 170, 60))
    token("Prison", "chain")
    chest_token()
    figures()
    gold = np.array(Image.open(PROJECT + "/Content/UI/Sprites/Gameplay/Rewards/GoldPile.png").convert("RGBA"))
    # the coins on the dark panel: brightness and warmth decide what stays
    rgb = gold[:, :, :3].astype(np.float32)
    lum = rgb @ np.array([0.299, 0.587, 0.114], np.float32)
    warmth = rgb[:, :, 0] - rgb[:, :, 2]
    alpha = np.clip((lum - 38) / 30, 0, 1) * np.clip((warmth - 10) / 25, 0, 1)
    alpha = cv2.GaussianBlur(alpha, (3, 3), 0)
    gold[:, :, 3] = (alpha * 255).astype(np.uint8)
    Image.fromarray(gold).save(os.path.join(OBJECTS, "GoldPile.png"))
    Image.fromarray(gold).save(PROJECT + "/Content/UI/Sprites/Gameplay/Rewards/GoldPile.png")
    print("common sprites and map objects written")


if __name__ == "__main__":
    main()
