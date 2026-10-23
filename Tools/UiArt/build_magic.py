"""Placeholder art and sounds for hero growth and magic: secondary skill and spell icons, the open spell book, the spell shop
and luck shrine map tokens, and three synthesized cues (a spell cast, good luck, bad luck).

One-off generation tool: the project works without it once the files exist; replace the pictures with painted art (see
Docs/ART_REQUESTS.md) and delete this script. Needs Python 3 with Pillow and NumPy; the icon glyphs come from the RPG Awesome
font (SIL OFL, icons/LICENSE.md).
"""
import math
import os
import re
import wave

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "HeroesOfMetalAge")).replace("\\", "/")
SKILLS = PROJECT + "/Content/UI/Sprites/Skills"
SPELLS = PROJECT + "/Content/UI/Sprites/Spells"
BOOK = PROJECT + "/Content/UI/Sprites/SpellBook"
OBJECTS = PROJECT + "/Content/World/MapObjects"
AUDIO = PROJECT + "/Content/Audio"
GOLD = (236, 196, 112)
RATE = 44100

SKILL_GLYPHS = {
    "Archery": "archer", "Armorer": "eye-shield", "Offense": "crossed-swords", "Diplomacy": "speech-bubbles", "Estates": "gold-bar",
    "Intelligence": "light-bulb", "Leadership": "castle-flag", "Logistics": "horseshoe", "Luck": "clover", "Mysticism": "crystal-ball",
    "Pathfinding": "compass", "Resistance": "fire-shield", "Sorcery": "fairy-wand", "Tactics": "chessboard", "Wisdom": "book",
}
# rim colour by spell level, from bronze to violet
LEVEL_RIMS = {1: (184, 128, 58), 2: (170, 170, 180), 3: (200, 80, 30), 4: (60, 120, 200), 5: (150, 60, 200)}
SPELL_GLYPHS = {
    "magic-arrow": ("supersonic-arrow", 1), "cure": ("health-increase", 1), "bless": ("sun", 1), "curse": ("skull", 1),
    "haste": ("feather-wing", 1), "slow": ("snail", 1), "bloodlust": ("dripping-blade", 1), "stone-skin": ("rune-stone", 1),
    "shield": ("round-shield", 1), "lightning-bolt": ("lightning-bolt", 2), "weakness": ("broken-shield", 2), "fireball": ("fire-bomb", 3),
    "battle-hymn": ("horn-call", 4), "meteor-shower": ("burning-meteor", 4), "implosion": ("implosion", 5), "armageddon": ("explosion", 5),
}


def glyph_font(size=92):
    css = open(os.path.join(HERE, "icons", "rpg-awesome.css"), encoding="utf-8").read()
    glyphs = dict(re.findall(r'\.ra-([a-z0-9-]+):before\s*\{\s*content:\s*"(.)"', css))
    return glyphs, ImageFont.truetype(os.path.join(HERE, "icons", "rpgawesome.ttf"), size)


def medallion(size, rim):
    """A bronze medallion with a dark enamel middle and four rivets (the map objects' token style)."""
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    c = size / 2
    draw.ellipse((4, 8, size - 4, size), fill=(0, 0, 0, 150))
    for r, colour in ((c - 6, (60, 36, 14)), (c - 9, rim), (c - 15, tuple(int(v * 0.55) for v in rim)), (c - 19, (28, 22, 18)), (c - 24, (38, 30, 24))):
        draw.ellipse((c - r, c - r - 2, c + r, c + r - 2), fill=colour + (255,))
    for angle in (45, 135, 225, 315):
        x = c + (c - 12) * math.cos(math.radians(angle))
        y = c - 2 + (c - 12) * math.sin(math.radians(angle))
        draw.ellipse((x - 4, y - 4, x + 4, y + 4), fill=(240, 210, 140, 255), outline=(70, 40, 14, 255))
    return image


