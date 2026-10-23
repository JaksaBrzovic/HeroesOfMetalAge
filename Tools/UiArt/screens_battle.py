"""Battle screen: frame with the battlefield viewport cut out, the war council and the battle chronicle."""
from uiart import *

MOCKUP = "07-battle-screen.png"
VIEWPORT = (22, 145, 1148, 840)


def build(segment):
    image = load(MOCKUP)
    pieces = {}
    erase(image, [(900, 20, 1060, 60), (1180, 20, 1315, 60), (1390, 22, 1472, 58), (1548, 22, 1630, 58)], "light")
    erase(image, [(92, 88, 475, 130), (715, 92, 1100, 128)], "light")
    erase(image, [(1192, 118, 1455, 160), (1345, 195, 1630, 330)], "light")
    erase(image, [(1268, 398, 1365, 440), (1488, 398, 1608, 440)], "light")
    erase(image, [(1198, 503, 1535, 548), (1198, 575, 1630, 725)], "light")
    erase(image, [(150, 856, 705, 892), (1338, 856, 1500, 892)], "light")
    # the portrait frame showed the acting stack; the live portrait covers it
    image[:, :, 3][VIEWPORT[1]:VIEWPORT[3], VIEWPORT[0]:VIEWPORT[2]] = 0
    pieces["CombatFrame"] = image
    layout = {
        "screen": "Combat",
        "backdrop": "CombatFrame",
        "viewport": VIEWPORT,
        "elements": [
            {"name": "DayText", "type": "text", "rect": (880, 18, 1080, 62), "style": "Number", "size": 22, "align": "center", "field": "header.dayText"},
            {"name": "GoldText", "type": "text", "rect": (1180, 20, 1325, 60), "style": "Number", "size": 26, "align": "left", "field": "header.goldText"},
            {"name": "HeroButton", "type": "button", "rect": (1340, 12, 1490, 66), "label": (1385, 22, 1485, 58), "text": "HERO [H]", "size": 18, "field": "header.heroButton"},
            {"name": "OptionsButton", "type": "button", "rect": (1500, 12, 1648, 66), "label": (1545, 22, 1640, 58), "text": "OPTIONS", "size": 18, "field": "header.optionsButton"},
            {"name": "TitleText", "type": "text", "rect": (95, 88, 700, 130), "style": "Title", "size": 34, "align": "left", "field": "titleText"},
            {"name": "TurnText", "type": "text", "rect": (700, 92, 1105, 128), "style": "Label", "size": 20, "align": "right", "field": "turnText"},
            {"name": "CouncilTitle", "type": "text", "rect": (1195, 116, 1500, 160), "style": "Heading", "size": 32, "align": "left", "text": "WAR COUNCIL"},
            {"name": "ActorPortrait", "type": "image", "rect": (1190, 192, 1326, 335), "field": "portraitImage"},
            {"name": "ActorText", "type": "text", "rect": (1348, 195, 1640, 232), "style": "Title", "size": 25, "align": "left", "field": "actorText"},
            {"name": "StatsText", "type": "text", "rect": (1350, 238, 1640, 330), "style": "Label", "size": 17, "align": "topleft", "field": "statsText"},
            {"name": "DefendButton", "type": "button", "rect": (1178, 380, 1402, 457), "label": (1262, 398, 1395, 440), "text": "DEFEND", "size": 20, "field": "defendButton"},
            {"name": "PassButton", "type": "button", "rect": (1410, 380, 1632, 457), "label": (1478, 398, 1625, 440), "text": "PASS TURN", "size": 20, "field": "passButton"},
            {"name": "LogTitle", "type": "text", "rect": (1198, 503, 1600, 548), "style": "Heading", "size": 32, "align": "left", "text": "BATTLE CHRONICLE"},
            {"name": "LogText", "type": "text", "rect": (1200, 572, 1632, 830), "style": "Body", "size": 18, "align": "topleft", "field": "logText"},
            {"name": "HelpText", "type": "text", "rect": (155, 856, 740, 892), "style": "Label", "size": 17, "align": "left", "field": "footer.helpText"},
            {"name": "AudioText", "type": "text", "rect": (1310, 856, 1505, 892), "style": "Label", "size": 17, "align": "right", "field": "footer.audioText"},
        ],
    }
    return pieces, [layout]
