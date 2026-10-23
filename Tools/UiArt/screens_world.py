"""World map screen: backdrop (frame with the map viewport cut out and every text erased) and its live elements."""
import numpy as np

from uiart import *

MOCKUP = "01-world-map-main-ui.png"
VIEWPORT = (19, 71, 1134, 869)
SLOTS = [(1166, 367, 1238, 487), (1241, 367, 1304, 487), (1308, 367, 1371, 487), (1375, 367, 1438, 487), (1442, 367, 1505, 487),
         (1509, 367, 1572, 487), (1576, 367, 1641, 487)]


def figures(image, segment):
    """The demon at the bottom left reaches over the map; it stays opaque."""
    demon = segment(image, (0, 660, 175, 880), [(0, 700, 120, 870), (0, 860, 175, 880)], [(165, 660, 175, 860), (140, 660, 175, 700)])
    return demon


def track(image, rect):
    """Paints an empty bar track: dark iron with a faint bronze rim, so the live fill can grow over it."""
    x0, y0, x1, y1 = rect
    h = y1 - y0
    shade = np.linspace(38, 14, h)[:, None]
    image[y0:y1, x0:x1, 0] = shade
    image[y0:y1, x0:x1, 1] = shade * 0.85
    image[y0:y1, x0:x1, 2] = shade * 0.7
    image[y0, x0:x1, :3] = (110, 80, 40)
    image[y1 - 1, x0:x1, :3] = (60, 42, 22)


def build(segment):
    image = load(MOCKUP)
    pieces = {}
    pieces["MovementBarFill"] = crop(image, (1357, 268, 1622, 288))
    pieces["KaelPortrait"] = crop(image, (1176, 110, 1316, 292))
    pieces["WarhostBanner"] = crop(image, (1445, 525, 1650, 735))

    erase(image, [(860, 18, 1047, 62)], "dark", threshold=30)
    erase(image, [(1135, 22, 1270, 58), (1355, 22, 1440, 58), (1515, 22, 1600, 58)], "light")
    erase(image, [(1338, 116, 1605, 160), (1338, 162, 1540, 190), (1385, 223, 1610, 254)], "light")
    track(image, (1357, 267, 1623, 289))
    erase(image, [(1168, 322, 1405, 360), (1172, 530, 1312, 570)], "light")
    erase(image, [(1180, 583, 1440, 722)], "light", threshold=30)
    erase(image, [(1248, 768, 1388, 808), (1488, 768, 1562, 808)], "light")
    erase(image, [(160, 876, 1404, 916)], "light")
    # the first army slot showed a unit: give it the empty look of its neighbour
    slot = crop(image, (1243, 372, 1302, 482))
    image[372:482, 1171:1233] = resize(slot, (62, 110))

    keep = figures(image, segment)
    x0, y0, x1, y1 = VIEWPORT
    alpha = image[:, :, 3]
    region = alpha[y0:y1, x0:x1]
    region[keep[y0:y1, x0:x1] == 0] = 0
    pieces["AdventureFrame"] = image

    layout = {
        "screen": "Adventure",
        "backdrop": "AdventureFrame",
        "viewport": VIEWPORT,
        "elements": [
            {"name": "PortraitImage", "type": "image", "rect": (1177, 111, 1314, 290), "field": "portraitImage"},
            {"name": "HeroText", "type": "text", "rect": (1340, 116, 1630, 160), "style": "Title", "size": 34, "align": "left", "field": "heroText"},
            {"name": "LevelText", "type": "text", "rect": (1340, 162, 1630, 190), "style": "Label", "size": 21, "align": "left", "field": "levelText"},
            {"name": "MovementText", "type": "text", "rect": (1385, 223, 1630, 254), "style": "Label", "size": 21, "align": "left", "field": "movementText"},
            {"name": "MovementBar", "type": "bar", "rect": (1357, 268, 1622, 288), "sprite": "MovementBarFill", "field": "movementBarImage"},
            {"name": "WarhostTitle", "type": "text", "rect": (1170, 322, 1420, 360), "style": "Heading", "size": 28, "align": "left", "text": "YOUR WARHOST"},
            {"name": "ArmySlots", "type": "slots", "rects": SLOTS, "field": "armySlots"},
            {"name": "MissionTitle", "type": "text", "rect": (1172, 530, 1330, 570), "style": "Heading", "size": 28, "align": "left", "text": "MISSION"},
            {"name": "Objectives", "type": "objectives", "rect": (1182, 580, 1446, 728), "rows": 5, "field": "objectiveRows"},
            {"name": "DayText", "type": "text", "rect": (860, 18, 1047, 62), "style": "Number", "size": 22, "align": "center", "color": "ink", "field": "header.dayText"},
            {"name": "GoldText", "type": "text", "rect": (1135, 22, 1285, 58), "style": "Number", "size": 26, "align": "left", "field": "header.goldText"},
            {"name": "HeroButton", "type": "button", "rect": (1296, 14, 1452, 66), "label": (1352, 22, 1447, 58), "text": "HERO [H]", "size": 18, "field": "header.heroButton"},
            {"name": "OptionsButton", "type": "button", "rect": (1462, 14, 1612, 66), "label": (1512, 22, 1607, 58), "text": "OPTIONS", "size": 18, "field": "header.optionsButton"},
            {"name": "HelpText", "type": "text", "rect": (160, 876, 1404, 916), "style": "Label", "size": 17, "align": "center", "field": "footer.helpText"},
            {"name": "RideButton", "type": "button", "rect": (1175, 750, 1398, 826), "label": (1245, 768, 1392, 808), "text": "RIDE [SPACE]", "field": "rideButton"},
            {"name": "EndDayButton", "type": "button", "rect": (1405, 750, 1570, 826), "label": (1478, 768, 1572, 808), "text": "END DAY", "field": "endDayButton"},
        ],
    }
    return pieces, [layout]
