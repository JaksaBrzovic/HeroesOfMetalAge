"""Spoils of war: the gold and the artefact won, also the source of the gold pile and the artefact frame."""
from dialogs import framed
from uiart import *

MOCKUP = "09-spoils-of-war.png"


def build(segment):
    image = load(MOCKUP)
    pieces = {}
    pieces["GoldPile"] = crop(image, (440, 330, 735, 495))
    frame = crop(image, (697, 478, 893, 662))
    plate(frame, (16, 16, frame.shape[1] - 16, frame.shape[0] - 16))
    pieces["ArtefactFrame"] = frame
    erase(image, [(420, 190, 1170, 285)], "both", threshold=22, dilate=5, radius=7)
    erase(image, [(745, 365, 1215, 455)], "both", threshold=22, dilate=5, radius=7)
    plate(image, (713, 494, 877, 646))
    erase(image, [(590, 665, 1000, 745)], "both", threshold=22, dilate=5, radius=7)
    erase(image, [(610, 770, 975, 815)], "both", threshold=24, dilate=4, radius=6)
    elements = [
        {"name": "TitleText", "type": "text", "rect": (300, 185, 1290, 290), "style": "Display", "size": 84, "align": "center", "field": "titleText"},
        {"name": "GoldText", "type": "text", "rect": (745, 360, 1250, 460), "style": "Display", "size": 72, "align": "left", "field": "goldText"},
        {"name": "ArtefactImage", "type": "image", "rect": (716, 498, 874, 642), "field": "artefactImage"},
        {"name": "ArtefactName", "type": "text", "rect": (430, 662, 1160, 705), "style": "Heading", "size": 34, "align": "center", "field": "artefactNameText"},
        {"name": "NoteText", "type": "text", "rect": (430, 703, 1160, 748), "style": "Label", "size": 30, "align": "center", "field": "noteText"},
        {"name": "ContinueButton", "type": "button", "rect": (518, 752, 1068, 838), "label": (560, 768, 1025, 822), "text": "CONTINUE THE OATH", "size": 32, "field": "continueButton"},
    ]
    frame_pieces, layout = framed(image, 0.62, "Rewards", "RewardsFrame", elements)
    pieces.update(frame_pieces)
    return pieces, [layout]