def token(path, glyph, rim=(184, 128, 58), glyph_colour=GOLD, size=192):
    glyphs, font = glyph_font()
    image = medallion(size, rim)
    layer = Image.new("RGBA", image.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    text = glyphs[glyph]
    box = draw.textbbox((0, 0), text, font=font)
    x = (size - (box[2] - box[0])) / 2 - box[0]
    y = (size - (box[3] - box[1])) / 2 - box[1] - 2
    for dx in (-3, 0, 3):
        for dy in (-3, 0, 3):
            draw.text((x + dx, y + dy), text, font=font, fill=(16, 8, 2, 255))
    draw.text((x, y), text, font=font, fill=glyph_colour + (255,))
    image.alpha_composite(layer)
    image.save(path)


def book(width=1400, height=860):
    """An open book: a dark leather cover, two parchment pages and the shadow of the spine."""
    rng = np.random.default_rng(7)
    cover = np.zeros((height, width, 4), np.float32)
    leather = np.array([70, 26, 18], np.float32) + rng.normal(0, 6, (height, width, 1))
    cover[:, :, :3] = leather
    cover[:, :, 3] = 255
    image = Image.fromarray(np.clip(cover, 0, 255).astype(np.uint8), "RGBA")
    mask = Image.new("L", (width, height), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, width - 1, height - 1), radius=36, fill=255)
    image.putalpha(mask)
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((8, 8, width - 9, height - 9), radius=30, outline=(190, 140, 60, 255), width=6)

    margin, spine = 44, 16
    for left in (True, False):
        x0 = margin if left else width // 2 + spine
        x1 = width // 2 - spine if left else width - margin
        w, h = x1 - x0, height - 2 * margin
        paper = np.empty((h, w, 3), np.float32)
        paper[:] = (226, 204, 160)
        paper += rng.normal(0, 5, (h, w, 1))
        xs = np.linspace(0, 1, w)[None, :, None]
        towards_spine = xs if left else 1 - xs
        # darker towards the spine and the edges, like a curved page
        paper *= 1 - 0.28 * towards_spine ** 6
        ys = np.linspace(-1, 1, h)[:, None, None]
        paper *= 1 - 0.08 * ys ** 8
        page = Image.fromarray(np.clip(paper, 0, 255).astype(np.uint8), "RGB").filter(ImageFilter.GaussianBlur(0.6))
        image.paste(page, (x0, margin))
        draw.rectangle((x0 + 18, margin + 18, x1 - 18, height - margin - 18), outline=(150, 100, 50, 255), width=2)
    draw.line((width // 2, margin - 6, width // 2, height - margin + 6), fill=(30, 12, 8, 255), width=6)
    image.save(BOOK + "/SpellBook.png")


def tone(seconds, voices, attack=0.01, release=0.3, noise=0.0, seed=1):
    t = np.arange(int(RATE * seconds)) / RATE
    signal = np.zeros_like(t)
    for frequency, gain, sweep in voices:
        phase = 2 * np.pi * (frequency * t + 0.5 * sweep * t * t)
        signal += gain * np.sin(phase)
    if noise:
        signal += noise * np.random.default_rng(seed).normal(0, 1, t.shape)
    envelope = np.minimum(1, t / attack) * np.exp(-np.maximum(0, t - attack) / release)
    signal *= envelope
    return signal / max(1e-6, np.max(np.abs(signal))) * 0.8


def save_wave(name, signal):
    data = (np.clip(signal, -1, 1) * 32767).astype(np.int16)
    with wave.open(AUDIO + "/" + name, "wb") as file:
        file.setnchannels(1)
        file.setsampwidth(2)
        file.setframerate(RATE)
        file.writeframes(data.tobytes())


def sounds():
    # a rising shimmer of fifths
    cast = sum(tone(1.1, [(f, 1.0, 600), (f * 1.5, 0.5, 900)], attack=0.05, release=0.35, noise=0.05, seed=i) for i, f in enumerate((440, 660, 880)))
    save_wave("spell_cast.wav", cast / np.max(np.abs(cast)) * 0.8)
    # two bright bell notes going up
    first = tone(0.5, [(1046, 1.0, 0), (2093, 0.4, 0)], release=0.18)
    second = tone(0.7, [(1568, 1.0, 0), (3136, 0.3, 0)], release=0.25)
    lucky = np.concatenate([first[: int(RATE * 0.12)], np.zeros(0)])
    lucky = np.concatenate([lucky, second])
    lucky[: first.size] += first[: lucky.size][: first.size] * 0.3
    save_wave("good_luck.wav", lucky / np.max(np.abs(lucky)) * 0.8)
    # a dull falling thud
    save_wave("bad_luck.wav", tone(0.8, [(180, 1.0, -150), (90, 0.6, -60)], attack=0.005, release=0.25, noise=0.15, seed=4))


def main():
    for folder in (SKILLS, SPELLS, BOOK, OBJECTS, AUDIO):
        os.makedirs(folder, exist_ok=True)
    for skill, glyph in SKILL_GLYPHS.items():
        token(SKILLS + "/" + skill + ".png", glyph, size=160)
    for spell, (glyph, level) in SPELL_GLYPHS.items():
        token(SPELLS + "/" + spell + ".png", glyph, rim=LEVEL_RIMS[level], size=160)
    token(BOOK + "/SpellBookIcon.png", "burning-book", rim=(150, 60, 200), size=160)
    token(OBJECTS + "/SpellShop.png", "burning-book", rim=(150, 60, 200), glyph_colour=(220, 180, 255))
    token(OBJECTS + "/LuckShrine.png", "clovers", rim=(60, 160, 80), glyph_colour=(170, 240, 150))
    book()
    sounds()


if __name__ == "__main__":
    main()
