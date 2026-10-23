"""The portrait dialogue box (the Dialogue feature's DialogueBox widget) and the speakers' portraits."""
from dialogs import framed
from uiart import *

MOCKUP = "02-world-map-dialogue.png"


def build(segment):
    image = load(MOCKUP)
    pieces = {}
    pieces["AirGuitaristSpeaker"] = crop(image, (105, 168, 466, 535))
    varg = load("11-town-dialogue.png")
    pieces["LordVargSpeaker"] = crop(varg, (112, 140, 525, 520))
    erase(image, [(525, 205, 1100, 285), (525, 320, 1860, 400)], "both", threshold=22, dilate=5, radius=7)
    erase(image, [(1620, 470, 1810, 520)], "both", threshold=24, dilate=4, radius=6)
    elements = [
        {"name": "PortraitImage", "type": "image", "rect": (105, 168, 466, 535), "field": "portraitImage"},
        {"name": "SpeakerNameText", "type": "text", "rect": (525, 200, 1860, 285), "style": "Title", "size": 60, "align": "left", "field": "speakerNameText"},
        {"name": "LineText", "type": "text", "rect": (525, 315, 1860, 445), "style": "Body", "size": 46, "align": "topleft", "field": "lineText"},
        {"name": "ContinueText", "type": "text", "rect": (1580, 452, 1860, 535), "style": "Button", "size": 34, "align": "center", "field": "continueText"},
    ]
    frame_pieces, layout = framed(image, 0.6, "DialogueBox", "DialogueBoxFrame", elements)
    pieces.update(frame_pieces)
    return pieces, [layout]
