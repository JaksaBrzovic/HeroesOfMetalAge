"""Helpers for the framed dialogs cut from mockups with a transparent background."""
import numpy as np

from uiart import crop


def bounds(image):
    """The box of every visible pixel."""
    ys, xs = np.where(image[:, :, 3] > 8)
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def framed(image, scale, screen, backdrop, elements):
    """The cropped frame as the backdrop and a layout in its own units (origin at the crop's top left)."""
    x0, y0, x1, y1 = bounds(image)
    pieces = {backdrop: crop(image, (x0, y0, x1, y1))}
    layout = {
        "screen": screen,
        "backdrop": backdrop,
        "fullscreen": False,
        "frame": (scale, x0, y0),
        "frameSize": [round((x1 - x0) * scale, 1), round((y1 - y0) * scale, 1)],
        "elements": elements,
    }
    return pieces, layout
