"""Hero screen: the rider portrait, stats, eleven equipment places around the hero, the inventory and the army."""
from uiart import *

MOCKUP = "04-hero-screen.png"
EQUIPMENT = [
    ("Head", (790, 89, 919, 200)),
    ("LeftArm", (575, 152, 708, 268)),
    ("RightArm", (994, 152, 1118, 268)),
    ("Gloves", (575, 279, 708, 398)),
    ("Torso", (994, 279, 1118, 398)),
    ("LeftRing", (575, 410, 708, 520)),
    ("RightRing", (994, 410, 1118, 520)),
    ("Amulet", (575, 531, 708, 656)),
    ("Necklace", (994, 531, 1118, 656)),
    ("Sash", (722, 602, 848, 698)),
    ("Boots", (859, 602, 984, 698)),
]
COLUMNS = [(1185, 1288), (1295, 1398), (1404, 1505), (1513, 1616)]
ROWS = [(128, 255), (262, 397), (402, 531), (541, 675)]
ARMY = [(567, 724, 700, 897), (711, 724, 844, 897), (856, 724, 989, 897), (1000, 724, 1144, 897), (1150, 724, 1294, 897),
        (1300, 724, 1444, 897), (1450, 724, 1594, 897)]


def build(segment):
    image = load(MOCKUP)
    pieces = {}
    pieces["KaelRider"] = crop(image, (80, 152, 497, 590))
    pieces["RoadiePortrait"] = crop(image, (572, 730, 700, 863))
    pieces["AirGuitaristPortrait"] = crop(image, (711, 730, 842, 863))
    # an empty inventory slot without its emblem: the plate drawn behind a worn artefact
    plate = crop(image, (1195, 138, 1279, 246))
    erase(plate, [(0, 0, plate.shape[1], plate.shape[0])], "light", threshold=12, dilate=4, radius=9)
    pieces["SlotPlate"] = plate

    erase(image, [(1105, 18, 1285, 54)], "dark", threshold=30)
    erase(image, [(1395, 18, 1525, 52)], "light")
    erase(image, [(128, 78, 460, 122), (186, 610, 388, 644)], "light")
    erase(image, [(150, 696, 380, 802)], "light")
    erase(image, [(250, 855, 362, 895)], "light")
    erase(image, [(568, 80, 745, 117), (1188, 84, 1372, 118)], "light")
    for _, (x0, y0, x1, y1) in EQUIPMENT:
        erase(image, [(x0 + 6, y1 - 38, x1 - 6, y1 - 6)], "light")
    erase(image, [(572, 862, 1600, 895), (580, 830, 860, 868)], "light")
    # the two filled army places showed units: give them the empty look of their neighbour
    empty = crop(image, (862, 730, 984, 865))
    for x0, y0, x1, y1 in ARMY[:2]:
        image[730:865, x0 + 6:x1 - 6] = resize(empty, (x1 - x0 - 12, 135))
    pieces["HeroScreen"] = image

    elements = [
        {"name": "DayText", "type": "text", "rect": (1080, 16, 1305, 56), "style": "Number", "size": 22, "align": "center", "color": "ink", "field": "header.dayText"},
        {"name": "GoldText", "type": "text", "rect": (1395, 18, 1535, 54), "style": "Number", "size": 26, "align": "left", "field": "header.goldText"},
        {"name": "ReturnIconButton", "type": "button", "rect": (1540, 6, 1598, 62), "text": "", "field": "header.heroButton"},
        {"name": "OptionsButton", "type": "button", "rect": (1600, 6, 1655, 62), "text": "", "field": "header.optionsButton"},
        {"name": "TitleText", "type": "text", "rect": (90, 76, 500, 124), "style": "Title", "size": 38, "align": "center", "field": "titleText"},
        {"name": "PortraitImage", "type": "image", "rect": (80, 152, 497, 590), "field": "portraitImage"},
        {"name": "LevelText", "type": "text", "rect": (90, 608, 490, 646), "style": "Heading", "size": 24, "align": "center", "field": "levelText"},
        {"name": "ExperienceBar", "type": "bar", "rect": (90, 650, 482, 666), "sprite": "", "color": "gold", "field": "experienceBarImage"},
        {"name": "StatsText", "type": "text", "rect": (150, 694, 470, 836), "style": "Label", "size": 19, "align": "topleft", "field": "statsText", "lineSpacing": 14},
        {"name": "ReturnButton", "type": "button", "rect": (95, 842, 480, 910), "label": (210, 855, 400, 895), "text": "RETURN", "size": 24, "field": "backButton"},
        {"name": "EquipmentTitle", "type": "text", "rect": (568, 78, 780, 118), "style": "Heading", "size": 28, "align": "left", "text": "EQUIPMENT"},
        {"name": "InventoryTitle", "type": "text", "rect": (1188, 82, 1420, 120), "style": "Heading", "size": 28, "align": "left", "text": "INVENTORY"},
        {"name": "HintText", "type": "text", "rect": (1430, 84, 1640, 120), "style": "Body", "size": 14, "align": "right", "field": "hintText"},
    ]
    for place, rect in EQUIPMENT:
        elements.append({"name": place + "Slot", "type": "equipment", "rect": rect, "place": place, "field": "equipmentSlots"})
    for row in ROWS:
        for column in COLUMNS:
            elements.append({"name": "InventorySlot", "type": "inventory", "rect": (column[0], row[0], column[1], row[1]), "field": "inventorySlots"})
    elements.append({"name": "ArmySlots", "type": "slots", "rects": ARMY, "field": "armySlots", "named": True})
    layout = {"screen": "Hero", "backdrop": "HeroScreen", "elements": elements}
    return pieces, [layout]
