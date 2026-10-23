"""Cuts the gameplay UI out of the mockups: writes the sprites into the project and one layout JSON per screen for the
prefab generator. usage: build_ui.py [screen ...] (default: all). Debug overlays go to %TEMP%/homa-ui-debug."""
import importlib
import json
import os
import sys

import cv2
import numpy as np
from PIL import Image, ImageDraw

PROJECT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "HeroesOfMetalAge")).replace("\\", "/")
SPRITES = PROJECT + "/Content/UI/Sprites/Gameplay"
LAYOUTS = PROJECT + "/Editor/UiLayouts"
SCRATCH = os.path.join(os.environ.get("TEMP", "/tmp"), "homa-ui-debug")
os.makedirs(SCRATCH, exist_ok=True)
DESIGN = (1920, 1080)


def segment(image, box, sure_fg, sure_bg, iterations=8):
    """GrabCut of a figure inside box: returns a full-size mask, 255 where the figure is."""
    bgr = cv2.cvtColor(np.ascontiguousarray(image[:, :, :3]), cv2.COLOR_RGB2BGR)
    mask = np.full(bgr.shape[:2], cv2.GC_BGD, np.uint8)
    x0, y0, x1, y1 = box
    mask[y0:y1, x0:x1] = cv2.GC_PR_FGD
    for a, b, c, d in sure_fg:
        mask[b:d, a:c] = cv2.GC_FGD
    for a, b, c, d in sure_bg:
        mask[b:d, a:c] = cv2.GC_BGD
    background = np.zeros((1, 65), np.float64)
    foreground = np.zeros((1, 65), np.float64)
    cv2.grabCut(bgr, mask, None, background, foreground, iterations, cv2.GC_INIT_WITH_MASK)
    result = np.where((mask == cv2.GC_FGD) | (mask == cv2.GC_PR_FGD), 255, 0).astype(np.uint8)
    # a soft, slightly shrunken edge so no map pixels fringe the figure
    result = cv2.erode(result, np.ones((3, 3), np.uint8))
    return result


def to_design(rect, size):
    sx = DESIGN[0] / size[0]
    sy = DESIGN[1] / size[1]
    x0, y0, x1, y1 = rect
    return [round(x0 * sx, 1), round(y0 * sy, 1), round(x1 * sx, 1), round(y1 * sy, 1)]


def scale_layout(layout, size, origin=(0, 0), frame=None):
    """Mockup pixels to design units: full screens fill 1920x1080, dialogs keep their pixel scale given by 'scale'."""
    def convert(rect):
        if frame is None:
            return to_design(rect, size)
        s, ox, oy = frame
        x0, y0, x1, y1 = rect
        return [round((x0 - ox) * s, 1), round((y0 - oy) * s, 1), round((x1 - ox) * s, 1), round((y1 - oy) * s, 1)]

    out = dict(layout)
    out["elements"] = []
    for element in layout["elements"]:
        e = dict(element)
        for key in ("rect", "label", "icon", "portrait", "nameRect", "description", "stats", "button"):
            if key in e:
                e[key] = convert(e[key])
        if "handleSize" in e and frame is not None:
            e["handleSize"] = [round(v * frame[0], 1) for v in e["handleSize"]]
        if "rects" in e:
            e["rects"] = [convert(r) for r in e["rects"]]
        if "size" in e and frame is None:
            e["size"] = round(e["size"] * DESIGN[1] / size[1], 1)
        elif "size" in e:
            e["size"] = round(e["size"] * frame[0], 1)
        out["elements"].append(e)
    if "viewport" in layout:
        out["viewport"] = convert(layout["viewport"])
    if "frameSize" in layout:
        out["frameSize"] = layout["frameSize"]
    return out


def overlay(name, backdrop, layout, offset=(0, 0)):
    image = Image.fromarray(backdrop).convert("RGBA")
    checker = Image.new("RGBA", image.size, (255, 0, 255, 255))
    image = Image.alpha_composite(checker, image)
    draw = ImageDraw.Draw(image)
    ox, oy = offset

    def shift(rect):
        return (rect[0] - ox, rect[1] - oy, rect[2] - ox, rect[3] - oy)

    for element in layout["elements"]:
        element = {k: (shift(v) if k in ("rect", "label", "portrait", "nameRect", "description", "stats", "button") else v) for k, v in element.items()}
        if "rects" in element:
            element["rects"] = [shift(r) for r in element["rects"]]
        rects = element.get("rects") or [element["rect"]]
        for rect in rects:
            draw.rectangle(rect, outline=(0, 255, 255, 255), width=2)
        for key in ("label", "portrait", "nameRect", "description", "stats", "button"):
            if key in element:
                draw.rectangle(element[key], outline=(255, 255, 0, 255), width=1)
        label = element.get("text") or element["name"]
        draw.text((rects[0][0] + 3, rects[0][1] + 2), label, fill=(255, 255, 0, 255))
    image.convert("RGB").save(os.path.join(SCRATCH, "debug_" + name + ".png"))


def main():
    names = sys.argv[1:] or ["world"]
    os.makedirs(SPRITES, exist_ok=True)
    os.makedirs(LAYOUTS, exist_ok=True)
    for name in names:
        module = importlib.import_module("screens_" + name)
        pieces, layouts = module.build(segment)
        main_layout = layouts[0]
        backdrop = pieces[main_layout["backdrop"]]
        frame = main_layout.get("frame")
        overlay(name, backdrop, {"elements": [e for layout in layouts for e in layout["elements"]]}, (frame[1], frame[2]) if frame else (0, 0))
        folder = os.path.join(SPRITES, main_layout["screen"])
        os.makedirs(folder, exist_ok=True)
        size = (backdrop.shape[1], backdrop.shape[0])
        for piece, array in pieces.items():
            if array is None:
                continue
            if piece == main_layout["backdrop"] and main_layout.get("fullscreen", True):
                array = np.array(Image.fromarray(array).resize(DESIGN, Image.LANCZOS))
            Image.fromarray(array).save(os.path.join(folder, piece + ".png"))
        for layout in layouts:
            scaled = scale_layout(layout, size, frame=layout.get("frame"))
            scaled["fullscreen"] = layout.get("fullscreen", True)
            scaled.pop("frame", None)
            for element in scaled["elements"]:
                if "rects" in element:
                    element["rects"] = [v for rect in element["rects"] for v in rect]
            scaled["spriteFolder"] = "Assets/HeroesOfMetalAge/Content/UI/Sprites/Gameplay/" + main_layout["screen"]
            with open(os.path.join(LAYOUTS, layout["screen"] + ".json"), "w", encoding="utf-8", newline="\n") as out:
                json.dump(scaled, out, indent=2)
        print(name, "->", folder, len(pieces), "sprites,", len(layouts), "layouts")


if __name__ == "__main__":
    main()
