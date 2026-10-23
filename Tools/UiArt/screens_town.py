"""Town screen: the town's picture and actions on the left, three recruitment rows, the quantity and the two armies."""
from uiart import *

MOCKUP = "10-town-recruitment.png"
ROW_STEP = 165
SLOT_X = [(850, 950), (958, 1058), (1068, 1168), (1178, 1278), (1285, 1385), (1392, 1492), (1500, 1600)]


def build(segment):
    image = load(MOCKUP)
    pieces = {}
    pieces["TownPicture"] = crop(image, (40, 165, 545, 535))
    for index, name in enumerate(("RoadieCard", "AirGuitaristCard", "BerserkerCard")):
        y = index * ROW_STEP
        pieces[name] = crop(image, (622, 117 + y, 822, 247 + y))

    erase(image, [(900, 20, 1060, 60), (1180, 20, 1315, 60), (1390, 22, 1472, 58), (1548, 22, 1630, 58)], "light")
    erase(image, [(90, 80, 400, 128), (90, 128, 545, 152)], "light")
    erase(image, [(65, 585, 545, 625), (65, 626, 530, 684)], "light")
    erase(image, [(150, 718, 530, 762), (190, 798, 450, 842)], "light")
    for index in range(3):
        y = index * ROW_STEP
        erase(image, [(842, 115 + y, 1225, 240 + y)], "light")
        erase(image, [(1430, 155 + y, 1575, 212 + y)], "light")
    erase(image, [(620, 605, 830, 640), (958, 603, 1078, 648), (1180, 605, 1290, 645)], "light")
    erase(image, [(620, 670, 840, 700), (620, 778, 845, 808), (640, 716, 820, 748), (640, 822, 820, 854)], "light")
    # both armies showed troops: every slot shows its empty look
    empty = crop(image, (1180, 678, 1276, 758))
    for x0, x1 in SLOT_X[:2]:
        image[678:758, x0 + 2:x1 - 2] = resize(empty, (x1 - x0 - 4, 80))
        image[786:866, x0 + 2:x1 - 2] = resize(empty, (x1 - x0 - 4, 80))
    pieces["TownScreen"] = image

    elements = [
        {"name": "DayText", "type": "text", "rect": (880, 18, 1080, 62), "style": "Number", "size": 22, "align": "center", "field": "header.dayText"},
        {"name": "GoldText", "type": "text", "rect": (1180, 20, 1325, 60), "style": "Number", "size": 26, "align": "left", "field": "header.goldText"},
        {"name": "HeroButton", "type": "button", "rect": (1340, 12, 1490, 66), "label": (1385, 22, 1485, 58), "text": "HERO [H]", "size": 18, "field": "header.heroButton"},
        {"name": "OptionsButton", "type": "button", "rect": (1500, 12, 1648, 66), "label": (1545, 22, 1640, 58), "text": "OPTIONS", "size": 18, "field": "header.optionsButton"},
        {"name": "TitleText", "type": "text", "rect": (92, 80, 545, 128), "style": "Title", "size": 36, "align": "left", "field": "titleText"},
        {"name": "SubtitleText", "type": "text", "rect": (92, 128, 545, 152), "style": "Label", "size": 15, "align": "left", "field": "subtitleText"},
        {"name": "TownImage", "type": "image", "rect": (40, 165, 545, 535), "field": "townImage"},
        {"name": "ForgeTitle", "type": "text", "rect": (65, 585, 545, 625), "style": "Heading", "size": 22, "align": "left", "field": "statusTitleText"},
        {"name": "DescriptionText", "type": "text", "rect": (65, 626, 535, 690), "style": "Body", "size": 16, "align": "topleft", "field": "descriptionText"},
        {"name": "ClaimButton", "type": "button", "rect": (55, 706, 550, 774), "label": (148, 718, 535, 762), "text": "CLAIM THE HOLD / FIGHT GARRISON", "size": 18, "field": "claimButton"},
        {"name": "ReturnButton", "type": "button", "rect": (55, 786, 550, 852), "label": (148, 798, 535, 842), "text": "RETURN TO THE PASS", "size": 18, "field": "backButton"},
        {"name": "QuantityLabel", "type": "text", "rect": (620, 603, 845, 642), "style": "Label", "size": 18, "align": "left", "text": "RECRUIT QUANTITY"},
        {"name": "LessButton", "type": "button", "rect": (885, 600, 955, 650), "text": "", "field": "lessButton"},
        {"name": "QuantityText", "type": "text", "rect": (958, 603, 1078, 648), "style": "Number", "size": 24, "align": "center", "field": "quantityText"},
        {"name": "MoreButton", "type": "button", "rect": (1075, 600, 1150, 650), "text": "", "field": "moreButton"},
        {"name": "MaxButton", "type": "button", "rect": (1165, 600, 1300, 650), "label": (1170, 605, 1295, 645), "text": "MAX", "size": 18, "field": "maxButton"},
        {"name": "HomeText", "type": "text", "rect": (620, 670, 845, 700), "style": "Label", "size": 15, "align": "left", "field": "homeText"},
        {"name": "GarrisonHeroButton", "type": "button", "rect": (625, 705, 832, 758), "label": (632, 712, 825, 752), "text": "Lord Varg", "size": 18, "style": "Body", "field": "garrisonHeroButton"},
        {"name": "GarrisonSlots", "type": "slots", "rects": [(x0, 675, x1, 760) for x0, x1 in SLOT_X], "field": "garrisonSlots", "named": True},
        {"name": "VisitingText", "type": "text", "rect": (620, 778, 845, 808), "style": "Label", "size": 15, "align": "left", "field": "visitingText"},
        {"name": "StationButton", "type": "button", "rect": (625, 812, 832, 864), "label": (632, 818, 825, 858), "text": "HERO -> HOME", "size": 18, "field": "stationButton"},
        {"name": "VisitingSlots", "type": "slots", "rects": [(x0, 783, x1, 868) for x0, x1 in SLOT_X], "field": "visitingSlots", "named": True},
    ]
    for index in range(3):
        y = index * ROW_STEP
        elements.append({"name": "RecruitRow", "type": "recruit", "rect": (603, 106 + y, 1625, 262 + y), "field": "recruitRows",
                         "portrait": (622, 117 + y, 822, 247 + y), "nameRect": (846, 115 + y, 1225, 150 + y),
                         "description": (846, 155 + y, 1260, 205 + y), "stats": (846, 210 + y, 1260, 240 + y),
                         "button": (1389, 140 + y, 1617, 219 + y), "label": (1430, 155 + y, 1575, 212 + y)})
    layout = {"screen": "Town", "backdrop": "TownScreen", "elements": elements}
    return pieces, [layout]
