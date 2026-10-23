"""The framed encounter dialog (also chests, dwellings, the exit portal and defeat), plus the shared 9-slice frame,
button and divider cut from it."""
import numpy as np

from dialogs import framed
from uiart import *

MOCKUP = "03-world-map-unit-encounter.png"


def build(segment):
    image = load(MOCKUP)
    pieces = {}
    # a button without its caption: the left cap of one and the right cap of the other, for 9-slicing
    pieces["ButtonFrame"] = hstitch(image, (180, 655, 300, 790), (1290, 655, 1407, 790))
    pieces["Divider"] = crop(image, (155, 312, 1432, 346))
    erase(image, [(170, 215, 700, 310), (172, 380, 1020, 440), (172, 455, 1100, 600)], "both", threshold=22, dilate=5, radius=7)
    erase(image, [(300, 690, 665, 755), (1035, 690, 1180, 755)], "both", threshold=22, dilate=5, radius=7)
    # a frame without the skull or the buttons for 9-slicing: the empty interior is copied over the buttons, then the left
    # and right thirds of the frame are joined, the top centre dropped
    plain = image.copy()
    plain[640:810, 150:1440] = plain[450:620, 150:1440]
    plain[300:360, 150:1440] = plain[450:510, 150:1440]
    frame = hstitch(plain, (45, 60, 470, 905), (1120, 60, 1545, 905))
    pieces["PanelFrame"] = frame
    pieces["ButtonFrame"] = pieces["ButtonFrame"]
    elements = [
        {"name": "TitleText", "type": "text", "rect": (170, 212, 1420, 312), "style": "Title", "size": 64, "align": "left", "field": "titleText"},
        {"name": "SubtitleText", "type": "text", "rect": (172, 378, 1420, 440), "style": "Heading", "size": 34, "align": "left", "field": "subtitleText"},
        {"name": "BodyText", "type": "text", "rect": (172, 450, 1420, 640), "style": "Body", "size": 34, "align": "topleft", "field": "bodyText"},
        {"name": "PrimaryButton", "type": "button", "rect": (180, 655, 772, 790), "label": (215, 680, 740, 765), "text": "WELCOME THEM", "size": 38, "field": "primaryButton"},
        {"name": "SecondaryButton", "type": "button", "rect": (815, 655, 1407, 790), "label": (850, 680, 1372, 765), "text": "LEAVE", "size": 38, "field": "secondaryButton"},
    ]
    dialog_pieces, layout = framed(image, 0.62, "Dialog", "DialogFrame", elements)
    pieces.update(dialog_pieces)

    # defeat: the same frame without the two buttons, which three framed buttons replace
    plain = image.copy()
    clean = crop(plain, (172, 452, 1420, 640))
    plain[648:836, 172:1420] = clean
    defeat_elements = [
        {"name": "TitleText", "type": "text", "rect": (170, 212, 1420, 312), "style": "Display", "size": 60, "align": "left", "field": "titleText"},
        {"name": "BodyText", "type": "text", "rect": (172, 380, 1420, 640), "style": "Body", "size": 34, "align": "topleft", "field": "bodyText"},
        {"name": "RetryButton", "type": "framedButton", "rect": (172, 660, 572, 780), "text": "RETRY BATTLE", "size": 34, "field": "retryButton"},
        {"name": "ReturnButton", "type": "framedButton", "rect": (596, 660, 996, 780), "text": "RETURN / PREPARE", "size": 34, "field": "returnButton"},
        {"name": "NewMissionButton", "type": "framedButton", "rect": (1020, 660, 1420, 780), "text": "NEW MISSION", "size": 34, "field": "newMissionButton"},
    ]
    defeat_pieces, defeat = framed(plain, 0.62, "Defeat", "DefeatFrame", defeat_elements)
    pieces.update(defeat_pieces)
    return pieces, [layout, defeat]
