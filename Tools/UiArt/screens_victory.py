"""Victory screen: the warrior raising his sword over the framed result."""
from dialogs import framed
from uiart import *

MOCKUP = "08-victory.png"


def build(segment):
    image = load(MOCKUP)
    pieces = {}
    erase(image, [(600, 545, 990, 614)], "both", threshold=18, dilate=3, radius=7)
    erase(image, [(570, 635, 1020, 672), (690, 680, 960, 712), (690, 728, 965, 762), (640, 776, 945, 828)], "both", threshold=24, dilate=4, radius=6)
    erase(image, [(660, 846, 910, 880)], "both", threshold=24, dilate=4, radius=6)
    elements = [
        {"name": "TitleText", "type": "text", "rect": (420, 540, 1170, 620), "style": "Display", "size": 66, "align": "center", "field": "titleText"},
        {"name": "SubtitleText", "type": "text", "rect": (420, 632, 1170, 674), "style": "Heading", "size": 26, "align": "center", "field": "subtitleText"},
        {"name": "ExperienceText", "type": "text", "rect": (695, 680, 1100, 712), "style": "Label", "size": 22, "align": "left", "field": "experienceText"},
        {"name": "LossesText", "type": "text", "rect": (695, 726, 1100, 762), "style": "Label", "size": 22, "align": "left", "field": "lossesText"},
        {"name": "BodyText", "type": "text", "rect": (420, 772, 1170, 832), "style": "Body", "size": 22, "align": "center", "field": "bodyText"},
        {"name": "ContinueButton", "type": "button", "rect": (618, 832, 972, 892), "label": (640, 842, 950, 884), "text": "CLAIM THE SPOILS", "size": 24, "field": "continueButton"},
    ]
    frame_pieces, layout = framed(image, 0.78, "Victory", "VictoryFrame", elements)
    pieces.update(frame_pieces)
    return pieces, [layout]
