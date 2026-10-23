"""Options overlay: three volume sliders, mute, the campaign buttons."""
import numpy as np

from dialogs import framed
from uiart import *

MOCKUP = "05-audio-campaign.png"
# slider rows: label, the track's inner groove and the handle
ROWS = [
    ("Music", (139, 313, 330, 369), 341, 811),
    ("Effects", (139, 408, 330, 464), 436, 950),
    ("Voices", (139, 500, 330, 556), 528, 898),
]
GROOVE_X = (389, 1244)
GROOVE_HALF = 14


def build(segment):
    image = load(MOCKUP)
    pieces = {}
    music_y = ROWS[0][2]
    pieces["SliderFill"] = crop(image, (395, music_y - GROOVE_HALF, 800, music_y + GROOVE_HALF))
    pieces["SliderHandle"] = crop(image, (811, music_y - 28, 865, music_y + 28))
    pieces["CheckboxFrame"] = crop(image, (142, 592, 200, 646))
    erase(image, [(140, 180, 760, 252)], "both", threshold=22, dilate=5, radius=7)
    for _, label, _, _ in ROWS:
        erase(image, [label], "both", threshold=24, dilate=4, radius=6)
    # empty grooves: the live slider draws its fill and handle
    for _, _, y, handle in ROWS:
        erase(image, [(handle - 8, y - 36, handle + 62, y + 36)], "all", radius=6)
        groove = (GROOVE_X[0], y - GROOVE_HALF, GROOVE_X[1], y + GROOVE_HALF)
        erase(image, [(GROOVE_X[0], y - 30, GROOVE_X[1], y + 30)], "both", threshold=40, dilate=3, radius=5)
        x0, y0, x1, y1 = groove
        shade = np.linspace(34, 12, y1 - y0)[:, None]
        image[y0:y1, x0:x1, 0] = shade
        image[y0:y1, x0:x1, 1] = shade * 0.85
        image[y0:y1, x0:x1, 2] = shade * 0.7
        for rim, colour in ((y0 - 3, (150, 105, 50)), (y0 - 2, (196, 146, 72)), (y0 - 1, (90, 60, 28)), (y1, (90, 60, 28)), (y1 + 1, (170, 120, 58)),
                            (y1 + 2, (110, 76, 36))):
            image[rim, x0:x1, :3] = colour
    erase(image, [(140, 590, 505, 650)], "both", threshold=24, dilate=4, radius=6)
    for rect in ((200, 700, 440, 770), (600, 700, 830, 770), (960, 700, 1260, 770), (300, 856, 760, 926), (1040, 856, 1200, 926)):
        erase(image, [rect], "both", threshold=24, dilate=4, radius=6)
    elements = [
        {"name": "TitleText", "type": "text", "rect": (140, 178, 1290, 252), "style": "Title", "size": 56, "align": "left", "text": "AUDIO / CAMPAIGN"},
    ]
    for name, label, y, _ in ROWS:
        elements.append({"name": name + "Label", "type": "text", "rect": label, "style": "Label", "size": 36, "align": "left", "text": name.upper()})
        elements.append({"name": name + "Slider", "type": "slider", "rect": (GROOVE_X[0], y - GROOVE_HALF, GROOVE_X[1], y + GROOVE_HALF),
                         "handleSize": (54, 56), "field": {"Music": "musicSlider", "Effects": "effectsSlider", "Voices": "voiceSlider"}[name]})
    elements += [
        {"name": "MuteToggle", "type": "toggle", "rect": (142, 592, 200, 646), "label": (225, 594, 700, 646), "text": "Mute all audio [M]", "size": 32, "field": "muteToggle"},
        {"name": "SaveButton", "type": "button", "rect": (128, 683, 506, 783), "label": (150, 700, 485, 768), "text": "SAVE", "size": 36, "field": "saveButton"},
        {"name": "LoadButton", "type": "button", "rect": (528, 683, 898, 783), "label": (550, 700, 878, 768), "text": "LOAD", "size": 36, "field": "loadButton"},
        {"name": "NewMissionButton", "type": "button", "rect": (920, 683, 1298, 783), "label": (940, 700, 1280, 768), "text": "NEW MISSION", "size": 36, "field": "newMissionButton"},
        {"name": "BackButton", "type": "button", "rect": (128, 839, 920, 939), "label": (150, 856, 900, 924), "text": "BACK TO THE OATH", "size": 36, "field": "backButton"},
        {"name": "QuitButton", "type": "button", "rect": (944, 839, 1298, 939), "label": (965, 856, 1280, 924), "text": "QUIT", "size": 36, "field": "quitButton"},
    ]
    dialog_pieces, layout = framed(image, 0.66, "Options", "OptionsFrame", elements)
    pieces.update(dialog_pieces)
    return pieces, [layout]
